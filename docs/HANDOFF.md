# SLX Windows — контекст передачи, 27 октября 2026

## Текущий подтверждённый выпуск alpha.26 — связь вне выбранных приложений

**Alpha.26 опубликована** из `c90bc890396d4cff55fed79f0951d68a778b118a` в2026-10-27T00:29:36Z.
[Windows CI40426248413](https://github.com/Sooliks/slx-windows/actions/runs/40426248413) успешен:732 основных теста+1WPF,
0failed/skipped,0warnings/errors, guards, чистая сборка и публикация пройдены. Все6 assets скачаны: SHA-256/provenance
совпадают; fullZIP480/changedZIP33 соответствуют Git с PS1CRLF export. Установщик127196209байт, SHA-256
`0162c1f3c31db8cee88a453b43f8740fe6acb149b2ae30c28551831ac4cc4d2e`.
Первый GETсайта вернул кэшированный25, повторный уже302на26; приложение использует собственный GitHub-каталог.
Никаких правок сайта не было.
Этот docs-коммит фиксирует доказательства, не меняя release assets. Установленная приёмка остаётся PARTIAL.

Владелец разрешил немедленный prod-выпуск после обязательных проверок. Рабочая база — опубликованная alpha.25
ec2d552 и docs-коммит0edd93b; remote main проверен, локальных сторонних правок не было.
Журнал27Oct00:13:58 UTC: current/Connected/Protection=true/Probe=true, Recovery2 ProbeFailed последний00:08:35,
ConnectedAt00:09:27. Health245/retained243, primary/repeatGstaticTLS~2.2s, alternateSLX200/431ms, DNSsuccess233ms,
IPv4обе/1 OwnedTunnel. Это состояние после восстановления, не доказательство перезагрузки сервера/пакетного outage.

Найдено: выбранный режим тоже ставил глобальный WFP DNS53block и направлял общий DNS через VPN. Теперь DNS53block
толькоWholeComputer; выбранные точныеEXEзащищеныкакпрежде. SharedHTTPSDNSSelectedидётDIRECTпрежними2peers,
WholeVPN;probeвсегдаVPN. При кратком remote-onlyсбоеSelectedдо90сек сохраняет исправные ядро/TUN/маршруты и прямые
сеансы, Checking/Probe=false, проверкис5секпаузами; ресурсныйсбой немедленно normalrecovery. ФорматIPCнерасширен,
чтобы строгий updater25 прочитал reply26. UI/docs объясняют DNSвнеVPNи отличают OwnedTunnelотVPNoutbound.
Реальный pinnedXrayloopbackDNScontract прошёл: Selected отвечаетприblackholeVPN, WholeневыходитDIRECT.
Полная локальная проверка exit0:732 основных теста+1WPF,0warnings/errors/skips, source-sizegate; завершена00:23:09UTC.
Rootповторил pinned-coreDNScontract, оба режима exit0; свежий RU940кадр просмотрен. Публикация и CIподтвержденывыше.
Живую службу/сеть/сервер не меняли.

## Подтверждённый выпуск alpha.25 — встроенное обновление

**0.1.0-alpha.25 опубликована** из `ec2d5524b4227f84779521717e2593b0a17088f1` в20:00:08 UTC.
[Windows CI40228346633](https://github.com/Sooliks/slx-windows/actions/runs/40228346633) завершился успешно:
**717 основных тестов +1 WPF**,0 failed/skipped,0 warnings/errors; все guards, чистая сборка и публикация прошли.
Все6 assets скачаны и проверены; SHA-256/provenance совпадают. Full ZIP474/changed ZIP86 побайтово соответствуют Git
(с CRLF-export для PowerShell). Установщик127169545 байт, SHA-256
`7c49a618f4a56b1f210fec0982a5924b01e6b577a984358fd6dd31fc42afb39e`. Сайт отдаёт302 на alpha.25.
Этот последующий docs-коммит фиксирует результат, не меняя immutable release assets. Установленная приёмка PARTIAL.

Поверх опубликованной alpha.24 добавлены отдельный WPF updater, фоновая загрузка, opt-in установки при выключенном
VPN, 15-секундное уведомление, явное отключение ради обновления и одноразовый возврат того же плана. Служба владеет
сохранённым GUID maintenance; установщик подтверждает его через фиксированный установленный admin CLI до Stop.
Проверка версий/службы и результат установки отделены от загрузки/старта процесса. Настройки/подписка не переносятся.
Первый прогон: сборка без ошибок/предупреждений, WPF1/1; 8 прежних assertions статуса требовали учёта нового поля
ServiceVersion при 689/697 успешных основных тестах. Исправление ожиданий сохраняет сравнение всех сетевых полей.
Повторная полная проверка прошла:715 основных тестов и1 WPF, сборка без предупреждений/ошибок, source-size gate.
Кандидат Inno собран; новый helper опубликован отдельно в bundle/updater с manifest401 файлов. Фактический метод
чтения локального WMI boot observation дал VALID_UTC без изменения сети. Сохранённый3010 завершается только после
изменения OS boot observation, проверки12 файлов и свежей службы/Off/точного release; автоматического VPN resume нет.
Окончательная CI-проверка и опубликованные assets подтверждены выше; кандидат не выдаётся за выпуск.
Работающую службу/подключение/подписку владельца ради тестов не меняли. Alpha.24→25 использует старый установщик;
полноценная приёмка нового встроенного процесса требует установленной alpha.25→следующий выпуск. STATUS: PARTIAL.

## Подтверждённый выпуск alpha.24

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
