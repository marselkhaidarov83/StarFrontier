using System;
using System.Collections.Generic;

[Serializable]
public class ShipRuntimeData
{
    public string ShipId;
    public string ShipConfigId;

    public int CurrentHull;
    public int HullCapacity;
    public int CurrentShield;
    public int CurrentEnergy;
    public int CurrentFuel;
    public int FuelCapacity;

    public List<string> EquippedWeaponIds = new();
    public List<string> EquippedModuleIds = new();

    public RuntimeCargoInventory Cargo = new ();
    public int CargoCapacity; //грузоподъемность корабля
}