using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public sealed class PlanetMarketEconomyTests
{
    private readonly List<ScriptableObject> _objects = new List<ScriptableObject>();
    private Session _session;
    private Configs _config;
    private SimpleEventBus _events;
    private InventoryService _inventory;
    private PlanetMarketService _market;
    private MarketProfileConfig _profile;
    private ItemConfig _item;
    private WeaponConfig _weapon;
    private MarketEconomyBalanceConfig _balance;
    private ShipRuntimeData Ship => _session.State.Player.GetActiveShip();
    private MarketRuntimeData Market => _market.CurrentMarket;
    private MarketRuntimeItemState Stock => Market.Items.Single(x => x.ItemId == _item.Id);
    private MarketItemEntry Entry => _profile.GalaxyLevels[0].Items[0];
    private T Asset<T>(string id) where T : BaseConfig
    { var a = ScriptableObject.CreateInstance<T>(); _objects.Add(a); Set(a, "id", id); return a; }
    private static void Set(object value, string field, object data)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        { var f = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
          if (f != null) { f.SetValue(value, data); return; } }
        throw new MissingFieldException(field);
    }
    [SetUp] public void Setup()
    {
        _session = new Session(); _session.StartNewSession(new GameRuntimeState());
        _session.State.Player.CurrentSystemId = "system"; _session.State.Player.CurrentPlanetId = "one";
        _session.State.Player.Credits = 100000;
        _session.State.Player.PlayerShipState = new ShipRuntimeState { ActiveShipId = "ship", OwnedShips = new List<ShipRuntimeData> {
            new ShipRuntimeData { ShipId = "ship", CurrentHull = 50, HullCapacity = 100, FuelCapacity = 100, CurrentFuel = 10, CargoCapacity = 1000 } } };
        _session.State.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "s1", IsUnlocked = true });
        _session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "system" });
        _item = Asset<ItemConfig>("medicine"); Set(_item, "basePrice", 10); Set(_item, "cargoSize", 2);
        _weapon = Asset<WeaponConfig>("ally_weapon");
        _profile = Asset<MarketProfileConfig>("profile");
        _profile.GalaxyLevels.Add(new MarketGalaxyLevelSettings { GalaxyLevel = 1, IntervalSeconds = 10,
            Items = new List<MarketItemEntry> { new MarketItemEntry { ItemConfig = _item, Quantity = 50, StockChange = 7, MaximumStock = 100, BasePrice = 10, MaximumPrice = 30 } },
            Equipment = new List<MarketEquipmentRule> { new MarketEquipmentRule { Weapon = _weapon, Allied = true, NewPrice = 100, SpawnIntervalSeconds = 10, LifetimeSeconds = 25, MaximumListings = 1 } } });
        var planetType = Asset<PlanetTypeConfig>("planet_type"); Set(planetType, "marketProfile", _profile);
        var one = Asset<PlanetConfig>("one"); Set(one, "planetTypeConfig", planetType);
        var two = Asset<PlanetConfig>("two"); Set(two, "planetTypeConfig", planetType);
        var system = Asset<StarSystemConfig>("system"); Set(system, "planetRefs", new[] { one, two });
        _balance = Asset<MarketEconomyBalanceConfig>("balance");
        _balance.ResearchLevels = new[] {
            new MarketResearchEffect { Level = 0, WarProductionLoss = 0.5f, WarConsumptionIncrease = 0.5f, MinimumEquipmentBuyoutPercent = 0.1f },
            new MarketResearchEffect { Level = 1, WarProductionLoss = 0, WarConsumptionIncrease = 0, MinimumEquipmentBuyoutPercent = 0.5f } };
        _config = new Configs { MarketEconomy = _balance, System = system, Item = _item, Weapon = _weapon, Planets = new[] { one, two } };
        _events = new SimpleEventBus(); _inventory = new InventoryService(_session, _config, _events);
        _market = new PlanetMarketService(_session, _config, _inventory, _events); _market.InitializeSession();
    }
    [TearDown] public void TearDown()
    { foreach (var item in _objects) if (item != null) UnityEngine.Object.DestroyImmediate(item); _objects.Clear(); }

    [TestCase(1, 57)] [TestCase(-1, 43)] [TestCase(0, 50)]
    public void SignedChange_AndPartialInterval(int direction, int expected)
    { Entry.StockChange = direction * 7; _market.AdvanceTime(9); Assert.That(Stock.Quantity, Is.EqualTo(50));
      _market.AdvanceTime(1); Assert.That(Stock.Quantity, Is.EqualTo(expected)); }
    [Test] public void NaturalLimits_AndOvercapProductionPreservesPlayerSale()
    { Stock.Quantity = 99; _market.AdvanceTime(10); Assert.That(Stock.Quantity, Is.EqualTo(100));
      Stock.Quantity = 130; _market.AdvanceTime(10); Assert.That(Stock.Quantity, Is.EqualTo(130));
      Entry.StockChange = -7; _market.AdvanceTime(1000); Assert.That(Stock.Quantity, Is.Zero); }
    [TestCase(0, 30)] [TestCase(50, 20)] [TestCase(100, 10)] [TestCase(130, 10)]
    public void PriceBoundsAndInterpolation(int stock, int expected)
    { Assert.That(MarketEconomyRules.Price(Entry, stock), Is.EqualTo(expected)); }
    [Test] public void SharedProfile_PlanetsHaveIndependentStocks()
    { Stock.Quantity = 17; _session.State.Player.CurrentPlanetId = "two"; Assert.That(Stock.Quantity, Is.EqualTo(50)); }
    [Test] public void LevelChange_PreservesStock_UsesNewPricesAndRate()
    {
        Stock.Quantity = 130;
        _profile.GalaxyLevels.Add(new MarketGalaxyLevelSettings { GalaxyLevel = 2, IntervalSeconds = 5,
            Items = new List<MarketItemEntry> { new MarketItemEntry { ItemConfig = _item, Quantity = 1, StockChange = -10, MaximumStock = 80, BasePrice = 20, MaximumPrice = 60 } } });
        _session.State.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "s2", IsUnlocked = true });
        Assert.That(_market.GetPrice(_item.Id, true), Is.EqualTo(20)); Assert.That(Stock.Quantity, Is.EqualTo(130));
        _market.AdvanceTime(5); Assert.That(Stock.Quantity, Is.EqualTo(120));
    }
    [TestCase(7, 53)] [TestCase(-7, 39)] [TestCase(0, 50)]
    public void WarChangesSignedRate(int rate, int expected)
    { Entry.StockChange = rate; _session.State.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Invasion;
      _market.AdvanceTime(10); Assert.That(Stock.Quantity, Is.EqualTo(expected)); }
    [TestCase(7, 57)] [TestCase(-7, 43)]
    public void ProtectionResearch_ReducesBothWarEffects(int rate, int expected)
    { Entry.StockChange = rate; _session.State.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Invasion;
      _session.State.Galaxy.Research.Levels.Add(new ResearchUpgradeLevel { Upgrade = ResearchUpgrade.EconomyWarProtection, Level = 1 });
      _market.AdvanceTime(10); Assert.That(Stock.Quantity, Is.EqualTo(expected)); }
    [Test] public void FractionalWarRate_IsIndependentOfFrameGrouping()
    {
        Entry.StockChange = 1; _session.State.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Invasion;
        string initial = JsonUtility.ToJson(_session.State); _market.AdvanceTime(30);
        int stock = Stock.Quantity; double fraction = Stock.FractionalStock;
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(initial));
        for (int i = 0; i < 30; i++) _market.AdvanceTime(1);
        Assert.That(Stock.Quantity, Is.EqualTo(stock)); Assert.That(Stock.FractionalStock, Is.EqualTo(fraction).Within(1e-8));
    }
    [Test] public void Offline_EqualsOnline_IncludingEquipmentIdentityWearAndRolls()
    {
        long from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks; _session.State.Meta.LastSaveUtc = from;
        string initial = JsonUtility.ToJson(_session.State);
        for (int i = 0; i < 40; i++) _market.AdvanceTime(1);
        int expected = Stock.Quantity; string equipment = JsonUtility.ToJson(Market.Equipment.Single());
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(initial));
        _market.ProcessOffline(from + 40 * TimeSpan.TicksPerSecond);
        Assert.That(Stock.Quantity, Is.EqualTo(expected)); Assert.That(JsonUtility.ToJson(Market.Equipment.Single()), Is.EqualTo(equipment));
        string once = JsonUtility.ToJson(_session.State); _market.ProcessOffline(from + 40 * TimeSpan.TicksPerSecond);
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(once));
    }
    [Test] public void MarketOffline_DoesNotTruncateToStationTwelveHourWindow()
    {
        _profile.GalaxyLevels[0].Equipment.Clear(); Entry.StockChange = 1; Entry.MaximumStock = 100000;
        long from = DateTime.UtcNow.AddDays(-1).Ticks; _session.State.Meta.LastSaveUtc = from;
        _market.ProcessOffline(from + TimeSpan.TicksPerDay);
        Assert.That(Stock.Quantity, Is.EqualTo(50 + 8640));
    }
    [Test] public void MaximumPurchase_UsesPhysicalSizeIncludingEquipmentAndMissionCargo()
    {
        Ship.CargoCapacity = 11;
        CargoWeapon(); // 2 units of cargo space.
        Ship.Cargo.entries.Add(new RuntimeCargoEntryData(_item.Id, 2) { MissionRuntimeId = "mission" }); // 4 units.
        int credits = _session.State.Player.Credits;
        int stock = Stock.Quantity;
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.EqualTo(2));
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true, true), Is.EqualTo(stock), "Warehouse purchases do not use the ship hold.");
        Assert.That(_market.Trade(_item.Id, 3, true, "oversized").ErrorCode, Is.EqualTo(EconomyErrorCode.NotEnoughCargoSpace));
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits));
        Assert.That(Stock.Quantity, Is.EqualTo(stock));
        Assert.That(_market.Trade(_item.Id, 2, true, "fits").Success, Is.True);
        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(10));
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.Zero);
    }
    [Test] public void MaximumPurchase_AlsoUsesCreditsAndMarketStock()
    {
        _session.State.Player.Credits = (int)_market.GetTotalPrice(_item.Id, 3, true);
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.EqualTo(3));
        Assert.That(_market.CanTrade(_item.Id, 4, true), Is.False);
        Stock.Quantity = 2; _session.State.Player.Credits = 100000;
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.EqualTo(2));
        Stock.Quantity = 0;
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.Zero);
    }
    [TestCase(false)] [TestCase(true)]
    public void BulkSale_MaxExcludesMissionUnits_AndSellsSelectedAmountOnce(bool warehouse)
    {
        var source = warehouse ? Market.PersonalStorage : Ship.Cargo;
        source.entries.Add(new RuntimeCargoEntryData(_item.Id, 4) { MissionRuntimeId = "mission" });
        source.AddLootItem(_item.Id, 3);
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, false, warehouse), Is.EqualTo(3));
        long quote = _market.GetTotalPrice(_item.Id, 2, false);
        int credits = _session.State.Player.Credits;
        long revision = Market.Revision;
        var result = _market.Trade(_item.Id, 2, false, "bulk-sale", revision, warehouse);
        Assert.That(result.Success, Is.True);
        Assert.That(result.ChangedCredits, Is.EqualTo(quote));
        Assert.That(source.GetQuantity(_item.Id), Is.EqualTo(5));
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, false, warehouse), Is.EqualTo(1));
        Assert.That(_market.Trade(_item.Id, 2, false, "bulk-sale", revision, warehouse).Success, Is.True);
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits + quote));
        Assert.That(source.GetQuantity(_item.Id), Is.EqualTo(5));
    }
    [TestCase(false)] [TestCase(true)]
    public void QuantityQuote_MatchesCommittedCost_AndDoesNotMutateTheMarket(bool warehouse)
    {
        int credits = _session.State.Player.Credits;
        int stock = Stock.Quantity; long revision = Market.Revision;
        long total = _market.GetTotalPrice(_item.Id, 10, true);
        Assert.That(total, Is.GreaterThan((long)_market.GetPrice(_item.Id, true) * 10));
        Assert.That(Stock.Quantity, Is.EqualTo(stock));
        Assert.That(Market.Revision, Is.EqualTo(revision));
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits));
        var result = _market.Trade(_item.Id, 10, true, "quantity-preview", revision, warehouse);
        Assert.That(result.Success, Is.True);
        Assert.That(-result.ChangedCredits, Is.EqualTo(total));
        Assert.That((warehouse ? Market.PersonalStorage : Ship.Cargo).GetQuantity(_item.Id), Is.EqualTo(10));
        Assert.That(_market.Trade(_item.Id, 10, true, "quantity-preview", revision, warehouse).Success, Is.True);
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits - total));
    }
    [Test] public void GoodsTrade_ReceiptSurvivesReload_AndObserversSeeCommittedState()
    {
        int saves = 0; int credits = _session.State.Player.Credits;
        _events.Subscribe<SaveNeedEvent>(_ => { saves++; Assert.That(Ship.Cargo.GetQuantity(_item.Id), Is.EqualTo(2));
            Assert.That(Stock.Quantity, Is.EqualTo(48)); Assert.That(Market.Receipts.Count, Is.EqualTo(1)); });
        var result = _market.Trade(_item.Id, 2, true, "buy"); Assert.That(result.Success, Is.True);
        int paid = -result.ChangedCredits; Assert.That(_session.State.Player.Credits, Is.EqualTo(credits - paid));
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        Assert.That(_market.Trade(_item.Id, 2, true, "buy").Success, Is.True);
        Assert.That(Stock.Quantity, Is.EqualTo(48)); Assert.That(saves, Is.EqualTo(1));
        Assert.That(_market.Trade(_item.Id, 1, true, "buy").ErrorCode, Is.EqualTo(EconomyErrorCode.OperationConflict));
    }
    [Test] public void StaleQuote_AndReentrantObserver_DoNotApplyAnotherTrade()
    {
        long revision = Market.Revision;
        _events.Subscribe<CreditsChangedEvent>(_ => Assert.That(_market.Trade(_item.Id, 1, true, "reentry").Success, Is.False));
        Assert.That(_market.Trade(_item.Id, 1, true, "first", revision).Success, Is.True);
        Assert.That(_market.Trade(_item.Id, 1, true, "stale", revision).ErrorCode, Is.EqualTo(EconomyErrorCode.MarketChanged));
        Assert.That(Stock.Quantity, Is.EqualTo(49));
    }
    [Test] public void ThrowingObserver_DoesNotPreventSaveOrReceipt()
    { int saves = 0; _events.Subscribe<CreditsChangedEvent>(_ => throw new Exception("observer")); _events.Subscribe<SaveNeedEvent>(_ => saves++);
      Assert.That(_market.Trade(_item.Id, 1, true, "safe").Success, Is.True); Assert.That(saves, Is.EqualTo(1)); }
    [TestCase("credits")] [TestCase("stock")] [TestCase("cargo")] [TestCase("quantity")] [TestCase("overflow")]
    public void Rejection_PreservesWalletCargoAndStock(string condition)
    {
        int quantity = 1;
        if (condition == "credits") _session.State.Player.Credits = 0;
        if (condition == "stock") Stock.Quantity = 0;
        if (condition == "cargo") Ship.CargoCapacity = 1;
        if (condition == "quantity") quantity = 0;
        if (condition == "overflow") { quantity = int.MaxValue; Stock.Quantity = int.MaxValue; }
        string before = JsonUtility.ToJson(_session.State);
        Assert.That(_market.Trade(_item.Id, quantity, true, "rejected").Success, Is.False);
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(before));
    }
    [Test] public void BothPublicFacades_UseSameMarketCommitAndSingleSave()
    {
        int saves = 0; _events.Subscribe<SaveNeedEvent>(_ => saves++);
        var economy = new EconomyService(_session, _market, _events); var legacy = new MarketTransactionService(economy);
        Assert.That(economy.BuyItem(_item.Id, 1).Success, Is.True); Assert.That(legacy.BuyItem(_item.Id, 1).IsSuccess, Is.True);
        Assert.That(Stock.Quantity, Is.EqualTo(48)); Assert.That(Ship.Cargo.GetQuantity(_item.Id), Is.EqualTo(2)); Assert.That(saves, Is.EqualTo(2));
    }
    [Test] public void SaleAboveCap_IsAllowed_AndRemainsAtBasePrice()
    { Stock.Quantity = 100; Ship.Cargo.AddItem(_item.Id, 2, Ship.CargoCapacity);
      Assert.That(_market.Trade(_item.Id, 2, false, "sell").ChangedCredits, Is.EqualTo(20)); Assert.That(Stock.Quantity, Is.EqualTo(102)); }
    [TestCase(1)] [TestCase(10)] [TestCase(50)]
    public void ImmediateRoundTrip_CannotGenerateCredits(int quantity)
    { int before = _session.State.Player.Credits; Assert.That(_market.Trade(_item.Id, quantity, true, "buy").Success, Is.True);
      Assert.That(_market.Trade(_item.Id, quantity, false, "sell").Success, Is.True); Assert.That(_session.State.Player.Credits, Is.EqualTo(before)); }
    [Test] public void BulkPrice_MatchesMarginalQuotes_WithIntegerRounding()
    { for (int stock = 0; stock <= 120; stock++) for (int count = 1; count <= 15; count++) {
        long sum = 0; for (int i = 1; i <= count; i++) sum += MarketEconomyRules.Price(Entry, stock + i);
        Assert.That(MarketEconomyRules.TotalPrice(Entry, stock, count, false), Is.EqualTo(sum)); } }
    [Test] public void BulkQuote_LargeInputIsBoundedAndDoesNotOverflow()
    { Entry.BasePrice = int.MaxValue; Entry.MaximumPrice = int.MaxValue; Entry.MaximumStock = int.MaxValue;
      Assert.That(MarketEconomyRules.TotalPrice(Entry, int.MaxValue, int.MaxValue, true), Is.EqualTo((long)int.MaxValue * int.MaxValue)); }
    [Test] public void MissionCargo_CannotBeSoldAsOrdinaryGoods()
    { Ship.Cargo.entries.Add(new RuntimeCargoEntryData(_item.Id, 3) { MissionRuntimeId = "mission" });
      Assert.That(_market.Trade(_item.Id, 1, false).Success, Is.False); Assert.That(Ship.Cargo.GetQuantity(_item.Id), Is.EqualTo(3)); }

    private AllyConfig ConfigureHull(string id, int weaponSlots, int cargo, ShipComponentSlots utility = ShipComponentSlots.All)
    {
        var hull = Asset<AllyConfig>(id);
        Set(hull, "weaponSlotCount", weaponSlots);
        Set(hull, "baseCargoCapacityMin", cargo);
        Set(hull, "availableComponentSlots", utility);
        _config.Hulls.Add(hull);
        return hull;
    }

    private HangarService ConfigureLoadout(AllyConfig hull)
    {
        Ship.AllyConfigId = hull.Id;
        ShipLoadoutRules.Initialize(Ship, hull, _config.GetWeaponConfigById);
        return new HangarService(_session, _config, _market, _inventory, _events);
    }

    [Test] public void InstalledWeapon_RetainsItsInstanceAndWearAfterReloadAndRemoval()
    {
        var hangar = ConfigureLoadout(ConfigureHull("hull-three", 3, 1000));
        var item = CargoWeapon(false, 37);
        int slot = ShipLoadoutRules.FirstWeaponSlot + 2;
        Assert.That(hangar.TryChangeLoadout(slot + 1, item.InstanceId, false, out _), Is.False);
        Assert.That(hangar.TryChangeLoadout(slot, item.InstanceId, false, out var error), Is.True, error);
        Assert.That(Ship.CargoEquipment, Is.Empty);
        Assert.That(Ship.EquippedWeaponIds[0], Is.Empty);
        Assert.That(Ship.EquippedWeaponIds[2], Is.EqualTo(item.WeaponConfigId));

        ReloadThroughSaveValidation();
        Assert.That(ShipLoadoutRules.Weapon(Ship, 2).CurrentDurability, Is.EqualTo(37));
        Assert.That(hangar.TryChangeLoadout(slot, null, true, out error), Is.True, error);
        Assert.That(Market.PersonalEquipment.Single().InstanceId, Is.EqualTo(item.InstanceId));
        Assert.That(Market.PersonalEquipment.Single().RolledStats.Damage, Is.EqualTo(item.RolledStats.Damage));
        Assert.That(Market.PersonalEquipment.Single().SlotIndex, Is.EqualTo(-1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveAllEquipment_PreservesHullAndInstancesAndUsesSelectedStorage(bool warehouse)
    {
        var tank = Asset<ShipComponentConfig>("bulk-remove-tank");
        tank.Slot = ShipComponentSlot.FuelTank;
        tank.FuelCapacity = 100;
        Set(tank, "cargoSize", 3);
        _config.Components.Add(tank);
        var hull = ConfigureHull("bulk-remove-hull", 1, 1000);
        Set(hull, "defaultComponents", new[] { tank });
        var hangar = ConfigureLoadout(hull);
        var weapon = CargoWeapon(false, 37);
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.FirstWeaponSlot, weapon.InstanceId, false, out var reason), Is.True, reason);
        var installedHull = Ship.InstalledHull;
        var installedTank = ShipLoadoutRules.Component(Ship, ShipComponentSlot.FuelTank);
        var revision = Market.Revision;

        int usedBefore = _inventory.GetUsedCargo();
        Ship.CargoCapacity = usedBefore;
        Assert.That(_inventory.GetFreeCargo(), Is.Zero);
        Assert.That(hangar.TryRemoveAllEquipment(warehouse, out reason), Is.True, reason);
        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(warehouse ? 0 : usedBefore));
        var destination = warehouse ? Market.PersonalEquipment : Ship.CargoEquipment;
        Assert.That(destination, Is.EquivalentTo(new[] { weapon, installedTank }));
        Assert.That(weapon.CurrentDurability, Is.EqualTo(37));
        Assert.That(destination.All(item => item.SlotIndex == -1), Is.True);
        Assert.That(Ship.InstalledHull, Is.SameAs(installedHull));
        Assert.That(Ship.InstalledWeapons, Is.Empty);
        Assert.That(Ship.InstalledComponents, Is.Empty);
        Assert.That(Ship.EquippedWeaponIds.All(string.IsNullOrEmpty), Is.True);
        Assert.That(Ship.CurrentFuel, Is.Zero);
        Assert.That(Ship.FuelCapacity, Is.Zero);
        Assert.That(hangar.TryRemoveAllEquipment(warehouse, out _), Is.False);
        Assert.That(Market.Revision, Is.EqualTo(revision + 1));

        ReloadThroughSaveValidation();
        var stored = (warehouse ? Market.PersonalEquipment : Ship.CargoEquipment).Single(item => item.IsComponent);
        Assert.That(stored.StoredFuel, Is.EqualTo(10));
        int tankSlot = ShipLoadoutRules.FirstComponentSlot + (int)ShipComponentSlot.FuelTank;
        Assert.That(hangar.TryChangeLoadout(tankSlot, stored.InstanceId, warehouse, out reason), Is.True, reason);
        Assert.That(Ship.CurrentFuel, Is.EqualTo(10));
        Assert.That(Ship.FuelCapacity, Is.EqualTo(100));
        Assert.That(stored.StoredFuel, Is.Zero);
    }

    [Test] public void InstalledEquipment_ReducesPurchaseLimitAndCannotBeCountedTwice()
    {
        var engine = Asset<ShipComponentConfig>("cargo-test-engine");
        engine.Slot = ShipComponentSlot.Engine;
        Set(engine, "cargoSize", 3);
        var larger = Asset<ShipComponentConfig>("cargo-test-larger-engine");
        larger.Slot = ShipComponentSlot.Engine;
        Set(larger, "cargoSize", 5);
        _config.Components.Add(engine);
        _config.Components.Add(larger);

        var hangar = ConfigureLoadout(ConfigureHull("cargo-test-hull", 1, 6));
        Ship.CargoCapacity = 6;
        var weapon = CargoWeapon(false, 37);
        int weaponSlot = ShipLoadoutRules.FirstWeaponSlot;
        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(2));
        Assert.That(hangar.TryChangeLoadout(weaponSlot, weapon.InstanceId, false, out var reason), Is.True, reason);
        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(2));
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.EqualTo(2));

        var first = SystemNpcWeaponRuntimeState.NewComponent(engine, "cargo-engine");
        var second = SystemNpcWeaponRuntimeState.NewComponent(larger, "cargo-larger-engine");
        Market.PersonalEquipment.Add(first);
        Market.PersonalEquipment.Add(second);
        int engineSlot = ShipLoadoutRules.FirstComponentSlot + (int)ShipComponentSlot.Engine;
        Assert.That(hangar.TryChangeLoadout(engineSlot, first.InstanceId, true, out reason), Is.True, reason);
        Assert.That(_inventory.GetFreeCargo(), Is.EqualTo(1));
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.Zero);

        Assert.That(hangar.TryChangeLoadout(engineSlot, second.InstanceId, true, out _), Is.False);
        Assert.That(ShipLoadoutRules.Component(Ship, ShipComponentSlot.Engine), Is.SameAs(first));
        Assert.That(Market.PersonalEquipment.Single(), Is.SameAs(second));
        Assert.That(hangar.TryChangeLoadout(weaponSlot, null, true, out reason), Is.True, reason);
        Assert.That(_inventory.GetFreeCargo(), Is.EqualTo(3));
        Assert.That(_market.GetMaximumTradeQuantity(_item.Id, true), Is.EqualTo(1));
    }

    [Test] public void ShipSpeed_FollowsInstalledEngineAcrossHullSwapAndRemoval()
    {
        var standard = Asset<ShipComponentConfig>("speed-standard");
        standard.Slot = ShipComponentSlot.Engine;
        standard.Speed = 100;
        var fast = Asset<ShipComponentConfig>("speed-fast");
        fast.Slot = ShipComponentSlot.Engine;
        fast.Speed = 175;
        _config.Components.Add(standard);
        _config.Components.Add(fast);

        var originalHull = ConfigureHull("speed-test-original", 1, 1000);
        var otherHull = ConfigureHull("speed-test-other", 1, 1000);
        Set(originalHull, "defaultComponents", new[] { standard });
        Set(otherHull, "defaultComponents", new[] { standard });
        var hangar = ConfigureLoadout(originalHull);
        Assert.That(hangar.GetActiveShipStats().Speed, Is.EqualTo(100));

        var upgrade = SystemNpcWeaponRuntimeState.NewComponent(fast, "speed-upgrade");
        Market.PersonalEquipment.Add(upgrade);
        int engineSlot = ShipLoadoutRules.FirstComponentSlot + (int)ShipComponentSlot.Engine;
        Assert.That(hangar.TryChangeLoadout(engineSlot, upgrade.InstanceId, true, out var reason), Is.True, reason);
        Assert.That(hangar.GetActiveShipStats().Speed, Is.EqualTo(175));

        var hullItem = Asset<HullEquipmentConfig>("speed-test-hull-item");
        hullItem.Hull = otherHull;
        _balance.HullCatalog.Add(new MarketHullRule { Hull = hullItem, NewPrice = 800 });
        var spare = new HullEquipmentRuntimeState
        {
            InstanceId = "speed-spare-hull", ConfigId = hullItem.Id,
            CurrentDurability = 120, MaximumDurability = 120
        };
        Market.PersonalHulls.Add(spare);
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.HullSlot, spare.InstanceId, true, out reason), Is.True, reason);
        Assert.That(hangar.GetActiveShipStats().Speed, Is.EqualTo(175));

        Assert.That(hangar.TryChangeLoadout(engineSlot, null, true, out reason), Is.True, reason);
        Assert.That(hangar.GetActiveShipStats().Speed, Is.Zero);
    }

    [Test] public void TankSwap_UsesOnlyTheTypedSlotAndReturnsTheOldTankToTheChosenInventory()
    {
        var tank = Asset<ShipComponentConfig>("tank-standard");
        tank.Slot = ShipComponentSlot.FuelTank; tank.FuelCapacity = 100; Set(tank, "cargoSize", 3);
        var next = Asset<ShipComponentConfig>("tank-large");
        next.Slot = ShipComponentSlot.FuelTank; next.FuelCapacity = 160; Set(next, "cargoSize", 5);
        _config.Components.Add(tank); _config.Components.Add(next);
        var hull = ConfigureHull("ship-with-tank", 1, 1000);
        Set(hull, "defaultComponents", new[] { tank });
        var hangar = ConfigureLoadout(hull);
        var spare = SystemNpcWeaponRuntimeState.NewComponent(next, "larger-tank");
        spare.CurrentDurability = 67;
        Market.PersonalEquipment.Add(spare);

        int slot = ShipLoadoutRules.FirstComponentSlot + (int)ShipComponentSlot.FuelTank;
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.FirstComponentSlot, spare.InstanceId, true, out _), Is.False);
        Assert.That(Market.PersonalEquipment.Single(), Is.SameAs(spare));
        Assert.That(hangar.TryChangeLoadout(slot, spare.InstanceId, true, out var error), Is.True, error);
        Assert.That(Ship.FuelCapacity, Is.EqualTo(160));
        Assert.That(Ship.CurrentFuel, Is.Zero);
        Assert.That(Market.PersonalEquipment.Single().StoredFuel, Is.EqualTo(10));
        Assert.That(Market.PersonalEquipment.Single().ComponentConfigId, Is.EqualTo(tank.Id));
        Assert.That(ShipLoadoutRules.Component(Ship, ShipComponentSlot.FuelTank).CurrentDurability, Is.EqualTo(67));
    }

    [Test] public void HullSwap_UsesOnlyWarehouseAndChecksCargoThatStaysOnTheShip()
    {
        var start = ConfigureHull("starting-hull", 3, 1000);
        var target = ConfigureHull("small-hull", 1, 15);
        var hangar = ConfigureLoadout(start);
        var weapon = CargoWeapon(false, 52);
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.FirstWeaponSlot + 2, weapon.InstanceId, false, out _), Is.True);
        var equipment = Asset<HullEquipmentConfig>("small-hull-item"); equipment.Hull = target;
        _balance.HullCatalog.Add(new MarketHullRule { Hull = equipment, NewPrice = 800 });
        var spare = new HullEquipmentRuntimeState
            { InstanceId = "spare", ConfigId = equipment.Id, MaximumDurability = 120, CurrentDurability = 72 };
        Market.PersonalHulls.Add(spare);

        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(2));
        Assert.That(_market.CanTransfer(spare.InstanceId, true, false, 1), Is.False);
        Assert.That(_market.Transfer(spare.InstanceId, true, false, 1, "load-hull", Market.Revision, out _), Is.False);
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.HullSlot, null, true, out _), Is.False);
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.HullSlot, spare.InstanceId, false, out _), Is.False);
        Assert.That(Ship.Cargo.AddLootItem(_item.Id, 10), Is.True);
        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.HullSlot, spare.InstanceId, true, out _), Is.False);
        Assert.That(Ship.AllyConfigId, Is.EqualTo(start.Id));
        Assert.That(Market.PersonalHulls.Single(), Is.SameAs(spare));
        Assert.That(Ship.CargoHulls, Is.Empty);
        Assert.That(Ship.Cargo.RemoveItem(_item.Id, 10), Is.True);
        Assert.That(Ship.Cargo.AddLootItem(_item.Id, 7), Is.True);

        Assert.That(hangar.TryChangeLoadout(ShipLoadoutRules.HullSlot, spare.InstanceId, true, out var error), Is.True, error);
        Assert.That(Ship.AllyConfigId, Is.EqualTo(target.Id));
        Assert.That(Ship.CurrentHull, Is.EqualTo(72));
        Assert.That(Market.PersonalHulls.Single().CurrentDurability, Is.EqualTo(50));
        Assert.That(Market.PersonalEquipment.Single().InstanceId, Is.EqualTo(weapon.InstanceId));
        Assert.That(ShipLoadoutRules.Weapon(Ship, 2), Is.Null);
        ReloadThroughSaveValidation();
        Assert.That(Ship.InstalledHull.InstanceId, Is.EqualTo(spare.InstanceId));
        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(14));
    }

    [Test] public void HangarRepair_QuotesSelectedAndFullRepairsAndChargesOnce()
    {
        var hangar = ConfigureLoadout(ConfigureHull("repair-hull", 2, 1000));
        var first = CargoWeapon(false, 160);
        first.MaximumDurability = 200;
        int slot = ShipLoadoutRules.FirstWeaponSlot;
        Assert.That(hangar.TryChangeLoadout(slot, first.InstanceId, false, out _), Is.True);
        var second = CargoWeapon(false, 75);
        second.InstanceId = "second-installed";
        Assert.That(hangar.TryChangeLoadout(slot + 1, second.InstanceId, false, out _), Is.True);
        var spare = CargoWeapon(false, 25);
        spare.InstanceId = "spare-in-cargo";
        int saves = 0;
        _events.Subscribe<SaveNeedEvent>(_ => saves++);
        int credits = _session.State.Player.Credits;

        long single = hangar.GetRepairPrice(HangarRepairScope.Installed, slot);
        Assert.That(single, Is.EqualTo(80));
        Assert.That(hangar.TryRepair(HangarRepairScope.Installed, slot, null, false, single - 1, out _), Is.False);
        Assert.That(hangar.TryRepair(HangarRepairScope.Installed, slot, null, false, single, out var reason), Is.True, reason);
        Assert.That(first.CurrentDurability, Is.EqualTo(200));
        Assert.That(second.CurrentDurability, Is.EqualTo(75));
        Assert.That(Ship.CurrentHull, Is.EqualTo(50));

        long full = hangar.GetRepairPrice(HangarRepairScope.FullShip, slot);
        Assert.That(full, Is.EqualTo(150));
        Assert.That(hangar.TryRepair(HangarRepairScope.FullShip, slot, null, false, full, out reason), Is.True, reason);
        Assert.That(hangar.TryRepair(HangarRepairScope.FullShip, slot, null, false, full, out _), Is.False);
        Assert.That(second.CurrentDurability, Is.EqualTo(100));
        Assert.That(Ship.CurrentHull, Is.EqualTo(Ship.HullCapacity));
        Assert.That(spare.CurrentDurability, Is.EqualTo(25));
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits - single - full));
        Assert.That(saves, Is.EqualTo(2));
    }

    [Test] public void HangarRepair_RepairsStoredHullInPlaceAndChecksFunds()
    {
        var hangar = ConfigureLoadout(ConfigureHull("active-hull", 1, 1000));
        var stored = new HullEquipmentRuntimeState
            { InstanceId = "spare-hull", ConfigId = "stored-hull", MaximumDurability = 120, CurrentDurability = 0 };
        Market.PersonalHulls.Add(stored);
        int slot = ShipLoadoutRules.HullSlot;
        long price = hangar.GetRepairPrice(HangarRepairScope.Inventory, slot, stored.InstanceId, true);
        Assert.That(price, Is.EqualTo(240));
        Assert.That(hangar.GetRepairPrice(HangarRepairScope.Inventory, slot, stored.InstanceId), Is.Zero);
        _session.State.Player.Credits = (int)price - 1;
        Assert.That(hangar.TryRepair(HangarRepairScope.Inventory, slot, stored.InstanceId, true, price, out _), Is.False);
        Assert.That(stored.CurrentDurability, Is.Zero);
        _session.State.Player.Credits = (int)price;
        Assert.That(hangar.TryRepair(HangarRepairScope.Inventory, slot, stored.InstanceId, true, price, out var reason), Is.True, reason);
        Assert.That(Market.PersonalHulls.Single(), Is.SameAs(stored));
        Assert.That(stored.CurrentDurability, Is.EqualTo(120));
        Assert.That(Ship.CurrentHull, Is.EqualTo(50));
        Assert.That(_session.State.Player.Credits, Is.Zero);
    }

    [Test] public void ComponentDurability_SurvivesDamageRepairTransferAndUsedSale()
    {
        var component = Asset<ShipComponentConfig>("test-engine");
        component.Slot = ShipComponentSlot.Engine;
        component.MaximumDurability = 140;
        Set(component, "cargoSize", 3);
        _config.Components.Add(component);
        _profile.GalaxyLevels[0].Equipment.Add(new MarketEquipmentRule
            { Component = component, StartInStock = true, NewPrice = 140, SpawnIntervalSeconds = 10, MaximumListings = 1 });
        var hangar = ConfigureLoadout(ConfigureHull("engine-hull", 1, 1000));
        ReloadThroughSaveValidation();
        var first = Market.Equipment.Single(x => x.Equipment.IsComponent).Equipment;
        Assert.That(_market.TradeEquipment(first.InstanceId, true, "buy-engine", -1, out _), Is.True);
        Assert.That(Market.Equipment.Any(x => x.Equipment.IsComponent), Is.False);
        _market.AdvanceTime(10);
        var second = Market.Equipment.Single(x => x.Equipment.IsComponent).Equipment;
        Assert.That(_market.TradeEquipment(second.InstanceId, true, "buy-second-engine", -1, out _), Is.True);
        Assert.That(Ship.Cargo.GetQuantity(component.Id), Is.Zero);
        Assert.That(Ship.CargoEquipment.Select(x => x.InstanceId).Distinct().Count(), Is.EqualTo(2));
        var engine = Ship.CargoEquipment[0];
        int slot = ShipLoadoutRules.FirstComponentSlot;
        Assert.That(hangar.TryChangeLoadout(slot, engine.InstanceId, false, out _), Is.True);
        Assert.That(hangar.TryDebugDamageSelection(slot, null, false, out _), Is.True);
        Assert.That(engine.CurrentDurability, Is.EqualTo(130));
        Assert.That(Ship.CargoEquipment.Single().CurrentDurability, Is.EqualTo(140));
        Assert.That(hangar.GetRepairPrice(HangarRepairScope.Installed, slot), Is.EqualTo(20));
        Assert.That(hangar.GetRepairPrice(HangarRepairScope.FullShip, slot), Is.EqualTo(120));
        Assert.That(hangar.TryChangeLoadout(slot, null, true, out _), Is.True);
        Assert.That(Market.PersonalEquipment.Single(), Is.SameAs(engine));
        Assert.That(_market.Transfer(engine.InstanceId, true, false, 1, "move-engine", -1, out _), Is.True);
        Assert.That(_market.EquipmentPrice(engine, false), Is.EqualTo(130));
        Assert.That(_market.TradeEquipment(engine.InstanceId, false, "sell-engine", -1, out _), Is.True);
        ReloadThroughSaveValidation();
        var sold = Market.Equipment.Single(x => x.Equipment.InstanceId == engine.InstanceId).Equipment;
        Assert.That(sold.CurrentDurability, Is.EqualTo(130));
        Assert.That(sold.MaximumDurability, Is.EqualTo(140));
    }

    [Test] public void LegacyWeaponAndHullCondition_MigratesToUnitsWithoutRestoringWear()
    {
        _session.State.Meta.SaveDataVersion = SaveDataVersions.ModularShipLoadout;
        var oldWeapon = JsonUtility.FromJson<SystemNpcWeaponRuntimeState>(
            "{\"InstanceId\":\"legacy-weapon\",\"WeaponConfigId\":\"ally_weapon\",\"DurabilityPercent\":37}");
        var oldHull = JsonUtility.FromJson<HullEquipmentRuntimeState>(
            "{\"InstanceId\":\"legacy-hull\",\"ConfigId\":\"hull\",\"CargoSize\":3,\"MaximumHull\":120,\"DurabilityPercent\":50,\"HasExactCondition\":true,\"RemainingHull\":57}");
        Ship.CargoEquipment.Add(oldWeapon);
        Ship.CargoHulls.Add(oldHull);
        ReloadThroughSaveValidation();
        Assert.That(Ship.CargoEquipment.Single().CurrentDurability, Is.EqualTo(37));
        Assert.That(Ship.CargoEquipment.Single().MaximumDurability, Is.EqualTo(100));
        Assert.That(Ship.CargoHulls.Single().CurrentDurability, Is.EqualTo(57));
        Assert.That(Ship.CargoHulls.Single().MaximumDurability, Is.EqualTo(120));
        Assert.That(Market.PersonalHulls, Is.Empty);
        Assert.That(_inventory.GetUsedCargo(), Is.EqualTo(_weapon.CargoSize));
    }

    private MarketHullRule EnterHullStation()
    {
        var config = Asset<HullEquipmentConfig>("hull_test"); config.MaximumHull = 120;
        var rule = new MarketHullRule { Hull = config, NewPrice = 200, SpawnIntervalSeconds = 10, LifetimeSeconds = 25, MaximumListings = 1 };
        _profile.GalaxyLevels[0].Hulls.Add(rule); _balance.HullCatalog.Add(rule);
        var station = Asset<StationConfig>("station");
        var stationType = Asset<StationTypeConfig>("station_type");
        Set(stationType, "defaultMarketProfile", _profile);
        Set(station, "stationTypeConfig", stationType);
        Set(_config.System, "station", station);
        _session.State.Galaxy.Systems[0].Station = new StationRuntimeState { ConfigId = "station", Level = 1 };
        _session.State.Player.CurrentPlanetId = ""; _session.State.Player.CurrentStationId = "station";
        Assert.That(_market.CurrentMarket, Is.Not.Null);
        return rule;
    }
    private void ReloadThroughSaveValidation()
    {
        var restored = JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State));
        // Match SaveService2A: migrate/normalize and validate before exposing the loaded session.
        // In particular, JsonUtility materializes an absent Station.Recovery as an empty object.
        var validation = new SaveValidationStage().ValidateAndNormalize(restored);
        Assert.That(validation.IsValid, Is.True, validation.BuildErrorMessage());
        _session.LoadSession(restored);
    }
    [Test] public void HullPurchaseAndSale_PreserveInstanceWearAndReceiptsAcrossReload()
    {
        EnterHullStation(); _market.AdvanceTime(10);
        var hull = Market.Hulls.Single().Hull; string id = hull.InstanceId;
        Assert.That(_market.CanTradeHull(id, true), Is.False, "Hulls are not bought into the hold.");
        Assert.That(_market.CanTradeHull(id, true, true), Is.True);
        Assert.That(_market.TradeHull(id, true, "hull-buy", Market.Revision, out _, true), Is.True);
        Assert.That(Market.PersonalHulls.Single().InstanceId, Is.EqualTo(id));
        Assert.That(Ship.CargoEquipment, Is.Empty);
        hull.CurrentDurability = 60;
        Assert.That(_market.HullPrice(hull, false), Is.EqualTo(100));
        int credits = _session.State.Player.Credits;
        Assert.That(_market.TradeHull(id, false, "hull-sell", Market.Revision, out _, true), Is.True);
        Assert.That(Market.PersonalHulls, Is.Empty);
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits + 100));
        Assert.That(_market.TradeHull(id, false, "hull-sell-again", -1, out _, true), Is.False);
        ReloadThroughSaveValidation();
        Assert.That(StationLocationRules.IsInside(_session.State, _config.System), Is.True);
        Assert.That(_session.State.Galaxy.Systems[0].Station.Recovery, Is.Null);
        Assert.That(Market.Receipts.Count(x => x.OperationId == "hull-sell"), Is.EqualTo(1));
        Assert.That(_market.TradeHull(id, false, "hull-sell", -1, out string replayReason, true), Is.True, replayReason);
        Assert.That(Market.Receipts, Has.Count.EqualTo(2), "A replay must not append a new receipt.");
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits + 100));
        Assert.That(_market.TradeHull(id, true, "hull-buyback", Market.Revision, out string buybackReason, true), Is.True, buybackReason);
        Assert.That(Market.PersonalHulls.Single().CurrentDurability, Is.EqualTo(60));
        Assert.That(Market.PersonalHulls.Single().MaximumDurability, Is.EqualTo(120));
        Assert.That(MarketPersistenceRules.Errors(_session.State), Is.Empty);
    }
    [Test] public void HullReceiptReplay_RejectsChangedIntentAfterValidatedReload()
    {
        EnterHullStation(); _market.AdvanceTime(10);
        string id = Market.Hulls.Single().Hull.InstanceId;
        Assert.That(_market.TradeHull(id, true, "hull-intent", -1, out _, true), Is.True);
        ReloadThroughSaveValidation();
        int credits = _session.State.Player.Credits;
        Assert.That(_market.TradeHull(id, false, "hull-intent", -1, out string reason, true), Is.False);
        Assert.That(reason, Is.EqualTo("OperationConflict"));
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits));
        Assert.That(Market.PersonalHulls.Single().InstanceId, Is.EqualTo(id));
        Assert.That(Market.Receipts, Has.Count.EqualTo(1));
    }
    [Test] public void HullTrade_StillRejectsStationWithRealRecoveryAfterValidatedReload()
    {
        EnterHullStation(); _market.AdvanceTime(10);
        string id = Market.Hulls.Single().Hull.InstanceId;
        var station = _session.State.Galaxy.Systems[0].Station;
        station.IsDestroyed = true; station.Health = 0;
        station.Recovery = new StationOperationRuntimeState { OperationId = "actual-recovery",
            Kind = StationOperationKind.Recovery, TargetLevel = station.Level, DurationSeconds = 60, ElapsedSeconds = 5 };
        ReloadThroughSaveValidation();
        Assert.That(_session.State.Galaxy.Systems[0].Station.Recovery.OperationId, Is.EqualTo("actual-recovery"));
        Assert.That(StationLocationRules.IsInside(_session.State, _config.System), Is.False);
        int credits = _session.State.Player.Credits;
        Assert.That(_market.CanTradeHull(id, true, true), Is.False);
        Assert.That(_market.TradeHull(id, true, "recovery-buy", -1, out _, true), Is.False);
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits));
        Assert.That(_session.State.Markets.Single(x => x.PlanetId == "station:station").Receipts, Is.Empty);
    }
    [Test] public void HullSale_RejectsInstalledLastHullAndWrongLocation_WithoutChangingMoney()
    {
        EnterHullStation(); _market.AdvanceTime(10);
        var hull = Market.Hulls.Single().Hull;
        Assert.That(_market.TradeHull(hull.InstanceId, true, "hull-store", -1, out _, true), Is.True);
        int credits = _session.State.Player.Credits;
        Assert.That(_market.TradeHull(hull.InstanceId, false, "hull-cargo-sale", -1, out _), Is.False);
        _session.State.Player.CurrentStationId = ""; _session.State.Player.CurrentPlanetId = "one";
        Assert.That(_market.TradeHull(hull.InstanceId, false, "hull-planet-sale", -1, out _, true), Is.False);
        _session.State.Player.CurrentStationId = "station"; _session.State.Player.CurrentPlanetId = "";
        Market.PersonalHulls.Remove(hull); Ship.InstalledHull = hull;
        Assert.That(_market.TradeHull(hull.InstanceId, false, "hull-last", -1, out _, true), Is.False);
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits));
        // Even a corrupt duplicate reference must not let the installed hull be sold.
        Market.PersonalHulls.Add(hull);
        Assert.That(_market.CanTradeHull(hull.InstanceId, false, true), Is.False);
        Assert.That(MarketPersistenceRules.Errors(_session.State), Does.Contain("InvalidStoredHull"));
    }
    [Test] public void PlanetHullOffers_StartInStock_PersistAndReplenishAfterPurchase()
    {
        var config = Asset<HullEquipmentConfig>("planet-hull");
        config.MaximumHull = 120;
        _profile.GalaxyLevels[0].Hulls.Add(new MarketHullRule
        {
            Hull = config, NewPrice = 200, StartInStock = true, SpawnIntervalSeconds = 10, LifetimeSeconds = 25
        });

        ReloadThroughSaveValidation();
        var hull = Market.Hulls.Single().Hull;
        _session.State.Player.CurrentPlanetId = "two";
        Assert.That(Market.Hulls.Single().Hull.InstanceId, Is.Not.EqualTo(hull.InstanceId));
        _session.State.Player.CurrentPlanetId = "one";
        ReloadThroughSaveValidation();
        Assert.That(Market.Hulls.Single().Hull.InstanceId, Is.EqualTo(hull.InstanceId));

        int credits = _session.State.Player.Credits;
        Assert.That(_market.TradeHull(hull.InstanceId, true, "planet-hull-buy", Market.Revision, out var reason, true), Is.True, reason);
        Assert.That(Market.PersonalHulls.Single().InstanceId, Is.EqualTo(hull.InstanceId));
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits - 200));
        Assert.That(_market.CanTradeHull(hull.InstanceId, false, true), Is.False);
        ReloadThroughSaveValidation();
        Assert.That(Market.Hulls, Is.Empty);
        _market.AdvanceTime(10);
        Assert.That(Market.Hulls.Single().Hull.InstanceId, Is.Not.EqualTo(hull.InstanceId));
    }

    [Test] public void HullListings_ExpireButPersonalHullsDoNot()
    {
        var rule = EnterHullStation(); _market.AdvanceTime(10);
        var hull = Market.Hulls.Single().Hull;
        Assert.That(_market.TradeHull(hull.InstanceId, true, "keep-hull", -1, out _, true), Is.True);
        _market.AdvanceTime(10); string publicId = Market.Hulls.Single().Hull.InstanceId;
        rule.MaximumListings = 0; _market.AdvanceTime(30);
        Assert.That(Market.Hulls.Any(x => x.Hull.InstanceId == publicId), Is.False);
        Assert.That(Market.PersonalHulls.Single().InstanceId, Is.EqualTo(hull.InstanceId));
        _session.State.Player.CurrentStationId = ""; _session.State.Player.CurrentPlanetId = "one";
        Assert.That(Market.Hulls, Is.Empty);
    }
    [Test] public void ProductionHullAssets_AreBoundToEveryLevelAndBalanceCatalog()
    {
        var balance = AssetDatabase.LoadAssetAtPath<MarketEconomyBalanceConfig>("Assets/_Project/Content/Configs/Game/marketEconomyBalance_01.asset");
        Assert.That(balance.HullCatalog.Any(x => x.Hull != null && x.NewPrice > 0), Is.True);
        foreach (var name in new[] { "hub", "mining", "industrial", "frontier" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<MarketProfileConfig>("Assets/_Project/Content/Configs/MarketProfiles/mMarketProfile_" + name + "_01.asset");
            Assert.That(profile.GalaxyLevels, Has.Count.EqualTo(10));
            foreach (var level in profile.GalaxyLevels)
                Assert.That(level.Hulls.Any(x => x.Hull != null && x.NewPrice > 0 && x.LifetimeSeconds > 0), Is.True);
        }
    }
    private SystemNpcWeaponRuntimeState CargoWeapon(bool enemy = false, int durability = 0)
    { var item = new SystemNpcWeaponRuntimeState { InstanceId = "instance", WeaponConfigId = _weapon.Id,
        CurrentDurability = durability, MaximumDurability = 100, RolledStats = new WeaponRuntimeStats { WeaponConfigId = _weapon.Id, CargoSize = 2, Damage = 19 },
        HasLootOrigin = true, LootOriginNpcType = enemy ? SystemNpcType.Enemy : SystemNpcType.Ally };
      Ship.CargoEquipment.Add(item); return item; }
    [Test] public void EquipmentWearAndResearchFloor_AreAppliedToSpecificInstance()
    { var item = CargoWeapon(); Assert.That(_market.EquipmentPrice(item, false), Is.EqualTo(10));
      _session.State.Galaxy.Research.Levels.Add(new ResearchUpgradeLevel { Upgrade = ResearchUpgrade.EquipmentBuyout, Level = 1 });
      Assert.That(_market.EquipmentPrice(item, false), Is.EqualTo(50)); item.CurrentDurability = 80; Assert.That(_market.EquipmentPrice(item, false), Is.EqualTo(80)); }
    [Test] public void AlliedEquipment_BuybackPreservesIdentityRollsAndWear_AndCannotSellTwice()
    {
        var item = CargoWeapon(false, 35); int credits = _session.State.Player.Credits;
        Assert.That(_market.TradeEquipment(item.InstanceId, false, "sell", Market.Revision, out _), Is.True);
        Assert.That(_market.TradeEquipment(item.InstanceId, false, "duplicate", -1, out _), Is.False);
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        Assert.That(_market.TradeEquipment(item.InstanceId, true, "buyback", Market.Revision, out _), Is.True);
        var restored = Ship.CargoEquipment.Single(); Assert.That(restored.InstanceId, Is.EqualTo("instance"));
        Assert.That(restored.CurrentDurability, Is.EqualTo(35)); Assert.That(restored.RolledStats.Damage, Is.EqualTo(19));
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits));
        Assert.That(_market.TradeEquipment(item.InstanceId, true, "buyback", -1, out _), Is.True); Assert.That(Ship.CargoEquipment.Count, Is.EqualTo(1));
    }
    [Test] public void EnemyEquipmentSale_PaysButDoesNotCreateBuybackListing()
    { var item = CargoWeapon(true); Assert.That(_market.TradeEquipment(item.InstanceId, false, "sell", -1, out _), Is.True);
      Assert.That(Market.Equipment, Is.Empty); Assert.That(_market.TradeEquipment(item.InstanceId, true, "buy", -1, out _), Is.False); }
    [Test] public void SoldEquipmentExpires_AndGeneratedEquipmentRespectsCapacity()
    { var item = CargoWeapon(); Assert.That(_market.TradeEquipment(item.InstanceId, false, "sell", -1, out _), Is.True);
      _market.AdvanceTime(20); Assert.That(Market.Equipment.Count, Is.EqualTo(1));
      _market.AdvanceTime(5); Assert.That(Market.Equipment.Any(x => x.Equipment.InstanceId == item.InstanceId), Is.False);
      _market.AdvanceTime(5); Assert.That(Market.Equipment.Count, Is.EqualTo(1)); }
    [Test] public void EquipmentPurchase_RejectsInsufficientCargoWithoutSpending()
    { _market.AdvanceTime(10); var item = Market.Equipment.Single().Equipment; Ship.CargoCapacity = 0; int credits = _session.State.Player.Credits;
      Assert.That(_market.TradeEquipment(item.InstanceId, true, "buy", -1, out _), Is.False); Assert.That(_session.State.Player.Credits, Is.EqualTo(credits)); }

    [Test] public void PartialRepair_UsesLiveBalanceAndSavesOnce_RejectsReentry()
    { int price = 3, saves = 0; var repair = new RepairService(_session, () => price, _events);
      _events.Subscribe<CreditsChangedEvent>(_ => Assert.That(repair.Repair(1).IsSuccess, Is.False)); _events.Subscribe<SaveNeedEvent>(_ => saves++);
      var result = repair.Repair(7); Assert.That(result.totalPrice, Is.EqualTo(21)); Assert.That(Ship.CurrentHull, Is.EqualTo(57));
      price = 5; Assert.That(repair.GetRepairUnitPrice(), Is.EqualTo(5)); Assert.That(saves, Is.EqualTo(1)); }
    [Test] public void PartialFuel_UsesLiveBalanceAndSurvivesThrowingObserver()
    { int price = 4, saves = 0; var fuel = new RefuelService(_session, _events, new RefuelCostProvider(() => price), new FuelAvailable());
      _events.Subscribe<FuelChangedEvent>(_ => throw new Exception("observer")); _events.Subscribe<SaveNeedEvent>(_ => saves++);
      Assert.That(fuel.Refuel(3).totalPrice, Is.EqualTo(12)); Assert.That(Ship.CurrentFuel, Is.EqualTo(13));
      price = 7; Assert.That(fuel.GetFuelUnitPrice(), Is.EqualTo(7)); Assert.That(saves, Is.EqualTo(1)); }
    [Test] public void LegacyMigration_PreservesStocksAndInitializesCollectionsIdempotently()
    { Stock.Quantity = 137; Market.Equipment = null; Market.Receipts = null; _session.State.Meta.SaveDataVersion = SaveDataVersions.StationEffectsAndResearch;
      SaveMigrationService.Migrate(_session.State); Assert.That(Stock.Quantity, Is.EqualTo(137)); Assert.That(Market.Equipment, Is.Not.Null);
      string once = JsonUtility.ToJson(_session.State); SaveMigrationService.Migrate(_session.State); Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(once)); }
    [TestCase(14, 0)] [TestCase(14, 100)] [TestCase(15, 0)] [TestCase(15, 100)]
    public void EmptyInstalledHullPlaceholder_CorePayloadLoadsWithoutChangingExistingShip(int version, int defaultDurability)
    {
        _session.State.Meta.SaveDataVersion = version;
        Ship.CurrentHull = 268; Ship.HullCapacity = 268;
        Ship.InstalledHull = JsonUtility.FromJson<HullEquipmentRuntimeState>("{\"DurabilityPercent\":" + defaultDurability + "}");
        var payload = new SaveCorePartData { Meta = _session.State.Meta, Player = _session.State.Player, Markets = _session.State.Markets };
        var restored = JsonUtility.FromJson<SaveCorePartData>(JsonUtility.ToJson(payload));
        _session.State.Meta = restored.Meta; _session.State.Player = restored.Player; _session.State.Markets = restored.Markets;
        Assert.That(MarketPersistenceRules.Errors(_session.State), Does.Not.Contain("InvalidInstalledHull"));
        var validation = new SaveValidationStage().ValidateAndNormalize(_session.State);
        Assert.That(validation.Errors, Does.Not.Contain("InvalidInstalledHull"));
        Assert.That(Ship.InstalledHull, Is.Null);
        Assert.That(Ship.CurrentHull, Is.EqualTo(268)); Assert.That(Ship.HullCapacity, Is.EqualTo(268));
        Assert.That(_session.State.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
        Assert.That(Market.PersonalHulls, Is.Empty);
        Assert.That(Market.Hulls, Is.Empty);
        string once = JsonUtility.ToJson(_session.State);
        SaveMigrationService.Migrate(_session.State);
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(once));
    }
    [Test] public void RealInstalledHull_SurvivesCorePayloadAndStillRejectsDuplicateStoredInstance()
    {
        EnterHullStation();
        _session.State.Meta.SaveDataVersion = SaveDataVersions.ModularShipLoadout;
        Ship.InstalledHull = JsonUtility.FromJson<HullEquipmentRuntimeState>(
            "{\"InstanceId\":\"installed-hull\",\"ConfigId\":\"hull_test\",\"CargoSize\":7,\"MaximumHull\":120,\"DurabilityPercent\":63}");
        var restored = JsonUtility.FromJson<SaveCorePartData>(JsonUtility.ToJson(new SaveCorePartData {
            Meta = _session.State.Meta, Player = _session.State.Player, Markets = _session.State.Markets }));
        _session.State.Meta = restored.Meta; _session.State.Player = restored.Player; _session.State.Markets = restored.Markets;
        SaveMigrationService.Migrate(_session.State);
        Assert.That(Ship.InstalledHull.InstanceId, Is.EqualTo("installed-hull"));
        Assert.That(Ship.InstalledHull.CurrentDurability, Is.EqualTo(76));
        Assert.That(MarketPersistenceRules.Errors(_session.State), Is.Empty);
        Market.PersonalHulls.Add(Ship.InstalledHull);
        Assert.That(MarketPersistenceRules.Errors(_session.State), Does.Contain("InvalidStoredHull"));
        Assert.That(_market.CanTradeHull("installed-hull", false, true), Is.False);
    }
    [TestCase("id")] [TestCase("config")] [TestCase("size")] [TestCase("hull")] [TestCase("durability")]
    public void PartialInstalledHull_IsNotDiscardedAsAnEmptyPlaceholder(string payload)
    {
        var hull = new HullEquipmentRuntimeState();
        if (payload == "id") hull.InstanceId = "real-id";
        if (payload == "config") hull.ConfigId = "real-config";
        if (payload == "size") Set(hull, "CargoSize", 7);
        if (payload == "hull") hull.MaximumDurability = 120;
        if (payload == "durability") hull.CurrentDurability = 63;
        Ship.InstalledHull = hull;
        MarketPersistenceRules.Normalize(_session.State);
        Assert.That(Ship.InstalledHull, Is.SameAs(hull));
        Assert.That(MarketPersistenceRules.Errors(_session.State), Does.Contain("InvalidInstalledHull"));
    }
    [Test] public void EmptyStoredHull_RemainsInvalidAfterOptionalSlotNormalization()
    {
        EnterHullStation(); Market.PersonalHulls.Add(new HullEquipmentRuntimeState());
        MarketPersistenceRules.Normalize(_session.State);
        Assert.That(Market.PersonalHulls, Has.Count.EqualTo(1));
        Assert.That(MarketPersistenceRules.Errors(_session.State), Does.Contain("InvalidStoredHull"));
    }
    [Test] public void RealCoreSavePayload_RoundTripsStockReceiptsWearAndOfflineWatermark()
    {
        CargoWeapon(false, 27);
        Assert.That(_market.TradeEquipment("instance", false, "sell", -1, out _), Is.True);
        Assert.That(_market.Trade(_item.Id, 1, true, "goods").Success, Is.True);
        _session.State.Meta.MarketOfflineProcessedUtc = 123456;
        var payload = new SaveCorePartData { Meta = _session.State.Meta, Player = _session.State.Player, Markets = _session.State.Markets };
        var restored = JsonUtility.FromJson<SaveCorePartData>(JsonUtility.ToJson(payload));
        _session.State.Meta = restored.Meta; _session.State.Player = restored.Player; _session.State.Markets = restored.Markets;
        Assert.That(Stock.Quantity, Is.EqualTo(49)); Assert.That(Market.Receipts.Count, Is.EqualTo(2));
        Assert.That(Market.Equipment.Single().Equipment.CurrentDurability, Is.EqualTo(27));
        Assert.That(_session.State.Meta.MarketOfflineProcessedUtc, Is.EqualTo(123456));
        Assert.That(_market.Trade(_item.Id, 1, true, "goods").Success, Is.True); Assert.That(Stock.Quantity, Is.EqualTo(49));
    }
    [Test] public void ValidationRejectsDuplicateEquipmentBetweenMarketAndCargo()
    { var item = CargoWeapon(); Market.Equipment.Add(new MarketEquipmentListing { Equipment = item, NewPrice = 100, ExpiresAtSeconds = 10 });
      Assert.That(MarketPersistenceRules.Errors(_session.State), Does.Contain("InvalidMarketEquipment")); }
    [Test] public void MarketAndEconomyLogging_RespectChannelAndMasterSwitch()
    { var logs = ScriptableObject.CreateInstance<DebugLogConfig>(); _objects.Add(logs);
      Set(logs, "logsEnabled", true); Set(logs, "marketLogs", false); Assert.That(logs.IsEnabled(DebugLogChannel.Market), Is.False);
      Set(logs, "marketLogs", true); Assert.That(logs.IsEnabled(DebugLogChannel.Market), Is.True);
      Set(logs, "economyLogs", true); Set(logs, "logsEnabled", false);
      Assert.That(logs.IsEnabled(DebugLogChannel.Market), Is.False); Assert.That(logs.IsEnabled(DebugLogChannel.Economy), Is.False); }
    [Test] public void ProductionAssets_AreBoundToTenLevelProfilesAndTwoResearchNodes()
    {
        var balance = AssetDatabase.LoadAssetAtPath<MarketEconomyBalanceConfig>("Assets/_Project/Content/Configs/Market/marketEconomyBalance_01.asset");
        Assert.That(balance, Is.Not.Null); Assert.That(balance.ResearchLevels.Length, Is.EqualTo(11));
        foreach (string id in AssetDatabase.FindAssets("t:MarketProfileConfig", new[] { "Assets/_Project/Content/Configs/MarketProfiles" })) {
            var profile = AssetDatabase.LoadAssetAtPath<MarketProfileConfig>(AssetDatabase.GUIDToAssetPath(id));
            Assert.That(profile.GalaxyLevels.Count, Is.EqualTo(10));
            for (int level = 1; level <= 10; level++) { var settings = profile.GetLevel(level); Assert.That(settings.GalaxyLevel, Is.EqualTo(level));
                Assert.That(settings.IntervalSeconds, Is.GreaterThan(0)); Assert.That(settings.Items.All(x => x.ItemConfig != null && x.MaximumStock > 0 && x.MaximumPrice >= x.BasePrice), Is.True); }
        }
        const string tradePath = "Assets/_Project/Content/Configs/Research/research_branch_trade.asset";
        var trade = AssetDatabase.LoadAssetAtPath<ResearchBranchConfig>(tradePath);
        Assert.That(trade, Is.Not.Null, "Trade research branch must be imported: " + tradePath);
        var loadedIds = (trade.Nodes ?? Array.Empty<ResearchNode>()).Where(x => x != null).Select(x => x.Upgrade).ToArray();
        Assert.That(loadedIds, Does.Contain(ResearchUpgrade.EconomyWarProtection),
            "Trade branch is missing EconomyWarProtection (17). Loaded IDs: " + string.Join(", ", loadedIds));
        Assert.That(loadedIds, Does.Contain(ResearchUpgrade.EquipmentBuyout),
            "Trade branch is missing EquipmentBuyout (18). Loaded IDs: " + string.Join(", ", loadedIds));
        var tree = AssetDatabase.LoadAssetAtPath<ResearchTreeConfig>("Assets/_Project/Content/Configs/Research/research_tree_galaxy.asset");
        Assert.That(tree, Is.Not.Null, "Production research tree must be imported.");
        Assert.That(tree.Branches, Does.Contain(trade), "Production tree must reference the trade branch asset.");
        Assert.That(ResearchRules.Errors(tree), Is.Empty);
        Assert.That(ResearchRules.Find(tree, ResearchUpgrade.EconomyWarProtection), Is.Not.Null,
            "Production tree must expose EconomyWarProtection (17).");
        Assert.That(ResearchRules.Find(tree, ResearchUpgrade.EquipmentBuyout), Is.Not.Null,
            "Production tree must expose EquipmentBuyout (18).");
    }

    [Test] public void ProductionInhabitedPlanets_HaveUsableMarketProfiles()
    {
        var ids = AssetDatabase.FindAssets("t:PlanetConfig", new[] { "Assets/_Project/Content/Configs/Galaxy/Planets" });
        int inhabited = 0;
        foreach (string id in ids)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            var planet = AssetDatabase.LoadAssetAtPath<PlanetConfig>(path);
            Assert.That(planet, Is.Not.Null, path);
            if (!planet.IsInhabited) continue;
            inhabited++;
            Assert.That(planet.MarketProfile, Is.Not.Null, "Inhabited planet has no market profile: " + path);
            for (int level = 1; level <= 10; level++)
            {
                var settings = planet.MarketProfile.GetLevel(level);
                Assert.That(settings, Is.Not.Null, path + " | Galaxy level=" + level);
                Assert.That(settings.Items.Any(x => x != null && x.IsAvailable && x.ItemConfig != null), Is.True,
                    "Inhabited planet has no available market goods: " + path + " | Galaxy level=" + level);
            }
        }
        Assert.That(inhabited, Is.GreaterThan(0), "No inhabited production planets were checked.");
    }

    [Test] public void OfflineCoordinator_AppliesProtectionOnlyAfterResearchCompletion()
    {
        Entry.StockChange = 10;
        _session.State.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Invasion;
        _session.State.Galaxy.Research.ActiveUpgrade = ResearchUpgrade.EconomyWarProtection;
        _session.State.Galaxy.Research.Active = new StationOperationRuntimeState {
            OperationId = "protection", TargetLevel = 1, DurationSeconds = 10, ElapsedSeconds = 0 };
        long from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        _session.State.Meta.LastSaveUtc = from;
        var infrastructure = new Science();
        var research = new ResearchService(null, _session, _config, infrastructure, null, _events);
        var stations = new StationService(_session, _config.GetStarSystemConfigById, _events,
            infrastructure: infrastructure, research: research, market: _market);
        stations.ProcessOffline(_session.State, from + 20 * TimeSpan.TicksPerSecond);
        Assert.That(Stock.Quantity, Is.EqualTo(65), "First interval +5 during war; second interval +10 after protection");
        Assert.That(_session.State.Galaxy.Research.GetLevel(ResearchUpgrade.EconomyWarProtection), Is.EqualTo(1));
        string once = JsonUtility.ToJson(_session.State);
        stations.ProcessOffline(_session.State, from + 20 * TimeSpan.TicksPerSecond);
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(once));
    }
    [Test] public void MarketReceiptCannotBeReusedOnAnotherPlanet()
    {
        Assert.That(_market.Trade(_item.Id, 1, true, "same-id").Success, Is.True);
        _session.State.Player.CurrentPlanetId = "two";
        Assert.That(_market.Trade(_item.Id, 1, true, "same-id").ErrorCode, Is.EqualTo(EconomyErrorCode.OperationConflict));
        Assert.That(Stock.Quantity, Is.EqualTo(50));
    }
    [Test] public void InvalidTimeAndClockRollback_DoNotAdvanceMarket()
    {
        long from = DateTime.UtcNow.Ticks; _session.State.Meta.LastSaveUtc = from;
        string before = JsonUtility.ToJson(_session.State);
        _market.AdvanceTime(double.NaN); _market.AdvanceTime(double.PositiveInfinity); _market.AdvanceTime(-1);
        _market.ProcessOffline(from - TimeSpan.TicksPerSecond);
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(before));
    }
    private StarSystemConfig AddDeferredSystem()
    {
        var planetType = Asset<PlanetTypeConfig>("remote_planet_type"); Set(planetType, "marketProfile", _profile);
        var planet = Asset<PlanetConfig>("remote_planet"); Set(planet, "planetTypeConfig", planetType);
        var remote = Asset<StarSystemConfig>("remote"); Set(remote, "planetRefs", new[] { planet });
        _config.Systems = new[] { _config.System, remote };
        _session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = remote.Id });
        _market = new PlanetMarketService(_session, _config, _inventory, _events);
        _market.InitializeSession();
        return remote;
    }
    private MarketRuntimeData RemoteMarket => _session.State.Markets.Single(x => x.SystemId == "remote" && x.PlanetId == "remote_planet");

    [Test] public void OnlineMarket_UsesCurrentSystemAndSharedMovementSelectionOnly()
    {
        var remote = AddDeferredSystem();
        var schedule = new Schedule { Current = _config.System };
        _market.AdvanceScheduledTime(10, 71, schedule);
        Assert.That(Stock.Quantity, Is.EqualTo(57));
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(50), "Unselected system must not be recalculated.");
        Assert.That(RemoteMarket.LastProcessedSimulationSeconds, Is.Zero);
        schedule.Selected.Add(remote);
        _market.AdvanceScheduledTime(10, 72, schedule);
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(64), "Selection catches up the full 20 seconds, not only the last frame.");
        Assert.That(RemoteMarket.LastProcessedSimulationSeconds, Is.EqualTo(20));
        Assert.That(schedule.LastTick, Is.EqualTo(72)); Assert.That(schedule.SelectionCalls, Is.EqualTo(2));
        Assert.That(_market.LastScheduledSystemCount, Is.EqualTo(2));
    }
    [Test] public void RepeatedOnlineFrames_DoNotRebuildWorldOrWalkEquipmentBeforeDeadline()
    {
        AddDeferredSystem();
        var schedule = new Schedule { Current = _config.System };
        int scans = _config.SystemListReads;
        for (int i = 0; i < 100; i++)
        {
            _market.AdvanceScheduledTime(0.01, 1, schedule);
            Assert.That(_market.LastEquipmentUpdateCount, Is.Zero);
            Assert.That(_market.LastProcessedMarketCount, Is.EqualTo(2), "Only two current-system planets are selected.");
        }
        Assert.That(_config.SystemListReads, Is.EqualTo(scans), "No steady-state full-world configuration enumeration.");
        Assert.That(RemoteMarket.LastProcessedSimulationSeconds, Is.Zero);
    }
    [Test] public void EnteringDeferredSystem_CatchesUpBeforeMarketQuote()
    {
        AddDeferredSystem();
        _market.AdvanceScheduledTime(20, 1, new Schedule { Current = _config.System });
        _session.State.Player.CurrentSystemId = "remote"; _session.State.Player.CurrentPlanetId = "remote_planet";
        Assert.That(_market.GetPrice(_item.Id, true), Is.EqualTo(MarketEconomyRules.Price(Entry, 64)));
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(64));
        Assert.That(RemoteMarket.LastProcessedSimulationSeconds, Is.EqualTo(20));
    }
    [Test] public void DeferredTime_SurvivesReloadAndAddsOfflineTimeExactlyOnce()
    {
        AddDeferredSystem();
        _market.AdvanceScheduledTime(10, 1, new Schedule { Current = _config.System });
        long from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        _session.State.Meta.LastSaveUtc = from;
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        _market.ProcessOffline(from + 10 * TimeSpan.TicksPerSecond);
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(64));
        Assert.That(_session.State.Meta.MarketSimulationSeconds, Is.EqualTo(20));
        _market.ProcessOffline(from + 10 * TimeSpan.TicksPerSecond);
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(64));
    }
    [Test] public void EquipmentPendingTime_SurvivesSaveAndSpawnsAtOriginalDeadline()
    {
        var schedule = new Schedule { Current = _config.System };
        _market.AdvanceScheduledTime(9, 1, schedule);
        Assert.That(Market.Equipment, Is.Empty); Assert.That(Market.EquipmentPendingSeconds, Is.EqualTo(9));
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        _market.AdvanceScheduledTime(1, 2, schedule);
        Assert.That(Market.Equipment.Count, Is.EqualTo(1));
        Assert.That(Market.EquipmentPendingSeconds, Is.Zero);
        Assert.That(Market.Equipment.Single().ExpiresAtSeconds, Is.EqualTo(35));
    }
    [Test] public void SharedSelectionContainingCurrentSystem_DoesNotDoubleAdvanceIt()
    {
        var schedule = new Schedule { Current = _config.System }; schedule.Selected.Add(_config.System);
        _market.AdvanceScheduledTime(10, 1, schedule);
        Assert.That(Stock.Quantity, Is.EqualTo(57)); Assert.That(Market.ElapsedSeconds, Is.EqualTo(10));
        Assert.That(_market.LastScheduledSystemCount, Is.EqualTo(1));
    }
    [Test] public void DeferredProtectionCompletion_SettlesOldRateBeforeNewEffect()
    {
        AddDeferredSystem(); Entry.StockChange = 10;
        _session.State.Galaxy.Systems.Single(x => x.SystemId == "remote").SystemStatus = StarSystemStatus.Invasion;
        _market.AdvanceScheduledTime(10, 1, new Schedule { Current = _config.System });
        _session.State.Galaxy.Research.Levels.Add(new ResearchUpgradeLevel { Upgrade = ResearchUpgrade.EconomyWarProtection, Level = 1 });
        _events.Publish(new ResearchCompletedEvent(ResearchUpgrade.EconomyWarProtection, 1));
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(55));
        _market.AdvanceTime(10);
        Assert.That(RemoteMarket.Items[0].Quantity, Is.EqualTo(65));
    }
    private sealed class Schedule : IGalaxyNpcSimulationScheduleService
    {
        public StarSystemConfig Current;
        public readonly List<StarSystemConfig> Selected = new List<StarSystemConfig>();
        public int SelectionCalls, LastTick;
        public IReadOnlyList<StarSystemConfig> GetScheduledOffscreenSystems(int quantTick) { SelectionCalls++; LastTick = quantTick; return Selected; }
        public IReadOnlyList<StarSystemConfig> GetInitialWarmupOffscreenSystems() => Selected;
        public StarSystemConfig GetCurrentSystem() => Current;
        public string CurrentSystemId => Current?.Id;
        public string PrioritySourceSystemId => Current?.Id;
        public bool IsInitialWarmupComplete => true;
        public void CompleteInitialWarmup() { }
    }

    [Test] public void WarehousePurchase_DoesNotNeedCargoSpace_AndReceiptBindsDestination()
    {
        Ship.CargoCapacity = 0;
        Assert.That(_market.CanTrade(_item.Id, 1, true, true), Is.True);
        Assert.That(_market.Trade(_item.Id, 1, true, "warehouse-buy", -1, true).Success, Is.True);
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(1));
        Assert.That(Ship.Cargo.GetQuantity(_item.Id), Is.Zero);
        Assert.That(_market.Trade(_item.Id, 1, true, "warehouse-buy", -1, false).ErrorCode, Is.EqualTo(EconomyErrorCode.OperationConflict));
    }
    [Test] public void PersonalWarehouse_IsSeparateFromSupplyAndOtherPlanets()
    {
        Assert.That(_market.Trade(_item.Id, 1, true, "store", -1, true).Success, Is.True);
        _market.AdvanceTime(10);
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(1));
        Assert.That(Stock.Quantity, Is.EqualTo(56));
        _session.State.Player.CurrentPlanetId = "two";
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.Zero);
        _session.State.Player.CurrentPlanetId = "one";
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(1));
    }
    [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
    public void TransferButton_MovesSelectedGoodsQuantity_AndRejectsWholeBatchWhenHoldIsTooSmall(bool fromWarehouse, bool insufficientSpace)
    {
        var source = fromWarehouse ? Market.PersonalStorage : Ship.Cargo;
        var target = fromWarehouse ? Ship.Cargo : Market.PersonalStorage;
        source.AddLootItem(_item.Id, 9);
        _session.State.Player.Credits = int.MaxValue; // Transfers must work even when a sale would overflow the wallet.
        if (insufficientSpace) Ship.CargoCapacity = 6; // Four units require eight cargo slots.
        int stock = Stock.Quantity;
        var root = new GameObject("MarketTransferFixture", typeof(RectTransform));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2560);
            var controller = root.AddComponent<MarketScreenController>();
            var view = root.AddComponent<MarketPanelView2A>();
            var transfer = typeof(MarketScreenController).GetMethod("Transfer", BindingFlags.Instance | BindingFlags.NonPublic);
            view.Build(null, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, () => { }, () => transfer.Invoke(controller, null));
            Set(controller, "_market", _market); Set(controller, "_session", _session);
            Set(controller, "_config", _config); Set(controller, "_events", _events);
            Set(controller, "_view", view); Set(controller, "_initialized", true);
            controller.Refresh();
            Set(controller, "_warehouse", fromWarehouse); controller.Refresh();
            Set(controller, "_rightKey", "goods:" + _item.Id); Set(controller, "_saleQuantity", 4);
            controller.Refresh();
            var button = root.transform.Find("SharedMarketLayout/MarketWorkspace/PersonalInventory/Transfer").GetComponent<UnityEngine.UI.Button>();
            Assert.That(button.interactable, Is.EqualTo(!insufficientSpace));
            // Invoke the real button callback, including the backend rejection if the UI is bypassed.
            button.onClick.Invoke();
            Assert.That(source.GetQuantity(_item.Id), Is.EqualTo(insufficientSpace ? 9 : 5));
            Assert.That(target.GetQuantity(_item.Id), Is.EqualTo(insufficientSpace ? 0 : 4));
            Assert.That(_session.State.Player.Credits, Is.EqualTo(int.MaxValue));
            Assert.That(Stock.Quantity, Is.EqualTo(stock));
            if (insufficientSpace) Assert.That(Market.Receipts, Is.Empty);
            else Assert.That(Market.Receipts.Single().Quantity, Is.EqualTo(4));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void TransferGoods_DoesNotChangeMoneyOrMarketStock_AndCannotReplay()
    {
        Ship.Cargo.AddItem(_item.Id, 2, Ship.CargoCapacity); int credits = _session.State.Player.Credits;
        Assert.That(_market.Transfer(_item.Id, false, true, 2, "deposit", -1, out _), Is.True);
        Assert.That(Ship.Cargo.GetQuantity(_item.Id), Is.Zero); Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(2));
        Assert.That(_market.Transfer(_item.Id, false, true, 2, "deposit", -1, out _), Is.True);
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(2));
        Ship.CargoCapacity = 1;
        Assert.That(_market.Transfer(_item.Id, false, false, 1, "reject-withdraw", -1, out _), Is.False);
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(2));
        Ship.CargoCapacity = 20;
        Assert.That(_market.Transfer(_item.Id, false, false, 1, "withdraw", -1, out _), Is.True);
        Assert.That(_session.State.Player.Credits, Is.EqualTo(credits)); Assert.That(Stock.Quantity, Is.EqualTo(50));
        Assert.That(MarketPersistenceRules.Errors(_session.State), Is.Empty);
    }
    [Test] public void PersonalEquipment_SurvivesReloadAndMarketExpiry_WithIdentityAndWear()
    {
        var equipment = CargoWeapon(false, 17);
        Assert.That(_market.Transfer(equipment.InstanceId, true, true, 1, "store-equipment", -1, out _), Is.True);
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        _market.AdvanceTime(100);
        Assert.That(Market.PersonalEquipment.Single().CurrentDurability, Is.EqualTo(17));
        Assert.That(_market.Transfer(equipment.InstanceId, true, false, 1, "take-equipment", -1, out _), Is.True);
        Assert.That(Ship.CargoEquipment.Single().InstanceId, Is.EqualTo(equipment.InstanceId));
        Assert.That(Ship.CargoEquipment.Single().RolledStats.Damage, Is.EqualTo(19));
        Assert.That(Market.PersonalEquipment, Is.Empty);
    }
    [Test] public void EquipmentCanBeBoughtAndSoldFromWarehouseWithoutCargoCapacity()
    {
        _market.AdvanceTime(10); Ship.CargoCapacity = 0;
        var equipment = Market.Equipment.Single().Equipment;
        Assert.That(_market.CanTradeEquipment(equipment.InstanceId, true, true), Is.True);
        Assert.That(_market.TradeEquipment(equipment.InstanceId, true, "buy-store-equipment", -1, out _, true), Is.True);
        Assert.That(Market.PersonalEquipment.Count, Is.EqualTo(1)); Assert.That(Ship.CargoEquipment, Is.Empty);
        Assert.That(_market.TradeEquipment(equipment.InstanceId, false, "sell-store-equipment", -1, out _, true), Is.True);
        Assert.That(Market.PersonalEquipment, Is.Empty); Assert.That(Market.Equipment.Single().Equipment.InstanceId, Is.EqualTo(equipment.InstanceId));
    }
    [Test] public void MissionOwnedGoods_CannotBeMovedIntoPersonalWarehouse()
    {
        Ship.Cargo.entries.Add(new RuntimeCargoEntryData(_item.Id, 2) { MissionRuntimeId = "mission" });
        Assert.That(_market.Transfer(_item.Id, false, true, 1, "mission-transfer", -1, out _), Is.False);
        Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.Zero);
    }
    [Test] public void SellingStoredGoods_DoesNotTakeSameGoodsFromCargo()
    {
        Ship.Cargo.AddItem(_item.Id, 3, Ship.CargoCapacity); Market.PersonalStorage.AddLootItem(_item.Id, 2);
        Assert.That(_market.Trade(_item.Id, 1, false, "sell-from-store", -1, true).Success, Is.True);
        Assert.That(Ship.Cargo.GetQuantity(_item.Id), Is.EqualTo(3)); Assert.That(Market.PersonalStorage.GetQuantity(_item.Id), Is.EqualTo(1));
        Assert.That(Stock.Quantity, Is.EqualTo(51));
    }

    private sealed class Science : IStationInfrastructureService
    {
        public void InitializeSession() { }
        public float GetRangerAttackProbability(string id, out bool configured) { configured = false; return 0; }
        public double GetScienceRate() => 1;
        public bool CanTravel(SystemNpcRuntimeState npc, string id) => true;
        public void AdvanceTime(double seconds) { } public void CompleteTick(int tick) { }
        public void AdvanceOfflineRepair(double seconds) { }
        public double NextRepairBoundary(double seconds) => seconds;
        public void RelocateStationMilitary() { }
    }

    private sealed class Session : IGameSessionService
    { public GameRuntimeState State { get; private set; } public bool HasActiveSession => State != null;
      public void StartNewSession(GameRuntimeState state) => State = state; public void LoadSession(GameRuntimeState state) => State = state; public void ClearSession() => State = null; }
    private sealed class FuelAvailable : IRefuelAvailabilityProvider
    { public bool IsRefuelAvailable() => true; }
    private sealed class Configs : IConfigService
    {
        public MarketEconomyBalanceConfig MarketEconomy { get; set; }
        public MissionGameplayConfig MissionGameplay => null;
        public GameTimeConfig GameTimeConfig => null;
        public readonly List<AllyConfig> Hulls = new List<AllyConfig>();
        public readonly List<ShipComponentConfig> Components = new List<ShipComponentConfig>();
        public StarSystemConfig System; public StarSystemConfig[] Systems; public int SystemListReads; public ItemConfig Item; public WeaponConfig Weapon; public PlanetConfig[] Planets;
        public DebugSettings DebugSettings => null; public SaveConfig SaveConfig => null; public GalaxyConfig GalaxyConfig => null; public NewGameConfig NewGameConfig => null;
        public PlayerControlConfig PlayerControlConfig => null; public ShipMovementConfig ShipMovementConfig => null; public SystemCameraConfig SystemCameraConfig => null;
        public TargetingConfig TargetingConfig => null; public InteractionConfig InteractionConfig => null; public SystemHudConfig SystemHudConfig => null;
        public GameVisualConfig GameVisualConfig => null; public CombatFxVisualConfig CombatFxVisualConfig => null;
        public InvasionScheduleConfig InvasionScheduleConfig => null; public CombatDamagePopupVisualConfig2A CombatDamagePopupVisualConfig => null;
        public NpcBehaviourTransitionMatrixConfig NpcBehaviourTransitionMatrixConfig => null;
        public IReadOnlyList<SectorConfig> GetAllSectors() => Array.Empty<SectorConfig>();
        public StarSystemLink GetCurrentStarSystemLink(string id) => null;
        public bool ContainsStarSystem(string id) => id == System.Id;
        public IReadOnlyList<StarSystemConfig> GetAllStarSystems() { SystemListReads++; return Systems ?? new[] { System }; }
        public bool TryGetStarSystem(string id, out StarSystemConfig value) { value = GetStarSystemConfigById(id); return value != null; }
        public StarSystemConfig GetStarSystemConfigById(string id) => (Systems ?? new[] { System }).FirstOrDefault(x => x.Id == id);
        public StarSystemConfig GetCurrentSystemConfig() => System;
        public IReadOnlyList<PlanetConfig> GetAllPlanets() => Planets;
        public PlanetConfig GetPlanetConfigById(string id) => Planets.FirstOrDefault(x => x.Id == id);
        public PlanetConfig GetCurrentPlanetConfig() => Planets[0];
        public IReadOnlyList<ItemConfig> GetAllItems() => new[] { Item };
        public ItemConfig GetItemConfigById(string id) => id == Item.Id ? Item : Components.Find(x => x.Id == id);
        public IReadOnlyList<AllyConfig> GetAllAllies() => Array.Empty<AllyConfig>();
        public EnemyConfig GetEnemyConfigById(string id) => null; public AllyConfig GetAllyConfigById(string id) => Hulls.Find(x => x.Id == id); public PirateConfig GetPirateConfigById(string id) => null;
        public AllySpawnRuleConfig GetAllySpawnRuleConfigById(string id) => null;
        public PirateGroupSpawnRuleConfig GetPirateGroupSpawnRuleConfigById(string id) => null;
        public IReadOnlyList<ModuleConfig> GetAllModules() => Array.Empty<ModuleConfig>(); public ModuleConfig GetModuleConfigById(string id) => null;
        public IReadOnlyList<WeaponConfig> GetAllWeapons() => new[] { Weapon }; public WeaponConfig GetWeaponConfigById(string id) => id == Weapon.Id ? Weapon : null;
    }
}
