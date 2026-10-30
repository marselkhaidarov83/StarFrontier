using System;
using System.Collections.Generic;

[Serializable]
public class ShipRuntimeData
{
    public string ShipId;
    public string AllyConfigId;
    // Debug commands affect this runtime ship only; loading restores its configured grabber.
    [NonSerialized] public bool DebugGrabberRemoved;

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
