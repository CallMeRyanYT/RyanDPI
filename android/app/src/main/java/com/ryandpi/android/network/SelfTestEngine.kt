package com.ryandpi.android.network

import com.ryandpi.android.engine.DohDnsResolver
import com.ryandpi.android.engine.RyanDpiSettings
import com.ryandpi.android.engine.VpnStateHolder
import com.ryandpi.android.engine.VpnStatus
import com.ryandpi.android.ui.AppStrings
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import okhttp3.ConnectionPool
import okhttp3.Dns
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Request
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Proxy
import java.net.Socket
import java.net.UnknownHostException
import java.util.concurrent.TimeUnit

data class SelfTestItem(
    val id: String,
    val name: String,
    val passed: Boolean,
    val detail: String,
    val latencyMs: Long
)

object SelfTestEngine {

    suspend fun runAll(settings: RyanDpiSettings, s: AppStrings): List<SelfTestItem> = coroutineScope {
        val isConnected = VpnStateHolder.state.value.status == VpnStatus.CONNECTED
        val dohResolver = DohDnsResolver(
            endpoints = settings.orderedDohEndpoints(),
            fallbackSocksPort = if (isConnected) settings.activeCiadpiPort() else null
        )

        val dohDns = object : Dns {
            override fun lookup(hostname: String): List<InetAddress> {
                val ips = queryAAddresses(dohResolver, hostname)
                if (ips.isNotEmpty()) return ips
                return Dns.SYSTEM.lookup(hostname)
            }
        }

        val testClient = OkHttpClient.Builder()
            .apply {
                if (isConnected) {
                    proxy(Proxy(Proxy.Type.SOCKS, InetSocketAddress("127.0.0.1", RyanDpiSettings.GATEWAY_PORT)))
                    dns(dohDns)
                }
            }
            .protocols(listOf(Protocol.HTTP_2, Protocol.HTTP_1_1))
            .connectionPool(ConnectionPool(8, 30, TimeUnit.SECONDS))
            .connectTimeout(5, TimeUnit.SECONDS)
            .readTimeout(6, TimeUnit.SECONDS)
            .writeTimeout(5, TimeUnit.SECONDS)
            .followRedirects(true)
            .build()

        val directBaselineClient = OkHttpClient.Builder()
            .protocols(listOf(Protocol.HTTP_2, Protocol.HTTP_1_1))
            .connectionPool(ConnectionPool(2, 30, TimeUnit.SECONDS))
            .connectTimeout(5, TimeUnit.SECONDS)
            .readTimeout(5, TimeUnit.SECONDS)
            .build()

        try {
            val checks = listOf(
                async(Dispatchers.IO) { checkLocalEngine(s) },
                async(Dispatchers.IO) { checkEncryptedDns(dohResolver, s) },
                async(Dispatchers.IO) { checkEndpoint(testClient, "discord", "Discord", "https://discord.com/api/v10/gateway", s) },
                async(Dispatchers.IO) { checkEndpoint(testClient, "roblox_app", s.testRobloxApp, "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer", s) },
                async(Dispatchers.IO) { checkEndpoint(testClient, "roblox_studio", s.testRobloxStudio, "https://apis.roblox.com/health", s) },
                async(Dispatchers.IO) { checkEndpoint(testClient, "roblox_web", s.testRobloxWeb, "https://www.roblox.com/", s) },
                async(Dispatchers.IO) { checkEndpoint(testClient, "wattpad", "Wattpad", "https://www.wattpad.com/", s) },
                async(Dispatchers.IO) { checkEndpoint(directBaselineClient, "baseline", s.testBaseline, "https://1.1.1.1/cdn-cgi/trace", s) }
            )
            checks.awaitAll()
        } finally {
            runCatching { testClient.dispatcher.executorService.shutdown() }
            runCatching { testClient.connectionPool.evictAll() }
            runCatching { directBaselineClient.dispatcher.executorService.shutdown() }
            runCatching { directBaselineClient.connectionPool.evictAll() }
            runCatching { dohResolver.close() }
        }
    }

    private fun checkLocalEngine(s: AppStrings): SelfTestItem {
        val start = System.nanoTime()
        return try {
            Socket().use { sock ->
                sock.connect(InetSocketAddress("127.0.0.1", RyanDpiSettings.GATEWAY_PORT), 1500)
                sock.soTimeout = 1500
                // Verify real SOCKS5 greeting response (0x05 0x00)
                sock.getOutputStream().write(byteArrayOf(0x05, 0x01, 0x00))
                sock.getOutputStream().flush()
                val resp = ByteArray(2)
                val n = sock.getInputStream().read(resp)
                val ms = ((System.nanoTime() - start) / 1_000_000L).coerceAtLeast(1L)
                if (n == 2 && resp[0] == 0x05.toByte() && resp[1] == 0x00.toByte()) {
                    SelfTestItem("engine", s.testLocalEngine, true, s.testEngineActive, ms)
                } else {
                    SelfTestItem("engine", s.testLocalEngine, false, s.testEngineInactive, ms)
                }
            }
        } catch (_: Exception) {
            val ms = ((System.nanoTime() - start) / 1_000_000L).coerceAtLeast(1L)
            SelfTestItem("engine", s.testLocalEngine, false, s.testEngineInactive, ms)
        }
    }

    private fun checkEncryptedDns(resolver: DohDnsResolver, s: AppStrings): SelfTestItem {
        val start = System.nanoTime()
        return try {
            val discordIps = queryAAddresses(resolver, "discord.com")
            val robloxIps = queryAAddresses(resolver, "www.roblox.com")
            val ms = ((System.nanoTime() - start) / 1_000_000L).coerceAtLeast(1L)
            val valid = discordIps.isNotEmpty() && robloxIps.isNotEmpty() &&
                discordIps.none { isLikelyPoisonedIp(it.hostAddress ?: "") }
            if (valid) {
                SelfTestItem("dns", s.testDnsResolution, true, s.testDnsClean, ms)
            } else {
                SelfTestItem("dns", s.testDnsResolution, false, s.testDnsFailed, ms)
            }
        } catch (e: Exception) {
            val ms = ((System.nanoTime() - start) / 1_000_000L).coerceAtLeast(1L)
            SelfTestItem("dns", s.testDnsResolution, false, "${s.testDnsFailed} (${e.message ?: "err"})", ms)
        }
    }

    private fun checkEndpoint(
        client: OkHttpClient,
        id: String,
        name: String,
        url: String,
        s: AppStrings
    ): SelfTestItem {
        return try {
            val req = Request.Builder()
                .url(url)
                .head()
                .header("User-Agent", "Mozilla/5.0 (Linux; Android 14) RyanDPI/2.4")
                .build()

            // Request 1: establish TLS + DPI desync handshake
            var code = 0
            val coldStart = System.nanoTime()
            client.newCall(req).execute().use { resp ->
                code = resp.code
                resp.body?.close()
            }
            val coldMs = ((System.nanoTime() - coldStart) / 1_000_000L).coerceAtLeast(1L)

            // Request 2: warm keep-alive HTTP/2 RTT for accurate live ping
            val warmStart = System.nanoTime()
            var warmMs = coldMs
            runCatching {
                client.newCall(req).execute().use { resp ->
                    if (resp.code in 200..499) code = resp.code
                    resp.body?.close()
                }
                warmMs = ((System.nanoTime() - warmStart) / 1_000_000L).coerceAtLeast(1L)
            }

            val ok = code in 200..499
            val displayMs = minOf(coldMs, warmMs)
            if (ok) {
                SelfTestItem(id, name, true, "${s.testUnlocked} (HTTP $code)", displayMs)
            } else {
                SelfTestItem(id, name, false, "${s.testBlocked} (HTTP $code)", displayMs)
            }
        } catch (e: Exception) {
            val reason = (e.message ?: e.javaClass.simpleName).take(46)
            SelfTestItem(id, name, false, "${s.testBlocked} ($reason)", 0L)
        }
    }

    private fun isLikelyPoisonedIp(ip: String): Boolean {
        return ip == "0.0.0.0" || ip == "127.0.0.1" || ip.startsWith("195.175.254.")
    }

    internal fun buildDnsAQuery(domain: String): ByteArray {
        val labels = domain.trim('.').split('.').filter { it.isNotEmpty() }
        val qnameLen = labels.sumOf { 1 + it.length } + 1
        val buf = ByteArray(12 + qnameLen + 4)
        // Random-ish txid
        val tx = (System.nanoTime() and 0xFFFF).toInt()
        buf[0] = ((tx ushr 8) and 0xFF).toByte()
        buf[1] = (tx and 0xFF).toByte()
        buf[2] = 0x01 // RD=1
        buf[3] = 0x00
        buf[5] = 0x01 // QDCOUNT=1

        var pos = 12
        for (label in labels) {
            val bytes = label.toByteArray(Charsets.US_ASCII)
            buf[pos++] = bytes.size.toByte()
            System.arraycopy(bytes, 0, buf, pos, bytes.size)
            pos += bytes.size
        }
        buf[pos++] = 0x00
        buf[pos++] = 0x00
        buf[pos++] = 0x01 // TYPE A
        buf[pos++] = 0x00
        buf[pos] = 0x01   // CLASS IN
        return buf
    }

    internal fun queryAAddresses(resolver: DohDnsResolver, domain: String): List<InetAddress> {
        val query = buildDnsAQuery(domain)
        val resp = resolver.resolveWireQuery(query)
        return parseAAddresses(resp)
    }

    internal fun parseAAddresses(packet: ByteArray): List<InetAddress> {
        if (packet.size < 12) return emptyList()
        val rcode = packet[3].toInt() and 0x0F
        if (rcode != 0) return emptyList()

        val qdCount = ((packet[4].toInt() and 0xFF) shl 8) or (packet[5].toInt() and 0xFF)
        val anCount = ((packet[6].toInt() and 0xFF) shl 8) or (packet[7].toInt() and 0xFF)
        if (anCount <= 0) return emptyList()

        var idx = 12
        for (i in 0 until qdCount) {
            idx = skipName(packet, idx) ?: return emptyList()
            idx += 4
            if (idx > packet.size) return emptyList()
        }

        val out = mutableListOf<InetAddress>()
        for (i in 0 until anCount) {
            idx = skipName(packet, idx) ?: break
            if (idx + 10 > packet.size) break
            val type = ((packet[idx].toInt() and 0xFF) shl 8) or (packet[idx + 1].toInt() and 0xFF)
            val rdLen = ((packet[idx + 8].toInt() and 0xFF) shl 8) or (packet[idx + 9].toInt() and 0xFF)
            idx += 10
            if (idx + rdLen > packet.size) break
            if (type == 1 && rdLen == 4) {
                val ipBytes = packet.copyOfRange(idx, idx + 4)
                runCatching { out.add(InetAddress.getByAddress(ipBytes)) }
            }
            idx += rdLen
        }
        return out
    }

    private fun skipName(packet: ByteArray, start: Int): Int? {
        var idx = start
        var guard = 0
        while (idx < packet.size && guard < 128) {
            guard++
            val len = packet[idx].toInt() and 0xFF
            if (len == 0) return idx + 1
            if ((len and 0xC0) == 0xC0) {
                return if (idx + 2 <= packet.size) idx + 2 else null
            }
            idx += 1 + len
        }
        return null
    }
}
