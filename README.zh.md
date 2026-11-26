# RyanDPI v3.0.0 — Unbound

[English](README.md) | [Türkçe](README.tr.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [فارسی](README.fa.md) | [Bahasa Indonesia](README.id.md) | **简体中文** | [العربية](README.ar.md)

**RyanDPI — Unbound** 是一款适用于 **Windows 与 Android** 的一键式零延迟 **DPI 绕过 + 加密 DNS + Discord 分流隧道**工具，支持全球通用 7 阶段自适应 DPI 绕过，以及针对**土耳其 (BTK)**、**俄罗斯 (TSPU)** 和**巴西 (Discord 屏幕共享 0 延迟分流)** 的专属优化预设，内置 **8 种语言**一键切换。

---

## 下载与快速开始 (`v3.0.0 — Unbound`)

[![下载 Windows 安装包](https://img.shields.io/badge/下载-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.0.0/RyanDPI-Setup-Windows-3.0.0.exe)
[![下载 Windows 便携版](https://img.shields.io/badge/下载-Windows%20Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.0.0/RyanDPI-Portable-Windows-3.0.0.zip)
[![下载 Android APK](https://img.shields.io/badge/下载-Android%20APK%20(v3.0.0)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.0.0/RyanDPI-Android-3.0.0.apk)

---

## 核心亮点

- **7 阶段全球通用零延迟 DPI 引擎：** 首阶段采用零重传 TLS 分片（`--tlsrec 1+s`），遇到严格 DPI 封锁自动升级至分片乱序、OOB 与低 TTL 伪造 SNI，并对每个 IP 缓存最佳策略 24 小时。
- **内置加密 DNS 防污染：** Windows 内置 **DNSCrypt-Proxy**，Android 内置 **HTTP/2 DoH 加密网关**（`1.1.1.1` / `8.8.8.8` / `9.9.9.9`），自动抵御 DNS 污染与 UDP/53 拦截。
- **游戏零延迟（0 ms 额外 Ping）：** 本地 DPI 模式不经过远程 VPN 服务器；巴西分流模式仅代理 Discord（支持内置免登录 **Cloudflare WARP MASQUE** 或自定义 **SOCKS5 代理**），所有游戏保持 100% 原生直连。
- **双端 8 种语言一键切换：** 支持简体中文 (`ZH`)、英语 (`EN`)、土耳其语 (`TR`)、巴西葡萄牙语 (`PT`)、俄语 (`RU`)、波斯语 (`FA`)、印尼语 (`ID`) 和阿拉伯语 (`AR`)。
- **实时自测与 SHA-256 自动更新：** 内置服务解锁与实时延迟自测，并在启动时自动检测 GitHub Releases 新版本与 SHA-256 校验。

---

## 使用步骤

### Windows (10 / 11 x64)
1. 下载并运行 **[RyanDPI-Setup-Windows-3.0.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.0.0/RyanDPI-Setup-Windows-3.0.0.exe)**（或解压 [便携版 ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.0.0/RyanDPI-Portable-Windows-3.0.0.zip) 运行 `RyanDPI.exe`）。
2. 首次启动时选择国家/地区模式（推荐 **全球通用模式**），点击顶部栏语言按钮切换为 **`ZH` 简体中文**。
3. 点击 **“安装并启用 RyanDPI”**，随后点击 **“运行测试”** 验证解锁与实时延迟。

### Android (Android 10–16，无需 Root)
1. 下载并安装 **[RyanDPI-Android-3.0.0.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.0.0/RyanDPI-Android-3.0.0.apk)**。
2. 选择地区模式并点击 **“连接并解锁”**，允许系统本地 VPN 权限即可生效，还可添加系统下拉快捷磁贴一键开关。

---

## 开源组件

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** • **[usque](https://github.com/Diniboy1123/usque)** • **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** • **[ProxiFyre](https://github.com/wiresock/proxifyre)** • **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)**

## 许可证

MIT License — 详见 [LICENSE](LICENSE).
