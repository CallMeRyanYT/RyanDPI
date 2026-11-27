import Foundation

struct AppStrings {
    let subtitle: String
    let tabConnect: String
    let tabTest: String
    let tabSettings: String
    let tabLogs: String

    let statusConnected: String
    let statusConnecting: String
    let statusDisconnected: String
    let statusError: String

    let tapToConnect: String
    let tapToDisconnect: String
    let editionFullVpn: String
    let editionFreeProxy: String
    let freeProxyGuideTitle: String
    let freeProxyGuideBody: String

    let regionTitle: String
    let regionGlobal: String
    let regionTurkey: String
    let regionRussia: String
    let regionBrazil: String
    let regionHint: String

    let dohTitle: String
    let profileTitle: String
    let quicToggle: String
    let udpToggle: String
    let customArgsLabel: String
    let brazilSocksLabel: String
    let exportDnsProfile: String
    let exportDnsHint: String

    let runSelfTest: String
    let testingNow: String
    let unlocked: String
    let blocked: String

    let copyLogs: String
    let clearLogs: String
    let copied: String

    static let supportedLanguages = ["en", "tr", "pt", "ru", "fa", "id", "zh", "ar"]

    static func forLanguage(_ code: String) -> AppStrings {
        switch code.lowercased() {
        case "tr":
            return AppStrings(
                subtitle: "Unbound — iOS İçin Tam Hız DPI Aşımı",
                tabConnect: "Bağlan",
                tabTest: "Test",
                tabSettings: "Ayarlar",
                tabLogs: "Kayıtlar",
                statusConnected: "BAĞLI",
                statusConnecting: "BAĞLANIYOR...",
                statusDisconnected: "HAZIR",
                statusError: "HATA",
                tapToConnect: "Bağlanmak için dokun",
                tapToDisconnect: "Durdurmak için dokun",
                editionFullVpn: "Tam Sistem VPN Sürümü",
                editionFreeProxy: "Ücretsiz Apple ID Proxy Sürümü",
                freeProxyGuideTitle: "Wi-Fi Proxy Kurulumu (10 sn)",
                freeProxyGuideBody: "Ayarlar → Wi-Fi → (i) → Proxy Ayarla → Elle:\nSunucu: 127.0.0.1   Kapı: 8080\n(Veya SOCKS5: 127.0.0.1:1080)",
                regionTitle: "Ülke / Bölge Profili",
                regionGlobal: "Global Evrensel",
                regionTurkey: "Türkiye (BTK)",
                regionRussia: "Rusya (TSPU)",
                regionBrazil: "Brezilya (Özel SOCKS5)",
                regionHint: "Bölgenizi seçin — en hızlı DPI stratejisi otomatik uygulanır.",
                dohTitle: "Şifreli DNS (DoH)",
                profileTitle: "DPI Aşım Stratejisi",
                quicToggle: "QUIC UDP/443 → TCP/443 (YouTube/Tarayıcı)",
                udpToggle: "UDP Ses/Oyun Desteği",
                customArgsLabel: "Özel ciadpi Parametreleri",
                brazilSocksLabel: "Özel SOCKS5 Sunucu (host:port)",
                exportDnsProfile: "iOS Şifreli DNS Profili İndir (.mobileconfig)",
                exportDnsHint: "Sideload gerektirmez — Ayarlar → Genel → VPN ve Aygıt Yönetimi'nden kurun.",
                runSelfTest: "Canlı Erişim Testini Başlat",
                testingNow: "Test ediliyor...",
                unlocked: "Açık",
                blocked: "Engelli",
                copyLogs: "Kopyala",
                clearLogs: "Temizle",
                copied: "Kopyalandı!"
            )
        case "ru":
            return AppStrings(
                subtitle: "Unbound — Обход DPI для iOS без потери скорости",
                tabConnect: "Главная",
                tabTest: "Тест",
                tabSettings: "Настройки",
                tabLogs: "Логи",
                statusConnected: "ПОДКЛЮЧЕНО",
                statusConnecting: "ПОДКЛЮЧЕНИЕ...",
                statusDisconnected: "ГОТОВ",
                statusError: "ОШИБКА",
                tapToConnect: "Нажмите для запуска",
                tapToDisconnect: "Нажмите для остановки",
                editionFullVpn: "Полный системный VPN",
                editionFreeProxy: "Бесплатный Apple ID (Локальный прокси)",
                freeProxyGuideTitle: "Настройка Wi-Fi Прокси (10 сек)",
                freeProxyGuideBody: "Настройки → Wi-Fi → (i) → Настройка прокси → Вручную:\nСервер: 127.0.0.1   Порт: 8080\n(Или SOCKS5: 127.0.0.1:1080)",
                regionTitle: "Регион / Страна",
                regionGlobal: "Глобальный",
                regionTurkey: "Турция (BTK)",
                regionRussia: "Россия (ТСПУ + YouTube)",
                regionBrazil: "Бразилия (Свой SOCKS5)",
                regionHint: "Для России включён специальный обход YouTube видео (googlevideo.com) и Discord.",
                dohTitle: "Шифрованный DNS (DoH)",
                profileTitle: "Стратегия DPI",
                quicToggle: "QUIC UDP/443 → TCP/443 (Видео YouTube)",
                udpToggle: "Поддержка UDP (Голос/Игры)",
                customArgsLabel: "Свои аргументы ciadpi",
                brazilSocksLabel: "Свой SOCKS5 (host:port)",
                exportDnsProfile: "Установить профиль DNS (.mobileconfig)",
                exportDnsHint: "Без ПК и sideload — Настройки → Основные → VPN и управление устройством.",
                runSelfTest: "Запустить тест доступности",
                testingNow: "Проверка...",
                unlocked: "Доступно",
                blocked: "Заблокировано",
                copyLogs: "Копировать",
                clearLogs: "Очистить",
                copied: "Скопировано!"
            )
        case "pt":
            return AppStrings(
                subtitle: "Unbound — Desbloqueio DPI para iOS em Velocidade Máxima",
                tabConnect: "Conectar",
                tabTest: "Teste",
                tabSettings: "Ajustes",
                tabLogs: "Logs",
                statusConnected: "CONECTADO",
                statusConnecting: "CONECTANDO...",
                statusDisconnected: "PRONTO",
                statusError: "ERRO",
                tapToConnect: "Toque para conectar",
                tapToDisconnect: "Toque para desconectar",
                editionFullVpn: "Edição VPN Completa",
                editionFreeProxy: "Edição Apple ID Grátis (Proxy)",
                freeProxyGuideTitle: "Configurar Proxy Wi-Fi (10 seg)",
                freeProxyGuideBody: "Ajustes → Wi-Fi → (i) → Configurar Proxy → Manual:\nServidor: 127.0.0.1   Porta: 8080\n(Ou SOCKS5: 127.0.0.1:1080)",
                regionTitle: "Região / País",
                regionGlobal: "Global Universal",
                regionTurkey: "Turquia (BTK)",
                regionRussia: "Rússia (TSPU)",
                regionBrazil: "Brasil (SOCKS5 Próprio)",
                regionHint: "Selecione sua região para aplicar a melhor rota automaticamente.",
                dohTitle: "DNS Criptografado (DoH)",
                profileTitle: "Perfil DPI",
                quicToggle: "Fallback QUIC UDP/443 → TCP/443",
                udpToggle: "Bypass UDP (Voz / Jogos)",
                customArgsLabel: "Argumentos ciadpi Personalizados",
                brazilSocksLabel: "Endpoint SOCKS5 (host:porta)",
                exportDnsProfile: "Exportar Perfil DNS iOS (.mobileconfig)",
                exportDnsHint: "Sem sideload — instale em Ajustes → Geral → VPN e Gerenciamento.",
                runSelfTest: "Executar Teste de Conexão",
                testingNow: "Testando...",
                unlocked: "Desbloqueado",
                blocked: "Bloqueado",
                copyLogs: "Copiar",
                clearLogs: "Limpar",
                copied: "Copiado!"
            )
        case "fa":
            return AppStrings(
                subtitle: "Unbound — عبور پرسرعت از فیلترینگ DPI برای آیفون",
                tabConnect: "اتصال",
                tabTest: "تست",
                tabSettings: "تنظیمات",
                tabLogs: "گزارش",
                statusConnected: "متصل",
                statusConnecting: "در حال اتصال...",
                statusDisconnected: "آماده",
                statusError: "خطا",
                tapToConnect: "برای اتصال لمس کنید",
                tapToDisconnect: "برای قطع لمس کنید",
                editionFullVpn: "نسخه VPN کامل سیستم",
                editionFreeProxy: "نسخه Apple ID رایگان (پروکسی)",
                freeProxyGuideTitle: "تنظیم پروکسی Wi-Fi (۱۰ ثانیه)",
                freeProxyGuideBody: "Settings → Wi-Fi → (i) → Configure Proxy → Manual:\nServer: 127.0.0.1   Port: 8080",
                regionTitle: "منطقه / کشور",
                regionGlobal: "جهانی",
                regionTurkey: "ترکیه",
                regionRussia: "روسیه",
                regionBrazil: "برزیل",
                regionHint: "بهترین روش عبور از DPI به صورت خودکار اعمال می‌شود.",
                dohTitle: "DNS رمزنگاری‌شده (DoH)",
                profileTitle: "پروفایل DPI",
                quicToggle: "مسدودسازی QUIC UDP/443 (یوتیوب)",
                udpToggle: "پشتیبانی UDP",
                customArgsLabel: "پارامترهای دلخواه ciadpi",
                brazilSocksLabel: "سرور SOCKS5 دلخواه",
                exportDnsProfile: "دانلود پروفایل DNS آیفون (.mobileconfig)",
                exportDnsHint: "بدون نیاز به Sideload — نصب از Settings → General → VPN & Device Management.",
                runSelfTest: "اجرای تست دسترسی",
                testingNow: "در حال تست...",
                unlocked: "باز",
                blocked: "مسدود",
                copyLogs: "کپی",
                clearLogs: "پاک کردن",
                copied: "کپی شد!"
            )
        case "id":
            return AppStrings(
                subtitle: "Unbound — Bypass DPI Kecepatan Penuh untuk iOS",
                tabConnect: "Hubungkan",
                tabTest: "Tes",
                tabSettings: "Setelan",
                tabLogs: "Log",
                statusConnected: "TERHUBUNG",
                statusConnecting: "MENGHUBUNGKAN...",
                statusDisconnected: "SIAP",
                statusError: "GALAT",
                tapToConnect: "Ketuk untuk menghubungkan",
                tapToDisconnect: "Ketuk untuk memutus",
                editionFullVpn: "Edisi VPN Sistem Penuh",
                editionFreeProxy: "Edisi Apple ID Gratis (Proxy)",
                freeProxyGuideTitle: "Setup Proxy Wi-Fi (10 dtk)",
                freeProxyGuideBody: "Pengaturan → Wi-Fi → (i) → Konfigurasi Proxy → Manual:\nServer: 127.0.0.1   Port: 8080",
                regionTitle: "Wilayah / Negara",
                regionGlobal: "Global Universal",
                regionTurkey: "Turki (BTK)",
                regionRussia: "Rusia (TSPU)",
                regionBrazil: "Brasil (SOCKS5 Kustom)",
                regionHint: "Pilih wilayah untuk strategi DPI tercepat.",
                dohTitle: "DNS Terenkripsi (DoH)",
                profileTitle: "Strategi DPI",
                quicToggle: "Fallback QUIC UDP/443 → TCP/443",
                udpToggle: "Dukungan UDP",
                customArgsLabel: "Argumen ciadpi Kustom",
                brazilSocksLabel: "Server SOCKS5 (host:port)",
                exportDnsProfile: "Unduh Profil DNS iOS (.mobileconfig)",
                exportDnsHint: "Tanpa sideload — pasang lewat Pengaturan → Umum → VPN & Manajemen Perangkat.",
                runSelfTest: "Jalankan Tes Koneksi",
                testingNow: "Menguji...",
                unlocked: "Terbuka",
                blocked: "Diblokir",
                copyLogs: "Salin",
                clearLogs: "Hapus",
                copied: "Tersalin!"
            )
        case "zh":
            return AppStrings(
                subtitle: "Unbound — iOS 零降速本地 DPI 绕过引擎",
                tabConnect: "连接",
                tabTest: "测试",
                tabSettings: "设置",
                tabLogs: "日志",
                statusConnected: "已连接",
                statusConnecting: "连接中...",
                statusDisconnected: "就绪",
                statusError: "错误",
                tapToConnect: "点击启动",
                tapToDisconnect: "点击停止",
                editionFullVpn: "完整系统 VPN 版",
                editionFreeProxy: "免费 Apple ID 本地代理版",
                freeProxyGuideTitle: "Wi-Fi 代理设置 (10秒)",
                freeProxyGuideBody: "设置 → 无线局域网 → (i) → 配置代理 → 手动:\n服务器: 127.0.0.1   端口: 8080",
                regionTitle: "国家 / 地区预设",
                regionGlobal: "全球通用",
                regionTurkey: "土耳其 (BTK)",
                regionRussia: "俄罗斯 (TSPU)",
                regionBrazil: "巴西 (自定义 SOCKS5)",
                regionHint: "自动匹配最佳无损 DPI 绕过策略。",
                dohTitle: "加密 DNS (DoH)",
                profileTitle: "DPI 绕过策略",
                quicToggle: "QUIC UDP/443 自动回退 TCP/443",
                udpToggle: "UDP 语音/游戏支持",
                customArgsLabel: "自定义 ciadpi 参数",
                brazilSocksLabel: "自定义 SOCKS5 (host:port)",
                exportDnsProfile: "导出 iOS 加密 DNS 描述文件 (.mobileconfig)",
                exportDnsHint: "无需签名侧载 — 设置 → 通用 → VPN 与设备管理安装。",
                runSelfTest: "运行实时连通性测试",
                testingNow: "测试中...",
                unlocked: "已解锁",
                blocked: "已拦截",
                copyLogs: "复制",
                clearLogs: "清空",
                copied: "已复制!"
            )
        case "ar":
            return AppStrings(
                subtitle: "Unbound — تجاوز حظر DPI لأجهزة iPhone بالسرعة الكاملة",
                tabConnect: "اتصال",
                tabTest: "اختبار",
                tabSettings: "الإعدادات",
                tabLogs: "السجل",
                statusConnected: "متصل",
                statusConnecting: "جاري الاتصال...",
                statusDisconnected: "جاهز",
                statusError: "خطأ",
                tapToConnect: "اضغط للاتصال",
                tapToDisconnect: "اضغط للإيقاف",
                editionFullVpn: "إصدار VPN الكامل للنظام",
                editionFreeProxy: "إصدار Apple ID المجاني (بروكسي)",
                freeProxyGuideTitle: "إعداد بروكسي Wi-Fi (10 ثوانٍ)",
                freeProxyGuideBody: "الإعدادات → Wi-Fi → (i) → تكوين البروكسي → يدوي:\nالخادم: 127.0.0.1   المنفذ: 8080",
                regionTitle: "المنطقة / الدولة",
                regionGlobal: "عالمي شامل",
                regionTurkey: "تركيا (BTK)",
                regionRussia: "روسيا (TSPU)",
                regionBrazil: "البرازيل (SOCKS5 مخصص)",
                regionHint: "اختر منطقتك لتطبيق أسرع استراتيجية تلقائياً.",
                dohTitle: "DNS مشفر (DoH)",
                profileTitle: "استراتيجية DPI",
                quicToggle: "تحويل QUIC UDP/443 إلى TCP/443",
                udpToggle: "دعم UDP للصوت والألعاب",
                customArgsLabel: "إعدادات ciadpi المخصصة",
                brazilSocksLabel: "خادم SOCKS5 (host:port)",
                exportDnsProfile: "تثبيت ملف DNS المشفر (.mobileconfig)",
                exportDnsHint: "بدون تثبيت جانبي — الإعدادات → عام → VPN وإدارة الأجهزة.",
                runSelfTest: "تشغيل اختبار الاتصال",
                testingNow: "جاري الاختبار...",
                unlocked: "مفتوح",
                blocked: "محظور",
                copyLogs: "نسخ",
                clearLogs: "مسح",
                copied: "تم النسخ!"
            )
        default:
            return AppStrings(
                subtitle: "Unbound — Full-Speed Local DPI Bypass for iOS",
                tabConnect: "Connect",
                tabTest: "Self-Test",
                tabSettings: "Settings",
                tabLogs: "Logs",
                statusConnected: "CONNECTED",
                statusConnecting: "CONNECTING...",
                statusDisconnected: "READY",
                statusError: "ERROR",
                tapToConnect: "Tap to Connect",
                tapToDisconnect: "Tap to Disconnect",
                editionFullVpn: "Full System-Wide VPN Edition",
                editionFreeProxy: "Free Apple ID Local Proxy Edition",
                freeProxyGuideTitle: "10-Second iPhone Wi-Fi Proxy Setup",
                freeProxyGuideBody: "Settings → Wi-Fi → (i) → Configure Proxy → Manual:\nServer: 127.0.0.1   Port: 8080\n(Or SOCKS5: 127.0.0.1:1080)",
                regionTitle: "Country / Region Preset",
                regionGlobal: "Global Universal",
                regionTurkey: "Turkey (BTK)",
                regionRussia: "Russia (TSPU + YouTube Video)",
                regionBrazil: "Brazil (Custom SOCKS5)",
                regionHint: "Pick your region to auto-tune Discord, YouTube videos (googlevideo.com) & Roblox.",
                dohTitle: "Encrypted DNS (DoH)",
                profileTitle: "DPI Bypass Strategy",
                quicToggle: "Block QUIC UDP/443 → TCP/443 (YouTube/Browsers)",
                udpToggle: "UDP Voice / Game Support",
                customArgsLabel: "Custom ciadpi Arguments",
                brazilSocksLabel: "Custom SOCKS5 Endpoint (host:port)",
                exportDnsProfile: "Export iOS Encrypted DNS Profile (.mobileconfig)",
                exportDnsHint: "Zero sideloading required — install via Settings → General → VPN & Device Management.",
                runSelfTest: "Run Live Connectivity Self-Test",
                testingNow: "Running checks...",
                unlocked: "Unlocked",
                blocked: "Blocked",
                copyLogs: "Copy",
                clearLogs: "Clear",
                copied: "Copied!"
            )
        }
    }
}
