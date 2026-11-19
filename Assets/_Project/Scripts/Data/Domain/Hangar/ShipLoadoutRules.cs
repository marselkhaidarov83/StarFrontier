using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShipLoadoutRules
{
    public const string EngineNotInstalledMessage = "Двигатель не установлен. Взлет не возможен";
    public const string FuelTankNotInstalledMessage = "Топливный бак не установлен. Перелет невозможен";

    public const int WeaponSlots = 5;
    public const int ComponentSlots = 7;
    public const int HullSlot = 0;
    public const int FirstComponentSlot = 1;
    public const int FirstWeaponSlot = FirstComponentSlot + ComponentSlots;
    public const int TotalSlots = FirstWeaponSlot + WeaponSlots;

    public static int AvailableWeapons(AllyConfig hull, HullConfig selected = null) =>
        Mathf.Clamp(selected != null ? selected.WeaponSlotCount : hull != null ? hull.WeaponSlotCount : 0, 0, WeaponSlots);

    public static bool Supports(AllyConfig hull, ShipComponentSlot slot, HullConfig selected = null)
    {
        int index = (int)slot;
        return hull != null && index >= 0 && index < ComponentSlots &&
            ((selected != null ? selected.AvailableComponentSlots : hull.AvailableComponentSlots) & (ShipComponentSlots)(1 << index)) != 0;
    }

    public static bool IsComponentMissing(ShipRuntimeData ship, ShipComponentSlot slot) =>
        ship?.HasModularLoadout == true && string.IsNullOrEmpty(ComponentId(ship, slot));

    public static string ComponentId(ShipRuntimeData ship, ShipComponentSlot slot)
    {
        return Component(ship, slot)?.ComponentConfigId ?? string.Empty;
    }

    public static SystemNpcWeaponRuntimeState Component(ShipRuntimeData ship, ShipComponentSlot slot) =>
        ship?.InstalledComponents?.Find(component => component != null && component.SlotIndex == (int)slot);

    public static SystemNpcWeaponRuntimeState Weapon(ShipRuntimeData ship, int index) =>
        ship?.InstalledWeapons?.Find(weapon => weapon != null && weapon.SlotIndex == index);

    public static string HullEquipmentId(string allyConfigId) => "hull_" + allyConfigId;

    public static HullEquipmentRuntimeState CurrentHull(ShipRuntimeData ship, AllyConfig config)
    {
        if (ship == null || config == null) return null;
        var installed = ship.InstalledHull;
        int maximum = Mathf.Max(1, ship.HullCapacity > 0 ? ship.HullCapacity : config.BaseHull);
        return new HullEquipmentRuntimeState
        {
            ConfigId = HullEquipmentRuntimeState.IsEmptyPlaceholder(installed) ? HullEquipmentId(config.Id) : installed.ConfigId,
            InstanceId = HullEquipmentRuntimeState.IsEmptyPlaceholder(installed) ? "installed-hull:" + ship.ShipId : installed.InstanceId,
            MaximumDurability = maximum,
            CargoCapacity = installed != null && installed.CargoCapacity > 0 ? installed.CargoCapacity : ship.CargoCapacity,
            CurrentDurability = Mathf.Clamp(ship.CurrentHull, 0, maximum)
        };
    }

    public static void Initialize(ShipRuntimeData ship, AllyConfig hull, Func<string, WeaponConfig> weapons, HullConfig selected = null)
    {
        if (ship == null || hull == null || ship.HasModularLoadout) return;

        ship.InstalledComponents = new List<SystemNpcWeaponRuntimeState>();
        if (hull.DefaultComponents != null)
            foreach (var component in hull.DefaultComponents)
                if (component != null && Supports(hull, component.Slot, selected))
                    ship.InstalledComponents.Add(SystemNpcWeaponRuntimeState.NewComponent(component,
                        "installed-component:" + ship.ShipId + ":" + (int)component.Slot, (int)component.Slot));

        ship.InstalledWeapons = new List<SystemNpcWeaponRuntimeState>();
        ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
        var previousWeapons = ship.EquippedWeaponIds;
        ship.EquippedWeaponIds = new List<string>(WeaponSlots);
        for (int i = 0; i < WeaponSlots; i++) ship.EquippedWeaponIds.Add(string.Empty);
        if (previousWeapons != null)
            for (int i = 0; i < previousWeapons.Count; i++)
            {
                string id = previousWeapons[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                var config = weapons(id);
                if (config == null) continue;
                string instanceId = "installed-weapon:" + ship.ShipId + ":" + i;
                var weapon = new SystemNpcWeaponRuntimeState
                {
                    InstanceId = instanceId,
                    WeaponConfigId = id,
                    CurrentDurability = config.MaximumDurability,
                    MaximumDurability = config.MaximumDurability,
                    RolledStats = config.RollRuntimeStats(StableSeed(instanceId))
                };
                if (i < AvailableWeapons(hull, selected))
                {
                    weapon.SlotIndex = i;
                    ship.EquippedWeaponIds[i] = id;
                    ship.InstalledWeapons.Add(weapon);
                }
                else ship.CargoEquipment.Add(weapon);
            }

        ship.InstalledHull = CurrentHull(ship, hull);
        if (ship.HullCapacity <= 0) ship.HullCapacity = ship.InstalledHull.MaximumDurability;
        ship.HasModularLoadout = true;
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in value) hash = hash * 31 + c;
            return hash;
        }
    }
}
