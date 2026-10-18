# SLX VPN for Windows

**Открытый исходный код · Open source.** Самостоятельный Windows-клиент SLX с выбором приложений для VPN, в стиле сайта SLX.

**0.1.0-alpha.1 — ранняя версия для Windows 11 x64.** Сборка установщика не означает завершённую системную приёмку. Проверки и ограничения: [docs/VERIFICATION.md](docs/VERIFICATION.md).

[Скачать alpha](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.1) · [SLX](https://slxvpn.com) · [Поддержка](https://t.me/slxvpn?direct)

## Возможности

- Импорт личной ссылки SLX: Hysteria2 и VLESS/XHTTP; выбор профиля.
- Два режима: VPN для выбранных .exe или всего компьютера.
- Выбор запущенных приложений и файлов .exe, включение отдельных правил.
- RU/EN, темы SLX и Terminal, системный трей и переподключение.
- Пользовательский интерфейс без прав администратора; отдельная сетевая служба.
- DPAPI для локального хранения личной ссылки; секреты не попадают в исходники или журналы.

## Начало работы

1. Установите SLX через setup.exe. Установка службы требует подтверждения Windows.
2. В кабинете SLX создайте отдельное устройство Windows и скопируйте его личную ссылку.
3. Нажмите «Добавить подписку» и вставьте ссылку. Импорт явно использует собственный ID этой установки.
4. Выберите «Выбранные приложения», добавьте нужные .exe и нажмите «Подключить».
5. При изменении списка нажмите «Переподключить». Уже открытым сетевым соединениям приложения может понадобиться перезапуск.

Ссылка, уже привязанная к Happ, требует штатного переноса/сброса устройства через кабинет. SLX не копирует HWID Happ.
Подписки и настройки остаются в %LOCALAPPDATA%/SLX/VPN; удаление приложения не стирает их автоматически.

## Ограничения alpha

**Kill switch пока нет:** при сбое ядра выбранные приложения могут выйти в интернет напрямую.
DNS выбранных приложений использует общий резолвер Windows напрямую. LAN/loopback обходят VPN.
Лаунчеры, дочерние процессы, Store-приложения и привилегированные службы могут требовать отдельных правил.
Несколько интерактивных пользователей Windows одновременно не поддерживаются.
Полная приёмка TCP/UDP/IPv6, сна, установки/обновления/удаления и чистой VM ещё не завершена.
См. [SECURITY.md](SECURITY.md). Production-серверы и Happ установщик не перенастраивает.

SLX-приложение и установщик пока не подписаны. Включённый Wintun сохраняет подлинную подпись производителя.
Не отключайте средства защиты Windows для запуска.

## Архитектура и сборка

C# / .NET 10 LTS, WPF/MVVM, Windows Service, Xray-core 26.9.9, официальный Wintun, Inno Setup 6.
Один локальный сетевой процесс обслуживает оба протокола. Исходники сайта и серверные секреты сюда не входят.

На Windows 11 x64: .NET SDK из global.json, PowerShell 7, Inno Setup 6.

```powershell
pwsh -File scripts/verify.ps1
pwsh -File scripts/build.ps1
```

Установщик: artifacts/release. Файлы runtime загружаются из закреплённого официального релиза, SHA256 и подпись проверяются.
Самодостаточная сборка включает runtime .NET 10.0.13; отдельная установка .NET пользователю не нужна.
Запуск из bin/Debug не заменяет установку: служба исполняет ядро только из защищённого Program Files.

[Архитектура](docs/ARCHITECTURE.md) · [Маршрутизация](docs/NETWORK.md) · [Контракт разработки](AGENTS.md)
MIT для исходников SLX; сторонние компоненты имеют собственные лицензии: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## English

An open-source SLX Windows client with per-application routing, Hysteria2/VLESS, RU/EN and native WPF UI.
Download the unsigned alpha installer from Releases, install it, import a separate SLX device link, select applications and connect.
This alpha has **no kill switch**; shared Windows DNS is direct in selected-app mode.
System/network acceptance is incomplete. See SECURITY.md and docs/VERIFICATION.md before relying on it.
