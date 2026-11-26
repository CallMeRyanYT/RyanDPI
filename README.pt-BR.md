# RyanDPI v3.1.3 — Unbound

[English](README.md) | [Türkçe](README.tr.md) | **Português (Brasil)** | [Русский](README.ru.md) | [فارسی](README.fa.md) | [Bahasa Indonesia](README.id.md) | [简体中文](README.zh.md) | [العربية](README.ar.md)

**RyanDPI — Unbound** é uma ferramenta moderna de 1 clique para **Windows & Android** criada para **liberar Compartilhar Tela (Go Live) e Câmera no Discord no Brasil sem dar LAG nos seus jogos**, além de desbloquear sites e apps bloqueados por DPI na Turquia, Rússia e no mundo todo!

---

## Download & Início Rápido (`v3.1.3 — Unbound`)

[![Baixar Instalador Windows](https://img.shields.io/badge/Baixar-Windows%20Setup%20(.exe)-FF1E3C?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.3/RyanDPI-Setup-Windows-3.1.3.exe)
[![Baixar ZIP Portátil](https://img.shields.io/badge/Baixar-Windows%20Port%C3%A1til%20(.zip)-111116?style=for-the-badge&logo=windows)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.3/RyanDPI-Portable-Windows-3.1.3.zip)
[![Baixar APK Android](https://img.shields.io/badge/Baixar-Android%20APK%20(v3.1.3)-00C853?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.3/RyanDPI-Android-3.1.3.apk)

---

## Como o Modo Brasil Libera o Compartilhar Tela no Discord com 0 ms de Lag nos Jogos

No Brasil, o Discord desativou o **Compartilhar Tela (Go Live)** e **Câmera** por **Geo-IP (decisão da ANPD / ECA Digital)** — não é um bloqueio de DPI da operadora. Por isso, um bypass DPI local sozinho não muda o país do seu IP para os servidores do Discord.

Mas usar uma VPN normal no PC inteiro destrói seu ping no Valorant, CS2, Roblox, League of Legends e Fortnite!

**O RyanDPI resolve isso com Split-Tunnel exclusivo para o Discord:**
1. **APENAS o Discord** (`Discord.exe`, `DiscordPTB`, `DiscordCanary` no Windows; `com.discord` no Android) passa pelo túnel — e você pode ativar opcionalmente navegadores (Chrome/Edge/Firefox/Brave) se usar Discord Web.
2. **100% dos seus jogos e outros apps ficam DIRETOS na sua internet do Brasil — 0 ms de lag extra!**
3. Escolha entre:
   - **Cloudflare WARP MASQUE Grátis Integrado (`usque`):** Sem login, sem conta, registra em 2 segundos automaticamente.
   - **Proxy SOCKS5 Personalizado fora do Brasil (EUA / Argentina / Europa):** Se o WARP sair em São Paulo (`BR`) na sua operadora, basta colar qualquer SOCKS5 fora do Brasil em **Configurações** para IP não-BR garantido com 0 ms de lag nos jogos!
4. **Modo API-Only (Opcional no Windows):** Desative *"Rotear UDP do Discord"* nas Configurações se quiser desbloquear a API do Go Live mantendo até o áudio/vídeo UDP direto com menor latência.
5. **Teste Integrado:** Clique em **"Testar"** para ver ao vivo o **País de Saída do Túnel Discord** (`Non-BR Exit — Screen Share Ready!`) e o **Ping Direto dos Jogos (0 ms Lag)**.

---

## Modos Globais e 8 Idiomas

- **Global Universal:** Motor DPI local de 7 estágios sem VPN remota para qualquer país com censura DPI.
- **Turquia (BTK)** & **Rússia (TSPU):** Perfis otimizados para Discord, Roblox, YouTube, Instagram e Wattpad com DNS DoH criptografado.
- **Brasil (Discord Split-Tunnel):** Focado em desbloquear Compartilhar Tela e Vídeo no Discord sem encostar no ping dos jogos.
- **8 Idiomas em 1 Clique:** `EN`, `TR`, `PT`, `RU`, `FA`, `ID`, `ZH`, `AR` no Windows e Android (+ instalador Windows em Português do Brasil!).

---

## Como Usar

### Windows (10 & 11 x64)
1. Baixe e instale **[RyanDPI-Setup-Windows-3.1.3.exe](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.3/RyanDPI-Setup-Windows-3.1.3.exe)** (ou extraia o [ZIP Portátil](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.3/RyanDPI-Portable-Windows-3.1.3.zip)).
2. Na primeira tela, selecione **Brasil — Compartilhar Tela & Vídeo no Discord** e mude o idioma para **`PT`** no topo.
3. Clique em **"Instalar e Ativar RyanDPI"** e reinicie o Discord (`Ctrl+R` ou feche na bandeja do sistema e abra de novo).
4. Clique em **"Testar"** para conferir seu IP de saída do Discord e confirmar que seus jogos estão 100% diretos.

### Android (Android 10–16, Sem Root)
1. Baixe e instale **[RyanDPI-Android-3.1.3.apk](https://github.com/CallMeRyanYT/RyanDPI/releases/download/v3.1.3/RyanDPI-Android-3.1.3.apk)**.
2. Escolha o modo **Brasil**, toque em **"Conectar & Liberar"** e aceite a permissão local do Android.
3. Somente o app do Discord passará pelo túnel — todos os jogos do celular continuam com 0 ms de lag!

---

## Componentes Open-Source

- **[ByeDPI (`ciadpi`)](https://github.com/hufrea/byedpi)** • **[usque](https://github.com/Diniboy1123/usque)** • **[hev-socks5-tunnel](https://github.com/heiher/hev-socks5-tunnel)** • **[ProxiFyre](https://github.com/wiresock/proxifyre)** • **[dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy)**

## Licença

MIT License — veja [LICENSE](LICENSE).
