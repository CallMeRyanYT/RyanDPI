# RyanDPI v3.1.1 — Unbound

[English](README.md) | [Türkçe](README.tr.md) | [Português (Brasil)](README.pt-BR.md) | **Русский** | [فارسی](README.fa.md) | [Bahasa Indonesia](README.id.md) | [简体中文](README.zh.md) | [العربية](README.ar.md)

**RyanDPI — Unbound** — современный инструмент в 1 клик для **Windows и Android** с нулевой потерей скорости и **0 мс пинга в играх**, созданный для обхода **ТСПУ в России** (**YouTube**, **Discord + Голос**, **Instagram**, **Roblox**), блокировок в Турции, ограничений демонстрации экрана Discord в Бразилии и DPI по всему миру!

---

## Скачать и Быстрый Старт (`v3.1.1 — Unbound`)

[![Скачать Windows Setup](https://img.shields.io/badge/Скачать-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.1/RyanDPI-Setup-Windows-3.1.1.exe)
[![Скачать Portable ZIP](https://img.shields.io/badge/Скачать-Windows%20Portable%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.1/RyanDPI-Portable-Windows-3.1.1.zip)
[![Скачать Android APK](https://img.shields.io/badge/Скачать-Android%20APK%20(v3.1.1)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.1/RyanDPI-Android-3.1.1.apk)

---

## Почему RyanDPI идеально подходит для России (ТСПУ)?

- **Мгновенный режим «Россия — ТСПУ»:** Стартует с проверенной против ТСПУ стратегии `--split 2 --disorder -1+s --tlsrec 1+s` и автоматически переключается на `OOB` и `fake-SNI` с низким TTL (`--fake -1 --ttl 5 --fake-sni www.google.com`) для Ростелекома, МТС, Билайна, МегаФона, Дом.ру и других провайдеров.
- **YouTube в 4K, Discord + Голос (UDP), Instagram и Roblox:** Включает обход UDP (`-a 1`) для стабильных голосовых каналов Discord и шифрованный DNS (`DNSCrypt-Proxy` на Windows / встроенный HTTP/2 DoH шлюз на Android).
- **Это НЕ удалённый VPN — 100% скорость вашего тарифа:** Трафик идёт напрямую через вашего провайдера без чужих серверов, без лимитов трафика и без высокого пинга.
- **Все приложения сразу:** Разблокирует браузеры, Discord, Roblox, игры и системные клиенты без ручного добавления списков.
- **Глобальные режимы + 8 языков:** При первом запуске выберите **Россия (ТСПУ)**, **Универсальный Глобальный**, **Турция (BTK)** или **Бразилия (Демонстрация экрана Discord)**. Русский интерфейс включается в 1 клик (а также доступен в самом установщике Windows!).
- **Живой самотест:** Проверяет доступность YouTube, Discord API, Discord Media, Instagram, Roblox, шифрованного DNS и базового пинга прямо внутри приложения.

---

## Установка и Запуск

### Windows (10 и 11 x64)
1. Скачайте и запустите **[RyanDPI-Setup-Windows-3.1.1.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.1/RyanDPI-Setup-Windows-3.1.1.exe)** (или распакуйте [Portable ZIP](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.1/RyanDPI-Portable-Windows-3.1.1.zip)).
2. Выберите режим **Россия — Полный обход ТСПУ** и язык **`RU`** в верхней панели.
3. Нажмите **«Установить и включить RyanDPI»**, затем **«Тест»**, чтобы проверить YouTube, Discord, Instagram и Roblox.

### Android (Android 10–16, Без Root)
1. Скачайте **[RyanDPI-Android-3.1.1.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.1/RyanDPI-Android-3.1.1.apk)** и установите на телефон.
2. Выберите регион **Россия — ТСПУ**, нажмите **«Подключить и открыть»** и подтвердите локальное VPN-разрешение Android *(трафик не уходит на удалённые VPN-серверы!)*.
3. Добавьте плитку **RyanDPI** в шторку быстрых настроек для включения в 1 нажатие.

---

## Открытые компоненты

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** • **[usque](https://github.com/Diniboy1123/usque)** • **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** • **[ProxiFyre](https://github.com/wiresock/proxifyre)** • **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)**

## Лицензия

MIT License — см. [LICENSE](LICENSE).
