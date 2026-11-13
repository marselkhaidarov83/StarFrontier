using System;
using System.Collections.Generic;

public class InventoryService : IInventoryService
{
    private readonly SimpleEventBus eventBus;
    private readonly IGameSessionService gameSessionService;
    private readonly IConfigService configService;

    public InventoryService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>(), Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()) { }
    public InventoryService(IGameSessionService session, IConfigService config, SimpleEventBus events)
    { gameSessionService = session; configService = config; eventBus = events; }

    private ShipRuntimeData Ship => gameSessionService.State?.Player?.PlayerShipState?.GetActiveShip();
    public IReadOnlyList<RuntimeCargoEntryData> GetItems() =>
        Ship?.Cargo?.entries ?? (IReadOnlyList<RuntimeCargoEntryData>)Array.Empty<RuntimeCargoEntryData>();
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        int ordinary = Ship?.Cargo?.GetQuantity(itemId) ?? 0;
        int components = 0;
        if (Ship?.CargoEquipment != null)
            foreach (var item in Ship.CargoEquipment)
                if (item?.ComponentConfigId == itemId) components++;
        return (int)Math.Min(int.MaxValue, (long)ordinary + components);
    }
    public int GetMaxCargo() => Ship?.CargoCapacity ?? 0;
    public int GetFreeCargo() => GetMaxCargo() - GetUsedCargo();

    public int GetUsedCargo()
    {
        var ship = Ship;
        if (ship == null) return 0;

        long total = 0;
        foreach (var entry in GetItems())
        {
            if (entry == null || entry.Quantity <= 0) continue;
            var item = configService.GetItemConfigById(entry.ItemId);
            if (item == null || item.CargoSize <= 0) return int.MaxValue;
            total += (long)item.CargoSize * entry.Quantity;
        }
        total += EquipmentCargoSize(ship.CargoEquipment);
        if (ship.HasModularLoadout)
            total += EquipmentCargoSize(ship.InstalledComponents) + EquipmentCargoSize(ship.InstalledWeapons);
        else if (ship.EquippedWeaponIds != null)
        {
            foreach (string id in ship.EquippedWeaponIds)
                if (!string.IsNullOrEmpty(id)) total += configService.GetWeaponConfigById(id)?.CargoSize ?? 0;
        }

        if (ship.EquippedModuleIds != null)
            foreach (string id in ship.EquippedModuleIds)
                if (!string.IsNullOrEmpty(id)) total += configService.GetModuleConfigById(id)?.CargoSize ?? 0;

        return (int)Math.Min(int.MaxValue, total);
    }

    private long EquipmentCargoSize(List<SystemNpcWeaponRuntimeState> equipment)
    {
        long size = 0;
        if (equipment != null)
            foreach (var item in equipment) size += GetEquipmentCargoSize(configService, item);
        return size;
    }

    internal static int GetEquipmentCargoSize(IConfigService config, SystemNpcWeaponRuntimeState item)
    {
        if (item == null) return 0;
        if (item.RolledStats.CargoSize > 0) return item.RolledStats.CargoSize;
        int size = item.IsComponent
            ? config.GetItemConfigById(item.ComponentConfigId)?.CargoSize ?? 1
            : config.GetWeaponConfigById(item.WeaponConfigId)?.CargoSize ?? 1;
        return Math.Max(1, size);
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
        if (!CanAddItem(itemId, quantity) || !AddToHold(itemId, quantity, false)) return false;
        PublishCargoChanged();
        return true;
    }

    public bool AddLootItem(string itemId, int quantity)
    {
        if (Ship?.Cargo == null || quantity <= 0 || !AddToHold(itemId, quantity, true)) return false;
        PublishCargoChanged();
        return true;
    }

    private bool AddToHold(string itemId, int quantity, bool allowOverflow)
    {
        var config = configService.GetItemConfigById(itemId);
        if (config == null) return false;
        if (config is ShipComponentConfig component)
        {
            Ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
            for (int i = 0; i < quantity; i++)
                Ship.CargoEquipment.Add(SystemNpcWeaponRuntimeState.NewComponent(component, Guid.NewGuid().ToString("N")));
            return true;
        }
        return allowOverflow ? Ship.Cargo.AddLootItem(itemId, quantity) : Ship.Cargo.AddItem(itemId, quantity, GetMaxCargo());
    }

    public bool RemoveItem(string itemId, int quantity)
    {
        if (Ship?.Cargo == null || quantity <= 0 || string.IsNullOrEmpty(itemId)) return false;
        if (configService.GetItemConfigById(itemId) is ShipComponentConfig)
        {
            var matching = Ship.CargoEquipment?.FindAll(item => item?.ComponentConfigId == itemId);
            if (matching == null || matching.Count < quantity) return false;
            for (int i = 0; i < quantity; i++) Ship.CargoEquipment.Remove(matching[i]);
        }
        else if (!Ship.Cargo.RemoveItem(itemId, quantity)) return false;
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
