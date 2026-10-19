# SLX VPN для Windows

Открытый клиент SLX: VPN для выбранных приложений или всего компьютера.

[Сайт SLX](https://slxvpn.com) · [Скачать приложение](https://slxvpn.com/download/windows) · [Поддержка](https://t.me/slxvpn?direct)

## Возможности

- Hysteria2 и VLESS/XHTTP, импорт личной ссылки из кабинета SLX.
- Библиотека установленных программ и игр с иконками, поиск по ярлыкам, ручной выбор файла.
- Переключатели VPN для отдельных приложений; изменения применяются автоматически.
- Блокировка защищённого трафика при обрыве и автоматическое восстановление подключения.
- Русский и английский интерфейс, две темы, системный трей и автозапуск при входе в Windows.
- Список программ остаётся на компьютере; личная ссылка хранится с защитой Windows DPAPI.

## Установка

1. Скачайте и запустите установщик. Установка сетевой службы требует подтверждения Windows.
2. В [кабинете SLX](https://slxvpn.com/account/devices) добавьте устройство Windows и скопируйте личную ссылку.
3. Вставьте ссылку в приложение и нажмите «Добавить подписку».
4. Выберите приложения или режим «Весь компьютер», затем нажмите «Подключиться».

Для ссылки, уже привязанной к Happ, используйте перенос устройства в кабинете. Обновление приложения сохраняет настройки и ID установки.

## Совместимость

Alpha.10 — предварительная версия. Целевые системы: Windows 10 (сборка 19041+) и Windows 11; проверка на Windows 10 ещё не завершена. Приложение и установщик пока без подписи, драйвер Wintun сохраняет подпись производителя.

При обрыве VPN выбранный публичный трафик блокируется до восстановления. Общий публичный DNS Windows также защищается; локальная сеть остаётся прямой. Полная системная приёмка ещё не завершена: [проверки](docs/VERIFICATION.md), [совместимость](docs/COMPATIBILITY.md), [безопасность](SECURITY.md).

## Сборка из исходников

C# / .NET 10, WPF, Windows Service, Xray, Wintun, Inno Setup 6.
Нужны Windows, SDK из [global.json](global.json), PowerShell 7 и Inno Setup 6. Готовый установщик включает .NET — пользователю устанавливать его отдельно не нужно.

```powershell
pwsh -File scripts/verify.ps1
pwsh -File scripts/build.ps1 -Candidate
# Для выпуска: проверенный чистый коммит, затем сборка без -Candidate.
pwsh -File scripts/build.ps1
pwsh -File scripts/package-source.ps1 -BaseRef v0.1.0-alpha.9
```

Результат — `artifacts/release/<version>/`. Запуск из `bin/Debug` не заменяет установку службы. [Архитектура](docs/ARCHITECTURE.md) · [Сеть](docs/NETWORK.md) · [Правила разработки](AGENTS.md).

Исходники SLX — MIT. Лицензии зависимостей: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## English

[SLX VPN](https://slxvpn.com/?lang=en) is an open-source Windows client with selected-app and whole-computer modes, Hysteria2/VLESS, an app library and automatic recovery. [Download](https://slxvpn.com/download/windows), install, add a personal device link from your SLX account, choose your apps and connect.

This is an unsigned prerelease. Windows 10 is a compatibility target with acceptance still pending. See [verification](docs/VERIFICATION.md) and [security](SECURITY.md) for current limits.
