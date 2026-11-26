package com.ryandpi.android.engine

import okhttp3.ConnectionPool
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.net.InetSocketAddress
import java.net.Proxy
import java.util.Locale
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong

/**
 * High-speed RFC 8484 Encrypted DNS-over-HTTPS (DoH) resolver with:
 * - Zero bootstrap DNS dependency (uses pure IP HTTPS endpoints: 1.1.1.1, 8.8.8.8, 9.9.9.9, 94.140.14.14)
 * - HTTP/2 persistent connection pooling for sub-30ms lookups
 * - Thread-safe TTL-aware LRU cache (<0.1ms cache hits)
 * - Automatic multi-provider failover + optional fallback through local ciadpi SOCKS5 proxy
 */
class DohDnsResolver(
    private val endpoints: List<String>,
    private val fallbackSocksPort: Int? = null
) {
    companion object {
        private val DNS_MESSAGE_MEDIA_TYPE = "application/dns-message".toMediaType()
        private const val MAX_CACHE_ENTRIES = 1024
        private const val MIN_TTL_SEC = 30L
        private const val MAX_TTL_SEC = 3600L
    }

    private data class CacheEntry(
        val responseWire: ByteArray,
        val expiresAtMs: Long
    )

    private val cacheLock = Any()
    private val cache = object : LinkedHashMap<String, CacheEntry>(256, 0.75f, true) {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<String, CacheEntry>?): Boolean {
            return size > MAX_CACHE_ENTRIES
        }
    }

    private val totalQueries = AtomicLong(0)
    private val cacheHits = AtomicLong(0)

    private val directClient: OkHttpClient = OkHttpClient.Builder()
        .protocols(listOf(Protocol.HTTP_2, Protocol.HTTP_1_1))
        .connectionPool(ConnectionPool(8, 5, TimeUnit.MINUTES))
        .connectTimeout(3500, TimeUnit.MILLISECONDS)
        .readTimeout(4000, TimeUnit.MILLISECONDS)
        .writeTimeout(3500, TimeUnit.MILLISECONDS)
        .retryOnConnectionFailure(true)
        .build()

    private val socksFallbackClient: OkHttpClient? = fallbackSocksPort?.let { port ->
        OkHttpClient.Builder()
            .proxy(Proxy(Proxy.Type.SOCKS, InetSocketAddress("127.0.0.1", port)))
            .protocols(listOf(Protocol.HTTP_2, Protocol.HTTP_1_1))
            .connectionPool(ConnectionPool(4, 3, TimeUnit.MINUTES))
            .connectTimeout(4000, TimeUnit.MILLISECONDS)
            .readTimeout(4500, TimeUnit.MILLISECONDS)
            .writeTimeout(4000, TimeUnit.MILLISECONDS)
            .retryOnConnectionFailure(true)
            .build()
    }

    fun getTotalQueries(): Long = totalQueries.get()
    fun getCacheHits(): Long = cacheHits.get()

    /**
     * Resolves a raw DNS wire-format query and returns a raw DNS wire-format response.
     */
    fun resolveWireQuery(query: ByteArray): ByteArray {
        totalQueries.incrementAndGet()
        if (query.size < 12) {
            return buildServFail(query)
        }

        val cacheKey = extractCacheKey(query)
        if (cacheKey != null) {
            val now = System.currentTimeMillis()
            val cached = synchronized(cacheLock) {
                val entry = cache[cacheKey]
                if (entry != null && entry.expiresAtMs > now) entry else {
                    if (entry != null) cache.remove(cacheKey)
                    null
                }
            }
            if (cached != null) {
                cacheHits.incrementAndGet()
                return patchTransactionId(cached.responseWire, query[0], query[1])
            }
        }

        // 1. Try direct encrypted DoH endpoints in priority order (fast path, zero extra hops)
        for (url in endpoints) {
            val resp = executeDohPost(directClient, url, query)
            if (resp != null && resp.size >= 12) {
                cacheIfValid(cacheKey, resp)
                return patchTransactionId(resp, query[0], query[1])
            }
        }

        // 2. Fallback: route DoH through local ciadpi SOCKS5 engine if ISP blocks direct 1.1.1.1:443 TLS
        val proxyClient = socksFallbackClient
        if (proxyClient != null) {
            for (url in endpoints.take(2)) {
                val resp = executeDohPost(proxyClient, url, query)
                if (resp != null && resp.size >= 12) {
                    cacheIfValid(cacheKey, resp)
                    return patchTransactionId(resp, query[0], query[1])
                }
            }
        }

        return buildServFail(query)
    }

    private fun executeDohPost(client: OkHttpClient, url: String, query: ByteArray): ByteArray? {
        return try {
            val request = Request.Builder()
                .url(url)
                .header("Accept", "application/dns-message")
                .header("User-Agent", "RyanDPI-Android")
                .post(query.toRequestBody(DNS_MESSAGE_MEDIA_TYPE))
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return null
                val body = response.body?.bytes() ?: return null
                if (body.size < 12) null else body
            }
        } catch (_: Exception) {
            null
        }
    }

    private fun cacheIfValid(cacheKey: String?, response: ByteArray) {
        if (cacheKey == null || response.size < 12) return
        val rcode = response[3].toInt() and 0x0F
        // Only cache NOERROR (0) or NXDOMAIN (3) responses
        if (rcode != 0 && rcode != 3) return

        val ttlSec = if (rcode == 3) 60L else extractMinTtlSeconds(response).coerceIn(MIN_TTL_SEC, MAX_TTL_SEC)
        val expiresAt = System.currentTimeMillis() + ttlSec * 1000L
        synchronized(cacheLock) {
            cache[cacheKey] = CacheEntry(response.copyOf(), expiresAt)
        }
    }

    fun onNetworkChanged() {
        runCatching { directClient.connectionPool.evictAll() }
        runCatching { socksFallbackClient?.connectionPool?.evictAll() }
    }

    fun close() {
        runCatching { directClient.dispatcher.executorService.shutdown() }
        runCatching { directClient.connectionPool.evictAll() }
        runCatching { socksFallbackClient?.dispatcher?.executorService?.shutdown() }
        runCatching { socksFallbackClient?.connectionPool?.evictAll() }
        synchronized(cacheLock) { cache.clear() }
    }

    private fun patchTransactionId(wire: ByteArray, tx0: Byte, tx1: Byte): ByteArray {
        val copy = wire.copyOf()
        if (copy.size >= 2) {
            copy[0] = tx0
            copy[1] = tx1
        }
        return copy
    }

    /**
     * Extracts normalized "qname|qtype|qclass" cache key from a standard 1-question DNS wire query.
     */
    internal fun extractCacheKey(packet: ByteArray): String? {
        if (packet.size < 17) return null
        val qdCount = ((packet[4].toInt() and 0xFF) shl 8) or (packet[5].toInt() and 0xFF)
        if (qdCount != 1) return null

        val sb = StringBuilder()
        var idx = 12
        var guard = 0
        while (idx < packet.size && guard < 128) {
            guard++
            val len = packet[idx].toInt() and 0xFF
            if (len == 0) {
                idx++
                break
            }
            if ((len and 0xC0) != 0) return null // Unexpected pointer in question
            idx++
            if (idx + len > packet.size) return null
            if (sb.isNotEmpty()) sb.append('.')
            for (i in 0 until len) {
                sb.append((packet[idx + i].toInt() and 0xFF).toChar())
            }
            idx += len
        }
        if (idx + 4 > packet.size) return null
        val qtype = ((packet[idx].toInt() and 0xFF) shl 8) or (packet[idx + 1].toInt() and 0xFF)
        val qclass = ((packet[idx + 2].toInt() and 0xFF) shl 8) or (packet[idx + 3].toInt() and 0xFF)
        return "${sb.toString().lowercase(Locale.ROOT)}|$qtype|$qclass"
    }

    internal fun extractMinTtlSeconds(packet: ByteArray): Long {
        if (packet.size < 12) return 120L
        val qdCount = ((packet[4].toInt() and 0xFF) shl 8) or (packet[5].toInt() and 0xFF)
        val anCount = ((packet[6].toInt() and 0xFF) shl 8) or (packet[7].toInt() and 0xFF)
        if (anCount <= 0) return 60L

        var idx = 12
        // Skip questions
        for (q in 0 until qdCount) {
            idx = skipDnsName(packet, idx) ?: return 120L
            idx += 4
            if (idx > packet.size) return 120L
        }

        var minTtl = Long.MAX_VALUE
        for (a in 0 until anCount) {
            idx = skipDnsName(packet, idx) ?: break
            if (idx + 10 > packet.size) break
            val ttl = ((packet[idx + 4].toLong() and 0xFF) shl 24) or
                ((packet[idx + 5].toLong() and 0xFF) shl 16) or
                ((packet[idx + 6].toLong() and 0xFF) shl 8) or
                (packet[idx + 7].toLong() and 0xFF)
            val rdLength = ((packet[idx + 8].toInt() and 0xFF) shl 8) or (packet[idx + 9].toInt() and 0xFF)
            if (ttl in 1..86400 && ttl < minTtl) {
                minTtl = ttl
            }
            idx += 10 + rdLength
            if (idx > packet.size) break
        }
        return if (minTtl == Long.MAX_VALUE) 120L else minTtl
    }

    private fun skipDnsName(packet: ByteArray, start: Int): Int? {
        var idx = start
        var steps = 0
        while (idx < packet.size && steps < 128) {
            steps++
            val len = packet[idx].toInt() and 0xFF
            if (len == 0) return idx + 1
            if ((len and 0xC0) == 0xC0) {
                return if (idx + 2 <= packet.size) idx + 2 else null
            }
            idx += 1 + len
        }
        return null
    }

    internal fun buildServFail(query: ByteArray): ByteArray {
        val size = query.size.coerceAtLeast(12)
        val out = ByteArray(size)
        if (query.isNotEmpty()) {
            System.arraycopy(query, 0, out, 0, query.size.coerceAtMost(size))
        }
        // Set QR=1 (response), preserve RD, set RA=1, RCODE=2 (SERVFAIL)
        out[2] = ((out[2].toInt() and 0x01) or 0x80).toByte()
        out[3] = 0x82.toByte()
        // Zero ANCOUNT, NSCOUNT, ARCOUNT
        for (i in 6..11) out[i] = 0
        return out
    }
}
