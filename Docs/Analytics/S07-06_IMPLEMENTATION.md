# 2A-S07-06 — основа аналитики действий игрока

Реализация подготовлена 23.10.2026 относительно `stage-2a-prereliz` / `main` @ `5f4e19b788fcae8660b5f82702d831bb5e426f7a`.
Старт рабочей сессии: 23.10.2026 14:21:44 МСК.
Плановая основа: SF_02/SF_02A/SF_04 v6.1 от 22.10.2026. SF_03/SF_06 не изменяются этим документом.

## Выбор сервиса и подключение

Владелец выбрал AppMetrica и предоставил два SDK API key: Production и Test. Ключи находятся в `Assets/_Project/Content/Configs/Game/playerAnalyticsConfig_01.asset`. Это клиентские ключи SDK, а не OAuth-токены или административные секреты.

`Packages/manifest.json` подключает официальный Unity-плагин **v6.10.0** и External Dependency Manager **1.2.187**. Для EDM указан scoped registry OpenUPM. Версия нативной Android-библиотеки в этом плагине — **8.4.1**. `Core.asmdef` ссылается на `AppMetrica`. При следующем открытии проекта Unity должен разрешить новые зависимости и обновить `Packages/packages-lock.json`; редактор и Android-сборка агентом пока не запускались.

Официальные основания:

- [Подключение Unity SDK](https://appmetrica.yandex.com/docs/en/sdk/unity/analytics/quick-start.html).
- [Настройки v6.10.0](https://github.com/appmetrica/appmetrica-unity-plugin/blob/v6.10.0/Runtime/AppMetricaConfig.cs).
- [Выгрузка событий Logs API](https://appmetrica.yandex.com/docs/en/mobile-api/logs/ref/events).

## Управление окружением

Открыть `Assets/_Project/Content/Configs/Game/debugLogConfig01.asset` и раздел **Player Analytics → Test Player Analytics**.

- Переключатель включён: используется **Test**, независимо от главного выключателя логов.
- Editor, Development Build или `DebugSettings.mobileTestMode` также всегда выбирают **Test**.
- **Production** выбирается только в обычной Android-сборке, когда выключены все перечисленные тестовые режимы.
- Если отсутствует/некорректен выбранный ключ, аналитика выключается. Подмены Test ключом Production нет.
- Выбор ключа фиксируется при старте приложения; после изменения режима требуется перезапуск. Уже накопленные SDK записи не переписываются на другое окружение.

В Unity Editor и на платформах кроме Android адаптер является локальной проверкой логики: AppMetrica SDK там не отправляет настоящие события. `AcceptedEvents` означает принятие адаптером/SDK, а не подтверждение приёма сервером. Реальную доставку подтверждает только отчёт после запуска Android.

## Настройки сбора

`playerAnalyticsConfig_01.asset` привязан к `BootstrapScene → Bootstrapper → Player Analytics Config`.

- `Collection Enabled` — разрешить сбор на уровне конфигурации.
- `Balance Version` — отдельная версия баланса; менять при изменении цен, наград и правил.
- `Max Queued Reports = 1000` — максимум записей в базе SDK, включая его служебные записи.
- `Dispatch Period Seconds = 30` — интервал отправки.
- `Session Timeout Seconds = 1800` — новая сессия после 30 минут в фоне.

В сцене настроек `SettingsController` создаёт кнопку **«Статистика игры: ВКЛ/ВЫКЛ»** и пояснение, используя стиль существующей кнопки Back. Выбор хранится в PlayerPrefs отдельно от игрового сохранения и сохраняется между новыми играми/загрузками.

При выключении новые пользовательские события не создаются, SDK получает запрет отправки и паузу сессии. При включении создаётся новый аналитический session_id. Публичный API SDK не используется для очистки его базы: без отдельного device-теста нельзя обещать удаление ранее накопленных записей или отсутствие их досылки после повторного включения. Уже принятые сервером данные настройка не удаляет.

В SDK явно выключены координаты, рекламные идентификаторы, автоматический сбор покупок, диплинков, crash/native crash и автоматическое ведение сессий. Жизненным циклом сессии управляет общий сервис. Базовые служебные данные устройства, которые формирует SDK, проверяются отдельно на Android; клиентские параметры событий не содержат имён/полных сохранений/сообщений ошибок/пользовательского ввода.

## Схема событий T01/T03

Общие поля: `schema_version`, `event_id`, случайный `installation_id`, аналитический `session_id`, UTC-время возникновения, версия приложения и номер Android-сборки, версия баланса, окружение, платформа, действие, результат, причина, ID игрового контента/системы/планеты, число открытых секторов, уровень игрока, наличие игрового состояния, кредиты/алмазы/топливо до и после, изменение кредитов, количество и стоимость.

`installation_id` хранится в PlayerPrefs независимо от сейва; это случайный GUID, а не `PlayerId`/device/ad ID. Очистка данных приложения сбросит этот ID; поведение восстановления системной резервной копии требует отдельной проверки. Новая игра и загрузка не создают новую аналитическую сессию. Новый запуск приложения и возврат после указанного интервала создают.

Строковые параметры допускают только короткие коды/ID из букв ASCII, цифр, `_`, `-`, `.`. Пробелы, URL, email и длинные значения заменяются `invalid_code`. Контракт закрыт типом `PlayerAnalyticsRecord`: произвольный словарь параметров не принимается.

| Событие | Когда записывается | Результат и причины |
|---|---|---|
| `sf_session_started` | Старт/новая аналитическая сессия | success |
| `sf_new_game` | Успешное открытие нового игрового состояния | success |
| `sf_continue_game` | Загрузка состояния либо конкретный отказ загрузки | success / NoSave / LoadFailed |
| `sf_travel_result` | `TravelFinishedEvent`; снимок до берётся на `TravelStartedEvent` | фактические Success / TravelFailReason |
| `sf_interaction_result` | Результат реального взаимодействия | Success / InteractionFailReason2A |
| `sf_buy_item`, `sf_sell_item` | Возврат операции в EconomyService или MarketTransactionService | соответствующий enum результата; реальная разница кредитов |
| `sf_refuel` | Возврат Refuel/RefuelToFull; вложенный вызов учитывается один раз | RefuelResultType |
| `sf_repair` | Возврат RepairToFull | RepairResultType |
| `sf_mission_accept` | Возврат AcceptMission | MissionNotFound / PirateRuleNotFound |
| `sf_mission_complete` | Возврат CompleteMission | MissionNotFound / NotReadyToTurnIn |
| `sf_mission_fail` | Возврат FailMission | MissionNotFound |
| `sf_mission_reward` | Отдельная фактическая попытка выдачи награды миссии | MissingMission / MissingPlayer / MissingReward / AlreadyGranted |
| `sf_government_reward` | Возврат ClaimReward со снимками действительных денег | NoActiveEncounter / RewardUnavailable / EncounterResolveFailed |
| `sf_combat_started`, `sf_combat_victory`, `sf_combat_defeat` | Соответствующее каноническое событие encounter | фактический результат / SystemEncounterDefeatReason |
| `sf_player_destroyed` | PlayerShipDestroyedByNpcEvent | DestroyedByNpc |

Успешные действия имеют `reason=None`. Исключение внутри наблюдаемой операции отражается `failure/UnexpectedException` и повторно выбрасывается исходному коду; аналитика не меняет контракт операции. Ошибки самой аналитики изолированы.

Проверки доступности `Can...`, кадры, тики и перемещения NPC не создают попыток игрока. Событие завершения миссии не заменяет событие выдачи награды. Для суммы экономики используются только экономические события, чтобы не складывать один доход ещё раз по контексту завершения/взаимодействия.

## Доставка и дубли T02/T04

Единственный владелец долговременной очереди и сетевых повторов — нативный AppMetrica SDK. Игра вызывает ReportEvent один раз на результат. Обёртка операции защищена от двойного Complete; RefuelToFull не записывает вложенный Refuel повторно. Собственной второй очереди поверх SDK нет.

Ограничение базы — 1000 записей; при переполнении SDK удаляет часть записей согласно своему контракту. Управляемого TTL 7 дней и лимита 1 МиБ эта реализация не обещает: они были предложениями в первоначальном плане, а не принятым требованием SF_02A.

Каждому факту присваивается event_id. SDK не предоставляет нашей C#-обёртке подтверждение доставки отдельной записи. Поэтому один event_id не является доказательством exactly-once на сервере.

Для проверки и итогового подсчёта добавлен `Tools/Analytics/build_player_analytics_report.py`. Он читает JSON/CSV Logs API (`event_name,event_json`), исключает повторы по `(environment, installation_id, event_id)`, сообщает о конфликтующих повторах и некорректных строках, строит HTML/JSON с фильтрами окружения, версии приложения и баланса. Сырые installation_id в итоговый HTML не попадают. Скрипт не обращается к сети и не требует токенов.

Пример после получения настоящей выгрузки:

```sh
python3 Tools/Analytics/build_player_analytics_report.py events.json --environment test --output analytics-test-report
```

Обычные счётчики в кабинете AppMetrica остаются показателями сервиса; отсутствие повторов там нельзя объявлять доказанным только по наличию event_id. При исследовании различать время возникновения и время поступления. Последнее событие не доказывает причину ухода игрока.

## Проверки T05 и текущие ограничения

До указания владельца «больше не запускать компиляцию» выполнена отдельная проверка C# сборок AppMetrica/Data/Core/Presentation для Editor и Android с фактическими исходниками SDK. Это проверка компилятором, не Unity Test Runner и не Android build. После неё добавлены безопасное ограничение стоимости в обёртке EconomyService и поиск UI только в текущей сцене настроек; повторная компиляция по указанию владельца не запускается.

Проверки чистого управляемого ядра: **27/27 PASSED** через отдельный .NET/NUnit-прогон. Покрыты Test/Production, отсутствие fallback, отключение сбора, ID и сессии, экономические снимки, закрытая схема, исключение свободного текста, повторный Complete и отказы SDK. Эти же тесты включены в Unity assembly `Sprint7Analytics`.

Дополнительно подготовлены Unity integration tests: реальные Refuel/RefuelToFull, отсутствие событий от CanRefuel, причины отказов, Travel EventBus и отписка, JsonUtility, конфигурация сборщиков SDK и test-переключатель при выключенных логах. Они пока **не запускались**.

Python-проверки отчёта выполнялись на синтетических данных. Это не события игроков и не доказательство доставки.

Не выполнено: разрешение новых зависимостей в Unity, Unity Test Runner, подписанная Android-сборка/устройство, реальная доставка и отчёт, авиарежим/перезапуск/переполнение SDK, фактический сетевой состав данных, пользовательская проверка меню настроек и performance/soak.

Известный пробел существующего кода: Bootstrapper регистрирует DebugGovernmentRewardPayoutService; тот только пишет лог. Аналитика записывает реальную разницу денег (в таком случае ноль), не подменяя её Credits из GovernmentRewardClaimedEvent. Исправление выплат и жизненного цикла миссий не включено скрытно в этот подэтап.

## Маршрут Android-проверки владельца

1. Оставить Test Player Analytics включённым; собрать/установить Android версию.
2. Проверить старт сессии, покупку/продажу, заправку, ремонт, переход и отказ с известной причиной. Сверить параметры с наблюдаемой игрой.
3. Повторить несколько действий в авиарежиме, закрыть/открыть приложение, вернуть интернет и сверить event_id в выгрузке.
4. Повторить после короткого/долгого ухода в фон; установка и версия баланса сохраняют смысл.
5. В настройках выключить сбор, выполнить действия и перезапустить игру; подтвердить отсутствие новых пользовательских событий.
6. Проверить данные SDK и поведение ранее накопленной очереди при повторном включении.
7. Проверить release без Development/MobileTest/test-переключателя: события должны поступать в Production.
8. Зафиксировать SHA, сборку, устройство и отчёт отдельно от пользовательской приёмки. До этого production-статус подэтапа не объявлять VERIFIED_WORKING/CANONICAL_COMPLETE.

## Дополнение к SF_07: фактические точки реализации

- `Core/Services/Analytics/PlayerAnalyticsService.cs`: общая схема/сессии/изоляция ошибок, зависимости передаются явно.
- `Core/Services/Analytics/AppMetricaPlayerAnalyticsTransport.cs`: SDK и лимиты его очереди.
- `Core/Services/Analytics/UnityPlayerAnalyticsPreferences.cs`: ID установки и выбор сбора вне сейва.
- `Core/Services/Analytics/PlayerAnalyticsUnityFactory.cs`: окружение, версия Android-пакета и игровой снимок.
- `Core/Services/Analytics/PlayerAnalyticsSafe.cs`, `PlayerAnalyticsOperation.cs`: безопасное наблюдение операций.
- `Core/Services/Analytics/PlayerAnalyticsEventBridge.cs`: подписки EventBus и освобождение подписок.
- `Data/Analytics/PlayerAnalyticsConfig.cs`, `PlayerAnalyticsRecord.cs`: настройки и закрытая схема.
- `Bootstrapper.InitializePlayerAnalytics()`: вызов после InitializeServices, регистрация IPlayerAnalytics и bridge; pause/quit/destroy передают lifecycle.
- `Presentation/Settings/SettingsController.cs`: доступный пользователю переключатель.
- `Assets/_Project/Tests/EditMode/Sprint7Analytics`: тестовая assembly.

Пути Core/Data/Presentation выше относительны `Assets/_Project/Scripts/`. Это дополнение описывает локальные изменения, ещё не отдельный GitHub SHA и не обновление канонических статусов.
