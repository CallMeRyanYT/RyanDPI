# Preserve JNI methods in hev-socks5-tunnel.aar
-keep class hev.htproxy.** { *; }

# Keep OkHttp / Kotlin coroutines cleanly
-dontwarn okhttp3.**
-dontwarn okio.**
-dontwarn javax.annotation.**
-dontwarn org.conscrypt.**
-dontwarn org.bouncycastle.**
-dontwarn org.openjsse.**
