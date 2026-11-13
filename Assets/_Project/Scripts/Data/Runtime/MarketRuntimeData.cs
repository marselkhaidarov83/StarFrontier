using System;
using System.Collections.Generic;

[Serializable]
public class MarketRuntimeData
{
    public string SystemId;
    public string PlanetId;
    public List<MarketRuntimeItemState> Items = new();
    public List<MarketEquipmentListing> Equipment = new();
    public List<MarketEquipmentSpawnState> EquipmentSpawns = new();
    public List<MarketTransactionReceipt> Receipts = new();
    // Personal storage belongs to the player at this location; never used as market supply.
    public RuntimeCargoInventory PersonalStorage = new RuntimeCargoInventory();
    public List<SystemNpcWeaponRuntimeState> PersonalEquipment = new List<SystemNpcWeaponRuntimeState>();
    public List<HullEquipmentRuntimeState> PersonalHulls = new();
    public List<MarketHullListing> Hulls = new();
    public long Revision;
    public int AppliedGalaxyLevel;
    public string AppliedProfileId;
    public double ElapsedSeconds;
    public double LastProcessedSimulationSeconds;
    // Equipment timers are updated only at their next deadline; pending elapsed time survives saves.
    public double EquipmentPendingSeconds;
    public double IntervalRemainder;
}

[Serializable]
public sealed class MarketEquipmentListing
{
    public SystemNpcWeaponRuntimeState Equipment;
    public int NewPrice;
    public bool Allied;
    public double ExpiresAtSeconds;
}
[Serializable]
public sealed class MarketEquipmentSpawnState
{
    public string WeaponId;
    public double Remainder;
    public long SpawnSequence;
}
[Serializable]
public sealed class MarketTransactionReceipt
{
    public string OperationId;
    public string Intent;
    public int TotalPrice;
    public int Quantity;
}