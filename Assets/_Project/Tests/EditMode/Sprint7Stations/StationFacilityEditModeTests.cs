using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationFacilityEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private Session _session; private StarSystemConfig _config; private StationFacilityService _service;
    private Market _market; private SimpleEventBus _events;
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Market : IMarketTransactionService
    {
        public int BuyCalls, SellCalls;
        public BuyItemResult BuyItem(string id, int quantity) { BuyCalls++; return BuyItemResult.Create(BuyItemResultType.Success, id, quantity, 1, quantity); }
        public SellItemResult SellItem(string id, int quantity) { SellCalls++; return SellItemResult.Create(SellItemResultType.Success, id, quantity, 1, quantity); }
    }
    private sealed class Fuel : IRefuelService
    {
        public int GetCurrentFuel() => 0; public int GetFuelCapacity() => 10; public int GetFuelUnitPrice() => 1;
        public bool CanRefuel(int count) => true;
        public RefuelResult Refuel(int count) => RefuelResult.Create(RefuelResultType.Success, count, count, count, 10);
        public RefuelResult RefuelToFull() => Refuel(10);
        public bool CanConsume(int count, out FuelConsumeFailReason reason) { reason = default; return true; }
        public bool Consume(int count, out FuelConsumeFailReason reason) { reason = default; return true; }
    }
    private sealed class Repair : IRepairService
    {
        public RepairResult RepairToFull() => RepairResult.Create(RepairResultType.Success, 1, 1, 10, 10);
        public int GetRepairUnitPrice() => 1;
    }
    private sealed class Inventory : IInventoryService
    {
        public IReadOnlyList<RuntimeCargoEntryData> GetItems() => Array.Empty<RuntimeCargoEntryData>();
        public int GetItemCount(string id) => 0; public int GetUsedCargo() => 0; public int GetMaxCargo() => 100; public int GetFreeCargo() => 100;
        public bool CanAddItem(string id, int quantity) => true; public bool AddItem(string id, int quantity) => true;
        public bool AddLootItem(string id, int quantity) => true; public bool RemoveItem(string id, int quantity) => true; public void Clear() { }
    }
    private T Asset<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); _assets.Add(item); return item; }
    private static void Set(object target, string field, object value)
    {
        var type = target.GetType(); FieldInfo member = null;
        while (type != null && member == null) { member = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance); type = type.BaseType; }
        Assert.That(member, Is.Not.Null, field); member.SetValue(target, value);
    }
    [SetUp] public void Setup()
    {
        _session = new Session(); _session.State.Player.CurrentSystemId = "sys"; _session.State.Player.CurrentStationId = "station";
        _session.State.Player.PlayerShipState.ActiveShipId = "ship";
        _session.State.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData { ShipId = "ship", CurrentHull = 10, HullCapacity = 10 });
        var station = Asset<StationConfig>(); Set(station, "id", "station");
        _config = Asset<StarSystemConfig>(); Set(_config, "id", "sys"); Set(_config, "station", station);
        _session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "sys", Station = new StationRuntimeState { ConfigId = "station", Level = 1 } });
        _events = new SimpleEventBus(); _market = new Market();
        _service = new StationFacilityService(_session, _ => _config, new Fuel(), new Repair(), _market, new Inventory(), _events);
    }
    [TearDown] public void Cleanup() { foreach (var a in _assets) UnityEngine.Object.DestroyImmediate(a); _assets.Clear(); }
    [Test] public void OutsideStation_DoesNotInvokeTransactionServices()
    {
        _session.State.Player.CurrentStationId = string.Empty;
        Assert.That(_service.TryBuy("item", 1, out string reason), Is.False); Assert.That(reason, Is.EqualTo("NotInsideStation"));
        Assert.That(_service.TrySell("item", 1, out _), Is.False); Assert.That(_service.TryRefuel(out _), Is.False); Assert.That(_service.TryRepairHull(out _), Is.False);
        Assert.That(_market.BuyCalls, Is.Zero); Assert.That(_market.SellCalls, Is.Zero);
    }
    [Test] public void DestroyedStation_DoesNotInvokeTransactionServices()
    {
        _session.State.Galaxy.Systems[0].Station.IsDestroyed = true;
        Assert.That(_service.TryBuy("item", 1, out _), Is.False); Assert.That(_market.BuyCalls, Is.Zero);
    }
    [Test] public void PlanetAndStationContext_CannotBeMixed()
    {
        _session.State.Player.CurrentPlanetId = "planet";
        Assert.That(_service.TryBuy("item", 1, out _), Is.False); Assert.That(_market.BuyCalls, Is.Zero);
    }
    [Test] public void AcceptedTrade_PublishesSaveAfterTransaction()
    {
        int saves = 0; _events.Subscribe<SaveNeedEvent>(evt => saves++);
        Assert.That(_service.TryBuy("item", 1, out _), Is.True); Assert.That(_market.BuyCalls, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
    }
    [Test] public void ReentrantObserver_CannotRepeatTrade()
    {
        bool repeated = true;
        _events.Subscribe<SaveNeedEvent>(evt => repeated = _service.TryBuy("item", 1, out _));
        Assert.That(_service.TryBuy("item", 1, out _), Is.True); Assert.That(repeated, Is.False); Assert.That(_market.BuyCalls, Is.EqualTo(1));
    }
}
