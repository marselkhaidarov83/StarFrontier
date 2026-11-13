using System.Collections.Generic;
using UnityEngine;

public partial class HangarService
{
    private bool _changingLoadout;

    public bool TryChangeLoadout(int slot, string itemId, bool warehouse, out string reason)
    {
        reason = "Ангар недоступен.";
        if (_changingLoadout) return false;
        var ship = GetActiveShipState();
        var hull = GetActiveShipData();
        var market = _market.CurrentMarket;
        if (ship?.HasModularLoadout != true || hull == null || market == null || ship.Cargo == null) return false;

        _changingLoadout = true;
        try
        {
            bool changed;
            if (slot == ShipLoadoutRules.HullSlot)
                changed = ReplaceHull(ship, hull, market, itemId, warehouse, out reason);
            else if (slot >= ShipLoadoutRules.FirstWeaponSlot && slot < ShipLoadoutRules.TotalSlots)
                changed = ChangeWeapon(ship, hull, market, slot - ShipLoadoutRules.FirstWeaponSlot, itemId, warehouse, out reason);
            else if (slot >= ShipLoadoutRules.FirstComponentSlot && slot < ShipLoadoutRules.FirstWeaponSlot)
                changed = ChangeComponent(ship, hull, market, (ShipComponentSlot)(slot - ShipLoadoutRules.FirstComponentSlot), itemId, warehouse, out reason);
            else
            {
                reason = "Этот слот недоступен.";
                return false;
            }

            if (!changed) return false;

            if (slot == ShipLoadoutRules.HullSlot || slot == ShipLoadoutRules.FirstComponentSlot + (int)ShipComponentSlot.Grabber)
                ship.DebugGrabberRemoved = false;
            NotifyLoadoutChanged(ship, market, "ChangeLoadout", slot == ShipLoadoutRules.HullSlot);
            AppLog.Info(DebugLogChannel.Hangar, typeof(HangarService),
                "Loadout changed | Ship=" + ship.ShipId + " | Slot=" + slot + " | Item=" + itemId);
            return true;
        }
        finally { _changingLoadout = false; }
    }

    public bool TryRemoveAllEquipment(bool warehouse, out string reason)
    {
        reason = "Ангар недоступен.";
        if (_changingLoadout) return false;
        var ship = GetActiveShipState();
        var market = _market.CurrentMarket;
        if (ship?.HasModularLoadout != true || GetActiveShipData() == null || market == null || ship.Cargo == null) return false;

        var equipment = new List<SystemNpcWeaponRuntimeState>(ship.InstalledComponents);
        equipment.AddRange(ship.InstalledWeapons);
        equipment.RemoveAll(item => item == null);
        if (equipment.Count == 0)
        {
            reason = "Нет установленного оборудования.";
            return false;
        }

        var storage = warehouse ? market.PersonalEquipment : ship.CargoEquipment;
        var storedIds = new HashSet<string>();
        foreach (var item in storage)
            if (item != null) storedIds.Add(item.InstanceId);

        foreach (var item in equipment)
        {
            if (!storedIds.Add(item.InstanceId))
            {
                reason = "Не удалось переместить повторяющийся экземпляр оборудования.";
                return false;
            }
        }

        _changingLoadout = true;
        try
        {
            StoreTankFuel(ship, ShipLoadoutRules.Component(ship, ShipComponentSlot.FuelTank));
            foreach (var item in equipment)
            {
                item.SlotIndex = -1;
                storage.Add(item);
            }

            ship.InstalledComponents.Clear();
            ship.InstalledWeapons.Clear();
            for (int i = 0; i < ship.EquippedWeaponIds.Count; i++)
                ship.EquippedWeaponIds[i] = string.Empty;

            ship.FuelCapacity = 0;
            ship.CurrentFuel = 0;
            ship.CurrentShield = 0;
            ship.DebugGrabberRemoved = false;
            reason = string.Empty;
            NotifyLoadoutChanged(ship, market, "RemoveAllEquipment");
            AppLog.Info(DebugLogChannel.Hangar, typeof(HangarService),
                "Equipment removed | Ship=" + ship.ShipId + " | Count=" + equipment.Count + " | Warehouse=" + warehouse);
            return true;
        }
        finally { _changingLoadout = false; }
    }

    private void NotifyLoadoutChanged(ShipRuntimeData ship, MarketRuntimeData market, string operation, bool hullChanged = false)
    {
        market.Revision++;
        if (hullChanged) PublishLoadoutEvent(new ActiveShipChangedEvent(ship.ShipId));
        PublishLoadoutEvent(new ShipEquipmentChangedEvent(ship.ShipId));
        PublishLoadoutEvent(new ShipStatsChangedEvent(ship.ShipId));
        PublishLoadoutEvent(new CargoChangedEvent(_inventory.GetUsedCargo()));
        PublishLoadoutEvent(new MarketStateChangedEvent(market.SystemId, market.PlanetId));
        SaveAfterSuccessfulHangarOperation(operation);
    }

    private bool ChangeComponent(ShipRuntimeData ship, AllyConfig hull, MarketRuntimeData market,
        ShipComponentSlot slot, string itemId, bool warehouse, out string reason)
    {
        reason = "Этот слот недоступен.";
        if (!ShipLoadoutRules.Supports(hull, slot)) return false;
        var storage = warehouse ? market.PersonalEquipment : ship.CargoEquipment;
        var old = ShipLoadoutRules.Component(ship, slot);
        var nextItem = string.IsNullOrEmpty(itemId) ? null : storage.Find(x => x?.InstanceId == itemId && x.IsComponent);
        var next = GetComponent(nextItem?.ComponentConfigId);
        reason = "Оборудование недоступно или не подходит для слота.";
        if (!string.IsNullOrEmpty(itemId) && (next == null || next.Slot != slot)) return false;
        if (old == null && nextItem == null) return false;
        if (old != null && storage.Exists(x => x?.InstanceId == old.InstanceId)) return false;
        if (nextItem != null && ship.InstalledComponents.Exists(x => x?.InstanceId == nextItem.InstanceId)) return false;

        long delta = warehouse ? (long)EquipmentSize(nextItem) - EquipmentSize(old) : 0;
        reason = "Недостаточно места в трюме.";
        if (!FitsCargo(delta, ship.CargoCapacity, ship.CargoCapacity)) return false;

        if (nextItem != null) storage.Remove(nextItem);
        if (old != null)
        {
            ship.InstalledComponents.Remove(old);
            old.SlotIndex = -1;
            storage.Add(old);
        }
        if (nextItem != null)
        {
            nextItem.SlotIndex = (int)slot;
            ship.InstalledComponents.Add(nextItem);
        }
        if (slot == ShipComponentSlot.FuelTank)
        {
            StoreTankFuel(ship, old);
            ship.FuelCapacity = next != null ? Mathf.Max(0, next.FuelCapacity) : 0;
            ship.CurrentFuel = nextItem != null ? Mathf.Clamp(nextItem.StoredFuel, 0, ship.FuelCapacity) : 0;
            if (nextItem != null) nextItem.StoredFuel = 0;
        }
        if (slot == ShipComponentSlot.ShieldGenerator)
            ship.CurrentShield = Mathf.Clamp(ship.CurrentShield, 0, ShieldCapacity(ship, hull));
        reason = string.Empty;
        return true;
    }

    private bool ChangeWeapon(ShipRuntimeData ship, AllyConfig hull, MarketRuntimeData market,
        int index, string itemId, bool warehouse, out string reason)
    {
        reason = "Этот оружейный слот недоступен.";
        if (index < 0 || index >= ShipLoadoutRules.AvailableWeapons(hull)) return false;
        var storage = warehouse ? market.PersonalEquipment : ship.CargoEquipment;
        var old = ShipLoadoutRules.Weapon(ship, index);
        var next = string.IsNullOrEmpty(itemId) ? null : storage.Find(x => x?.InstanceId == itemId && !x.IsComponent);
        reason = "Оружие недоступно.";
        if ((!string.IsNullOrEmpty(itemId) && (next == null || _configService.GetWeaponConfigById(next.WeaponConfigId) == null)) ||
            (old == null && next == null)) return false;
        if (old != null && storage.Exists(x => x?.InstanceId == old.InstanceId)) return false;
        if (next != null && ship.InstalledWeapons.Exists(x => x?.InstanceId == next.InstanceId)) return false;

        long delta = warehouse ? (long)EquipmentSize(next) - EquipmentSize(old) : 0;
        reason = "Недостаточно места в трюме.";
        if (!FitsCargo(delta, ship.CargoCapacity, ship.CargoCapacity)) return false;

        if (next != null) storage.Remove(next);
        if (old != null)
        {
            ship.InstalledWeapons.Remove(old);
            old.SlotIndex = -1;
            storage.Add(old);
        }
        if (next != null)
        {
            next.SlotIndex = index;
            ship.InstalledWeapons.Add(next);
        }
        ship.EquippedWeaponIds[index] = next?.WeaponConfigId ?? string.Empty;
        reason = string.Empty;
        return true;
    }

    private bool ReplaceHull(ShipRuntimeData ship, AllyConfig hull, MarketRuntimeData market,
        string itemId, bool warehouse, out string reason)
    {
        reason = "Корпус можно только заменить.";
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!warehouse)
        {
            reason = "Корпус можно установить только со склада.";
            return false;
        }
        var storage = market.PersonalHulls;
        var next = storage.Find(x => x?.InstanceId == itemId);
        var old = ShipLoadoutRules.CurrentHull(ship, hull);
        var config = next != null ? _market.GetHullConfig(next.ConfigId)?.Hull : null;
        reason = "Корпус недоступен или разрушен.";
        if (config == null || next.MaximumDurability <= 0 || next.InstanceId == old.InstanceId ||
            storage.Exists(x => x?.InstanceId == old.InstanceId)) return false;
        int remaining = next.CurrentDurability;
        if (remaining <= 0 || remaining > next.MaximumDurability) return false;

        var nextStats = _statCalculator.Calculate(config, ship.EquippedModuleIds);
        int capacity = Mathf.Max(0, nextStats.CargoCapacity);
        long delta = 0;

        var detached = ship.InstalledComponents.FindAll(x => x != null &&
            !ShipLoadoutRules.Supports(config, (ShipComponentSlot)x.SlotIndex));
        if (detached.Exists(item => market.PersonalEquipment.Exists(x => x?.InstanceId == item.InstanceId)))
        {
            reason = "На складе уже есть экземпляр этого модуля.";
            return false;
        }

        int weapons = ShipLoadoutRules.AvailableWeapons(config);
        var detachedWeapons = ship.InstalledWeapons.FindAll(x => x != null && x.SlotIndex >= weapons);
        if (detachedWeapons.Exists(weapon => market.PersonalEquipment.Exists(x => x?.InstanceId == weapon.InstanceId)))
        {
            reason = "На складе уже есть экземпляр этого оружия.";
            return false;
        }

        // Incompatible equipment leaves the ship for the local warehouse.
        foreach (var component in detached) delta -= EquipmentSize(component);
        foreach (var weapon in detachedWeapons) delta -= EquipmentSize(weapon);
        reason = "В новом корпусе недостаточно места для текущего груза.";
        if (!FitsCargo(delta, ship.CargoCapacity, capacity)) return false;

        foreach (var component in detached)
        {
            if (component.SlotIndex == (int)ShipComponentSlot.FuelTank) StoreTankFuel(ship, component);
            ship.InstalledComponents.Remove(component);
            component.SlotIndex = -1;
            market.PersonalEquipment.Add(component);
        }
        foreach (var weapon in detachedWeapons)
        {
            ship.EquippedWeaponIds[weapon.SlotIndex] = string.Empty;
            ship.InstalledWeapons.Remove(weapon);
            weapon.SlotIndex = -1;
            market.PersonalEquipment.Add(weapon);
        }

        storage.Remove(next);
        storage.Add(old);
        ship.InstalledHull = next;
        ship.AllyConfigId = config.Id;
        ship.HullCapacity = next.MaximumDurability;
        ship.CurrentHull = remaining;
        ship.CargoCapacity = capacity;
        if (GetComponent(ShipLoadoutRules.ComponentId(ship, ShipComponentSlot.FuelTank)) == null) ship.FuelCapacity = 0;
        ship.CurrentFuel = Mathf.Clamp(ship.CurrentFuel, 0, ship.FuelCapacity);
        ship.CurrentShield = Mathf.Clamp(ship.CurrentShield, 0, ShieldCapacity(ship, config));
        ship.CurrentEnergy = Mathf.Clamp(ship.CurrentEnergy, 0, Mathf.Max(0, nextStats.MaxEnergy));
        reason = detached.Count + detachedWeapons.Count > 0 ? "Несовместимое оборудование перемещено на склад." : string.Empty;
        return true;
    }

    private static void StoreTankFuel(ShipRuntimeData ship, SystemNpcWeaponRuntimeState tank)
    {
        if (tank != null) tank.StoredFuel = Mathf.Max(0, ship.CurrentFuel);
    }

    private bool FitsCargo(long delta, int oldCapacity, int nextCapacity)
    {
        long used = _inventory.GetUsedCargo();
        return used + delta <= nextCapacity || (oldCapacity == nextCapacity && delta <= 0);
    }

    private int EquipmentSize(SystemNpcWeaponRuntimeState item) =>
        InventoryService.GetEquipmentCargoSize(_configService, item);

    private ShipComponentConfig GetComponent(string id) =>
        string.IsNullOrEmpty(id) ? null : _configService.GetItemConfigById(id) as ShipComponentConfig;

    private int ShieldCapacity(ShipRuntimeData ship, AllyConfig hull)
    {
        var generator = GetComponent(ShipLoadoutRules.ComponentId(ship, ShipComponentSlot.ShieldGenerator));
        int baseCapacity = _statCalculator.Calculate(hull, ship.EquippedModuleIds).MaxShield;
        return generator != null ? Mathf.Max(0, Mathf.RoundToInt(baseCapacity * generator.ShieldMultiplier)) : 0;
    }

    private void PublishLoadoutEvent<T>(T evt)
    {
        _eventBus.PublishSafely(evt, exception => AppLog.Warning(DebugLogChannel.Hangar,
            "[Hangar] Loadout observer failed | " + exception.Message));
    }
}
