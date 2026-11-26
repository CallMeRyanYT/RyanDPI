# RyanDPI v3.1.4 — Unbound

[English](README.md) | **Türkçe** | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [فارسی](README.fa.md) | [Bahasa Indonesia](README.id.md) | [简体中文](README.zh.md) | [العربية](README.ar.md)

**RyanDPI — Unbound**, **Türkiye, Rusya, Brezilya ve tüm dünyadaki** engelli uygulama ve siteleri — **Discord, Discord Ekran Paylaşımı, Roblox, Roblox Studio, YouTube, Instagram, Wattpad** dahil — **internet hızını düşürmeden ve oyun pingini artırmadan** tek tıkla açan modern **Windows & Android** aracıdır.

**v3.1.4 — Unbound** ile ilk açılış **Ülke/Bölge Seçici**, **7 Aşamalı Evrensel Küresel DPI Motoru**, özel **Türkiye (BTK)**, **Rusya (TSPU)** ve **Brezilya (0 ms Oyun Pingli Discord Bölünmüş Tünel)** modları ve **8 dahili dil** tek uygulamada birleşiyor!

---

## İndirme ve Hızlı Başlangıç (`v3.1.4 — Unbound`)

[![Windows Kurulumunu İndir](https://img.shields.io/badge/İndir-Windows%20Kurulum%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.4/RyanDPI-Setup-Windows-3.1.4.exe)
[![Taşınabilir ZIP İndir](https://img.shields.io/badge/İndir-Windows%20Taşınabilir%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.4/RyanDPI-Portable-Windows-3.1.4.zip)
[![Android APK İndir](https://img.shields.io/badge/İndir-Android%20APK%20(v3.1.4)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.4/RyanDPI-Android-3.1.4.apk)

---

## Ülke ve Küresel Modlar

İlk açılışta (veya üst bardaki **Bölge** butonundan istediğin zaman) ülkene uygun modu seç:

| Mod | Hedef | Nasıl Çalışır? (Sıfır Oyun Gecikmesi) |
| :--- | :--- | :--- |
| **Evrensel Küresel** *(Önerilen)* | Türkiye, Rusya, İran, Endonezya, Mısır ve DPI engelli tüm İSS'ler | İlk aşamada 0 ms `--tlsrec 1+s` ile başlayan, sıkı İSS'lerde otomatik yükselen 7 aşamalı yerel motor + 24 saatlik IP önbelleği + Şifreli DNS. **Uzak VPN değildir — tam doğal internet hızı!** |
| **Türkiye — BTK Sıfır Gecikme** | Discord, Roblox, Roblox Studio, Wattpad ve tüm BTK engelleri | Sıfır tekrarlı TLS bölme + Şifreli DNS (Türk Telekom / Superonline / Vodafone / Turkcell UDP/53 engellerini aşar). Oyun UDP paketleri %100 doğrudan gider (0 ms ekstra ping). |
| **Rusya — TSPU Tam Erişim** | YouTube, Discord (+ Ses), Instagram, Roblox | İlk paketten TSPU kanıtlı `--split 2 --disorder -1+s --tlsrec 1+s` + OOB/fake-SNI zinciri + UDP ses bypass (`-a 1`) + Şifreli DoH DNS. |
| **Brezilya — Discord Ekran Paylaşımı** | ANPD Coğrafi-IP kısıtlamasında Discord **Go Live / Ekran Paylaşımı & Kamera** açma | **Yalnızca Discord Bölünmüş Tüneli (Oyunlarda 0 ms ping!):** Valorant, CS2, Roblox, Fortnite %100 doğrudan kalırken **SADECE Discord** dahili ücretsiz **Cloudflare WARP MASQUE (`usque`)** veya **Özel Brezilya Dışı SOCKS5** üzerinden geçer. |

---

## Öne Çıkan Özellikler

- **Windows & Android Desteği:** Windows 10/11 (x64) yerel masaüstü uygulaması + rootsuz evrensel **Android APK** (Android 10–16, Hızlı Ayarlar kutucuğu ve uygulama hariç tutma dahil).
- **8 Dahili Dil (Tek Tıkla Seçim):** `EN`, `TR`, `PT`, `RU`, `FA`, `ID`, `ZH`, `AR` dilleri arasında üst bardan tek tıkla geçiş.
- **Sıfır Hız Kaybı ve 0 ms Oyun Pingi:** Yerel DPI modu üçüncü parti VPN sunucularını kullanmaz; tüm indirmeler ve oyunlar gerçek internet hızınızda çalışır.
- **Dahili Şifreli DNS:** Windows'ta **DNSCrypt-Proxy**, Android'de dahili **HTTP/2 DoH Ağ Geçidi** ile DNS zehirlenmesini ve UDP/53 engellerini sıfır ayar ile çözer.
- **Bölgeye Özel Canlı Test:** Discord, Roblox Uygulaması, Roblox Studio, YouTube, Instagram, Wattpad, Şifreli DNS, doğrudan oyun pingi ve (Brezilya modunda) canlı çıkış ülkesini test eder.
- **Otomatik Wi-Fi/Ağ Kurtarma ve SHA-256 Otomatik Güncelleyici:** Wi-Fi/Ethernet değişimlerinde kendini otomatik onarır, yeni sürümleri SHA-256 doğrulamasıyla tek tıkla günceller.

---

## Hızlı Kurulum

### Windows (10 & 11 x64)
1. **[RyanDPI-Setup-Windows-3.1.4.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.4/RyanDPI-Setup-Windows-3.1.4.exe)** kurulumunu indirip çalıştır (veya [Taşınabilir ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.4/RyanDPI-Portable-Windows-3.1.4.zip) çıkarıp `RyanDPI.exe`'yi aç).
2. İlk açılışta **Türkiye** veya **Evrensel Küresel** modunu seç (üst bardan dilediğin zaman değiştirebilirsin).
3. **"RyanDPI'yı Kur ve Etkinleştir"** düğmesine tıkla, ardından **"Testi Çalıştır"** ile Discord, Roblox ve canlı pingi doğrula.

### Android (Android 10–16, Root Gerekmez)
1. **[RyanDPI-Android-3.1.4.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.4/RyanDPI-Android-3.1.4.apk)** dosyasını indirip telefonuna kur.
2. Ülkeni seç, **"Bağlan & Aç"** düğmesine dokun ve Android yerel VPN iznini onayla *(uzak VPN sunucusu kullanmaz!)*.
3. Bildirim paneline **RyanDPI Hızlı Ayarlar Kutucuğu** ekleyerek tek dokunuşla aç/kapa.

---

## Açık Kaynak Bileşenler

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** — hufrea
- **[usque](https://github.com/Diniboy1123/usque)** — Diniboy1123 (Cloudflare WARP HTTP/3 MASQUE)
- **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** — heiher
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** — NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** — Frank Denis

---

## Lisans

MIT Lisansı — bkz. [LICENSE](LICENSE).
