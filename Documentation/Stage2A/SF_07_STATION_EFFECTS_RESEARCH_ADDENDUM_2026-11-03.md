# SF_07 — локальная реализация S07-02 и S07-03

Дата: 03.11.2026. Проект `/Users/marselhaidarov/StarFrontier`, ветка `stage-2a-prereliz`, исходный HEAD `a327f964f57a0fcc0186b34388ed10f341a7234c`. Основание — решения владельца в чате «2A-S07-02» от 02–03.11.2026 и прямое поручение внести локальный код, отключаемые логи и исходники автотестов. Коммит и push агентом не выполняются.

**Компиляция, автотесты и приложение не запускались по прямому указанию владельца.** Ниже описано наличие исходников/ассетов и их связи, а не результат исполнения. Production-статусы SF_03 и QA-результаты SF_06 не повышаются.

## Реализация и точки подключения

Все пути ниже — относительно корня Unity-проекта.

| Путь | Назначение |
|---|---|
| `Assets/_Project/Scripts/Data/Configs/StationLevelEffectsConfig.cs` | Таблица уровней 1–10: Population, RangerAttackProbability, ScienceRate, кривая EfficiencyByDamage, FullRepairSeconds, RequiredGalaxyLevel, мгновенные варианты RepairPaymentOptions/RecoveryPaymentOptions. UnopposedInvasionDamagePerTick — предварительный урон незащищённой инфраструктуре за шаг вторжения. |
| `Assets/_Project/Scripts/Data/Configs/StationInstantPaymentOption.cs` | Вариант оплаты ремонта/восстановления без поля времени. |
| `Assets/_Project/Scripts/Data/Configs/StationConfig.cs` | Ссылка LevelEffects, прежние цены строительства/улучшений; восстановление использует варианты текущего уровня. Старая таблица восстановления скрыта и сохранена для совместимости. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/StationInfrastructureRules.cs` | Саморемонт, эффективность, пропорциональная стоимость ремонта и условия захвата. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/StationPaymentRules.cs` | Проверка всего набора и подготовка изменений до одновременного списания кредитов, алмазов и обычного груза. Миссионный груз не расходуется. Доли стоимости округляются вверх по каждому ресурсу. |
| `Assets/_Project/Scripts/Core/Services/Station/StationResourcePaymentGateway.cs` | Подключённая реализация IStationPaymentGateway к кошельку и грузу текущего игрока. |
| `Assets/_Project/Scripts/Core/Services/Station/StationService.cs` | TryStartOperation: оплата/запуск, мгновенные Repair/Recovery, проверка уровня галактики и Revision. ProcessOffline: общий ограниченный интервал для станции, ремонта, военного населения и науки; отдельного офлайн-бюджета исследования нет. |
| `Assets/_Project/Scripts/Core/Services/Station/StationInfrastructureService.cs` | Эффекты из текущего состояния, население военной базы, ремонт без врагов, вероятность рейнджеров, научная скорость, ограничение типов баз в секторе, случайное размещение военных базы после загрузки. |
| `Assets/_Project/Scripts/Data/Runtime/Galaxy/StationMilitaryRules.cs` | Источник базы кодируется в SpawnRuleId (`station-military|system|config|rule`), который уже сохраняется обычным/порционным компактным NPC-save. Дополнительный счётчик кораблей не создаётся. Допустимы только системы исходного сектора вне Captured. |
| `Assets/_Project/Scripts/Core/Services/Npc/SystemNpcBehaviorService.cs` | В существующем выборе поведения ranger-вероятность подставляется для EngageEnemies после проверок цели/матрицы. Для военных базы добавлен секторный переход. TravelWithinSector в ассете переиспользует реализацию TravelToAnotherSystem с обязательной фильтрацией сектора. |
| `Assets/_Project/Scripts/Core/Services/Npc/SystemNpcMovementService.cs` | Фильтр станции/сектора на выборе и повторно перед прибытием. |
| `Assets/_Project/Scripts/Core/Services/Npc/SystemNpcSimulationSaveService.cs` | Восстановление военных базы в системах без планет и вызов нового перераспределения после восстановления компактного save. |
| `Assets/_Project/Scripts/Core/Services/Npc/SystemNpcOfflineRelocationService.cs` | Старое размещение military в рамках системы не переопределяет новое размещение военных базы. |
| `Assets/_Project/Scripts/Data/Configs/Research/ResearchTreeConfig.cs` / `ResearchBranchConfig.cs` | Корень веток, узлы, зависимости только внутри ветки, 10 уровней и варианты ресурсов/времени. |
| `Assets/_Project/Scripts/Data/Runtime/Research/ResearchRuntimeState.cs` / `ResearchRules.cs` | Стабильный enum, отдельно уровень 0–10, единственное активное исследование с принятой оплатой/временем; проверка циклов, дубликатов, зависимостей и повреждённого прогресса. Реакции на достижения не реализуются. |
| `Assets/_Project/Scripts/Core/Services/Research/ResearchService.cs` | TryEnterStation/TryStart, единый снимок, ход науки, пауза при нулевой скорости, завершение и событие. |
| `Assets/_Project/Scripts/Data/Events/Station/ResearchCompletedEvent.cs` | Enum улучшения и новый уровень; состояние фиксируется до рассылки. |
| `Assets/_Project/Scripts/Presentation/Station/ScienceStationResearchWindow2A.cs` | Окно общего исследования/уровней/оплаты у научной станции; при выборе поблизости входит в станционный контекст. Подключается StationSelectableView2A.Initialize только научным станциям. Полная посадка/услуги и общий UI станции остаются S07-04. |
| `Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs` | Gateway до StationService; инфраструктура после NPC population; ResearchService после инфраструктуры. Поле researchTree. |
| `Assets/_Project/Scenes/BootstrapScene.unity` | researchTree назначен на новый ассет дерева. |
| `Assets/_Project/Scripts/Core/Services/Time/GameTimeService.cs` | TickAllServices обновляет исследования, станции и инфраструктуру. |
| `Assets/_Project/Scripts/Core/Services/Game/ContinueGameService.cs` | Общий офлайн после доступности восстановленных NPC, до старого relocation, меняющего LastSaveUtc. |
| `Assets/_Project/Scripts/Core/Services/Galaxy/SystemSecurityService.cs`, `SystemStatusService.cs`, `Enemy/InvasionService.cs` | Захват запрещён при целой станции или живом military. На обычном шаге вторжения инфраструктура получает настроенный урон лишь когда прежние правила подтверждают отсутствие защитников и игрока; иначе новый запрет мог блокировать захват навсегда. |
| `Assets/_Project/Scripts/Data/Save/SaveDataVersions.cs` / `SaveMigrationService.cs` | Версия 11, наука внутри Galaxy (существующий Galaxy multipart-save), нормализация и старые Captured-системы с разрушенной станцией. |
| `Assets/_Project/Scripts/Core/Services/Save/Pipeline/SaveIntegrityStage.cs` / `SaveValidationStage.cs` | Проверка исходного JSON старой схемы до миграции, проверка прогресса науки/ремонта. |

## Ассеты и предварительные значения

Каталог `Assets/_Project/Content/Configs/Stations2A/`:

- `Levels/station_levels_military.asset`, `station_levels_ranger_base.asset`, `station_levels_science.asset`, `station_levels_trade.asset`, `station_levels_medical.asset`: десять настроек каждого типа. Ссылки назначены 50 существующим StationConfig в `Content/Configs/Galaxy/Stations/`.
- `Population/station_population_L01.asset` … `L10.asset` и соответствующие `station_military_spawn_L01.asset` … `L10.asset`: только military, состав корабля по уровню галактики, число по уровню базы.
- `Research/research_tree_stage2a.asset`: корень двух примерных веток «Военное превосходство»/«Торговое превосходство», по одному примерному узлу с десятью уровнями. Это стартовое редактируемое наполнение, не окончательная матрица науки. Зависимости поддерживаются схемой и исходниками тестов; дальнейшее содержимое задаётся владельцем.

Предварительные значения: число military 1–10 на базу; интервал 170–80 сек.; ranger-вероятность 10–100%; научный вклад 1–10; полное самовосстановление 600×уровень сек.; ослабление линейно по повреждению. Цены настраиваются вариантами кредитов + астралитов AI либо алмазов. Ремонт и восстановление без времени. Поле InitialLevel существующих станций и уже сохранённые уровни не повышаются автоматически.

## Как редактировать и включить логи

В Unity Project открыть `Assets/_Project/Content/Configs/Game/debugLogConfig01.asset`. В Inspector: включить `Master → Logs Enabled`, затем `Station Effects / Research → Station Effects Logs` и/или `Research Logs`. Оба новых флага исходно выключены; существующий `Station Lifecycle Logs` продолжает управлять жизненным циклом/отказами станционных команд. Новые каналы добавлены в конец enum без перенумерации прежних.

Для уровня станции открыть её StationConfig и перейти по Level Effects к соответствующему профилю. Population/Science Rate/Ranger Attack Probability и кривая повреждения редактируются в записи уровня. Repair/Recovery Payment Options не имеют времени. Полное время саморемонта задаётся Full Repair Seconds. Строительство и улучшения продолжают использовать Construction Payment Options / Upgrade Settings самого StationConfig.

Дерево редактируется от `Research/research_tree_stage2a.asset` через Branches → Nodes → Dependencies/Levels. Добавляя новые значения ResearchUpgrade, сохранять старые численные значения enum. До отдельной реализации потребителей изменение научного enum не меняет боевые или торговые характеристики.

## Исходники автотестов — без запуска

Единая сборка `Assets/_Project/Tests/EditMode/Sprint7Stations/Sprint7Verification.asmdef` включает станционные тесты, три новых файла ниже и тесты аналитики Sprint 7. Тесты аналитики и их .meta перемещены непосредственно в `Sprint7Stations`; отдельные Analytics asmdef/asmref удалены. Зависимость AppMetrica сохранена. Результирующее имя — `Sprint7Verification.dll` после компиляции Unity (агент её не запускал).

- `StationInfrastructureAndPaymentsEditModeTests.cs`: пропорциональный саморемонт, пауза врагами, дробный прогресс, разрушение, эффективность, захват, атомарные ресурсы/миссионный груз, ремонтный quote, происхождение/сектор military и log gates.
- `ResearchTreeAndServiceEditModeTests.cs`: десять уровней, зависимости/циклы, сохранение, повреждённые данные, пауза/скорость, snapshot isolation, однократная оплата/событие и общий офлайн-лимит без повторной обработки.
- `StationPaidOperationsEditModeTests.cs`: реальная оплата улучшения, нехватка ресурсов, мгновенный ремонт/восстановление, продолжение улучшения, Revision и загрузка без повторной оплаты.

Прежний тест длительного восстановления в StationServiceEditModeTests сохранён как явно обозначенная совместимость старого оплаченного snapshot. Новые команды восстановления проверяются на мгновенность. Наличие этих исходников не означает PASSED.

## Неподтверждённое

Компиляция, выполнение тестов, Play Mode/UA, Android, performance/soak и интеграция в main не подтверждены этим обновлением. Графика и окончательные торговые/медицинские/научные реакции не добавлялись. Перед повышением production-статуса нужны разрешённые владельцем запуски и точные результаты.

## Исправление по сообщению владельца о падении теста

`LegacyMigration_LeavesAbsentStationForConfigAwareSessionBinding` ожидал прежний конечный формат StationLifecycle (10). Миграция намеренно переводит данные в актуальный формат StationEffectsAndResearch (11). Ожидание исправлено на SaveDataVersions.Current; исходная версия старого сохранения, проверка отсутствующей станции и нулевой StationStateVersion сохранены. Владелец сообщил о падении; повторный запуск агентом не выполнялся. Все тесты Sprint 7 настроены на одну сборку Sprint7Verification, отдельные Stations/Analytics asmdef заменены общей сборкой и ссылкой.

## Устранение конфликта определений сборок

После сообщения владельца о конфликте Sprint7Analytics.asmdef/asmref проверено фактическое содержимое: на диске оставался только asmref. Чтобы исключить неоднозначность импорта определения/ссылки с прежним GUID, PlayerAnalyticsContractTests.cs и PlayerAnalyticsIntegrationTests.cs вместе с их .meta перенесены в Sprint7Stations. В Sprint7Analytics нет определений или ссылок сборки. Все девять файлов тестов Sprint 7 принадлежат одному Sprint7Verification.asmdef непосредственно. Компиляция, тесты и принудительный Unity refresh агентом не запускались.


## Абсолютное здоровье станции — 05.11.2026

По прямому решению владельца здоровье станции хранится в единицах: уровни 1–10 имеют максимумы 400/450/500/550/600/650/700/750/800/850. Во всех пяти ассетах `Assets/_Project/Content/Configs/Stations2A/Levels/station_levels_*.asset` назначено поле `MaxHealth` для каждого уровня. Нулевое поле настройки использует формулу по уровню, позволяя читать старые ассеты.

`StationRuntimeState` содержит Health/MaxHealth/HealthVersion/HealthLevel. После миграции Health является источником истины. `StationInfrastructureRules.EnsureHealth` переносит старую долю повреждений, не выдавая полное здоровье повреждённой/разрушенной станции; `UpdateDamage` обновляет старое поле InfrastructureDamage как округлённый процент для UI (1–99 при живой повреждённой станции). Эффективность и стоимость ремонта вычисляются из точной доли недостающего здоровья. Старый дробный прогресс ремонта преобразуется в единицы HP. Сохранение использует существующий вложенный JSON станции; отсутствующие поля определяются HealthVersion=0. Пользовательские сохранения на диске не изменялись.

`StarSystemRuntimeState.ApplyInfrastructureDamageFromWar` снимает абсолютные единицы здоровья станции и разрушает её при нуле. `UnopposedInvasionDamagePerTick` теперь задаёт HP за шаг, предварительное значение 10 оставлено. Старый путь процентного урона для систем без станции сохранён. Debug-команда с forceDestroyed продолжает уничтожать станцию независимо от MaxHealth.

`StationLifecycleRules.Advance` и `StationService` применяют максимум целевого уровня при завершении улучшения: сохраняется число недостающих единиц, прирост максимума прибавляется к текущему здоровью. Строительство и восстановление дают полный запас текущего уровня. Платный ремонт полностью восполняет здоровье. Саморемонт восстанавливает MaxHealth / FullRepairSeconds HP в секунду, накапливает дробную часть и при врагах не продвигается; офлайн-граница ремонта переведена на 1 HP.

Логи Health=N/M проходят через существующий StationLifecycle/debugLogConfig. В общую Sprint7Verification добавлены исходники проверок уровней, абсолютного урона и разрушения при нуле, миграции и повторной загрузки, настраиваемого MaxHealth, улучшения, восстановления, дробного ремонта и паузы при врагах. Прежние сценарии оплаты 30% ремонта скорректированы на 135 HP для уровня 2. Код и ассеты проверены статически; компиляция, автотесты, Unity/Play Mode не запускались. Статус: IMPLEMENTED_UNVERIFIED; GitHub/интеграция не подтверждались этой правкой.


## Саморемонт в единицах здоровья за игровой тик — 05.11.2026

По новому решению владельца прежняя настройка FullRepairSeconds заменена на HealthPerTick в StationLevelEffectsSettings и всех 50 записях пяти station_levels_*.asset. Предварительные значения: военная 2×уровень, рейнджерская/торговая/научная 1×уровень, медицинская 3×уровень HP за завершённый игровой тик. Это редактируемые значения, не окончательный баланс S09-10. 0 отключает саморемонт.

Подтверждено чтением исходников: прежний AutoRepair вызывался из AdvanceTime(deltaTime) каждый кадр, а не на границе GameTime quantum. С ростом уровня FullRepairSeconds росло быстрее MaxHealth, поэтому абсолютная скорость снижалась. При разборе учитывается различие кадра и завершённого игрового тика (GameTimeState.SecondsPerDay); наблюдаемая владельцем неисправность в Play Mode агентом не воспроизводилась.

Теперь GameTimeService.CompleteCurrentTickIfNeeded вызывает IStationInfrastructureService.CompleteTick после событий quantum/day и всех кадровых обновлений боя/передвижения. AdvanceTime больше не лечит станцию. GalaxyRuntimeState.StationLastRepairTick защищает от повторного начисления в том же тике, включая повтор после загрузки. AutoRepair добавляет HealthPerTick, ограничивает Health максимумом, обновляет процент и Revision; расчёт не использует округлённый процент повреждений как количество лечения. Саморемонт запрещён для разрушенной/нулевой/отключённой/захваченной станции, при отсутствии конфигурации, Recovery и живых вражеских NPC/пиратов в системе. Пропущенные при врагах тики не накапливают право на последующее лечение. При 100% повреждениях требуется платное восстановление игроком.

StationService.ProcessOffline разделяет время по ближайшей границе полного ремонтного тика и вызывает AdvanceOfflineRepair после операций/населения. Неполный тик не лечит. Остаток StationOfflineRepairSeconds сохраняется в Galaxy; существующий StationOfflineProcessedUtc предотвращает повторный офлайн за один интервал. Эффекты/наука пересчитываются на последующих шагах после ремонта. Старое поле RepairDamageCredit оставлено только для чтения старых сохранений и сбрасывается при новом тик-ремонте; новых дробных HP нет.

Логи AUTO_REPAIR показывают Tick/Offline, систему, Health до/после и HealthPerTick через существующий StationEffects/debugLogConfig. Исходники общей Sprint7Verification дополнены проверками: кадры не лечат; конец тика лечит один раз; повтор/загрузка не дублируют; враги/пираты блокируют без накопления; уничтожение не отменяется саморемонтом; частичный офлайн-тик и его сохранение; значения всех пяти типов/десяти уровней; предел здоровья и большой пакет тиков. Старые проверки длительности ремонта переведены на HP/тик. Компиляция, автотесты и Unity не запускались. Статус IMPLEMENTED_UNVERIFIED; проверка — статический просмотр кода/ассетов, не runtime и не GitHub integration.


## Стоимость полного восстановления по типу и уровню — 05.11.2026

По запросу владельца RecoveryPaymentOptions в пяти station_levels_*.asset теперь имеют разные базовые цены по типам. Стоимость умножается на достигнутый уровень 1–10: торговая 500 кредитов + 2 астралита / 25 алмазов; рейнджерская 600 + 3 / 30; военная 700 + 4 / 35; научная 800 + 5 / 40; медицинская 900 + 6 / 45. Это предварительные значения, окончательная настройка S09-10. Применяются существующим StationConfig.TryCreateOperation(Recovery, currentLevel, optionId) и общей формой оплаты. Срок восстановления остаётся мгновенным, уровень сохраняется. Стоимость частичного ремонта (RepairPaymentOptions), постройки и улучшений этой правкой не изменена. Ассеты пяти типов обслуживают 50 станций.

Добавлены исходники проверки выбора цен двух вариантов для каждого типа на всех десяти уровнях через фактический TryCreateOperation. Компиляция, тесты и Unity не запускались; значения и перенос проверены статически.


## Улучшение требует полного здоровья и отсутствия врагов — 05.11.2026

Решение владельца: повреждённая станция не может начать улучшение; уже оплаченный Upgrade не продвигается при наличии врагов или до полного восстановления здоровья. StationLifecycleRules.CanBegin возвращает StationDamaged при Health < MaxHealth (включая 1 HP). StationService проверяет правило до TryPay. Общая форма показывает причину через StationInterfaceRules.Reason, кнопка улучшения в карточке повреждённой станции неактивна/серая.

StationOperationPauseReason дополнен Damaged=3, прежние значения enum сохранены. RefreshDevelopmentPause вычисляет приоритет Destroyed → UnderAttack → Damaged → None для Upgrade. Урон сразу фиксирует паузу. StationService.Reconcile и AdvanceTime повторно проверяют фактических живых вражеских NPC/пиратов текущей системы станции; для изолированных тестов добавлен необязательный Func<string,bool> hasEnemies. Без явного enemy-контекста Normalize сохраняет ранее записанную UnderAttack, не сбрасывая её на каждом чтении. Перед продвижением времени всегда используется актуальная проверка.

Саморемонт по HealthPerTick продолжает работать на границе тика, когда врагов нет. После последнего ремонтного тика, доведшего Health до MaxHealth, улучшение продолжает прежний оплаченный прогресс со следующего шага, без повторной оплаты и без начисления пропущенного времени. Мгновенные улучшения также не завершаются под атакой. StationService.ProcessOffline использует те же правила на последовательных границах ремонта/операций: время ремонта не засчитывается в Upgrade. Construction уровня 0 и правила Recovery не менялись этой правкой. Логи StationLifecycle/debugLogConfig содержат причину паузы.

Исходники Sprint7Verification дополнены отказом оплаты при повреждении 1 HP, остановкой/возобновлением при врагах и частичном ремонте, сохранением прогресса/оплаты после загрузки, мгновенным Upgrade под атакой, офлайн-порядком «сначала 2 ремонтных тика, затем 4 секунды улучшения за общий интервал 6 секунд» и повторным watermark. Прежние проверки, допускавшие улучшение повреждённой станции, обновлены под новое правило. Компиляция, тесты и Unity не запускались; статус IMPLEMENTED_UNVERIFIED, подтверждение статическим просмотром локального кода.
