# SLX Windows — контекст передачи, 25 октября 2026

## Текущий подтверждённый выпуск alpha.24

**0.1.0-alpha.24 опубликована** из коммита `4df36d2517b5077b121158689d553775d0470065` в 16:07:28 UTC.
[Windows CI 40209908805](https://github.com/Sooliks/slx-windows/actions/runs/40209908805) успешен: 664 основных
теста и 1 WPF, установщик и шесть assets. SHA-256 скачанных файлов и provenance проверены; full ZIP433/changed ZIP59
совпадают с исходниками коммита. Сайт после обновления кэша отдаёт установщик alpha.24. Локальные исходники,
подписку/настройки и установленные службу/сеть ради проверки не подменяли. Установленная приёмка остаётся PARTIAL.
Этот последующий docs-коммит фиксирует результат CI, не изменяя уже опубликованные assets.

Владелец попросил повысить стабильность и поставить в начало выбора приложений браузеры, Telegram, Discord и игры.
Подготовлена alpha.24: общий приоритет библиотеки до лимитов источников/кэша, сохранение выбранных правил и карточек;
обновление точных versioned EXE при recovery; безопасная проверка локального хранилища без перезаписи повреждённых
настроек; независимая загрузка подписки; точная семантика HTTP-активности/отключения и допуска к установке обновления.
Сбой native protection переводит в защищённое recovery с правильным before/after планом, без повторного AddRule.
Локальный verify и последующий CI завершились успешно: 664 основных теста и 1 WPF, без пропусков и предупреждений.
Порядок выпуска не менялся: Codex делает commit/push, существующий Windows CI проверяет и публикует.

Владелец прислал live alpha.23 в 15:59:00 UTC: Connected/Protection=true/Probe=true/current, ConnectedAt15:24:59,
Health incidents3/retained3. Последняя primary Gstatic TLS2302ms, затем repeat Gstatic204/278ms и alternate SLX200/556ms.
Обе IPv4-половины OwnedTunnel /1 metric0; IPv6 без usable public source пропущен штатно. Это успешное наблюдение
владельца, не установленная причина первой TLS-ошибки и не приёмка друга. После alpha.24 владелец попросит журнал
друга, если проблема сохранится. Не требовать его заранее и не объявлять чужую проблему решённой по owner-логу.

## Предыдущий подтверждённый выпуск alpha.23

STATUS: PARTIAL по установленной приёмке. **0.1.0-alpha.23 опубликована** из коммита
`85b514ffc317c92c14e34153bfbdc611cdebfe34`, основа — alpha.22 `f694b51c8a815ce041fe67b26c8e21627aaa5cda`.
[Windows CI 40205535509](https://github.com/Sooliks/slx-windows/actions/runs/40205535509) завершился успешно:
617 основных тестов и один составной WPF-тест, установщик и шесть release-assets опубликованы.
[Выпуск alpha.23](https://github.com/Sooliks/slx-windows/releases/tag/v0.1.0-alpha.23) проверен скачиванием:
SHA-256, provenance и оба source ZIP совпали с коммитом. Редирект сайта уже ведёт на alpha.23.
Это подтверждает выпуск, но не установку, исправление ПК друга или работу CS2. Последующая запись этих
результатов в документацию не меняет исходный коммит и assets опубликованного выпуска.

## Что произошло между alpha.18 и alpha.22

- Alpha.18: независимые pipe-слоты для status/off, сериализованные мутации, отмена ожидания явным off,
  повторяемая очистка, rollback только подтверждённо неудачной первой попытки и реальный outbound interface.
  Владелец отправлял исходники сам; после проблем Inno Setup/изменения входов сборки CI был доработан.
- Alpha.19: сжатый ограниченный GitHub release catalog и общий бюджет проверки обновлений 30 секунд.
  Проверка размера/источника/контрольной суммы установщика сохранена.
- Alpha.20: параллельный выбор двух прежних HTTPS DNS через VPN, проверка обычного DNS-маршрута и полного
  ответа; безопасная диагностика до трёх попыток. Скриншот друга с VPN_DNS_FAILED не доказывал причину.
- Alpha.21: DNS host routes и ограниченное ожидание их готовности после OtherInterface/RouteMismatch у друга.
  У владельца позднее были Connected и 10 health incidents/10 retained, без Recovery; причинность Steam не доказана.
- Alpha.22: Checking отделён от реального Reconnecting; Help показывает primary/repeat/alternate health checks.
  Владелец сообщил о 538 проверках плюс WPF. Это историческое сообщение о предыдущем выпуске, не результат
  текущего прогона alpha.23. Фактические команды и новые результаты фиксируются в VERIFICATION.md.

## Последний случай друга и границы вывода

В alpha.22 выбранные приложения и весь ПК, по его словам, не работали через VPN. Журнал скопирован почти
сразу после попытки: Captured 13:55:36 UTC, Disconnected/Protection=false/Probe=false/Code:none, Observed
13:55:15 UTC; DNS Success/OwnedTunnel /32 metric0 — 13:54:36 UTC. Не утверждать, что он нажал Disconnect.
ObservedAt — публикация состояния, а не время получения UI. Предыдущий UI при IsBusy пропускал status polling,
сохраняя current для старого снимка; это найденный дефект диагностики, а не доказанная причина отсутствия трафика.
DNS host-route success не проверяет общие маршруты приложения. Wi-Fi/провайдер/конкретный DNS не установлены.

## Что подготовлено в alpha.23

- Четыре owned split-default /1 вместо /0, прежние DNS host routes; Xray владеет созданием и удалением.
  До Connected — до шести read-only снимков за три секунды: обе IPv4-половины и условный IPv6 при usable
  preferred public source. Это не пакеты и не проверка каждого адреса. Ошибка VPN_TRAFFIC_ROUTE_FAILED;
  обычные маршруты также участвуют в периодическом подтверждении, без ослабления защиты и rollback.
- Optional TrafficRoutes в IPC v2 и безопасный блок Help. UI читает status во время мутации, используя прежний
  ten-second timeout и один запрос одновременно. Generation + observationRevision отсекают поздние ответы.
  Pending action, service-read time и service-state time различаются; неизвестная мутация не повторяется.
  Если read/unknown marker успел обновить revision до ответа мутации, Success/Code отделены от устаревшего snapshot;
  один bounded fresh read в пределах текущего deadline сверяет состояние. Ошибка оставляет unknown,
  явный off сменяет generation и отменяет ожидание; повтор мутации для получения статуса запрещён. Pending label
  снимается после известного результата команды, до завершения fresh read; periodic reads на это время не дублируются.
- Один UI на Windows user/session: manual duplicate только показывает окно, --tray не показывает;
  второй процесс не создаёт модель и не отправляет off. Render-check изолирован, handles/waits освобождаются.
- DisplayIcon сохраняет запятые внутри пути; known Codex/ChatGPT package paths обновляются в user context
  перед новым планом. Неразрешённые exact paths сохраняются для безопасного отказа, wildcard-правил нет.

- Enabled exact EXE в выбранных приложениях получают canonical casing через bounded local handle-walk,
  без reparses и с запретом rename до сравнения исходного/canonical native WFP appID. Разрешена только разница
  case/slash; 8.3/reparse/device/remote/remapped aliases дают TRAFFIC_APPLICATION_PATH_UNSUPPORTED, разные IDs —
  TRAFFIC_APPLICATION_IDENTITY_MISMATCH. Одинаковый список используется в start/update/restore/recovery для
  WFP/Xray/current/pinned/intents/revision. Whole-PC и disabled rows не требуют native resolution. Это отдельный
  selected-app дефект, не установленная причина whole-PC случая друга; результаты fixtures — в VERIFICATION.md.

## Следующий шаг и ограничения среды

Не изменять сайт, серверы, аккаунты, БД, реальные настройки, запущенную службу или сеть ПК ради проверки. Отдельное
исправление website release-cache упоминалось раньше; факт его production deployment не подтверждён.
Эта итерация сайт не меняет.

Первоначальный порядок передачи был changed ZIP, полный source ZIP и команды для владельца. Затем владелец
явно поручил Codex самому сделать push и сразу выпустить обновление. Актуальный порядок: проверенные исходники
фиксируются в существующем main и отправляются в origin; версия в Directory.Build.props запускает Windows CI.
CI выполняет проверки, сборку и публикацию; changed/full source ZIP, удаления и provenance формируются из коммита.
Не создавать новую Git-историю, force-push или patch installer. Не включать .git, bin/obj, runtime-бинарники,
настоящие env/подписки/секреты, шрифты, установленные данные или результаты тестов в source-архивы.

Приёмка на установленной системе остаётся открытой: друг и владелец, оба режима, новый и существующий app
session, IPv4/IPv6/shared DNS, очистка owned routes без чужих изменений, reboot/sleep/network changes,
Windows10, DPI, обновление/откат и CS2. Не обещать работу на любом ПК, DNS или интернете по unit fixtures.
