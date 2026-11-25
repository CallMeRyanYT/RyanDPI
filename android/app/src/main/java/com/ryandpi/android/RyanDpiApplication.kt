package com.ryandpi.android

import android.app.Application
import com.ryandpi.android.engine.LogRepository

class RyanDpiApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        LogRepository.info("RyanDPI Android v${BuildConfig.VERSION_NAME} initialized.")
    }
}
