# Обновление SLX VPN до 0.1.0-alpha.24

Основа — опубликованная alpha.23, тег `v0.1.0-alpha.23`, коммит `85b514ffc317c92c14e34153bfbdc611cdebfe34`.
Codex по поручению владельца делает commit/push и выпуск через существующий Windows CI. Команды ниже сохранены для воспроизведения; после успешного выпуска повторять их не требуется.

## Установить

После публикации используйте обновление в настройках SLX или [установщик alpha.24](https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.24/SLX-VPN-0.1.0-alpha.24-win-x64-setup.exe).
Перед установкой явно отключите VPN и полностью выйдите из SLX через трей. Интерфейс и служба устанавливаются вместе поверх прежней версии; подписка и настройки сохраняются.

1. Убедитесь, что отображается alpha.24. Откройте выбор приложений: в сохранённом списке и после обновления браузеры, Telegram, Discord и распознанные игры должны быть выше остальных программ.
2. Проверьте поиск и добавление нужного EXE. Найденные программы не должны сами попадать в VPN; сохранённые переключатели должны остаться прежними. Изменения применяются к новым соединениям приложения.
3. Проверьте подключение и явное отключение в обоих режимах. После обычного обновления подписки при выключенном VPN кнопка сохраняет смысл «Подключиться»; при активном VPN «Отключиться» остаётся доступной.
4. На ПК друга повторите его обычный сценарий. Если проблема остаётся, сохраните диагностику во время действия и после результата, до ручного отключения: Phase, Protection, Probe passed, Code, UI operation, DNS, Health и Traffic route. Успешный журнал владельца не заменяет этот результат.
5. Не меняйте чужие адаптеры, глобальные DNS или настройки безопасности ради проверки. Для проверки Telegram полностью выйдите из него через трей и запустите заново; существующие сеансы могут сохранять прежний маршрут.

Если настройки повреждены или недоступны, SLX сохраняет исходный файл и показывает постоянное предупреждение в настройках. Изменения в этой сессии не сохраняются. Восстановите корректный файл/доступ и перезапустите приложение. Автоматического сброса данных в этом выпуске нет.

## Воспроизвести выпуск исходников

Исходники уже находятся в `C:\Users\xls\Desktop\slx-windows`. Changed ZIP из GitHub рассчитан на предыдущий тег; полный ZIP содержит исходники именно коммита выпуска. Данные установленного приложения в архивы не включаются.

```powershell
Set-Location "$env:USERPROFILE\Desktop\slx-windows" -ErrorAction Stop
[xml]$buildProps = Get-Content .\Directory.Build.props -Raw
if ($buildProps.Project.PropertyGroup.Version -ne '0.1.0-alpha.24') { throw 'Открыта другая версия' }
git status --short
git diff --stat
git remote -v
git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Проверьте изменения' }
```

Для ещё не зафиксированного исправления на `main` с origin `Sooliks/slx-windows`:

```powershell
git add -- Directory.Build.props PROJECT_SPEC.md README.md docs src tests
if ($LASTEXITCODE -ne 0) { throw 'Не удалось подготовить изменения' }
git commit -m 'Release alpha.24: prioritize apps and strengthen recovery and local state'
if ($LASTEXITCODE -ne 0) { throw 'Коммит не создан' }
git push origin HEAD:main
if ($LASTEXITCODE -ne 0) { throw 'Push не выполнен' }
```

Изменение версии запускает [Windows workflow](https://github.com/Sooliks/slx-windows/actions/workflows/release.yml).
Он выполняет проверки, собирает установщик и публикует шесть assets: установщик, полный и changed ZIP, DELETIONS.txt, provenance.json и SHA256SUMS.txt. Точный предыдущий тег alpha.23 обязателен. Не заменяйте старые assets и не используйте force-push.

После успешного CI проверьте [страницу alpha.24](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.24). Сайт имеет отдельный кэш; публикация Windows-приложения не развёртывает сайт.

```powershell
curl.exe --noproxy "*" --max-time 20 -sS -D - -o NUL -H "Cache-Control: no-cache" "https://slxvpn.com/download/windows" |
    Select-String '^(HTTP/|Location:|Cache-Control:|X-SLX-Release-)'
```

Ожидается редирект к установщику alpha.24. Пока кэш обновляется, доступна прямая ссылка GitHub.

## Проверка и откат

```powershell
pwsh -NoProfile -File .\scripts\verify.ps1
if ($LASTEXITCODE -ne 0) { throw 'Проверки не прошли' }
pwsh -NoProfile -File .\scripts\build.ps1 -Candidate
if ($LASTEXITCODE -ne 0) { throw 'Кандидат не собран' }
```

Эти команды не публикуют выпуск. Для отката отключите VPN, выйдите из SLX и установите alpha.23 целиком. Сохранённые форматы совместимы. Возврат старой версии не подтверждает исправление исходной проблемы друга.
