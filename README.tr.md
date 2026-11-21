# RyanDPI

[English](README.md) | **Türkçe**

**RyanDPI**, Türkiye'de engelli **tüm uygulama ve web sitelerini** — **Roblox Studio**, **Roblox Uygulaması (Player)**, **Discord**, **Wattpad**, tüm tarayıcılar, geliştirici araçları ve diğer tüm masaüstü uygulamaları dahil — **internet hızını veya oyun pingini etkilemeden** tek tıkla açmak için tasarlanmış modern, yüksek hızlı bir Windows DPI aşım aracıdır (anında **Türkçe / İngilizce** dil değiştirme destekli).

RyanDPI karmaşık çoklu araç kurulumlarını tek bir yüksek hızlı, sistem geneli çalışan motorla değiştirir ve kutudan çıktığı gibi sorunsuz çalışır.

---

## Neden RyanDPI?

- **Her Şeyi Otomatik Açar (Beyaz Liste Gerekmez):** Sadece belirli uygulamaları listeleyen ve **Roblox Studio**, WebView2 giriş pencereleri, terminal araçları veya yeni kurulan programları engelli bırakan kurulumların aksine, RyanDPI sistem genelindeki **tüm Windows uygulamalarını** şeffaf biçimde açar.
- **Sıfır Hız Kaybı — Doğal İSS Hızları:** RyanDPI bir **VPN değildir**. Trafiğin hiçbir uzak sunucudan geçmez. Doğrudan kendi internet hattın üzerinden bağlanır; sadece ilk TLS/HTTP el sıkışma paketine gecikmesiz TLS kayıt bölme (`--tlsrec 1+s`) uygular, tüm indirme, yayın ve yüklemeleri tam hat hızında aktarır (`128 KB` soket tamponları ve `4.096` eşzamanlı bağlantı desteği ile).
- **En Düşük Ping ve 0 ms Doğal Oyun Pingi:** Varsayılan olarak RyanDPI yalnızca **TCP** trafiğini (HTTP/HTTPS/TLS/WebSocket) işlerken **UDP** trafiğini (çevrimiçi oyun sunucuları, RakNet, ses) tamamen doğrudan bırakır; ayrıca yerel ağ (LAN) ve localhost trafiğini otomatik atlar.
- **En Hızlı Sunucu Öncelikli Şifreli DNS:** Yerel **DNSCrypt-Proxy** (`lb_strategy = 'first'` ile Cloudflare, Google ve Quad9 arasından en düşük pingli sunucuyu seçer, `8.192` kayıtlık önbellek ve `120 sn` keepalive kullanır) veya isteğe bağlı Windows 11 Yerel DoH ile İSS DNS zehirlenmesini tamamen ortadan kaldırır.
- **6 Aşamalı Uyarlanabilir Aşım Stratejisi:** Gecikmesiz TLS kayıt bölme ile başlar ve Türk Telekom, Superonline, Vodafone, Türksat Kablonet, TurkNet ve mobil ağlarda 24 saatlik IP önbellekleme ve temiz geçiş yedeği ile otomatik uyum sağlar.
- **WinDivert Antivirüs Sorunları Yok:** WinDivert yerine Windows Packet Filter (`NDISRD`) + ProxiFyre + ByeDPI mimarisini kullanır.
- **Dahili Otomatik Güncelleyici:** Uygulama açıldığında GitHub Releases üzerinden yeni sürümleri otomatik kontrol eder, SHA-256 doğrulamasıyla indirip sessizce kurar (hem Windows Kurulumu hem Taşınabilir ZIP için), uygulamayı yeniden başlatır ve arka plan hizmetlerini otomatik günceller.
- **Neon Kırmızı & Siyah Arayüz + Tek Tıkla Türkçe / İngilizce Değiştirme:** Üst başlıktan tek tıkla tüm uygulamayı Türkçe veya İngilizce yapabilir, Koyu/Açık tema seçebilir ve dahili **8 adımlı Erişim ve Hız Testi** ile **Discord**, **Roblox Uygulaması**, **Roblox Studio**, **Wattpad** ve canlı pingi anında doğrulayabilirsin.

---

## İndirme ve Hızlı Başlangıç

[![Windows Kurulumunu İndir](https://img.shields.io/badge/İndir-Windows%20Kurulum%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.0/RyanDPI-Setup-Windows-2.3.0.exe)
[![Taşınabilir ZIP İndir](https://img.shields.io/badge/İndir-Taşınabilir%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.0/RyanDPI-Portable-Windows-2.3.0.zip)

1. **[RyanDPI-Setup-Windows-2.3.0.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.0/RyanDPI-Setup-Windows-2.3.0.exe)** dosyasını indirip çalıştır (veya [Taşınabilir ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v2.3.0/RyanDPI-Portable-Windows-2.3.0.zip) arşivini çıkarıp `RyanDPI.exe` dosyasını aç).
2. **RyanDPI** uygulamasını başlat (Windows hizmetlerini ve DNS'i yönetmek için Yönetici izni otomatik istenir).
3. Sağ üst başlıktaki **`TR`** düğmesine tıklayarak tüm uygulamayı anında **Türkçe** yapabilirsin.
4. **"RyanDPI'yı Kur ve Etkinleştir"** düğmesine tıkla ve 5 adımlı otomatik kurulumun (~10 saniye) tamamlanmasını bekle.
5. **"Testi Çalıştır"** düğmesine basarak Discord, Roblox Uygulaması, Roblox Studio API, Roblox Web, Wattpad ve anlık ping değerlerini canlı doğrula.

RyanDPI arka planda sessizce Windows hizmetleri (`RyanDpiEngine`, `ProxiFyreService` ve `dnscrypt-proxy`) olarak çalışır; böylece bilgisayarını her yeniden başlattığında her şey otomatik olarak açık kalır.

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

---

## Tamamen Kaldırma

RyanDPI'yı ve yaptığı tüm sistem değişikliklerini (hizmetler, güvenlik duvarı kuralları ve otomatik DHCP DNS ayarları) istediğin zaman şu yöntemlerle tamamen kaldırabilirsin:
- Uygulama içindeki **Kaldır** düğmesine tıklayarak, veya
- Windows *Ayarlar > Uygulamalar* bölümünden **RyanDPI**'yı kaldırarak.

---

## Açık Kaynak Bileşenler

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** — hufrea
- **[ProxiFyre](https://github.com/wiresock/proxifyre)** & **Windows Packet Filter (`NDISRD`)** — NT Kernel / Vadim Smirnov
- **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)** — Frank Denis

---

## Lisans

MIT Lisansı — ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.
