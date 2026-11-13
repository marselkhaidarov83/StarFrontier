using System;
using System.Collections.Generic;

[Serializable]
public class ShipRuntimeData
{
    public string ShipId;
    public string AllyConfigId;
    // Debug commands affect this runtime ship only; loading restores its configured grabber.
    [NonSerialized] public bool DebugGrabberRemoved;

    // Null in legacy saves: the existing ship still represents its installed hull.
    public HullEquipmentRuntimeState InstalledHull;
    public bool HasModularLoadout;
    // Retained for migrating loadouts saved before components had individual condition.
    public List<string> InstalledComponentIds = new();
    public List<SystemNpcWeaponRuntimeState> InstalledComponents = new();
    public List<SystemNpcWeaponRuntimeState> InstalledWeapons = new();
    // Legacy save data. Hulls can no longer be put in the hold.
    public List<HullEquipmentRuntimeState> CargoHulls = new();
    public int CurrentHull;
    public int HullCapacity;
    public int CurrentShield;
    public int CurrentEnergy;
    public int CurrentFuel;
    public int FuelCapacity;

    public List<string> EquippedWeaponIds = new();
    public List<string> EquippedModuleIds = new();

    public RuntimeCargoInventory Cargo = new ();
    // Individual equipment in the hold keeps its rolled statistics and wear.
    public List<SystemNpcWeaponRuntimeState> CargoEquipment = new ();
    public int CargoCapacity;
}
