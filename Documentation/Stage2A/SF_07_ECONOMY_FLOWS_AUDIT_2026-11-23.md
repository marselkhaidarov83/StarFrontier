# SF_07 — карта поступлений и расходов экономики

Дата: 23.11.2026. Локальная ветка stage-2a-prereliz, HEAD 14595ebf109a905fb6f7e203900f074b5a886878. Проверка статическим чтением, без компиляции, тестов и запуска. Production-статусы не изменяются. Дополняет существующие SF_07 по рынку, станциям и модульности; не заменяет SF_03/SF_06.

Полный реестр потоков и связь с листами согласованного Excel: Documentation/Economy/SF_ECONOMY_FLOWS_AUDIT.md. Пути ниже относительно Assets/_Project/Scripts.

Актуализация после уточнения владельца: при том же HEAD локальная рабочая копия содержит новые изменения. MarketEconomyBalanceConfig и его ассет удалены; текущие услуги читают BasePrice товаров через LocationServicePrices. Нижняя запись прежнего глобального источника заменена актуальной. Эти изменения игровых исходников не выполнялись в рамках аудита. Номера строк регистраций ниже относятся к первоначальному снимку и после локальных правок могли сместиться.

| Назначение | Точный файл и точки |
|---|---|
| Генерация наград доставки, разведки и маршрутов | Core/Services/Mission/PlanetMissionOfferGenerator.cs: BuildMission; Core/Services/Mission/DeliveryMissionTargetSelector.cs: CalculateCredits |
| Боевой учёт личных уничтожений | Core/Services/Mission/MissionService.Gameplay.cs: RecordBattleProgress, ReevaluateGameplayMission; Data/Domain/Mission/MissionBattleRules.cs: Credits |
| Получение боевой награды на планете | Core/Services/Combat/GovernmentRewardService.cs: ClaimReward вызывает MissionService.CompleteMission, не выдаёт отдельную вторую награду |
| Изменение кошелька при сдаче миссии | Core/Services/Mission/MissionService.cs: CompleteMission; Core/Services/Mission/RewardService.cs: TryGrantMissionRewardCore, реестр уже выданных наград |
| Генерация предметной и денежной добычи | Core/Services/Npc/NpcLootProcessor.cs; Data/Configs/Npc/NpcLootTableConfig.cs |
| Сбор кредитов, алмазов и предметов игроком | Core/Services/Npc/SystemSpaceLootService.cs: TryCollectForPlayer; Core/Services/Economy/EconomyService.cs: AddCredits |
| Торговля обычными товарами | Core/Services/Market/PlanetMarketService.cs: Trade, CanTrade; Data/Runtime/MarketEconomyRules.cs: Price, TotalPrice |
| Покупка/продажа экземпляров оснащения | Core/Services/Market/PlanetMarketService.cs: TradeEquipment, EquipmentPrice, EquipmentBasePrice; Data/Runtime/MarketEconomyRules.cs: EquipmentSellPrice |
| Покупка/продажа корпуса | Core/Services/Market/PlanetMarketService.Hulls.cs: TradeHull, HullPrice |
| Заправка и расход топлива | Core/Services/Fuel/RefuelService.cs: Refuel, Consume; Core/Services/Travel/TravelService2A.cs; Core/Services/Fuel/RefuelCostProvider.cs |
| Платный ремонт корпуса | Core/Services/Repair/RepairService.cs: Repair |
| Платный ремонт выбранного/всего оснащения и складских предметов | Core/Services/Hangar/HangarService.Repair.cs: BuildRepairPlan, AddEquipmentRepair, TryRepair |
| Услуги станции как фасад | Core/Services/Station/StationFacilityService.cs вызывает существующие заправку, ремонт и рыночный контур; отдельное второе списание не вводится |
| Станционные операции и оплата | Core/Services/Station/StationService.cs: TryStartOperation; Core/Services/Station/StationResourcePaymentGateway.cs: TryPay; Data/Runtime/Galaxy/StationPaymentRules.cs: TrySpend; Data/Runtime/Galaxy/StationLifecycleRules.cs: Construction/Upgrade/Recovery |
| Оплата этапа исследования | Core/Services/Research/ResearchService.cs: TryStart; Data/Runtime/Research/ResearchRules.cs: TryQuote отклоняет пустые PaymentOptions; используется тот же IStationPaymentGateway |
| Стартовый баланс и debug-исключение | Core/Services/Game/NewGameFactory.cs: CreatePlayerProfile, IsMobileTestMode |
| Цены услуг из товаров | Core/Services/Market/LocationServicePrices.cs: Price читает ItemConfig.BasePrice по ID; FuelItemId, HullRepairItemId, WeaponRepairItemId, ComponentRepairItemId. Используется RefuelService, RepairService и HangarService.Repair |
| Оценки оснащения и корпусов для рынка/выкупа | Core/Services/Config/IConfigService.cs и ConfigService.cs: EquipmentCatalog/HullCatalog. Каталоги передаются из списков Core/Bootstrap/Bootstrapper.cs. PlanetMarketService берёт NewPrice позиции/правила, затем MarketEconomyRules.EquipmentSellPrice учитывает прочность с минимумом 1 кредит, без прежнего глобального процента |

Регистрации проверены в Core/Bootstrap/Bootstrapper.cs: IStationPaymentGateway и IStationService — строки 316–317; IPlanetMarketService и IEconomyService — 337–340; IRefuelService — 342; IRepairService и IRewardService — 354–355; IResearchService с общим шлюзом оплаты — 372–374. Это номера строк текущего локального снимка, не обещание неизменности после будущих правок.

Поиск VIP/Vip в C# не выявил механизма бонуса доходов. MissionCreditReward, MissionAdvancePayment, ScienceTrophyRecycling, TradeFuelWonder, TradeRepairWonder найдены как enum, но применение соответствующих эффектов в проверенных платёжных путях не обнаружено. Наличие данных эффекта не подтверждает его использование.
