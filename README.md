# RyanDPI

**English** | [Türkçe](README.tr.md)

**RyanDPI** is a sleek, one-click Windows DPI bypass tool (with instant **English & Turkish** language switching) designed to unlock **every blocked app and website in Turkey** — including **Roblox Studio**, **Roblox Player**, **Discord**, **Wattpad**, web browsers, developer tools, and every other desktop application — **without affecting your internet speed or gaming ping**.

RyanDPI replaces complex multi-tool setups with a single, high-speed, system-wide engine that works right out of the box.

---

## Why RyanDPI?

- **Unlocks Everything Automatically (Zero Whitelisting):** Unlike app-whitelisted ByeDPI setups that leave apps like **Roblox Studio**, embedded WebView2 logins, CLI tools, or newly installed programs blocked, RyanDPI transparently unlocks **all Windows applications** system-wide.
- **Zero Speed Loss — Native ISP Speeds:** RyanDPI is **not a VPN**. Your traffic never touches any remote server. It connects directly over your own internet line, using zero-retransmit TLS record splitting (`--tlsrec 1+s`) on the first handshake packet while transferring all downloads, streams, and uploads at full wire speed (tuned with `128 KB` socket buffers and `4,096` concurrent connections).
- **Lowest Possible Ping & Native 0ms Gaming Ping:** By default, RyanDPI handles **TCP** (HTTP/HTTPS/TLS/WebSockets) while leaving **UDP** (online game servers, RakNet, voice) completely direct, plus bypasses LAN/localhost traffic automatically.
- **Fastest-First Encrypted DNS:** Eliminates Turkish ISP DNS poisoning automatically via local **DNSCrypt-Proxy** (`lb_strategy = 'first'` preferring the lowest-ping resolver across Cloudflare, Google & Quad9 with an `8,192`-entry cache and `120s` keepalive) or optional Windows 11 Native DoH.
- **6-Stage Adaptive Bypass Strategy:** Starts with zero-retransmit TLS splitting and automatically adapts across Türk Telekom, Superonline, Vodafone, Türksat Kablonet, TurkNet, and mobile networks with 24-hour per-IP strategy caching and clean passthrough fallback.
- **No WinDivert Antivirus Headaches:** Uses Windows Packet Filter (`NDISRD`) + ProxiFyre + ByeDPI instead of WinDivert.
- **Built-in Automatic Updater:** Automatically checks GitHub Releases on startup, downloads & SHA-256 verifies new updates, silently installs them (both Windows Installer and Portable editions), relaunches RyanDPI, and upgrades background services automatically.
- **Neon Red & Black UI + Instant English / Turkish Toggle:** Switch the entire app between English and Turkish with one click in the title bar, plus Dark & Light modes, one-click Install/Pause/Uninstall, live progress bar, and a built-in **8-check Unblock & Speed Self-Test** (including **Roblox App**, **Roblox Studio**, **Discord**, **Wattpad**, and live ping).

---

## Download & Quick Start

[![Download Windows Setup](https://img.shields.io/badge/Download-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.2/RyanDPI-Setup-Windows-2.3.2.exe)
[![Download Portable ZIP](https://img.shields.io/badge/Download-Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.2/RyanDPI-Portable-Windows-2.3.2.zip)

1. Download and run **[RyanDPI-Setup-Windows-2.3.2.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.2/RyanDPI-Setup-Windows-2.3.2.exe)** (or extract the [Portable ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.2/RyanDPI-Portable-Windows-2.3.2.zip) and run `RyanDPI.exe`).
2. Launch **RyanDPI** (Administrator privileges are requested automatically to manage Windows services and DNS).
3. *(Optional)* Click **`TR`** in the top-right title bar anytime to switch the entire app to Turkish.
4. Click **"Install & Enable RyanDPI"** and wait ~10 seconds for the 5-step automatic setup to complete.
5. Click **"Run Test"** to verify Discord, Roblox App, Roblox Studio API, Roblox Web, Wattpad, and live ping.

RyanDPI runs quietly as Windows background services (`RyanDpiEngine`, `ProxiFyreService`, and `dnscrypt-proxy`), so everything stays unlocked automatically every time you restart your PC.

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

---

## Complete Removal

You can remove RyanDPI and revert every system change (services, firewall rules, and automatic DHCP DNS) at any time by:
- Clicking **Uninstall** directly inside RyanDPI, or
- Uninstalling **RyanDPI** from Windows *Settings > Apps*.

---

## Open-Source Components

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** by hufrea
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** by NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** by Frank Denis

---

## License

MIT License — see [LICENSE](LICENSE).
