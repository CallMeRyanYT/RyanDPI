# RyanDPI v3.1.0 — Unbound

**English** | [Türkçe](README.tr.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [فارسی](README.fa.md) | [Bahasa Indonesia](README.id.md) | [简体中文](README.zh.md) | [العربية](README.ar.md)

**RyanDPI — Unbound** is a sleek, one-click **Windows & Android** global zero-lag DPI bypass & Discord split-tunnel tool built to unlock **Discord, Discord Screen Sharing & Video, Roblox, Roblox Studio, YouTube, Instagram, Wattpad, and every blocked website/app worldwide** — **without increasing your gaming ping or cutting your internet speed**.

With **v3.1.0 — Unbound**, RyanDPI goes global with a **First-Startup Country/Region Picker**, a **7-Stage Universal Global DPI Engine**, dedicated **Turkey (BTK)**, **Russia (TSPU)**, and **Brazil (Discord Screen Share 0ms-Game-Lag Split-Tunnel)** modes, and **8 built-in languages**.

---

## Download & Quick Start (`v3.1.0 — Unbound`)

[![Download Windows Setup](https://img.shields.io/badge/Download-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.0/RyanDPI-Setup-Windows-3.1.0.exe)
[![Download Portable ZIP](https://img.shields.io/badge/Download-Windows%20Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.0/RyanDPI-Portable-Windows-3.1.0.zip)
[![Download Android APK](https://img.shields.io/badge/Download-Android%20APK%20(v3.1.0)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.0/RyanDPI-Android-3.1.0.apk)

---

## Country & Global Bypass Modes

On first startup (or anytime via the top-bar **Region Pill**), choose the mode that matches your country:

| Mode | Designed For | How It Works (Zero Gaming Lag) |
| :--- | :--- | :--- |
| **Universal Global** *(Recommended)* | Turkey, Russia, Iran, Indonesia, Egypt/MENA & any DPI-censored ISP worldwide | 7-stage zero-retransmit local DPI engine starting at 0ms `--tlsrec 1+s` and auto-escalating across strict ISPs with 24h per-IP caching + Encrypted DNS. **Not a remote VPN — full native speed!** |
| **Turkey — BTK Zero-Lag** | Discord, Roblox, Roblox Studio, Wattpad & all BTK-blocked sites | Zero-retransmit TLS splitting + Encrypted DNS (defeats Türk Telekom / Superonline / Vodafone / Turkcell UDP/53 blocks). Gaming UDP stays 100% direct at 0ms added ping. |
| **Russia — TSPU Full Unblock** | YouTube, Discord (+ Voice), Instagram, Roblox & TSPU-throttled sites | Starts immediately on TSPU-proven `--split 2 --disorder -1+s --tlsrec 1+s`, auto-escalating to OOB & fake-SNI TTL packets + UDP voice bypass (`-a 1`) + Encrypted DoH DNS. |
| **Brazil — Discord Screen Share & Video** | Restoring Discord **Go Live / Screen Sharing & Camera** restricted under Brazil's ANPD Geo-IP order | **Discord-Only Split-Tunnel (0ms lag in games!):** Routes **ONLY Discord** (leaving Valorant, CS2, Roblox, Fortnite & every game 100% direct!) through built-in zero-login **Cloudflare WARP MASQUE (`usque`)** OR your own **Custom Non-Brazil SOCKS5 Proxy (US / AR / EU)**. |

> **Important Note for Brazil Users:** Discord disables Screen Share & Camera in Brazil based on your **public Geo-IP** (ANPD ECA Digital order), not ISP DPI. Pure local DPI splitting cannot spoof a public IP address. RyanDPI solves this without lagging your games by tunneling **ONLY Discord** (`ProxiFyre` app-only rule on Windows; `VpnService.addAllowedApplication("com.discord")` on Android). Run the built-in **Self-Test** to check your live Discord Tunnel Exit Country — if Cloudflare WARP exits locally in São Paulo (`BR`), paste any US/AR/EU SOCKS5 proxy in **Settings** for a guaranteed non-BR exit while keeping **0ms lag in all games**.

---

## Key Features

- **Windows & Android Support:** Native desktop app for Windows 10/11 (x64) AND zero-root **Android APK** (Android 10–16, Universal `arm64-v8a` / `armeabi-v7a` / `x86_64` / `x86`) with Quick Settings Tile and Split Tunnel controls.
- **8 Built-In Languages (1-Click Picker):** Instant in-app switching across **English (`EN`)**, **Türkçe (`TR`)**, **Português Brasil (`PT`)**, **Русский (`RU`)**, **فارسی (`FA`)**, **Bahasa Indonesia (`ID`)**, **简体中文 (`ZH`)**, and **العربية (`AR`)** on both Windows and Android.
- **Zero Speed Loss & 0ms Gaming Ping:** Local DPI mode never routes traffic through remote VPN servers. Brazil mode strictly isolates Discord so games stay 100% direct.
- **Built-In Encrypted DNS:** Automatically defeats ISP DNS poisoning and plain UDP/53 blocks using **DNSCrypt-Proxy** on Windows and a fast **HTTP/2 Encrypted DoH Gateway** on Android.
- **Region-Aware Live Self-Test:** Tests Discord API, Discord Media, Roblox App, Roblox Studio, YouTube, Instagram, Wattpad, Encrypted DNS, Direct Game Baseline Ping, and (in Brazil mode) live Discord Tunnel Exit IP & Country.
- **Automatic Wi-Fi / Network Recovery & SHA-256 Auto-Updater:** Automatically heals when switching Wi-Fi/Ethernet on Windows and verifies SHA-256 hashes before upgrading on both Windows and Android.

---

## Quick Start

### Windows (10 & 11 x64)
1. Download and run **[RyanDPI-Setup-Windows-3.1.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.0/RyanDPI-Setup-Windows-3.1.0.exe)** (or extract [RyanDPI-Portable-Windows-3.1.0.zip](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.0/RyanDPI-Portable-Windows-3.1.0.zip) and run `RyanDPI.exe`).
2. Pick your country mode (**Universal Global**, **Turkey**, **Russia**, or **Brazil**) on first launch — or change it anytime from the top bar.
3. Click the **Language button (`EN`)** in the top bar to switch to any of the 8 supported languages in 1 click.
4. Click **"Install & Enable RyanDPI"** and then **"Run Test"** to verify unlocked services and live ping.

### Android (Android 10–16, No Root Required)
1. Download **[RyanDPI-Android-3.1.0.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.0/RyanDPI-Android-3.1.0.apk)** on your phone and tap to install *(allow "Install unknown apps" if prompted)*.
2. Pick your region (**Universal Global**, **Turkey**, **Russia**, or **Brazil**), tap **"Connect & Unlock"**, and approve Android's local VPN prompt.
3. Tap **"Run Test"** to verify unlocked services, exit country, and ping. Add the **RyanDPI Quick Settings Tile** for 1-tap toggling anywhere.

---

## Building from Source

### Windows (Requires .NET 8 SDK + optional Inno Setup 6)
```bat
git clone https://github.com/CallMeRyanYT/RyanDPI.git
cd RyanDPI
src\build.bat
```

### Android (Requires JDK 17–21 + Android SDK API 36)
```bat
cd android
gradlew.bat assembleRelease
```

---

## Complete Removal

Click **Uninstall** inside RyanDPI (or uninstall normally via Windows *Settings > Apps* / Android Settings) to cleanly remove all services, firewall rules, and restore automatic DHCP DNS.

---

## Open-Source Components

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** by hufrea
- **[usque](https://github.com/Diniboy1123/usque)** by Diniboy1123 (Cloudflare WARP HTTP/3 MASQUE engine)
- **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** by heiher (Android native TUN engine)
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** by NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** by Frank Denis

---

## License

MIT License — see [LICENSE](LICENSE).
