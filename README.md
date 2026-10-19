# SLX VPN for Windows

**Открытый исходный код · Open source.** Самостоятельный Windows-клиент SLX с выбором приложений для VPN, в стиле сайта SLX.

**0.1.0-alpha.6 — ранняя версия для Windows 10 / 11.** Восстановление VPN с защитой трафика, защищённый DNS и удобная библиотека приложений. Сборка установщика не означает завершённую системную приёмку. Проверки и ограничения: [docs/VERIFICATION.md](docs/VERIFICATION.md).

[Скачать alpha.6](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.6) · [Предыдущая alpha.5](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.5) · [SLX](https://slxvpn.com) · [Поддержка](https://t.me/slxvpn?direct)

## Возможности

- Импорт личной ссылки SLX: Hysteria2 и VLESS/XHTTP; выбор профиля.
- Два режима: VPN для выбранных .exe или всего компьютера.
- Каталог из 18 программ, ярлыки рабочего стола и меню «Пуск», иконки и ручной выбор .exe. Codex / ChatGPT добавляются одной записью с помощниками. Программы можно добавлять до запуска.
- Переключатели приложений на главной, длительность подключения, активный протокол и время ответа службы.
- Защита выбранного трафика при сбое и автоматическое восстановление; RU/EN, темы SLX и Terminal, системный трей.
- Каталог обновляется с SLX и работает без сети; список программ компьютера не отправляется на сервер.
- Пользовательский интерфейс без прав администратора; отдельная сетевая служба.
- DPAPI для локального хранения личной ссылки; секреты не попадают в исходники или журналы.

## Начало работы

1. Установите SLX через setup.exe. Установка или обновление службы требует подтверждения Windows.
2. В кабинете SLX создайте отдельное устройство Windows и скопируйте его личную ссылку.
3. Нажмите «Добавить подписку» и вставьте ссылку. Импорт явно использует собственный ID этой установки.
4. Выберите «Выбранные приложения», откройте «Приложения» и добавьте нужную программу из списка. Ручной выбор .exe также доступен. Нажмите «Подключить».
5. Изменения списка применяются автоматически. Туннель кратко переподключается; уже открытым сетевым соединениям приложения может понадобиться перезапуск.

Ссылка, уже привязанная к Happ, требует штатного переноса/сброса устройства через кабинет. SLX не копирует HWID Happ.
Подписки и настройки остаются в %LOCALAPPDATA%/SLX/VPN; удаление приложения не стирает их автоматически.
Обновление alpha.4 → alpha.5 использует тот же установщик приложения и идентификатор установки; повторно создавать устройство ради обновления не нужно.

## Ограничения alpha

При сбое VPN защищённый публичный трафик блокируется до автоматического восстановления; «Отключиться» снимает защиту.
Публичный DNS из TUN использует HTTPS через VPN. Это общий Windows DNS; LAN/loopback остаются прямыми.
Лаунчеры, дочерние процессы, Store-приложения и привилегированные службы могут требовать отдельных правил.
Несколько интерактивных пользователей Windows одновременно не поддерживаются.
Полная приёмка трафика через реальные серверы, IPv6, сна, обновления/удаления и чистой VM ещё не завершена.
См. [SECURITY.md](SECURITY.md). Production-серверы и Happ установщик не перенастраивает.

SLX-приложение и установщик пока не подписаны. Включённый Wintun сохраняет подлинную подпись производителя.
Не отключайте средства защиты Windows для запуска.

## Архитектура и сборка

C# / .NET 10 LTS, WPF/MVVM, Windows Service, Xray-core 26.9.9, официальный Wintun, Inno Setup 6.
Один локальный сетевой процесс обслуживает оба протокола. Исходники сайта и серверные секреты сюда не входят.

На Windows 10 / 11: .NET SDK из global.json, PowerShell 7, Inno Setup 6.

```powershell
pwsh -File scripts/verify.ps1
# Только локальный кандидат для проверки установки и службы:
pwsh -File scripts/build.ps1 -Candidate
# После приёмки, проверки и коммита исходников:
pwsh -File scripts/build.ps1
pwsh -File scripts/package-source.ps1 -BaseRef v0.1.0-alpha.5
```

Версия берётся из Directory.Build.props. Установщик и архивы: artifacts/release/0.1.0-alpha.6/.
Рабочие сборки: artifacts/work/0.1.0-alpha.6/. Предыдущие версии не перезаписываются.
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
Alpha.4 adds a curated local-first application catalog, dashboard switches and connection facts, and mitigates a confirmed browser IPv6 source-address failure.
Download the unsigned alpha installer from Releases, install it, import a separate SLX device link, select applications and connect.
Persistent WFP protection blocks protected public traffic outside the owned TUN during recovery. Shared public DNS captured by TUN uses HTTPS through VPN; LAN remains direct. Full existing-flow/IPv6/boot acceptance remains open.
System/network acceptance is incomplete. See SECURITY.md and docs/VERIFICATION.md before relying on it.

Alpha.3: библиотека с локальными иконками, автоматическое применение списка и проверяемое исправление TLS/IPv6. Подробности: [диагностика](docs/NETWORK_DIAGNOSIS.md).

Windows 10: начиная со сборки 19041, целевая совместимость предварительной версии. Фактическая проверка на Windows 10 ещё не завершена. [Границы совместимости](docs/COMPATIBILITY.md).
Автоматическая библиотека исключает типовые deinstaller EXE для всех продуктов. Пользовательские правила и ручной выбор файла сохраняются.
