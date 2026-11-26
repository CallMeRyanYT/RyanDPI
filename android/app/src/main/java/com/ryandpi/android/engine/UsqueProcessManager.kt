package com.ryandpi.android.engine

import android.content.Context
import java.io.BufferedReader
import java.io.File
import java.io.InputStreamReader
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Manages the native Usque (Cloudflare WARP MASQUE HTTP/3 + UDP SOCKS5) binary
 * packaged as jniLibs/arm64-v8a/libusque.so for Brazil Discord-Only Split-Tunnel mode.
 */
class UsqueProcessManager(private val context: Context) {
    private val running = AtomicBoolean(false)
    @Volatile
    private var process: Process? = null
    private var monitorThread: Thread? = null

    fun resolveBinaryFile(): File = File(context.applicationInfo.nativeLibraryDir, "libusque.so")
    private fun resolveConfigFile(): File = File(context.filesDir, "usque-config.json")

    private fun isValidConfig(file: File): Boolean {
        return runCatching {
            if (!file.exists() || file.length() < 50L) return false
            val txt = file.readText(Charsets.UTF_8)
            txt.contains("\"private_key\"", ignoreCase = true) || txt.contains("\"endpoint", ignoreCase = true)
        }.getOrDefault(false)
    }

    fun start(
        listenPort: Int = RyanDpiSettings.USQUE_SOCKS_PORT,
        onUnexpectedExit: (() -> Unit)? = null
    ): Result<Unit> {
        stop()

        val binary = resolveBinaryFile()
        if (!binary.exists()) {
            val msg = "Built-in WARP MASQUE requires a 64-bit ARM phone (arm64-v8a). Select Custom SOCKS5 in Settings for this device."
            LogRepository.error(msg)
            return Result.failure(IllegalStateException(msg))
        }
        if (!binary.canExecute()) {
            runCatching { binary.setExecutable(true, false) }
        }

        val configFile = resolveConfigFile()
        if (!isValidConfig(configFile)) {
            runCatching { if (configFile.exists()) configFile.delete() }
            LogRepository.info("Registering zero-login Cloudflare WARP MASQUE identity...")
            var regOk = false
            for (attempt in 1..2) {
                regOk = runCatching {
                    val regProc = ProcessBuilder(binary.absolutePath, "register", "-a", "-c", configFile.absolutePath)
                        .directory(context.filesDir)
                        .redirectErrorStream(true)
                        .start()
                    regProc.waitFor(20, TimeUnit.SECONDS)
                    isValidConfig(configFile)
                }.getOrDefault(false)
                if (regOk) break
                runCatching { if (configFile.exists()) configFile.delete() }
                if (attempt == 1) runCatching { Thread.sleep(1000) }
            }

            if (!regOk) {
                val err = "Failed to register Cloudflare WARP MASQUE identity. Check internet connection or use Custom SOCKS5."
                LogRepository.error(err)
                return Result.failure(IllegalStateException(err))
            }
        }

        LogRepository.info("Starting Brazil Discord Split-Tunnel engine (WARP MASQUE on 127.0.0.1:$listenPort)...")
        return try {
            val proc = ProcessBuilder(
                binary.absolutePath,
                "socks",
                "-c", configFile.absolutePath,
                "-b", "127.0.0.1",
                "-p", listenPort.toString(),
                "--always-reconnect",
                "--udp-timeout", "180s"
            )
                .directory(context.filesDir)
                .redirectErrorStream(true)
                .start()

            process = proc
            running.set(true)

            monitorThread = Thread({
                try {
                    BufferedReader(InputStreamReader(proc.inputStream)).use { reader ->
                        var line: String?
                        while (reader.readLine().also { line = it } != null) {
                            val text = line?.trim().orEmpty()
                            if (text.isNotEmpty()) {
                                LogRepository.info("[usque] $text")
                            }
                        }
                    }
                    runCatching { proc.waitFor() }
                } catch (_: Exception) {
                    // Process stopped
                } finally {
                    if (running.get() && process === proc) {
                        onUnexpectedExit?.invoke()
                    }
                }
            }, "ryandpi-usque-log").apply {
                isDaemon = true
                start()
            }

            if (!waitUntilPortListening(listenPort, timeoutMs = 8000L)) {
                stop()
                val err = "WARP MASQUE engine failed to listen on 127.0.0.1:$listenPort"
                LogRepository.error(err)
                return Result.failure(IllegalStateException(err))
            }

            LogRepository.info("Brazil Split-Tunnel WARP MASQUE ready on 127.0.0.1:$listenPort.")
            Result.success(Unit)
        } catch (e: Exception) {
            stop()
            val err = "Failed to start WARP MASQUE: ${e.message}"
            LogRepository.error(err)
            Result.failure(e)
        }
    }

    fun isRunning(): Boolean {
        val p = process ?: return false
        return running.get() && isAlive(p)
    }

    fun stop() {
        running.set(false)
        val p = process
        process = null
        if (p != null) {
            runCatching { p.destroy() }
            runCatching {
                Thread.sleep(100)
                if (isAlive(p)) p.destroyForcibly()
            }
        }
        monitorThread = null
    }

    private fun isAlive(p: Process): Boolean {
        return try {
            p.exitValue()
            false
        } catch (_: IllegalThreadStateException) {
            true
        }
    }

    private fun waitUntilPortListening(port: Int, timeoutMs: Long): Boolean {
        val deadline = System.currentTimeMillis() + timeoutMs
        while (System.currentTimeMillis() < deadline) {
            val p = process ?: return false
            if (!isAlive(p)) return false
            try {
                Socket().use { s ->
                    s.connect(InetSocketAddress("127.0.0.1", port), 250)
                    return true
                }
            } catch (_: Exception) {
                try {
                    Thread.sleep(100)
                } catch (_: InterruptedException) {
                    return false
                }
            }
        }
        return false
    }
}
