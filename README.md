# RyanDPI v3.2.0 — Unbound

**English** | [Türkçe](README.tr.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [فارسی](README.fa.md) | [Bahasa Indonesia](README.id.md) | [简体中文](README.zh.md) | [العربية](README.ar.md)

**RyanDPI — Unbound** is a sleek, one-click **Windows, Android & iOS (iPhone / iPad)** global zero-lag DPI bypass & Discord split-tunnel tool built to unlock **Discord, Discord Screen Sharing & Video, Roblox, Roblox Studio, YouTube (Web + HD Video Streams), Instagram, Wattpad, and every blocked website/app worldwide** — **without increasing your gaming ping or cutting your internet speed**.

With **v3.2.0 — Unbound**, RyanDPI adds **native iOS / iPhone support** (Full System-Wide VPN `.ipa`, Free Apple ID Local Proxy `.ipa`, and 1-tap Zero-Sideload `.mobileconfig` Encrypted DNS), fixes **Russia YouTube HD video playback (`*.googlevideo.com` GGC)** + **IPv4/IPv6 dual-stack routing** + **Russia Discord opening**, and keeps **8 built-in languages** across desktop and mobile.

---

## Download & Quick Start (`v3.2.0 — Unbound`)

[![Download Windows Setup](https://img.shields.io/badge/Download-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Setup-Windows-3.2.0.exe)
[![Download Portable ZIP](https://img.shields.io/badge/Download-Windows%20Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Portable-Windows-3.2.0.zip)
[![Download Android APK](https://img.shields.io/badge/Download-Android%20APK%20(v3.2.0)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Android-3.2.0.apk)

[![Download iOS Full VPN IPA](https://img.shields.io/badge/Download-iOS%20Full%20VPN%20(.ipa)-007AFF?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-3.2.0.ipa)
[![Download iOS Free Apple ID IPA](https://img.shields.io/badge/Download-iOS%20Free%20Apple%20ID%20(.ipa)-5856D6?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-FreeAppleID-3.2.0.ipa)
[![Download iOS Encrypted DNS Profile](https://img.shields.io/badge/Download-iOS%20Encrypted%20DNS%20(.mobileconfig)-00B8D4?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-EncryptedDNS.mobileconfig)

---

## Country & Global Bypass Modes

On first startup (or anytime via the top-bar **Region Pill**), choose the mode that matches your country:

| Mode | Designed For | How It Works (Zero Gaming Lag) |
| :--- | :--- | :--- |
| **Universal Global** *(Recommended)* | Turkey, Russia, Iran, Indonesia, Egypt/MENA & any DPI-censored ISP worldwide | Dedicated `googlevideo.com` YouTube video fast-track + zero-retransmit Stage 1 (`--tlsrec 1+s`) auto-escalating across strict ISPs with 24h per-IP caching + Encrypted DNS + IPv4/IPv6 routing. **Not a remote VPN — full native speed!** |
| **Turkey — BTK Zero-Lag** | Discord, Roblox, Roblox Studio, Wattpad & all BTK-blocked sites | Zero-retransmit TLS splitting + Encrypted DNS (defeats Türk Telekom / Superonline / Vodafone / Turkcell UDP/53 blocks). Gaming UDP stays 100% direct at 0ms added ping. |
| **Russia — TSPU Full Unblock** | YouTube (**Web + HD Videos `googlevideo.com`**), Discord (+ Voice), Instagram & Roblox | Dedicated `--hosts :googlevideo.com` double TLS-record split (`1+s` + `-5+se` right through `google\|video.com`) + 3-byte SNI chop + proven Discord `--split 2 --disorder -1+s --tlsrec 1+s` chain + automatic QUIC `UDP/443 → TCP/443` fallback + IPv4/IPv6 interception! |
| **Brazil — Discord Screen Share & Video** | Restoring Discord **Go Live / Screen Sharing & Camera** restricted under Brazil's ANPD Geo-IP order | **Discord-Only Split-Tunnel (0ms lag in games!):** Routes **ONLY Discord** (leaving Valorant, CS2, Roblox, Fortnite & every game 100% direct!) through built-in zero-login **Cloudflare WARP MASQUE (`usque`)** OR your own **Custom Non-Brazil SOCKS5 Proxy (US / AR / EU)**. |

> **Important Note for Brazil Users:** Discord disables Screen Share & Camera in Brazil based on your **public Geo-IP** (ANPD ECA Digital order), not ISP DPI. Pure local DPI splitting cannot spoof a public IP address. RyanDPI solves this without lagging your games by tunneling **ONLY Discord** (`ProxiFyre` app-only rule on Windows; `VpnService.addAllowedApplication("com.discord")` on Android). Run the built-in **Self-Test** to check your live Discord Tunnel Exit Country — if Cloudflare WARP exits locally in São Paulo (`BR`), paste any US/AR/EU SOCKS5 proxy in **Settings** for a guaranteed non-BR exit while keeping **0ms lag in all games**.

---

## Key Features

- **Windows, Android & iOS Support:** Native desktop app for Windows 10/11 (x64), zero-root **Android APK** (Android 10–16, Universal 4-ABI), AND native **iOS / iPhone** builds (Full System-Wide VPN `.ipa`, Free Apple ID `.ipa`, and Zero-Sideload `.mobileconfig`).
- **8 Built-In Languages (1-Click Picker):** Instant in-app switching across **English (`EN`)**, **Türkçe (`TR`)**, **Português Brasil (`PT`)**, **Русский (`RU`)**, **فارسی (`FA`)**, **Bahasa Indonesia (`ID`)**, **简体中文 (`ZH`)**, and **العربية (`AR`)** on Windows, Android, and iOS.
- **Zero Speed Loss & 0ms Gaming Ping:** Local DPI mode never routes traffic through remote VPN servers. Brazil mode strictly isolates Discord so games stay 100% direct.
- **Built-In Encrypted DNS + IPv4/IPv6 Protection:** Automatically defeats ISP DNS poisoning and plain UDP/53 blocks using **DNSCrypt-Proxy** on Windows and fast **Encrypted DoH Gateways** on Android & iOS, with full IPv4+IPv6 traffic interception so YouTube video servers never leak past the DPI engine.
- **Region-Aware Live Self-Test:** Tests Discord API, Discord Media, Roblox App, Roblox Studio, YouTube Web, **YouTube Video (GGC `googlevideo.com`)**, Instagram, Wattpad, Encrypted DNS, Direct Game Baseline Ping, and (in Brazil mode) live Discord Tunnel Exit IP & Country.
- **Automatic Wi-Fi / Network Recovery & SHA-256 Auto-Updater:** Automatically heals when switching Wi-Fi/Ethernet on Windows and verifies SHA-256 hashes before upgrading on both Windows and Android.

---

## Quick Start

### Windows (10 & 11 x64)
1. Download and run **[RyanDPI-Setup-Windows-3.2.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Setup-Windows-3.2.0.exe)** (or extract [RyanDPI-Portable-Windows-3.2.0.zip](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Portable-Windows-3.2.0.zip) and run `RyanDPI.exe`).
2. Pick your country mode (**Universal Global**, **Turkey**, **Russia**, or **Brazil**) on first launch — or change it anytime from the top bar.
3. Click the **Language button (`EN`)** in the top bar to switch to any of the 8 supported languages in 1 click.
4. Click **"Install & Enable RyanDPI"** and then **"Run Test"** to verify unlocked services and live ping.

### Android (Android 10–16, No Root Required)
1. Download **[RyanDPI-Android-3.2.0.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Android-3.2.0.apk)** on your phone and tap to install *(allow "Install unknown apps" if prompted)*.
2. Pick your region (**Universal Global**, **Turkey**, **Russia**, or **Brazil**), tap **"Connect & Unlock"**, and approve Android's local VPN prompt.
3. Tap **"Run Test"** to verify unlocked services, exit country, and ping. Add the **RyanDPI Quick Settings Tile** for 1-tap toggling anywhere.

### iOS / iPhone & iPad (iOS 15+, 3 Options)
Pick the option that fits your iPhone:

1. **Option 1 — Full System-Wide VPN (`RyanDPI-iOS-3.2.0.ipa`)** *(Best — unlocks Discord, YouTube, Roblox & ALL apps on Wi-Fi + 5G)*
   - Download **[RyanDPI-iOS-3.2.0.ipa](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-3.2.0.ipa)** and install via **TrollStore**, **ESign / Feather / KravaSign / MapleSign**, or **Xcode / Sideloadly** with an Apple Developer certificate (which supports Apple's `NetworkExtension` entitlement).
   - Open **RyanDPI**, select your region, tap the power orb, and approve the local VPN prompt.
2. **Option 2 — Free Apple ID Sideload (`RyanDPI-iOS-FreeAppleID-3.2.0.ipa`)** *(Works with ANY Free Apple ID via AltStore / SideStore / Sideloadly!)*
   - Apple blocks `NetworkExtension` on free 7-day signing certificates, so this edition runs the exact same native `ciadpi` + Encrypted DoH engine as a local HTTP/HTTPS & SOCKS5 proxy inside the app.
   - Install **[RyanDPI-iOS-FreeAppleID-3.2.0.ipa](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-FreeAppleID-3.2.0.ipa)** with AltStore, SideStore, or Sideloadly.
   - Open **RyanDPI**, tap **Connect**, then set **Settings → Wi-Fi → (i) → Configure Proxy → Manual → Server: `127.0.0.1`, Port: `8080`**.
3. **Option 3 — 10-Second Zero-Sideload Encrypted DNS (`RyanDPI-iOS-EncryptedDNS.mobileconfig`)** *(No PC, no sideloading, works on every iPhone!)*
   - Open **[RyanDPI-iOS-EncryptedDNS.mobileconfig](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-EncryptedDNS.mobileconfig)** directly in Safari on your iPhone, tap **Allow**, then go to **Settings → General → VPN & Device Management → RyanDPI Encrypted DNS → Install**.

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

### iOS (Requires macOS + Xcode 16 + XcodeGen)
```bash
brew install xcodegen
./ios/scripts/build_native_libs.sh
cd ios && xcodegen generate --spec project.yml
```

---

## Complete Removal

Click **Uninstall** inside RyanDPI (or uninstall normally via Windows *Settings > Apps* / Android Settings / iOS Home Screen) to cleanly remove all services, firewall rules, and restore automatic DHCP DNS.

---

## Open-Source Components

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** by hufrea
- **[usque](https://github.com/Diniboy1123/usque)** by Diniboy1123 (Cloudflare WARP HTTP/3 MASQUE engine)
- **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** by heiher (Android & iOS native TUN engine)
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** by NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** by Frank Denis

---

## License

MIT License — see [LICENSE](LICENSE).
