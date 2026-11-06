using System;
public sealed class StationFacilityService : IStationFacilityService
{
    private readonly IGameSessionService _session;
    private readonly Func<string, StarSystemConfig> _resolve;
    private readonly IRefuelService _refuel;
    private readonly IRepairService _repair;
    private readonly IMarketTransactionService _market;
    private readonly IInventoryService _inventory;
    private readonly SimpleEventBus _events;
    private bool _busy;
    public StationFacilityService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GetStarSystemConfigById,
        Bootstrapper.Instance.ServiceRegistry.Get<IRefuelService>(), Bootstrapper.Instance.ServiceRegistry.Get<IRepairService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IMarketTransactionService>(), Bootstrapper.Instance.ServiceRegistry.Get<IInventoryService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()) { }
    public StationFacilityService(IGameSessionService session, Func<string, StarSystemConfig> resolve, IRefuelService refuel,
        IRepairService repair, IMarketTransactionService market, IInventoryService inventory, SimpleEventBus events)
    { _session = session; _resolve = resolve; _refuel = refuel; _repair = repair; _market = market; _inventory = inventory; _events = events; }
    private bool Begin(out string reason)
    {
        reason = "NotInsideStation";
        if (_busy || _session.State?.Player == null || !StationLocationRules.IsInside(_session.State, _resolve(_session.State.Player.CurrentSystemId))) return false;
        _busy = true; reason = string.Empty; return true;
    }
    private void Changed(string action)
    {
        var p = _session.State.Player;
        _events?.PublishSafely(new CreditsChangedEvent(p.Credits), _ => { });
        _events?.PublishSafely(new CargoChangedEvent(_inventory.GetUsedCargo()), _ => { });
        _events?.PublishSafely(new SaveNeedEvent("station_" + action), _ => { });
        AppLog.Info(DebugLogChannel.StationInterface, "[StationInterface] SERVICE | Action=" + action + " | Station=" + p.CurrentStationId);
    }
    public bool TryRefuel(out string reason)
    {
        if (!Begin(out reason)) return false;
        try { var result = _refuel.RefuelToFull(); reason = result.IsSuccess ? string.Empty : "RefuelUnavailable";
            AppLog.Info(DebugLogChannel.StationInterface, "[StationInterface] REFUEL | Success=" + result.IsSuccess); return result.IsSuccess; }
        finally { _busy = false; }
    }
    public bool TryRepairHull(out string reason)
    {
        if (!Begin(out reason)) return false;
        try { var result = _repair.RepairToFull(); reason = result.IsSuccess ? string.Empty : result.resultType.ToString(); if (result.IsSuccess) Changed("hull_repaired"); return result.IsSuccess; }
        finally { _busy = false; }
    }
    public bool TryBuy(string itemId, int quantity, out string reason)
    {
        if (!Begin(out reason)) return false;
        try { var result = _market.BuyItem(itemId, quantity); reason = result.IsSuccess ? string.Empty : result.resultType.ToString(); if (result.IsSuccess) Changed("bought"); return result.IsSuccess; }
        finally { _busy = false; }
    }
    public bool TrySell(string itemId, int quantity, out string reason)
    {
        if (!Begin(out reason)) return false;
        try { var result = _market.SellItem(itemId, quantity); reason = result.IsSuccess ? string.Empty : result.resultType.ToString(); if (result.IsSuccess) Changed("sold"); return result.IsSuccess; }
        finally { _busy = false; }
    }
}
