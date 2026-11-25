package com.ryandpi.android

import com.ryandpi.android.engine.BypassProfile
import com.ryandpi.android.engine.DnsMode
import com.ryandpi.android.engine.DohDnsResolver
import com.ryandpi.android.engine.DohProvider
import com.ryandpi.android.engine.RyanDpiSettings
import com.ryandpi.android.engine.RyanDpiSocksGateway
import com.ryandpi.android.network.SelfTestEngine
import com.ryandpi.android.network.UpdateChecker
import com.ryandpi.android.ui.StringsRepository
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.DataInputStream
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket

class RyanDpiAndroidTests {

    @Test
    fun settingsBuildValidCiadpiArgsForAllProfiles() {
        val auto = RyanDpiSettings(
            dnsMode = DnsMode.ENCRYPTED_DOH,
            bypassProfile = BypassProfile.AUTO_UNIVERSAL,
            udpBypass = true
        )
        assertEquals(1081, auto.activeCiadpiPort())
        val args = auto.buildCiadpiArgList()
        assertTrue(args.contains("-i"))
        assertTrue(args.contains("127.0.0.1"))
        assertTrue(args.contains("1081"))
        assertTrue(args.contains("-a"))
        assertTrue(args.contains("--tlsrec"))
        assertTrue(args.contains("1+s"))
        assertTrue(args.contains("--auto=torst,redirect,ssl_err"))

        val direct = RyanDpiSettings(
            dnsMode = DnsMode.DIRECT_NATIVE_DNS,
            bypassProfile = BypassProfile.CLASSIC_BYEDPI,
            udpBypass = false
        )
        assertEquals(1080, direct.activeCiadpiPort())
        val directArgs = direct.buildCiadpiArgList()
        assertFalse(directArgs.contains("-a"))
        assertTrue(directArgs.contains("1080"))

        val custom = RyanDpiSettings(
            bypassProfile = BypassProfile.CUSTOM,
            customStrategyArgs = "  --split 2\n--disorder 1+s  "
        )
        val customArgs = custom.buildCiadpiArgList(1081)
        assertTrue(customArgs.contains("--split"))
        assertTrue(customArgs.contains("2"))
        assertTrue(customArgs.contains("--disorder"))
        assertTrue(customArgs.contains("1+s"))
    }

    @Test
    fun dohResolverResolvesDiscordAndRobloxLiveAndCaches() {
        val resolver = DohDnsResolver(
            endpoints = listOf(
                DohProvider.CLOUDFLARE.endpointUrl,
                DohProvider.GOOGLE.endpointUrl
            )
        )
        try {
            val discordQuery = SelfTestEngine.buildDnsAQuery("discord.com")
            val cacheKey = resolver.extractCacheKey(discordQuery)
            assertEquals("discord.com|1|1", cacheKey)

            val resp1 = resolver.resolveWireQuery(discordQuery)
            val ips1 = SelfTestEngine.parseAAddresses(resp1)
            assertTrue("Expected non-empty IPv4 answers for discord.com via DoH", ips1.isNotEmpty())
            assertTrue("Expected unpoisoned Discord IP", ips1.none { it.hostAddress == "195.175.254.2" })

            // Repeat query with a different Transaction ID must hit cache and rewrite TXID accurately
            val discordQuery2 = discordQuery.copyOf()
            discordQuery2[0] = 0x5A
            discordQuery2[1] = 0xA5.toByte()
            val t0 = System.nanoTime()
            val resp2 = resolver.resolveWireQuery(discordQuery2)
            val cacheMs = (System.nanoTime() - t0) / 1_000_000L
            assertEquals(0x5A.toByte(), resp2[0])
            assertEquals(0xA5.toByte(), resp2[1])
            assertTrue("Expected >=1 cache hit", resolver.getCacheHits() >= 1L)
            assertTrue("Expected fast cache hit (<25ms), got ${cacheMs}ms", cacheMs < 25L)

            val robloxIps = SelfTestEngine.queryAAddresses(resolver, "www.roblox.com")
            assertTrue("Expected non-empty IPv4 answers for www.roblox.com via DoH", robloxIps.isNotEmpty())
        } finally {
            resolver.close()
        }
    }

    @Test
    fun socksGatewayInterceptsUdpDnsRefusesPrivateDnsProbeAndRelaysTcp() {
        // Mock upstream SOCKS5 server (simulating ciadpi)
        val mockUpstream = ServerSocket(0, 16, InetAddress.getByName("127.0.0.1"))
        val mockThread = Thread {
            while (!mockUpstream.isClosed) {
                val s = try {
                    mockUpstream.accept()
                } catch (_: Exception) {
                    break
                }
                Thread {
                    s.use { sock ->
                        val inp = DataInputStream(sock.getInputStream())
                        val out = sock.getOutputStream()
                        // Greeting
                        inp.readUnsignedByte()
                        val nm = inp.readUnsignedByte()
                        inp.readFully(ByteArray(nm))
                        out.write(byteArrayOf(0x05, 0x00))
                        out.flush()
                        // Request
                        inp.readUnsignedByte() // ver
                        inp.readUnsignedByte() // cmd
                        inp.readUnsignedByte() // rsv
                        val atyp = inp.readUnsignedByte()
                        when (atyp) {
                            0x01 -> inp.readFully(ByteArray(4))
                            0x04 -> inp.readFully(ByteArray(16))
                            0x03 -> inp.readFully(ByteArray(inp.readUnsignedByte()))
                        }
                        inp.readUnsignedShort()
                        // Success reply
                        out.write(byteArrayOf(0x05, 0x00, 0x00, 0x01, 127, 0, 0, 1, 0, 80))
                        out.flush()
                        // Echo received bytes back
                        val buf = ByteArray(64)
                        val n = inp.read(buf)
                        if (n > 0) {
                            out.write(buf, 0, n)
                            out.flush()
                        }
                    }
                }.start()
            }
        }.apply { isDaemon = true; start() }

        val resolver = DohDnsResolver(listOf(DohProvider.CLOUDFLARE.endpointUrl, DohProvider.GOOGLE.endpointUrl))
        val gateway = RyanDpiSocksGateway(
            listenPort = 0,
            upstreamCiadpiPort = mockUpstream.localPort,
            dohResolver = resolver
        )
        gateway.start()
        val gwPort = gateway.boundPort

        try {
            // 1. Test fast refusal of 10.10.10.2:853 (Android Private DNS probe)
            Socket("127.0.0.1", gwPort).use { sock ->
                val out = sock.getOutputStream()
                val inp = DataInputStream(sock.getInputStream())
                out.write(byteArrayOf(0x05, 0x01, 0x00))
                out.flush()
                assertEquals(0x05, inp.readUnsignedByte())
                assertEquals(0x00, inp.readUnsignedByte())

                // CONNECT 10.10.10.2:853 (0x0355)
                out.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 10, 10, 10, 2, 0x03, 0x55))
                out.flush()
                assertEquals(0x05, inp.readUnsignedByte())
                assertEquals("Expected SOCKS5 Connection Refused (0x05) for 10.10.10.2:853", 0x05, inp.readUnsignedByte())
            }

            // 2. Test UDP ASSOCIATE DNS interception to 10.10.10.2:53 via Encrypted DoH
            Socket("127.0.0.1", gwPort).use { ctrl ->
                val out = ctrl.getOutputStream()
                val inp = DataInputStream(ctrl.getInputStream())
                out.write(byteArrayOf(0x05, 0x01, 0x00))
                out.flush()
                assertEquals(0x05, inp.readUnsignedByte())
                assertEquals(0x00, inp.readUnsignedByte())

                // UDP ASSOCIATE 0.0.0.0:0
                out.write(byteArrayOf(0x05, 0x03, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                out.flush()
                assertEquals(0x05, inp.readUnsignedByte())
                assertEquals(0x00, inp.readUnsignedByte())
                inp.readUnsignedByte()
                assertEquals(0x01, inp.readUnsignedByte())
                val bndAddr = ByteArray(4).also { inp.readFully(it) }
                val relayPort = inp.readUnsignedShort()
                assertTrue(relayPort > 0)

                val dnsQuery = SelfTestEngine.buildDnsAQuery("discord.com")
                // RFC 1928 UDP header targeting 10.10.10.2:53
                val header = byteArrayOf(0x00, 0x00, 0x00, 0x01, 10, 10, 10, 2, 0x00, 53)
                val packetBytes = header + dnsQuery

                DatagramSocket().use { udp ->
                    udp.soTimeout = 5000
                    udp.send(DatagramPacket(packetBytes, packetBytes.size, InetSocketAddress(InetAddress.getByAddress(bndAddr), relayPort)))
                    val recvBuf = ByteArray(4096)
                    val recvPkt = DatagramPacket(recvBuf, recvBuf.size)
                    udp.receive(recvPkt)
                    assertTrue(recvPkt.length > 22)
                    // Verify header preserved (10.10.10.2:53)
                    assertEquals(10.toByte(), recvBuf[4])
                    assertEquals(2.toByte(), recvBuf[7])
                    assertEquals(53.toByte(), recvBuf[9])
                    val dnsResp = recvBuf.copyOfRange(10, recvPkt.length)
                    val ips = SelfTestEngine.parseAAddresses(dnsResp)
                    assertTrue("Expected resolved IPv4 addresses via SOCKS5 UDP DoH", ips.isNotEmpty())
                }
            }

            // 3. Test regular TCP CONNECT relay through mock upstream ciadpi
            Socket("127.0.0.1", gwPort).use { sock ->
                val out = sock.getOutputStream()
                val inp = DataInputStream(sock.getInputStream())
                out.write(byteArrayOf(0x05, 0x01, 0x00))
                out.flush()
                assertEquals(0x05, inp.readUnsignedByte())
                assertEquals(0x00, inp.readUnsignedByte())

                // CONNECT 1.1.1.1:443
                out.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 1, 1, 1, 1, 0x01, 0xBB.toByte()))
                out.flush()
                assertEquals(0x05, inp.readUnsignedByte())
                assertEquals(0x00, inp.readUnsignedByte())
                inp.readFully(ByteArray(8))

                val payload = "RYANDPI-OK".toByteArray(Charsets.US_ASCII)
                out.write(payload)
                out.flush()
                val echoed = ByteArray(payload.size)
                inp.readFully(echoed)
                assertEquals("RYANDPI-OK", String(echoed, Charsets.US_ASCII))
            }
        } finally {
            gateway.stop()
            runCatching { mockUpstream.close() }
        }
    }

    @Test
    fun updateCheckerSelectsApkAndValidatesSecurity() {
        val sampleJson = """
        {
          "tag_name": "v2.5.0",
          "name": "RyanDPI v2.5.0",
          "html_url": "https://github.com/CallMeRyanYT/RyanDPI/releases/tag/v2.5.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "name": "RyanDPI-Setup-Windows-2.5.0.exe",
              "size": 18000000,
              "browser_download_url": "https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.5.0/RyanDPI-Setup-Windows-2.5.0.exe"
            },
            {
              "name": "RyanDPI-Android-2.5.0.apk",
              "size": 9500000,
              "digest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
              "browser_download_url": "https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.5.0/RyanDPI-Android-2.5.0.apk"
            }
          ]
        }
        """.trimIndent()

        val result = UpdateChecker.parseReleaseJson(sampleJson, "2.4.0")
        assertTrue(result.success)
        assertTrue(result.updateAvailable)
        assertEquals("2.5.0", result.latestVersion)
        assertEquals("RyanDPI-Android-2.5.0.apk", result.assetName)
        assertEquals(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            result.expectedSha256
        )

        val same = UpdateChecker.parseReleaseJson(sampleJson, "2.5.0")
        assertTrue(same.success)
        assertFalse(same.updateAvailable)

        assertTrue(UpdateChecker.isTrustedGitHubUrl("https://objects.githubusercontent.com/test.apk"))
        assertFalse(UpdateChecker.isTrustedGitHubUrl("http://github.com/test.apk"))
        assertFalse(UpdateChecker.isTrustedGitHubUrl("https://evil-github.com/test.apk"))
        assertNotNull(UpdateChecker.parseSha256Digest("sha256:" + "a".repeat(64)))
        assertNull(UpdateChecker.parseSha256Digest("sha256:short"))
    }

    @Test
    fun englishAndTurkishStringsAreComplete() {
        val en = StringsRepository.forLanguage("en")
        val tr = StringsRepository.forLanguage("tr")
        assertTrue(en.btnConnect.isNotBlank())
        assertTrue(tr.btnConnect.isNotBlank())
        assertTrue(en.selfTestTitle != tr.selfTestTitle)
        assertEquals("tr", RyanDpiSettings.normalizeLanguage("tr-TR"))
        assertEquals("en", RyanDpiSettings.normalizeLanguage("en-US"))
    }
}
