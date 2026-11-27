package com.ryandpi.android.engine

import java.io.BufferedInputStream
import java.io.DataInputStream
import java.io.EOFException
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.net.SocketAddress
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.ExecutorService
import java.util.concurrent.LinkedBlockingQueue
import java.util.concurrent.SynchronousQueue
import java.util.concurrent.ThreadPoolExecutor
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/**
 * Local RFC 1928 SOCKS5 gateway listening on 127.0.0.1:1080 in front of ciadpi (127.0.0.1:1081).
 *
 * - Intercepts ALL DNS traffic (UDP/53 and TCP/53, including virtual 10.10.10.2:53 and public 1.1.1.1:53)
 *   and resolves it over Encrypted HTTPS DoH (bypassing Turkish ISP UDP/53 blocks & DNS poisoning).
 * - Immediately refuses non-53 probes to virtual DNS IP 10.10.10.2 (e.g., Android Private DNS port 853 probe)
 *   so Android switches to encrypted DoH in <1ms without waiting for a timeout.
 * - Relays all other TCP and UDP traffic directly to local ciadpi at loopback wire speed.
 */
class RyanDpiSocksGateway(
    private val listenPort: Int,
    private val upstreamCiadpiPort: Int,
    private val dohResolver: DohDnsResolver,
    private val blockQuicUdp443: Boolean = true
) {
    companion object {
        private const val BUFFER_SIZE = 16 * 1024
        private const val POOL_MAX_BUFFERS = 24
        private const val VIRTUAL_DNS_IP = RyanDpiSettings.VIRTUAL_DNS_IPV4
    }

    private val running = AtomicBoolean(false)
    private var serverSocket: ServerSocket? = null
    private var acceptThread: Thread? = null

    // On-demand shrinking thread pools: 0 idle threads when resting, high ceiling (512) without CallerRunsPolicy
    // so acceptThread NEVER stalls accepting new connections even under heavy multi-tab + Discord load!
    private val ioExecutor: ExecutorService = ThreadPoolExecutor(
        0, 512, 30L, TimeUnit.SECONDS, SynchronousQueue(),
        { r -> Thread(r, "ryandpi-io").apply { isDaemon = true } },
        ThreadPoolExecutor.AbortPolicy()
    )
    private val dnsExecutor: ExecutorService = ThreadPoolExecutor(
        0, 4, 30L, TimeUnit.SECONDS, LinkedBlockingQueue(),
        { r -> Thread(r, "ryandpi-doh").apply { isDaemon = true } }
    ).apply { allowCoreThreadTimeOut(true) }

    private val activeSockets = ConcurrentHashMap.newKeySet<Socket>()
    private val activeUdpSockets = ConcurrentHashMap.newKeySet<DatagramSocket>()
    private val bufferPool = ArrayBlockingQueue<ByteArray>(POOL_MAX_BUFFERS)

    private fun acquireBuffer(): ByteArray = bufferPool.poll() ?: ByteArray(BUFFER_SIZE)
    private fun releaseBuffer(buf: ByteArray) {
        bufferPool.offer(buf)
    }

    val boundPort: Int
        get() = serverSocket?.localPort ?: listenPort

    fun start() {
        if (!running.compareAndSet(false, true)) return
        val ss = ServerSocket().apply {
            reuseAddress = true
            bind(InetSocketAddress(InetAddress.getByName("127.0.0.1"), listenPort), 256)
        }
        serverSocket = ss

        acceptThread = Thread({
            while (running.get()) {
                val client = try {
                    ss.accept()
                } catch (_: IOException) {
                    break
                }
                configureSocket(client)
                activeSockets.add(client)
                try {
                    ioExecutor.execute {
                        try {
                            handleClient(client)
                        } catch (_: Exception) {
                            // Normal close / reset
                        } finally {
                            activeSockets.remove(client)
                            runCatching { client.close() }
                        }
                    }
                } catch (_: Exception) {
                    activeSockets.remove(client)
                    runCatching { client.close() }
                }
            }
        }, "ryandpi-socks-accept").apply {
            isDaemon = true
            start()
        }
        LogRepository.info("Encrypted DoH SOCKS5 Gateway listening on 127.0.0.1:${ss.localPort} -> ciadpi:$upstreamCiadpiPort")
    }

    fun stop() {
        if (!running.compareAndSet(true, false)) return
        runCatching { serverSocket?.close() }
        serverSocket = null

        for (s in activeSockets) runCatching { s.close() }
        activeSockets.clear()

        for (u in activeUdpSockets) runCatching { u.close() }
        activeUdpSockets.clear()

        runCatching { ioExecutor.shutdownNow() }
        runCatching { dnsExecutor.shutdownNow() }
        runCatching { dohResolver.close() }
    }

    private fun configureSocket(s: Socket) {
        runCatching { s.tcpNoDelay = true }
        runCatching { s.soTimeout = 300_000 }
        runCatching { s.receiveBufferSize = 65536 }
        runCatching { s.sendBufferSize = 65536 }
    }

    private fun handleClient(client: Socket) {
        val rawIn = BufferedInputStream(client.getInputStream(), 4096)
        val input = DataInputStream(rawIn)
        val output = client.getOutputStream()

        // 1. SOCKS5 Method Negotiation
        val ver = input.readUnsignedByte()
        if (ver != 0x05) return
        val nMethods = input.readUnsignedByte()
        if (nMethods > 0) {
            val methods = ByteArray(nMethods)
            input.readFully(methods)
        }
        output.write(byteArrayOf(0x05, 0x00))
        output.flush()

        // 2. SOCKS5 Request Header
        val reqVer = input.readUnsignedByte()
        if (reqVer != 0x05) return
        val cmd = input.readUnsignedByte()
        val rsv = input.readUnsignedByte()
        val atyp = input.readUnsignedByte()

        val addrPayload: ByteArray
        val dstHost: String
        when (atyp) {
            0x01 -> {
                addrPayload = ByteArray(4)
                input.readFully(addrPayload)
                dstHost = InetAddress.getByAddress(addrPayload).hostAddress ?: ""
            }
            0x03 -> {
                val len = input.readUnsignedByte()
                val domainBytes = ByteArray(len)
                input.readFully(domainBytes)
                addrPayload = ByteArray(1 + len)
                addrPayload[0] = len.toByte()
                System.arraycopy(domainBytes, 0, addrPayload, 1, len)
                dstHost = String(domainBytes, Charsets.US_ASCII)
            }
            0x04 -> {
                addrPayload = ByteArray(16)
                input.readFully(addrPayload)
                dstHost = InetAddress.getByAddress(addrPayload).hostAddress ?: ""
            }
            else -> {
                sendSocksReply(output, 0x08) // Address type not supported
                return
            }
        }

        val portHi = input.readUnsignedByte()
        val portLo = input.readUnsignedByte()
        val dstPort = (portHi shl 8) or portLo

        when (cmd) {
            0x01 -> handleTcpConnect(
                client = client,
                clientIn = rawIn,
                clientOut = output,
                rsv = rsv,
                atyp = atyp,
                addrPayload = addrPayload,
                portHi = portHi,
                portLo = portLo,
                dstHost = dstHost,
                dstPort = dstPort
            )
            0x03 -> handleUdpAssociate(client, input, output)
            else -> sendSocksReply(output, 0x07) // Command not supported
        }
    }

    private fun handleTcpConnect(
        client: Socket,
        clientIn: InputStream,
        clientOut: OutputStream,
        rsv: Int,
        atyp: Int,
        addrPayload: ByteArray,
        portHi: Int,
        portLo: Int,
        dstHost: String,
        dstPort: Int
    ) {
        // Fast-refuse Android Private DNS probe on virtual DNS IP (e.g., 10.10.10.2:853) in <1ms
        if (dstHost == VIRTUAL_DNS_IP && dstPort != 53) {
            sendSocksReply(clientOut, 0x05) // Connection refused
            return
        }

        // Intercept DNS-over-TCP (port 53) and resolve over Encrypted DoH
        if (dstPort == 53) {
            sendSocksReply(clientOut, 0x00)
            handleDnsOverTcp(clientIn, clientOut)
            return
        }

        // Forward all regular TCP directly to local ciadpi SOCKS5 engine
        val upstream = Socket()
        configureSocket(upstream)
        activeSockets.add(upstream)
        try {
            upstream.connect(InetSocketAddress("127.0.0.1", upstreamCiadpiPort), 5000)
            val upOut = upstream.getOutputStream()
            val upIn = BufferedInputStream(upstream.getInputStream(), 4096)
            val upDataIn = DataInputStream(upIn)

            // Upstream greeting
            upOut.write(byteArrayOf(0x05, 0x01, 0x00))
            upOut.flush()
            val gVer = upDataIn.readUnsignedByte()
            val gMethod = upDataIn.readUnsignedByte()
            if (gVer != 0x05 || gMethod != 0x00) {
                sendSocksReply(clientOut, 0x01)
                return
            }

            // Replay exact SOCKS5 CONNECT request to ciadpi
            val reqBuf = ByteArray(4 + addrPayload.size + 2)
            reqBuf[0] = 0x05
            reqBuf[1] = 0x01
            reqBuf[2] = rsv.toByte()
            reqBuf[3] = atyp.toByte()
            System.arraycopy(addrPayload, 0, reqBuf, 4, addrPayload.size)
            reqBuf[4 + addrPayload.size] = portHi.toByte()
            reqBuf[5 + addrPayload.size] = portLo.toByte()
            upOut.write(reqBuf)
            upOut.flush()

            // Read upstream reply and mirror directly back to client
            val repVer = upDataIn.readUnsignedByte()
            val repCode = upDataIn.readUnsignedByte()
            val repRsv = upDataIn.readUnsignedByte()
            val repAtyp = upDataIn.readUnsignedByte()
            val repAddr: ByteArray = when (repAtyp) {
                0x01 -> ByteArray(4).also { upDataIn.readFully(it) }
                0x03 -> {
                    val l = upDataIn.readUnsignedByte()
                    ByteArray(1 + l).also {
                        it[0] = l.toByte()
                        upDataIn.readFully(it, 1, l)
                    }
                }
                0x04 -> ByteArray(16).also { upDataIn.readFully(it) }
                else -> {
                    sendSocksReply(clientOut, 0x01)
                    return
                }
            }
            val repPort = ByteArray(2).also { upDataIn.readFully(it) }

            val replyOut = ByteArray(4 + repAddr.size + 2)
            replyOut[0] = repVer.toByte()
            replyOut[1] = repCode.toByte()
            replyOut[2] = repRsv.toByte()
            replyOut[3] = repAtyp.toByte()
            System.arraycopy(repAddr, 0, replyOut, 4, repAddr.size)
            replyOut[4 + repAddr.size] = repPort[0]
            replyOut[5 + repAddr.size] = repPort[1]
            clientOut.write(replyOut)
            clientOut.flush()

            if (repCode != 0x00) return

            // Bidirectional full-speed loopback splice with proper TCP half-close
            val downToUp = ioExecutor.submit {
                pumpStream(clientIn, upOut)
                runCatching { upstream.shutdownOutput() }
            }
            try {
                pumpStream(upIn, clientOut)
                runCatching { client.shutdownOutput() }
            } finally {
                runCatching { downToUp.get() }
            }
        } catch (_: Exception) {
            runCatching { sendSocksReply(clientOut, 0x05) }
        } finally {
            activeSockets.remove(upstream)
            runCatching { upstream.close() }
        }
    }

    private fun handleDnsOverTcp(clientIn: InputStream, clientOut: OutputStream) {
        val dataIn = DataInputStream(clientIn)
        try {
            while (running.get()) {
                val len = try {
                    dataIn.readUnsignedShort()
                } catch (_: EOFException) {
                    break
                }
                if (len <= 0 || len > 65535) break
                val query = ByteArray(len)
                dataIn.readFully(query)
                val response = dohResolver.resolveWireQuery(query)
                val framed = ByteArray(2 + response.size)
                framed[0] = ((response.size ushr 8) and 0xFF).toByte()
                framed[1] = (response.size and 0xFF).toByte()
                System.arraycopy(response, 0, framed, 2, response.size)
                clientOut.write(framed)
                clientOut.flush()
            }
        } catch (_: IOException) {
            // Client closed TCP DNS connection
        }
    }

    private fun handleUdpAssociate(client: Socket, controlIn: DataInputStream, controlOut: OutputStream) {
        val localUdp = DatagramSocket(0, InetAddress.getByName("127.0.0.1")).apply {
            runCatching { receiveBufferSize = 262144 }
            runCatching { sendBufferSize = 262144 }
        }
        activeUdpSockets.add(localUdp)

        val relayPort = localUdp.localPort
        val reply = byteArrayOf(
            0x05, 0x00, 0x00, 0x01,
            127, 0, 0, 1,
            ((relayPort ushr 8) and 0xFF).toByte(),
            (relayPort and 0xFF).toByte()
        )
        controlOut.write(reply)
        controlOut.flush()

        val assocAlive = AtomicBoolean(true)
        var upstreamCtrlSocket: Socket? = null
        var upstreamUdpSocket: DatagramSocket? = null
        var upstreamRelayTarget: InetSocketAddress? = null
        val upstreamLock = Any()

        val lastClientUdpAddr = AtomicReference<SocketAddress?>(null)

        fun ensureUpstreamUdpRelay(): Pair<DatagramSocket, InetSocketAddress>? {
            synchronized(upstreamLock) {
                val existingSock = upstreamUdpSocket
                val existingTarget = upstreamRelayTarget
                if (existingSock != null && existingTarget != null && !existingSock.isClosed) {
                    return existingSock to existingTarget
                }
                return try {
                    val ctrl = Socket()
                    configureSocket(ctrl)
                    ctrl.connect(InetSocketAddress("127.0.0.1", upstreamCiadpiPort), 4000)
                    val out = ctrl.getOutputStream()
                    val inp = DataInputStream(BufferedInputStream(ctrl.getInputStream(), 1024))

                    out.write(byteArrayOf(0x05, 0x01, 0x00))
                    out.flush()
                    if (inp.readUnsignedByte() != 0x05 || inp.readUnsignedByte() != 0x00) {
                        ctrl.close()
                        return null
                    }
                    // Send UDP ASSOCIATE 0.0.0.0:0 to ciadpi
                    out.write(byteArrayOf(0x05, 0x03, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                    out.flush()

                    val rVer = inp.readUnsignedByte()
                    val rRep = inp.readUnsignedByte()
                    inp.readUnsignedByte() // RSV
                    val rAtyp = inp.readUnsignedByte()
                    val bndIp: InetAddress = when (rAtyp) {
                        0x01 -> InetAddress.getByAddress(ByteArray(4).also { inp.readFully(it) })
                        0x04 -> InetAddress.getByAddress(ByteArray(16).also { inp.readFully(it) })
                        0x03 -> {
                            val l = inp.readUnsignedByte()
                            val b = ByteArray(l).also { inp.readFully(it) }
                            InetAddress.getByName(String(b, Charsets.US_ASCII))
                        }
                        else -> {
                            ctrl.close()
                            return null
                        }
                    }
                    val bndPort = inp.readUnsignedShort()
                    if (rVer != 0x05 || rRep != 0x00 || bndPort <= 0) {
                        ctrl.close()
                        return null
                    }
                    val resolvedIp = if (bndIp.isAnyLocalAddress) InetAddress.getByName("127.0.0.1") else bndIp
                    val target = InetSocketAddress(resolvedIp, bndPort)

                    val upUdp = DatagramSocket(0, InetAddress.getByName("127.0.0.1")).apply {
                        runCatching { receiveBufferSize = 262144 }
                        runCatching { sendBufferSize = 262144 }
                    }
                    activeSockets.add(ctrl)
                    activeUdpSockets.add(upUdp)
                    upstreamCtrlSocket = ctrl
                    upstreamUdpSocket = upUdp
                    upstreamRelayTarget = target

                    // Start reverse pump: ciadpi UDP replies -> hev-socks5-tunnel
                    ioExecutor.execute {
                        val buf = ByteArray(65535)
                        val dp = DatagramPacket(buf, buf.size)
                        while (assocAlive.get() && running.get() && !upUdp.isClosed) {
                            try {
                                dp.length = buf.size
                                upUdp.receive(dp)
                                val dest = lastClientUdpAddr.get() ?: continue
                                val outPkt = DatagramPacket(dp.data, dp.offset, dp.length, dest)
                                localUdp.send(outPkt)
                            } catch (_: IOException) {
                                break
                            }
                        }
                    }

                    upUdp to target
                } catch (_: Exception) {
                    null
                }
            }
        }

        // UDP receive worker
        val udpWorker = ioExecutor.submit {
            val buf = ByteArray(65535)
            val pkt = DatagramPacket(buf, buf.size)
            while (assocAlive.get() && running.get() && !localUdp.isClosed) {
                try {
                    pkt.length = buf.size
                    localUdp.receive(pkt)
                    val len = pkt.length
                    if (len < 10) continue
                    val sender = pkt.socketAddress
                    lastClientUdpAddr.set(sender)

                    // Parse RFC 1928 UDP header: RSV(2) + FRAG(1) + ATYP(1) + ADDR + PORT(2)
                    val frag = buf[2].toInt() and 0xFF
                    if (frag != 0) continue // Fragmented SOCKS5 UDP not supported
                    val atyp = buf[3].toInt() and 0xFF
                    val headerLen = when (atyp) {
                        0x01 -> 4 + 4 + 2
                        0x04 -> 4 + 16 + 2
                        0x03 -> {
                            val dLen = buf[4].toInt() and 0xFF
                            4 + 1 + dLen + 2
                        }
                        else -> continue
                    }
                    if (len < headerLen) continue
                    val dstPort = ((buf[headerLen - 2].toInt() and 0xFF) shl 8) or
                        (buf[headerLen - 1].toInt() and 0xFF)

                    if (dstPort == 53) {
                        // Intercept DNS UDP packet asynchronously over Encrypted DoH
                        val headerCopy = buf.copyOfRange(0, headerLen)
                        val queryCopy = buf.copyOfRange(headerLen, len)
                        dnsExecutor.execute {
                            val dnsReply = dohResolver.resolveWireQuery(queryCopy)
                            val respPacket = ByteArray(headerCopy.size + dnsReply.size)
                            System.arraycopy(headerCopy, 0, respPacket, 0, headerCopy.size)
                            System.arraycopy(dnsReply, 0, respPacket, headerCopy.size, dnsReply.size)
                            runCatching {
                                if (!localUdp.isClosed) {
                                    localUdp.send(DatagramPacket(respPacket, respPacket.size, sender))
                                }
                            }
                        }
                    } else if (blockQuicUdp443 && dstPort == 443) {
                        // Drop ISP-throttled HTTP/3 QUIC (UDP/443) so Chrome & YouTube app instantly fall back
                        // to full-speed TCP/443 HTTP/2 through ciadpi (fixes Russia YouTube in browsers & app!)
                        continue
                    } else {
                        // Zero-copy forward non-DNS UDP (Discord Voice, Roblox Game UDP) to ciadpi
                        val relay = ensureUpstreamUdpRelay() ?: continue
                        val (upSock, upTarget) = relay
                        runCatching {
                            upSock.send(DatagramPacket(buf, 0, len, upTarget))
                        }
                    }
                } catch (_: IOException) {
                    break
                }
            }
        }

        // Keep UDP association alive until the control TCP connection closes
        try {
            val drain = ByteArray(64)
            while (running.get() && !client.isClosed) {
                val read = controlIn.read(drain)
                if (read < 0) break
            }
        } catch (_: IOException) {
            // Control socket closed
        } finally {
            assocAlive.set(false)
            activeUdpSockets.remove(localUdp)
            runCatching { localUdp.close() }
            synchronized(upstreamLock) {
                upstreamUdpSocket?.let {
                    activeUdpSockets.remove(it)
                    runCatching { it.close() }
                }
                upstreamCtrlSocket?.let {
                    activeSockets.remove(it)
                    runCatching { it.close() }
                }
            }
            runCatching { udpWorker.cancel(true) }
        }
    }

    private fun pumpStream(input: InputStream, output: OutputStream) {
        val buf = acquireBuffer()
        try {
            while (running.get()) {
                val n = input.read(buf)
                if (n <= 0) break
                output.write(buf, 0, n)
                output.flush()
            }
        } catch (_: IOException) {
            // Connection closed / reset
        } finally {
            releaseBuffer(buf)
        }
    }

    private fun sendSocksReply(output: OutputStream, repCode: Int) {
        output.write(byteArrayOf(0x05, repCode.toByte(), 0x00, 0x01, 0, 0, 0, 0, 0, 0))
        output.flush()
    }
}
