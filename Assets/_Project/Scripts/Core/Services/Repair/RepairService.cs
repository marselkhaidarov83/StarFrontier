using System;

public class RepairService : IPartialRepairService
{
    private readonly IGameSessionService _session;
    private readonly Func<int> _unitPrice;
    private readonly SimpleEventBus _events;
    private bool _busy;
    public RepairService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        () => Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GameConfig?.MarketEconomy?.HullRepairUnitPrice ?? 0,
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()) { }
    public RepairService(IGameSessionService session, Func<int> unitPrice, SimpleEventBus events)
    { _session = session; _unitPrice = unitPrice; _events = events; }
    public int GetRepairUnitPrice() => _unitPrice();
    public RepairResult RepairToFull()
    {
        var ship = _session.State?.Player?.GetActiveShip();
        return Repair(Math.Max(0, (ship?.HullCapacity ?? 0) - (ship?.CurrentHull ?? 0)));
    }
    public RepairResult Repair(int hullUnits)
    {
        var player = _session.State?.Player;
        var ship = player?.GetActiveShip();
        RepairResult Result(RepairResultType type, int repaired = 0, int total = 0) =>
            RepairResult.Create(type, repaired, total, ship?.CurrentHull ?? 0, ship?.HullCapacity ?? 0);
        if (player == null || ship == null) return Result(RepairResultType.MissingPlayerProfile);
        if (_busy || hullUnits < 0) return Result(RepairResultType.InvalidHullCapacity);
        if (ship.HullCapacity <= 0) return Result(RepairResultType.InvalidHullCapacity);
        if (ship.CurrentHull >= ship.HullCapacity) return Result(RepairResultType.HullAlreadyFull);
        int unit = GetRepairUnitPrice();
        if (unit <= 0) return Result(RepairResultType.InvalidRepairPrice);
        int amount = Math.Min(hullUnits, ship.HullCapacity - ship.CurrentHull);
        long total = (long)amount * unit;
        if (amount <= 0 || total > int.MaxValue) return Result(RepairResultType.InvalidRepairPrice);
        if (player.Credits < total) return Result(RepairResultType.NotEnoughCredits, 0, (int)total);
        _busy = true;
        try
        {
            player.Credits -= (int)total;
            ship.CurrentHull += amount;
            var result = Result(RepairResultType.Success, amount, (int)total);
            _events?.PublishSafely(new ShipRepairedEvent(ship.ShipId), _ => { });
            _events?.PublishSafely(new CreditsChangedEvent(player.Credits), _ => { });
            _events?.PublishSafely(new SaveNeedEvent("hull_repaired"), _ => { });
            AppLog.Info(DebugLogChannel.Economy, "[Repair] Hull=" + amount + " | Total=" + total);
            using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.Repair, null, amount))
                observation.Complete(true, result.resultType.ToString(), result.totalPrice);
            return result;
        }
        finally { _busy = false; }
    }
}
