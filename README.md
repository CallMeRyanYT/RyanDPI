# RyanDPI

**English** | [Türkçe](README.tr.md)

**RyanDPI** is a sleek, one-click **Windows & Android** DPI bypass tool (with instant **English & Turkish** language switching) designed to unlock **every blocked app and website in Turkey** — including **Discord**, **Roblox**, **Roblox Studio**, **Wattpad**, all web browsers, and every other app — **without affecting your internet speed or gaming ping**.

RyanDPI replaces complex multi-tool setups with a single, high-speed, zero-root local engine that works right out of the box on both PC and Android.

---

## Why RyanDPI?

- **Windows & Android Support:** Full native desktop app for Windows 10/11 AND a zero-root **Android APK** (Android 10–16, Universal `arm64-v8a` / `armeabi-v7a` / `x86_64` / `x86`) with Quick Settings Tile, Split Tunnel app exclusion, and built-in Encrypted DoH DNS.
- **Unlocks Everything Automatically (Zero Whitelisting):** Unlike app-whitelisted ByeDPI setups that leave apps like **Roblox Studio**, embedded WebView2 logins, CLI tools, or newly installed apps blocked, RyanDPI transparently unlocks **all apps** system-wide.
- **Zero Speed Loss — Native ISP Speeds:** RyanDPI is **not a remote VPN**. Your traffic never touches any third-party server. It connects directly over your own Wi-Fi, Ethernet, or 4G/5G line using zero-retransmit TLS record splitting (`--tlsrec 1+s`) on the first handshake packet while transferring all downloads, streams, and uploads at full line speed.
- **Lowest Possible Ping & Native 0ms Gaming Ping:** On Windows, RyanDPI handles **TCP** while leaving **UDP** (online game servers, RakNet) completely direct and bypasses LAN/localhost automatically.
- **Built-in Encrypted DNS (Defeats Turkish ISP UDP/53 Blocks):** Automatically eliminates Turkish ISP DNS poisoning & UDP/53 blocking — using local **DNSCrypt-Proxy** on Windows and a built-in **HTTP/2 Encrypted DoH Gateway** (`1.1.1.1` / `8.8.8.8` / `9.9.9.9` with LRU TTL caching and automatic failover) on Android with zero phone settings required.
- **6-Stage Adaptive Bypass Strategy:** Starts with zero-retransmit TLS splitting and automatically adapts across Türk Telekom, Superonline, Vodafone, Turkcell, Türksat Kablonet, and TurkNet with 24-hour per-IP caching.
- **Built-in Automatic Updater:** Both Windows and Android check GitHub Releases on launch, verify SHA-256 digests, and upgrade cleanly with one click.
- **Neon Red & Black UI + Instant English / Turkish Toggle:** Switch between English and Turkish with one tap in the top bar, Dark & Light modes, and a live **8-check Unblock & Speed Self-Test** (Discord, Roblox App, Roblox Studio, Roblox Web, Wattpad, Encrypted DNS, Engine & `1.1.1.1` baseline ping).

---

## Download & Quick Start

[![Download Windows Setup](https://img.shields.io/badge/Download-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Setup-Windows-2.4.0.exe)
[![Download Portable ZIP](https://img.shields.io/badge/Download-Windows%20Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Portable-Windows-2.4.0.zip)
[![Download Android APK](https://img.shields.io/badge/Download-Android%20APK%20(v2.4.0)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Android-2.4.0.apk)

### Windows (10 & 11 x64)
1. Download and run **[RyanDPI-Setup-Windows-2.4.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Setup-Windows-2.4.0.exe)** (or extract the [Portable ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Portable-Windows-2.4.0.zip) and run `RyanDPI.exe`).
2. Launch **RyanDPI** (Administrator privileges are requested automatically to manage Windows services and DNS).
3. *(Optional)* Click **`TR`** in the top-right title bar anytime to switch the entire app to Turkish.
4. Click **"Install & Enable RyanDPI"** and wait ~10 seconds for the 5-step automatic setup to complete.
5. Click **"Run Test"** to verify Discord, Roblox App, Roblox Studio API, Roblox Web, Wattpad, and live ping.

RyanDPI runs quietly as Windows background services (`RyanDpiEngine`, `ProxiFyreService`, and `dnscrypt-proxy`) with automatic Wi-Fi/network-change recovery.

### Android (Android 10–16, No Root Required)
1. Download **[RyanDPI-Android-2.4.0.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Android-2.4.0.apk)** directly on your Android phone and open it to install *(allow "Install unknown apps" for your browser if prompted)*.
2. Open **RyanDPI**, tap **"Connect & Unlock All"**, and approve Android's local VPN dialog *(100% on-device — no remote VPN server!)*.
3. Tap **"Run Test"** to verify Discord, Roblox App, Wattpad & Encrypted DoH DNS live.
4. *(Tip)* Add the **RyanDPI Quick Settings Tile** to your Android notification shade to toggle bypass in 1 tap from anywhere!

---

## Optional Settings

RyanDPI works out of the box with zero configuration, but you can customize it anytime in the **Settings** tab:

- **DPI Bypass Profile:**
  - *Auto Universal (Recommended)* — 6-stage zero-retransmit adaptive Turkey ISP chain
  - *Classic Mode* — Lightweight TLS record split with automatic fallback and full-speed buffers
  - *Strict ISP* — Aggressive TLS record + TCP split + OOB + low-TTL fake chain
  - *Custom* — Supply your own `ciadpi.exe` flags
- **DNS Protection:** Encrypted DNS (`dnscrypt-proxy`), Windows Native DoH, or Keep Existing DNS.
- **Bypass LAN & Localhost:** Enabled by default so local servers and LAN devices stay direct.
- **Also Tunnel UDP (Voice / QUIC):** Off by default to keep gaming ping untouched; enable if your ISP blocks Discord voice UDP or QUIC.
- **Auto-Update on Startup:** Enabled by default; checks GitHub Releases and updates automatically when opened.
- **Excluded Apps:** Optional list of executables to skip RyanDPI (core engine and DNS services/binaries are always excluded automatically to guarantee loop prevention).

---

## Building from Source

### Requirements
- Windows 10 or Windows 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- *(Optional)* [Inno Setup 6](https://jrsoftware.org/isinfo.php) to build the Windows Setup `.exe`

### Compile
```bat
git clone https://github.com/CallMeRyanYT/RyanDPI.git
cd RyanDPI
src\build.bat
```
Or directly with `dotnet`:
```powershell
dotnet build src\RyanDPI\RyanDPI.csproj -c Release
```
The ready-to-run build (including all bundled engine binaries and prerequisites) is output to `dist\RyanDPI\RyanDPI.exe` (or `src\RyanDPI\bin\Release\net8.0-windows\RyanDPI.exe`).

### Compile Android APK
Requires JDK 17–21 and Android SDK (API 36):
```bat
cd android
gradlew.bat assembleRelease
```

---

## Complete Removal

You can remove RyanDPI and revert every system change (services, firewall rules, and automatic DHCP DNS) at any time by:
- Clicking **Uninstall** directly inside RyanDPI, or
- Uninstalling **RyanDPI** from Windows *Settings > Apps* (or uninstalling the Android app normally).

---

## Open-Source Components

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** by hufrea
- **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** by heiher (Android native TUN engine)
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** by NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** by Frank Denis

---

## License

MIT License — see [LICENSE](LICENSE).
