# RyanDPI

[English](README.md) | **Türkçe**

**RyanDPI**, Türkiye'de engelli **tüm uygulama ve web sitelerini** — **Discord**, **Roblox**, **Roblox Studio**, **Wattpad**, tüm tarayıcılar ve diğer tüm uygulamalar dahil — **internet hızını veya oyun pingini etkilemeden** tek tıkla açmak için tasarlanmış modern, yüksek hızlı bir **Windows & Android** DPI aşım aracıdır (anında **Türkçe / İngilizce** dil değiştirme destekli).

RyanDPI karmaşık çoklu araç kurulumlarını hem bilgisayarda hem telefonda rootsuz, kutudan çıktığı gibi sorunsuz çalışan tek bir yerel motorla değiştirir.

---

## Neden RyanDPI?

- **Windows ve Android Desteği:** Windows 10/11 masaüstü uygulaması VE rootsuz **Android APK** (Android 10–16, Evrensel `arm64-v8a` / `armeabi-v7a` / `x86_64` / `x86`), Hızlı Ayarlar kutucuğu, uygulama hariç tutma (Split Tunnel) ve dahili Şifreli DoH DNS ile gelir.
- **Her Şeyi Otomatik Açar (Beyaz Liste Gerekmez):** Yalnızca belirli uygulamaları listeleyen kurulumların aksine, RyanDPI sistem genelindeki **tüm uygulamaları** şeffaf biçimde açar.
- **Sıfır Hız Kaybı — Doğal İSS Hızları:** RyanDPI bir **uzak VPN değildir**. Trafiğin hiçbir üçüncü parti sunucudan geçmez. Doğrudan kendi Wi-Fi, Ethernet veya 4G/5G hattın üzerinden yalnızca ilk el sıkışma paketine gecikmesiz TLS kayıt bölme (`--tlsrec 1+s`) uygular; tüm indirme, yayın ve yüklemeler tam hat hızında aktarılır.
- **En Düşük Ping ve 0 ms Doğal Oyun Pingi:** Windows'ta varsayılan olarak yalnızca **TCP** trafiğini işlerken **UDP** oyun trafiğini doğrudan bırakır ve LAN/localhost'u otomatik atlar.
- **Dahili Şifreli DNS (Türk İSS UDP/53 Engelini Aşar):** Türk İSS DNS zehirlenmesini ve UDP/53 engelini otomatik ortadan kaldırır — Windows'ta yerel **DNSCrypt-Proxy**, Android'de ise telefon ayarı değiştirmeden çalışan dahili **HTTP/2 Şifreli DoH Ağ Geçidi** (`1.1.1.1` / `8.8.8.8` / `9.9.9.9`, LRU önbellekli ve otomatik yedekli) kullanır.
- **6 Aşamalı Uyarlanabilir Aşım Stratejisi:** Türk Telekom, Superonline, Vodafone, Turkcell, Türksat Kablonet ve TurkNet üzerinde 24 saatlik IP önbellekleme ile otomatik uyum sağlar.
- **Dahili Otomatik Güncelleyici:** Hem Windows hem Android sürümü açılışta GitHub Releases üzerinden yeni sürümleri SHA-256 doğrulamasıyla denetler ve tek tıkla günceller.
- **Neon Kırmızı & Siyah Arayüz + Tek Tıkla Türkçe / İngilizce Değiştirme:** Üst başlıktan tek tıkla tüm uygulamayı Türkçe veya İngilizce yapabilir, Koyu/Açık tema seçebilir ve dahili **8 adımlı Erişim ve Hız Testi** ile Discord, Roblox Uygulaması, Roblox Studio, Wattpad ve canlı pingi doğrulayabilirsin.

---

## İndirme ve Hızlı Başlangıç

[![Windows Kurulumunu İndir](https://img.shields.io/badge/İndir-Windows%20Kurulum%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Setup-Windows-2.4.0.exe)
[![Taşınabilir ZIP İndir](https://img.shields.io/badge/İndir-Windows%20Taşınabilir%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Portable-Windows-2.4.0.zip)
[![Android APK İndir](https://img.shields.io/badge/İndir-Android%20APK%20(v2.4.0)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Android-2.4.0.apk)

### Windows (10 & 11 x64)
1. **[RyanDPI-Setup-Windows-2.4.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Setup-Windows-2.4.0.exe)** dosyasını indirip çalıştır (veya [Taşınabilir ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Portable-Windows-2.4.0.zip) arşivini çıkarıp `RyanDPI.exe` dosyasını aç).
2. **RyanDPI** uygulamasını başlat (Windows hizmetlerini ve DNS'i yönetmek için Yönetici izni otomatik istenir).
3. Sağ üst başlıktaki **`TR`** düğmesine tıklayarak tüm uygulamayı anında **Türkçe** yapabilirsin.
4. **"RyanDPI'yı Kur ve Etkinleştir"** düğmesine tıkla ve 5 adımlı otomatik kurulumun (~10 saniye) tamamlanmasını bekle.
5. **"Testi Çalıştır"** düğmesine basarak Discord, Roblox Uygulaması, Roblox Studio API, Roblox Web, Wattpad ve anlık ping değerlerini canlı doğrula.

RyanDPI arka planda sessizce Windows hizmetleri (`RyanDpiEngine`, `ProxiFyreService` ve `dnscrypt-proxy`) olarak çalışır ve Wi-Fi/ağ değişikliklerinde otomatik kendini yeniler.

### Android (Android 10–16, Root Gerekmez)
1. **[RyanDPI-Android-2.4.0.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.4.0/RyanDPI-Android-2.4.0.apk)** dosyasını Android telefonuna indir ve açarak kur *(gerekirse tarayıcın için "Bilinmeyen uygulamalara izin ver" seçeneğini aç)*.
2. **RyanDPI** uygulamasını aç, **"Bağlan & Her Şeyi Aç"** düğmesine dokun ve Android'in yerel VPN iznini onayla *(tamamen cihaz içi çalışır — uzak VPN sunucusu yoktur!)*.
3. **"Test Et"** düğmesine basarak Discord, Roblox Uygulaması, Wattpad ve Şifreli DoH DNS'i canlı doğrula.
4. *(İpucu)* Android bildirim paneline **RyanDPI Hızlı Ayarlar Kutucuğu** ekleyerek her yerden tek dokunuşla açıp kapatabilirsin!

---

## İsteğe Bağlı Ayarlar

RyanDPI hiçbir ayar yapmadan doğrudan çalışır, ancak **Ayarlar** sekmesinden dilediğin zaman özelleştirebilirsin:

- **DPI Aşım Profili:**
  - *Otomatik Evrensel (Önerilen)* — Tüm Türk İSS'leri için 6 aşamalı gecikmesiz uyarlanabilir zincir
  - *Klasik Mod* — Otomatik yedekli ve tam hızlı tamponlu hafif TLS kayıt bölme
  - *Sıkı İSS* — Superonline / mobil hatlar için agresif TLS + TCP bölme, OOB ve düşük TTL sahte paket zinciri
  - *Özel Parametreler* — Kendi `ciadpi.exe` parametrelerini gir
- **DNS Koruması:** Şifreli DNS (`dnscrypt-proxy`), Windows Yerel DoH veya Mevcut DNS Ayarlarını Koru.
- **Yerel Ağ (LAN) ve Localhost'u Atla:** Yerel cihazlar ve localhost geliştirici sunucularının doğrudan kalması için varsayılan olarak açıktır.
- **UDP'yi de Tünelle (Ses / QUIC):** Oyun pingini etkilememek için varsayılan olarak kapalıdır; sadece İSS'n Discord sesini veya QUIC'i engelliyorsa aç.
- **Başlangıçta Otomatik Güncelle:** Varsayılan olarak açıktır; uygulama açıldığında GitHub Releases üzerinden yeni sürümleri otomatik indirip kurar.
- **Hariç Tutulan Uygulamalar:** RyanDPI'dan atlamak istediğin uygulamaları isteğe bağlı olarak ekleyebilirsin (motor ve DNS hizmetleri döngüyü önlemek için her zaman otomatik hariç tutulur).

---

## Kaynak Koddan Derleme

### Gereksinimler
- Windows 10 veya Windows 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) veya daha yenisi
- *(İsteğe bağlı)* Windows Kurulum `.exe` dosyası üretmek için [Inno Setup 6](https://jrsoftware.org/isinfo.php)

### Derleme
```bat
git clone https://github.com/CallMeRyanYT/RyanDPI.git
cd RyanDPI
src\build.bat
```
Veya doğrudan `dotnet` ile:
```powershell
dotnet build src\RyanDPI\RyanDPI.csproj -c Release
```
Çalıştırmaya hazır derleme (tüm motor dosyaları ve gereksinimler dahil) `dist\RyanDPI\RyanDPI.exe` konumunda oluşturulur.

### Android APK Derleme
JDK 17–21 ve Android SDK (API 36) gerektirir:
```bat
cd android
gradlew.bat assembleRelease
```

---

## Tamamen Kaldırma

RyanDPI'yı ve yaptığı tüm sistem değişikliklerini (hizmetler, güvenlik duvarı kuralları ve otomatik DHCP DNS ayarları) istediğin zaman şu yöntemlerle tamamen kaldırabilirsin:
- Uygulama içindeki **Kaldır** düğmesine tıklayarak, veya
- Windows *Ayarlar > Uygulamalar* bölümünden **RyanDPI**'yı kaldırarak (Android uygulamasını normal şekilde kaldırabilirsin).

---

## Açık Kaynak Bileşenler

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** — hufrea
- **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** — heiher (Android yerel TUN motoru)
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** — NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** — Frank Denis

---

## Lisans

MIT Lisansı — ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.
