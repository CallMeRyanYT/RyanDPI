package com.ryandpi.android.network

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.core.content.FileProvider
import com.ryandpi.android.BuildConfig
import com.ryandpi.android.engine.LogRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.net.URI
import java.security.MessageDigest
import java.util.Locale
import java.util.concurrent.TimeUnit

data class AndroidUpdateInfo(
    val success: Boolean,
    val updateAvailable: Boolean,
    val currentVersion: String,
    val latestVersion: String = "",
    val releaseName: String = "",
    val releaseUrl: String = UpdateChecker.RELEASES_PAGE_URL,
    val assetName: String = "",
    val downloadUrl: String = "",
    val expectedSize: Long = 0L,
    val expectedSha256: String? = null,
    val errorMessage: String? = null
)

object UpdateChecker {
    const val GITHUB_OWNER = "CallMeRyanYT"
    const val GITHUB_REPO = "RyanDPI"
    const val LATEST_RELEASE_API_URL = "https://api.github.com/repos/$GITHUB_OWNER/$GITHUB_REPO/releases/latest"
    const val RELEASES_PAGE_URL = "https://github.com/$GITHUB_OWNER/$GITHUB_REPO/releases"

    private val TRUSTED_HOSTS = setOf(
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "github-releases.githubusercontent.com"
    )

    private val httpClient: OkHttpClient by lazy {
        OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(25, TimeUnit.SECONDS)
            .followRedirects(true)
            .build()
    }

    suspend fun checkForUpdate(currentVersion: String = BuildConfig.VERSION_NAME): AndroidUpdateInfo =
        withContext(Dispatchers.IO) {
            try {
                val req = Request.Builder()
                    .url(LATEST_RELEASE_API_URL)
                    .header("Accept", "application/vnd.github+json")
                    .header("X-GitHub-Api-Version", "2022-11-28")
                    .header("User-Agent", "RyanDPI-Android/$currentVersion")
                    .build()

                httpClient.newCall(req).execute().use { resp ->
                    if (!resp.isSuccessful) {
                        return@withContext AndroidUpdateInfo(
                            success = false,
                            updateAvailable = false,
                            currentVersion = currentVersion,
                            errorMessage = "GitHub API returned HTTP ${resp.code}"
                        )
                    }
                    val body = resp.body?.string().orEmpty()
                    parseReleaseJson(body, currentVersion)
                }
            } catch (e: Exception) {
                AndroidUpdateInfo(
                    success = false,
                    updateAvailable = false,
                    currentVersion = currentVersion,
                    errorMessage = e.message ?: "Update check failed"
                )
            }
        }

    fun parseReleaseJson(json: String, currentVersion: String): AndroidUpdateInfo {
        return try {
            val root = JSONObject(json)
            val tagName = root.optString("tag_name", "").trim()
            if (tagName.isEmpty()) {
                return AndroidUpdateInfo(false, false, currentVersion, errorMessage = "Invalid GitHub release payload.")
            }
            if (root.optBoolean("draft", false) || root.optBoolean("prerelease", false)) {
                return AndroidUpdateInfo(true, false, currentVersion)
            }

            val latestClean = cleanVersion(tagName)
                ?: return AndroidUpdateInfo(false, false, currentVersion, errorMessage = "Invalid version tag: $tagName")
            val currentClean = cleanVersion(currentVersion) ?: "0.0.0"

            val releaseName = root.optString("name", "").ifBlank { "RyanDPI v$latestClean" }
            val releaseUrl = root.optString("html_url", RELEASES_PAGE_URL).ifBlank { RELEASES_PAGE_URL }

            if (compareVersions(latestClean, currentClean) <= 0) {
                return AndroidUpdateInfo(
                    success = true,
                    updateAvailable = false,
                    currentVersion = currentClean,
                    latestVersion = latestClean,
                    releaseName = releaseName,
                    releaseUrl = releaseUrl
                )
            }

            val assets = root.optJSONArray("assets")
            var chosenName = ""
            var chosenUrl = ""
            var chosenSize = 0L
            var chosenSha256: String? = null

            if (assets != null) {
                var fallbackApkIdx = -1
                var preferredApkIdx = -1
                for (i in 0 until assets.length()) {
                    val item = assets.optJSONObject(i) ?: continue
                    val name = item.optString("name", "")
                    val url = item.optString("browser_download_url", "")
                    if (!name.lowercase(Locale.ROOT).endsWith(".apk")) continue
                    if (!isTrustedGitHubUrl(url)) continue
                    if (fallbackApkIdx == -1) fallbackApkIdx = i
                    if (name.contains("Android", ignoreCase = true) || name.contains("RyanDPI", ignoreCase = true)) {
                        preferredApkIdx = i
                        break
                    }
                }
                val idx = if (preferredApkIdx >= 0) preferredApkIdx else fallbackApkIdx
                if (idx >= 0) {
                    val asset = assets.getJSONObject(idx)
                    chosenName = asset.optString("name", "RyanDPI-Android-$latestClean.apk")
                    chosenUrl = asset.optString("browser_download_url", "")
                    chosenSize = asset.optLong("size", 0L)
                    chosenSha256 = parseSha256Digest(asset.optString("digest", ""))
                }
            }

            if (chosenUrl.isBlank()) {
                return AndroidUpdateInfo(
                    success = false,
                    updateAvailable = false,
                    currentVersion = currentClean,
                    latestVersion = latestClean,
                    releaseName = releaseName,
                    releaseUrl = releaseUrl,
                    errorMessage = "No Android APK found in release $tagName."
                )
            }

            AndroidUpdateInfo(
                success = true,
                updateAvailable = true,
                currentVersion = currentClean,
                latestVersion = latestClean,
                releaseName = releaseName,
                releaseUrl = releaseUrl,
                assetName = chosenName,
                downloadUrl = chosenUrl,
                expectedSize = chosenSize,
                expectedSha256 = chosenSha256
            )
        } catch (e: Exception) {
            AndroidUpdateInfo(false, false, currentVersion, errorMessage = e.message ?: "Parse error")
        }
    }

    suspend fun downloadAndVerifyApk(
        context: Context,
        update: AndroidUpdateInfo,
        onProgress: (Int) -> Unit
    ): Result<File> = withContext(Dispatchers.IO) {
        if (!update.updateAvailable || !isTrustedGitHubUrl(update.downloadUrl)) {
            return@withContext Result.failure(IllegalArgumentException("Untrusted or invalid update URL"))
        }

        val updatesDir = File(context.cacheDir, "updates").apply { mkdirs() }
        // Clean old cached APKs
        updatesDir.listFiles()?.forEach { runCatching { it.delete() } }

        val safeName = File(update.assetName.ifBlank { "RyanDPI-Android-${update.latestVersion}.apk" }).name
        val partFile = File(updatesDir, "$safeName.part")
        val finalFile = File(updatesDir, safeName)

        try {
            val req = Request.Builder()
                .url(update.downloadUrl)
                .header("User-Agent", "RyanDPI-Android/${BuildConfig.VERSION_NAME}")
                .build()

            httpClient.newCall(req).execute().use { resp ->
                if (!resp.isSuccessful) {
                    return@withContext Result.failure(IllegalStateException("Download failed: HTTP ${resp.code}"))
                }
                val finalUrl = resp.request.url.toString()
                if (!isTrustedGitHubUrl(finalUrl)) {
                    return@withContext Result.failure(SecurityException("Redirected to untrusted host"))
                }

                val body = resp.body ?: return@withContext Result.failure(IllegalStateException("Empty download body"))
                val totalBytes = body.contentLength().takeIf { it > 0 } ?: update.expectedSize
                val digest = MessageDigest.getInstance("SHA-256")
                var received = 0L
                var lastPct = -1

                body.byteStream().use { input ->
                    FileOutputStream(partFile).use { output ->
                        val buf = ByteArray(32 * 1024)
                        while (true) {
                            val n = input.read(buf)
                            if (n <= 0) break
                            output.write(buf, 0, n)
                            digest.update(buf, 0, n)
                            received += n
                            if (totalBytes > 0) {
                                val pct = ((received * 100L) / totalBytes).toInt().coerceIn(0, 100)
                                if (pct != lastPct) {
                                    lastPct = pct
                                    onProgress(pct)
                                }
                            }
                        }
                    }
                }

                if (partFile.length() < 500_000L) {
                    partFile.delete()
                    return@withContext Result.failure(IllegalStateException("Downloaded APK is unexpectedly small"))
                }
                if (update.expectedSize > 0 && partFile.length() != update.expectedSize) {
                    partFile.delete()
                    return@withContext Result.failure(IllegalStateException("Downloaded APK size mismatch"))
                }

                // Verify APK ZIP magic header ('P','K')
                val magic = ByteArray(2)
                partFile.inputStream().use { it.read(magic) }
                if (magic[0] != 0x50.toByte() || magic[1] != 0x4B.toByte()) {
                    partFile.delete()
                    return@withContext Result.failure(IllegalStateException("Downloaded file is not a valid APK"))
                }

                // Verify SHA-256 checksum when provided by GitHub API
                val expectedHash = update.expectedSha256
                if (!expectedHash.isNullOrBlank()) {
                    val actualHash = digest.digest().joinToString("") { "%02x".format(it) }
                    if (!actualHash.equals(expectedHash, ignoreCase = true)) {
                        partFile.delete()
                        return@withContext Result.failure(SecurityException("SHA-256 checksum verification failed"))
                    }
                    LogRepository.info("Android update SHA-256 verified: $actualHash")
                }

                if (finalFile.exists()) finalFile.delete()
                if (!partFile.renameTo(finalFile)) {
                    partFile.copyTo(finalFile, overwrite = true)
                    partFile.delete()
                }
                onProgress(100)
                Result.success(finalFile)
            }
        } catch (e: Exception) {
            runCatching { partFile.delete() }
            Result.failure(e)
        }
    }

    fun buildInstallIntent(context: Context, apkFile: File): Intent {
        val uri: Uri = FileProvider.getUriForFile(
            context,
            "${context.packageName}.fileprovider",
            apkFile
        )
        return Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
    }

    fun isTrustedGitHubUrl(url: String?): Boolean {
        if (url.isNullOrBlank()) return false
        return try {
            val uri = URI(url)
            if (!uri.scheme.equals("https", ignoreCase = true)) return false
            val host = (uri.host ?: return false).lowercase(Locale.ROOT)
            TRUSTED_HOSTS.any { trusted -> host == trusted || host.endsWith(".$trusted") }
        } catch (_: Exception) {
            false
        }
    }

    fun parseSha256Digest(raw: String?): String? {
        if (raw.isNullOrBlank()) return null
        val cleaned = raw.trim().removePrefix("sha256:").removePrefix("SHA256:").trim()
        if (cleaned.length != 64) return null
        if (!cleaned.all { it in '0'..'9' || it in 'a'..'f' || it in 'A'..'F' }) return null
        return cleaned.lowercase(Locale.ROOT)
    }

    fun cleanVersion(raw: String?): String? {
        if (raw.isNullOrBlank()) return null
        var s = raw.trim().removePrefix("v").removePrefix("V")
        val cut = s.indexOfAny(charArrayOf('-', '+'))
        if (cut > 0) s = s.substring(0, cut)
        val parts = s.split('.')
        if (parts.isEmpty() || parts.size > 4) return null
        if (parts.any { it.toIntOrNull() == null }) return null
        return when (parts.size) {
            1 -> "${parts[0]}.0.0"
            2 -> "${parts[0]}.${parts[1]}.0"
            else -> "${parts[0]}.${parts[1]}.${parts[2]}"
        }
    }

    fun compareVersions(a: String, b: String): Int {
        val pa = (cleanVersion(a) ?: "0.0.0").split('.').map { it.toInt() }
        val pb = (cleanVersion(b) ?: "0.0.0").split('.').map { it.toInt() }
        for (i in 0 until 3) {
            val diff = pa[i] - pb[i]
            if (diff != 0) return diff
        }
        return 0
    }
}
