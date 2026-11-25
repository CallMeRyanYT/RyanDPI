package com.ryandpi.android.engine

import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

enum class LogLevel { INFO, WARN, ERROR }

data class LogEntry(
    val timestamp: String,
    val level: LogLevel,
    val message: String
)

object LogRepository {
    private const val TAG = "RyanDPI"
    private const val MAX_ENTRIES = 300

    private val lock = Any()
    private val timeFormat = SimpleDateFormat("HH:mm:ss", Locale.US)
    private val _entries = MutableStateFlow<List<LogEntry>>(emptyList())
    val entries: StateFlow<List<LogEntry>> = _entries.asStateFlow()

    fun info(message: String) = append(LogLevel.INFO, message)
    fun warn(message: String) = append(LogLevel.WARN, message)
    fun error(message: String) = append(LogLevel.ERROR, message)

    private fun append(level: LogLevel, message: String) {
        val clean = message.trim()
        if (clean.isEmpty()) return

        runCatching {
            when (level) {
                LogLevel.INFO -> Log.i(TAG, clean)
                LogLevel.WARN -> Log.w(TAG, clean)
                LogLevel.ERROR -> Log.e(TAG, clean)
            }
        }

        synchronized(lock) {
            val ts = timeFormat.format(Date())
            val next = ArrayList<LogEntry>((_entries.value.size + 1).coerceAtMost(MAX_ENTRIES))
            val current = _entries.value
            val startIdx = if (current.size >= MAX_ENTRIES) current.size - MAX_ENTRIES + 1 else 0
            for (i in startIdx until current.size) {
                next.add(current[i])
            }
            next.add(LogEntry(ts, level, clean))
            _entries.value = next
        }
    }

    fun clear() {
        synchronized(lock) {
            _entries.value = emptyList()
        }
    }

    fun exportAsText(): String {
        return _entries.value.joinToString("\n") { "[${it.timestamp}] [${it.level.name}] ${it.message}" }
    }
}
