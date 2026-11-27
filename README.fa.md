<div dir="rtl">

# RyanDPI v3.2.0 — Unbound

[English](README.md) | [Türkçe](README.tr.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | **فارسی** | [Bahasa Indonesia](README.id.md) | [简体中文](README.zh.md) | [العربية](README.ar.md)

**RyanDPI — Unbound** یک ابزار سریع و تک‌کلیکی برای **ویندوز و اندروید** است که با موتور **۷ مرحله‌ای دور زدن DPI** و **DNS رمزنگاری‌شده (DoH / DNSCrypt)** به باز کردن سایت‌ها، دیسکورد، روبلاکس و سرویس‌های محدودشده توسط DPI بدون افت سرعت و بدون افزایش پینگ بازی کمک می‌کند.

---

## دانلود و شروع سریع (`v3.2.0 — Unbound`)

[![دانلود نسخه نصب ویندوز](https://img.shields.io/badge/Download-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Setup-Windows-3.2.0.exe)
[![دانلود نسخه پرتابل ویندوز](https://img.shields.io/badge/Download-Windows%20Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Portable-Windows-3.2.0.zip)
[![دانلود نسخه اندروید APK](https://img.shields.io/badge/Download-Android%20APK%20(v3.2.0)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Android-3.2.0.apk)
[![Download iOS Full VPN IPA](https://img.shields.io/badge/Download-iOS%20Full%20VPN%20(.ipa)-007AFF?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-3.2.0.ipa)
[![Download iOS Free Apple ID IPA](https://img.shields.io/badge/Download-iOS%20Free%20Apple%20ID%20(.ipa)-5856D6?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-FreeAppleID-3.2.0.ipa)
[![Download iOS Encrypted DNS Profile](https://img.shields.io/badge/Download-iOS%20Encrypted%20DNS%20(.mobileconfig)-00B8D4?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-EncryptedDNS.mobileconfig)

---

## ویژگی‌های کلیدی

- **موتور ۷ مرحله‌ای جهانی بدون لگ:** با تکنیک‌های `TLS Record Split`، `Disorder`، `OOB` و `Fake-SNI TTL` به‌طور خودکار با محدودیت‌های DPI سازگار می‌شود و تا ۲۴ ساعت بهترین استراتژی را برای هر IP ذخیره می‌کند.
- **محافظت کامل DNS (ضد مسمومیت DNS):** در ویندوز از **DNSCrypt-Proxy** و در اندروید از **درگاه داخلی HTTP/2 DoH** (`1.1.1.1` / `8.8.8.8` / `9.9.9.9`) استفاده می‌کند تا محدودیت‌های DNS روی پورت ۵۳ دور زده شود.
- **پینگ ۰ در بازی‌ها:** در حالت DPI محلی هیچ سرور VPN دوری در میان نیست؛ ترافیک بازی‌های آنلاین مستقیم باقی می‌ماند. همچنین در بخش تنظیمات می‌توانید از **Cloudflare WARP MASQUE (`usque`)** یا **SOCKS5 اختصاصی** برای تونل مجزای دیسکورد استفاده کنید.
- **پشتیبانی از ۸ زبان (از جمله فارسی):** از نوار بالایی با یک کلیک زبان برنامه را به **فارسی (`FA`)** تغییر دهید.
- **به‌روزرسانی خودکار با بررسی SHA-256:** هر دو نسخه ویندوز و اندروید به‌طور خودکار نسخه‌های جدید را از GitHub Releases بررسی و با تایید SHA-256 نصب می‌کنند.

---

## نصب سریع

### ویندوز (۱۰ و ۱۱، ۶۴ بیتی)
1. فایل **[RyanDPI-Setup-Windows-3.2.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Setup-Windows-3.2.0.exe)** را دانلود و اجرا کنید.
2. در اولین اجرا حالت **جهانی یونیورسال (Universal Global)** یا **سخت‌گیرانه (Strict ISP)** را انتخاب کنید و زبان را از نوار بالا روی **`FA`** بگذارید.
3. روی **«نصب و فعال‌سازی RyanDPI»** کلیک کنید و سپس با دکمه **«اجرای تست»** وضعیت دسترسی و پینگ را بررسی کنید.

### اندروید (اندروید ۱۰ تا ۱۶، بدون نیاز به روت)
1. فایل **[RyanDPI-Android-3.2.0.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-Android-3.2.0.apk)
[![Download iOS Full VPN IPA](https://img.shields.io/badge/Download-iOS%20Full%20VPN%20(.ipa)-007AFF?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-3.2.0.ipa)
[![Download iOS Free Apple ID IPA](https://img.shields.io/badge/Download-iOS%20Free%20Apple%20ID%20(.ipa)-5856D6?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-FreeAppleID-3.2.0.ipa)
[![Download iOS Encrypted DNS Profile](https://img.shields.io/badge/Download-iOS%20Encrypted%20DNS%20(.mobileconfig)-00B8D4?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.2.0/RyanDPI-iOS-EncryptedDNS.mobileconfig)** را دانلود و نصب کنید.
2. حالت **جهانی یونیورسال** را انتخاب کرده و روی **«اتصال و باز کردن»** بزنید.

---

## اجزای متن‌باز

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** • **[usque](https://github.com/Diniboy1123/usque)** • **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** • **[ProxiFyre](https://github.com/wiresock/proxifyre)** • **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)**

## مجوز

MIT License — فایل [LICENSE](LICENSE) را ببینید.

</div>
