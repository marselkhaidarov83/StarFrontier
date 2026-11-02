# SF_07 — локальная реализация основы жизненного цикла станций

Дата: 02.11.2026. Подэтап: **2A-S07-01**. Локальный проект: `/Users/marselhaidarov/StarFrontier`. Ветка: `stage-2a-prereliz`. Базовый HEAD: `473db1c55a4e6392996a29d85d44fe464d97bfbe`, совпадение с GitHub проверено перед работой. Main: `5f4e19b788fcae8660b5f82702d831bb5e426f7a`. Изменения находятся в рабочем дереве; commit/push не выполнялись.

Основание — согласованная в текущем чате концепция станций и поручение внести S07-01, отключаемые логи и исходники автотестов в локальный код. По последующему прямому указанию владельца **компиляция, тесты и приложение не запускались**. Сведения ниже подтверждены чтением и изменением локальных файлов, а не запуском.

## Точные файлы и точки подключения

Пути относительно корня Unity-проекта.

| Файл | Назначение / точка |
|---|---|
| `Assets/_Project/Scripts/Data/Configs/StationConfig.cs` | `initialLevel`, `operationInteractionDistance` (исходно 300), `constructionPaymentOptions`, `upgradeSettings`, `recoveryPaymentOptions`; `TryCreateOperation()` создаёт независимый снимок выбранного варианта. |
| `Assets/_Project/Scripts/Data/Configs/StationOperationSettings.cs` | Сериализуемые варианты ресурсов/времени, настройки переходов уровней, создание снимка оплаты. Для астралитов указывается конкретный ItemConfig. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/StationRuntimeState.cs` | Уровень, сохранённый тип, ConfigId, разрушение, Revision; отдельные `Development` и `Recovery` с ID, стоимостью, длительностью, прогрессом и причиной паузы. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/StationLifecycleRules.cs` | Чистые переходы `Create`, `BeginPaidOperation`, `Destroy`, `ApplySystemStatus`, `Advance`, нормализация и проверка целостности. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/StarSystemRuntimeState.cs` | Единственный вложенный `Station`, маркер `StationStateVersion`; переходы войны вызывают правила станции, в том числе прямой путь `MarkCaptured()`. Восстановление защищает от инфраструктурного урона. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/GalaxyRuntimeStateFactory.cs` | `CreateNewGalaxyRuntimeState()` создаёт станцию из ассета, проверяет пустые/повторные SystemId и ID назначенной станции. |
| `Assets/_Project/Scripts/Core/Services/Station/IStationService.cs` | API состояния/операций, согласования и времени; граница оплаты `IStationPaymentGateway`. |
| `Assets/_Project/Scripts/Core/Services/Station/StationService.cs` | Координация через session/config/event bus без сцены; конструктор с явными зависимостями для тестов. `TryStartOperation()` требует ожидаемые уровень/Revision до оплаты, защищает от повторных устаревших кликов. |
| `Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs` | `RegisterServices()` регистрирует `IStationService → StationService` после `IConfigService` и перед `IShipStatsService`. |
| `Assets/_Project/Scripts/Core/Services/Session/GameSessionService.cs` | `StartNewSession()` / `LoadSession()` вызывают `InitializeStations(newGame)` после присвоения State. |
| `Assets/_Project/Scripts/Core/Services/Time/GameTimeService.cs` | `TickAllServices()` вызывает `AdvanceTime(deltaTime)` после обновления боя/популяции/планировщика вторжений. На паузе игрового времени этот путь не выполняется. |
| `Assets/_Project/Scripts/Core/Services/Galaxy/SystemSecurityService.cs` | `SetSystemStatus`, `CaptureSystem`, `LiberateSystemByPlayer`, `ApplyInfrastructureDamageFromWar` уведомляют сервис; попытка урона во время восстановления отклоняется. |
| `Assets/_Project/Scripts/Data/Runtime/Game/OfflineProgressWindow.cs` | Общая функция ограниченного офлайн-интервала, максимум 12 ч. |
| `Assets/_Project/Scripts/Core/Services/Game/ContinueGameService.cs` | Обе точки входа вызывают `ProcessOfflineStations()` после war catch-up, до NPC relocation, которое переписывает LastSaveUtc. |
| `Assets/_Project/Scripts/Core/Services/Npc/SystemNpcOfflineRelocationService.cs` | `CalculateOfflineHours()` использует тот же общий ограничитель. Остальная логика перемещения NPC не заменена. |
| `Assets/_Project/Scripts/Data/Runtime/Game/GameRuntimeMetaState.cs` | `StationOfflineProcessedUtc` предотвращает повторное применение того же офлайн-интервала. |
| `Assets/_Project/Scripts/Data/Save/SaveDataVersions.cs` | `StationLifecycle = 10`, `Current = StationLifecycle`. |
| `Assets/_Project/Scripts/Data/Save/SaveMigrationService.cs` | `Migrate()` нормализует присутствующие состояния станции. Отсутствующая legacy-станция связывается с конфигом на открытии сессии. |
| `Assets/_Project/Scripts/Core/Services/Save/Pipeline/SaveValidationStage.cs` | `ValidateAndNormalize()` добавляет ошибки дубликатов и некорректных станционных операций. |
| `Assets/_Project/Scripts/Core/Services/Save/Pipeline/SaveIntegrityStage.cs` | `VerifySerializedJson()` проверяет исходный JSON legacy v9 до миграции новых полей; изменённый payload по-прежнему отклоняется. |
| `Assets/_Project/Scripts/Data/Events/Station/StationStateChangedEvent.cs` | Независимые снимки Previous/Current; наблюдатели не изменяют живое состояние или снимки друг друга. |
| `Assets/_Project/Scripts/Data/Events/SimpleEventBus.cs` | Новый `PublishSafely()` изолирует исключения подписчиков; обычный `Publish()` сохраняет прежнее поведение. |
| `Assets/_Project/Scripts/Data/Configs/Debug/DebugLogConfig.cs` | Добавлен канал `StationLifecycle` в конец enum, без сдвига старых значений; флаг `stationLifecycleLogs` и общий master gate. |
| `Assets/_Project/Content/Configs/Game/debugLogConfig01.asset` | `stationLifecycleLogs: 0`, по умолчанию выключено. |

Вложенное состояние станции попадает в существующий `SaveGalaxyPartData.Galaxy` в `Assets/_Project/Scripts/Data/Save/SaveMultiFileData.cs`. Отдельный файл сохранения или ручное копирование состояния станции не добавлялись.

## Правила совместимости и сохранности операций

- Новая игра использует `StationConfig.InitialLevel`. Повторная загрузка использует сохранённый уровень, включая осознанный 0.
- Legacy-сохранение без станционных полей: если назначен конфиг станции, используется **fallback уровня 1**, чтобы не превращать прежнюю существовавшую станцию в новую стройку. Это политика миграции реализации, не изменение начальных уровней новой игры. Разрушение также учитывает сохранённую инфраструктуру.
- Если legacy-конфиг отсутствует, слот остаётся без придуманной станции и с незавершённым маркером привязки; после возвращения конфига можно связать его при следующей инициализации сессии.
- Если конфиг уже сохранённой станции удалён или заменён, её ID/тип/уровень и принятые оплаты/длительности сохраняются. Новые операции отклоняются как `StationConfigMissing`; уже принятые продолжаются по снимку.
- Строительство/улучшение и восстановление хранят независимые операции. Захват отменяет только восстановление. Его оплата не возвращается. Возобновление прерванной операции не оплачивается повторно.
- После завершения восстановления оставшаяся часть того же интервала времени применяется к прерванной операции, без удвоения времени. Восстановление уровня 0 не выдаёт уровень 1 до завершения строительства.
- Пауза восстановления начинается при `Invasion`, не при прогнозе `Threat`; после отражения снимается автоматически.
- Неопределённые/нечисловые таймеры отклоняются валидацией, а не превращаются в бесплатное завершение. Допустимые конечные значения нормализуются; неизвестная будущая схема не принимается.

## Граница S07-01

Сейчас реализованы модель, правила переходов, согласование с войной, состояние/события/миграция и подключение времени. Схема платёжных вариантов редактируется в StationConfig; продуктивные цены и полные таблицы не назначались.

**Реальное списание кошелька/инвентаря относится к S07-03.** `IStationPaymentGateway` — точка подключения атомарной оплаты. В текущем Bootstrapper такой gateway не регистрируется; при его отсутствии попытка запуска новой операции через сервис возвращает `PaymentUnavailable`, а не бесплатную постройку. Нижнеуровневый `BeginPaidOperation` предназначен только для уже принятой оплаты; будущие UI и игровые команды должны обращаться к сервису. Подключение gateway не является выполненным результатом этого обновления.

Эффекты пяти типов относятся к S07-02, полные игровые оплаты и восстановление — к S07-03, маршрут/окна/посадка, заправка/рынок/ремонт, тусклый вид и вылет при разрушении — к S07-04. Эти UI и услуги данным изменением не объявляются реализованными. Станционные миссии остаются после пререлиза. Автоматические сценарии используют тестовый gateway, который не является игровой оплатой.

## Исходники автотестов

Сборка: `Assets/_Project/Tests/EditMode/Sprint7Stations/Sprint7Stations.asmdef`, Editor + UNITY_INCLUDE_TESTS, ссылки Core/Data и Unity test runners, NUnit.

- `StationLifecycleRulesEditModeTests.cs`: уровни, операции, время, разрушение/восстановление, военные паузы, захват, дубликаты и испорченные данные.
- `StationConfigAndPersistenceEditModeTests.cs`: варианты ассета/времени, снимки оплаты, сериализация, миграция, log gates и общий офлайн-лимит.
- `StationWorldFactoryEditModeTests.cs`: отклонение пустых ID/повторных владельцев и применение уровней конкретных станций без нарушения скрытости систем.
- `StationServiceEditModeTests.cs`: gateway-граница, мгновенность и повторные команды, устаревший клик, отсутствие/замена конфига, session binding, офлайн, изоляция наблюдателей и доставка событий при исключении подписчика.

52 тестовых метода, с параметризованными вариантами 61 кейс. Это количество исходников, **не результаты запуска**. Компиляция, запуск тестов, Play Mode, UA, Android и performance отсутствуют по прямому указанию владельца. Формальные SF_03/SF_06 не повышены.

## Где включить диагностику после разрешённого пользователем запуска

В Project выбрать `Assets/_Project/Content/Configs/Game/debugLogConfig01.asset`. В Inspector: **Station Lifecycle → Station Lifecycle Logs**. Для вывода также должен быть включён **Master → Logs Enabled**. По умолчанию станционный флаг выключен. Сообщения идут через AppLog с каналом StationLifecycle и префиксом `[StationLifecycle]`; прогресс каждого кадра в Console не печатается.

Новые GUID сохранены в `.meta`. Уже существовавшее пользовательское изменение `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` не относится к этой работе и не перезаписывается.


## Исправление JSON загрузки после сообщения владельца

Владелец сообщил падение `Offline_UsesSameCappedWindowForAllStations_AndDoesNotReplayAfterLoad` в `InitializeSession(false)`: `Invalid station operation clock/identity: first`. Это подтверждение одного неуспешного запуска владельцем, не результат повторного запуска агентом.

Добавлена `StationLifecycleRules.NormalizeSerializedPlaceholders()`: пустые inline-объекты отсутствующих операций и пустой станции восстанавливаются в null. Удаляются только полностью пустые операции с Kind=None, без ID, ресурсов, времени и прогресса. Настоящая операция с повреждённым ID/таймером сохраняется и по-прежнему отклоняется валидацией. `InitializeSession()` очищает представление отсутствующих значений до первого ValidateWorld; обычная нормализация/миграция использует ту же функцию.

Добавлены четыре регрессионных исходника: JSON только с развитием, явные пустые операции, повреждённая настоящая операция и пустой слот станции. Исходный офлайн-тест и его проверки не ослаблялись. Особенность inline-null описана в официальных правилах Unity: https://docs.unity.com/en-us/engine/6000.3/manual/scripting/compilation-and-code-reload/script-serialization/rules . Компиляция, тесты и приложение после исправления агентом не запускались; прохождение ожидает подтверждения запуском.


## Подготовка данных прежних тестов скрытости систем

Владелец сообщил падение `NewGame_ShouldNotAutoDiscoverSystemMarkedHiddenAtStart` до проверки скрытости из-за пустого SystemId. Причина: `S02_HiddenSystemDiscoveryContract_RED_Test` создавал StarSystemConfig без ID; новая проверка идентичности мира отклоняет такой конфиг.

В `Assets/_Project/Tests/EditMode/Sprint2Verification/Pending_ContractDecision/S02_HiddenSystemDiscoveryContract_RED_Test.cs` назначены ID тестовой галактики, секторов и уникальные ID скрытых/видимых систем в обоих сценариях. Ожидаемая скрытость и все прежние проверки сохранены. Производственная фабрика и запрет пустых/повторных ID не изменены.

Добавлен `StationWorldFactoryEditModeTests.cs`: три метода (пять кейсов с параметрами) на пустую идентичность, повторный SystemId между секторами и начальные уровни конкретных станций при скрытых системах. Компиляция, тесты и приложение агентом не запускались; прохождение ожидает подтверждения запуском.
