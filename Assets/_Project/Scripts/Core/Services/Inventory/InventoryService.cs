using System;
using System.Collections.Generic;

public class InventoryService : IInventoryService
{
    private readonly SimpleEventBus eventBus;
    private readonly IGameSessionService gameSessionService;
    private readonly IConfigService configService;

    public InventoryService()
    {
        var registry = Bootstrapper.Instance.ServiceRegistry;
        gameSessionService = registry.Get<IGameSessionService>();
        configService = registry.Get<IConfigService>();
        eventBus = registry.Get<SimpleEventBus>();
    }

    private ShipRuntimeData Ship => gameSessionService.State?.Player?.PlayerShipState?.GetActiveShip();
    public IReadOnlyList<RuntimeCargoEntryData> GetItems() =>
        Ship?.Cargo?.entries ?? (IReadOnlyList<RuntimeCargoEntryData>)Array.Empty<RuntimeCargoEntryData>();
    public int GetItemCount(string itemId) => Ship?.Cargo?.GetQuantity(itemId) ?? 0;
    public int GetMaxCargo() => Ship?.CargoCapacity ?? 0;
    public int GetFreeCargo() => Math.Max(0, GetMaxCargo() - GetUsedCargo());

    public int GetUsedCargo()
    {
        long total = 0;
        foreach (var entry in GetItems())
        {
            if (entry == null || entry.Quantity <= 0) continue;
            var item = configService.GetItemConfigById(entry.ItemId);
            if (item == null || item.CargoSize <= 0) return int.MaxValue;
            total += (long)item.CargoSize * entry.Quantity;
        }
        return (int)Math.Min(int.MaxValue, total);
    }

    public bool CanAddItem(string itemId, int quantity)
    {
        if (Ship?.Cargo == null || string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return false;
        var item = configService.GetItemConfigById(itemId);
        return item != null && item.CargoSize > 0 &&
            (long)GetUsedCargo() + (long)item.CargoSize * quantity <= GetMaxCargo();
    }

    public bool AddItem(string itemId, int quantity)
    {
        if (!CanAddItem(itemId, quantity) || !Ship.Cargo.AddItem(itemId, quantity, GetMaxCargo())) return false;
        PublishCargoChanged();
        return true;
    }

    public bool RemoveItem(string itemId, int quantity)
    {
        if (Ship?.Cargo == null || !Ship.Cargo.RemoveItem(itemId, quantity)) return false;
        PublishCargoChanged();
        return true;
    }

    public void Clear()
    {
        Ship?.Cargo?.Clear();
        PublishCargoChanged();
    }

    private void PublishCargoChanged()
    {
        try { eventBus.Publish(new CargoChangedEvent(GetUsedCargo())); }
        catch (Exception exception)
        { AppLog.Error(DebugLogChannel.Mission, "[MissionCargo] Observer failed | " + exception.Message); }
    }
}
