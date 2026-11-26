package com.ryandpi.android.engine

import android.content.Context
import java.io.BufferedReader
import java.io.File
import java.io.InputStreamReader
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Manages the native ByeDPI (ciadpi) ELF executable packaged as jniLibs/<abi>/libciadpi.so.
 * Android extracts libciadpi.so into applicationInfo.nativeLibraryDir (with execute permissions
 * permitted by SELinux on Android 7 through Android 16).
 */
class CiadpiProcessManager(private val context: Context) {
    private val running = AtomicBoolean(false)
    @Volatile
    private var process: Process? = null
    private var monitorThread: Thread? = null

    fun resolveBinaryFile(): File {
        val nativeDir = context.applicationInfo.nativeLibraryDir
        return File(nativeDir, "libciadpi.so")
    }

    fun start(
        settings: RyanDpiSettings,
        listenPort: Int,
        onUnexpectedExit: (() -> Unit)? = null
    ): Result<Unit> {
        stop()

        val binary = resolveBinaryFile()
        if (!binary.exists()) {
            val msg = "Native DPI binary not found at ${binary.absolutePath}"
            LogRepository.error(msg)
            return Result.failure(IllegalStateException(msg))
        }

        if (!binary.canExecute()) {
            runCatching { binary.setExecutable(true, false) }
        }

        val cmd = mutableListOf(binary.absolutePath)
        cmd.addAll(settings.buildCiadpiArgList(listenPort))

        LogRepository.info("Starting native ciadpi engine on 127.0.0.1:$listenPort (${settings.bypassProfile.name})...")
        return try {
            val pb = ProcessBuilder(cmd)
                .directory(context.cacheDir)
                .redirectErrorStream(true)
            val proc = pb.start()
            process = proc
            running.set(true)

            monitorThread = Thread({
                try {
                    BufferedReader(InputStreamReader(proc.inputStream)).use { reader ->
                        var line: String?
                        while (reader.readLine().also { line = it } != null) {
                            val text = line?.trim().orEmpty()
                            if (text.isNotEmpty()) {
                                LogRepository.info("[ciadpi] $text")
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
            }, "ryandpi-ciadpi-log").apply {
                isDaemon = true
                start()
            }

            if (!waitUntilPortListening(listenPort, timeoutMs = 4000L)) {
                val exited = !isAlive(proc)
                val exitMsg = if (exited) " (exited with code ${runCatching { proc.exitValue() }.getOrDefault(-1)})" else ""
                stop()
                val err = "ciadpi failed to listen on 127.0.0.1:$listenPort$exitMsg"
                LogRepository.error(err)
                return Result.failure(IllegalStateException(err))
            }

            LogRepository.info("Native ciadpi engine ready on 127.0.0.1:$listenPort.")
            Result.success(Unit)
        } catch (e: Exception) {
            stop()
            val err = "Failed to start ciadpi: ${e.message}"
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
                if (isAlive(p)) {
                    p.destroyForcibly()
                }
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
                    s.connect(InetSocketAddress("127.0.0.1", port), 180)
                    return true
                }
            } catch (_: Exception) {
                try {
                    Thread.sleep(60)
                } catch (_: InterruptedException) {
                    return false
                }
            }
        }
        return false
    }
}
