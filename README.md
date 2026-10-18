# SLX VPN for Windows

**Открытый исходный код · Open source.** Самостоятельный Windows-клиент SLX с выбором приложений для VPN, в стиле сайта SLX.

**0.1.0-alpha.2 — ранняя версия для Windows 11 x64.** Исправления связи со службой и интерфейса. Сборка установщика не означает завершённую системную приёмку. Проверки и ограничения: [docs/VERIFICATION.md](docs/VERIFICATION.md).

[Скачать alpha.2](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.2) · [Предыдущая alpha.1](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.1) · [SLX](https://slxvpn.com) · [Поддержка](https://t.me/slxvpn?direct)

## Возможности

- Импорт личной ссылки SLX: Hysteria2 и VLESS/XHTTP; выбор профиля.
- Два режима: VPN для выбранных .exe или всего компьютера.
- Быстрый выбор открытых программ с поиском, ручной выбор .exe и включение отдельных правил.
- RU/EN, темы SLX и Terminal, системный трей и переподключение.
- Пользовательский интерфейс без прав администратора; отдельная сетевая служба.
- DPAPI для локального хранения личной ссылки; секреты не попадают в исходники или журналы.

## Начало работы

1. Установите SLX через setup.exe. Установка или обновление службы требует подтверждения Windows.
2. В кабинете SLX создайте отдельное устройство Windows и скопируйте его личную ссылку.
3. Нажмите «Добавить подписку» и вставьте ссылку. Импорт явно использует собственный ID этой установки.
4. Выберите «Выбранные приложения», откройте «Приложения» и добавьте нужную программу из списка. Ручной выбор .exe также доступен. Нажмите «Подключить».
5. При изменении списка нажмите «Переподключить». Уже открытым сетевым соединениям приложения может понадобиться перезапуск.

Ссылка, уже привязанная к Happ, требует штатного переноса/сброса устройства через кабинет. SLX не копирует HWID Happ.
Подписки и настройки остаются в %LOCALAPPDATA%/SLX/VPN; удаление приложения не стирает их автоматически.
Обновление alpha.1 → alpha.2 использует тот же установщик приложения и идентификатор установки; повторно создавать устройство ради обновления не нужно.

## Ограничения alpha

**Kill switch пока нет:** при сбое ядра выбранные приложения могут выйти в интернет напрямую.
DNS выбранных приложений использует общий резолвер Windows напрямую. LAN/loopback обходят VPN.
Лаунчеры, дочерние процессы, Store-приложения и привилегированные службы могут требовать отдельных правил.
Несколько интерактивных пользователей Windows одновременно не поддерживаются.
Полная приёмка трафика через реальные серверы, IPv6, сна, обновления/удаления и чистой VM ещё не завершена.
См. [SECURITY.md](SECURITY.md). Production-серверы и Happ установщик не перенастраивает.

SLX-приложение и установщик пока не подписаны. Включённый Wintun сохраняет подлинную подпись производителя.
Не отключайте средства защиты Windows для запуска.

## Архитектура и сборка

C# / .NET 10 LTS, WPF/MVVM, Windows Service, Xray-core 26.9.9, официальный Wintun, Inno Setup 6.
Один локальный сетевой процесс обслуживает оба протокола. Исходники сайта и серверные секреты сюда не входят.

На Windows 11 x64: .NET SDK из global.json, PowerShell 7, Inno Setup 6.

```powershell
pwsh -File scripts/verify.ps1
# Только локальный кандидат для проверки установки и службы:
pwsh -File scripts/build.ps1 -Candidate
# После приёмки, проверки и коммита исходников:
pwsh -File scripts/build.ps1
pwsh -File scripts/package-source.ps1 -BaseRef v0.1.0-alpha.1
```

Версия берётся из Directory.Build.props. Установщик и архивы: artifacts/release/0.1.0-alpha.2/.
Рабочие сборки: artifacts/work/0.1.0-alpha.2/. Предыдущие версии не перезаписываются.
Кандидаты -Candidate изолированы в artifacts/candidates/<version>/<timestamp>/ и не допускаются к публикации.
Самодостаточная сборка включает .NET 10.0.13; пользователю не нужна отдельная установка .NET.
Файлы Xray загружаются из закреплённого официального релиза, SHA256 и подпись драйвера проверяются.
Для публикации node scripts/release.mjs проверяет чистый коммит, соответствие установщика исходникам и все контрольные суммы.
Скрипт не заменяет уже опубликованный релиз; публикация остаётся явным действием владельца.

Запуск из bin/Debug не заменяет установку: служба исполняет ядро только из защищённого Program Files.
[Архитектура](docs/ARCHITECTURE.md) · [Маршрутизация](docs/NETWORK.md) · [Контракт разработки](AGENTS.md)
MIT для исходников SLX; сторонние компоненты имеют собственные лицензии: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## English

An open-source SLX Windows client with per-application routing, Hysteria2/VLESS, RU/EN and native WPF UI.
Alpha.2 improves local service communication and desktop usability; installed-service acceptance must be checked separately.
Download the unsigned alpha installer from Releases, install it, import a separate SLX device link, select applications and connect.
This alpha has **no kill switch**; shared Windows DNS is direct in selected-app mode.
System/network acceptance is incomplete. See SECURITY.md and docs/VERIFICATION.md before relying on it.
