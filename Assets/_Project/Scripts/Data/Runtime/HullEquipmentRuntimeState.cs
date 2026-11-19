using System;
using UnityEngine;

[Serializable]
public sealed class HullEquipmentRuntimeState
{
    public string InstanceId;
    public string ConfigId;
    // Kept serialized for the integrity checks on saves created before hulls stopped using cargo space.
    [SerializeField, HideInInspector] private int CargoSize;
    public int CurrentDurability;
    public int MaximumDurability;
    public int CargoCapacity;

    [SerializeField, HideInInspector] private int MaximumHull;
    [SerializeField, HideInInspector] private int DurabilityPercent = -1;
    [SerializeField, HideInInspector] private bool HasExactCondition;
    [SerializeField, HideInInspector] private int RemainingHull;

    public void MigrateDurability()
    {
        if (MaximumDurability != 0 || MaximumHull <= 0) return;
        MaximumDurability = MaximumHull;
        CurrentDurability = HasExactCondition ? RemainingHull : DurabilityPercent == -1 ? MaximumHull :
            (int)(((long)MaximumHull * DurabilityPercent + 99) / 100);
        MaximumHull = 0;
        DurabilityPercent = -1;
        HasExactCondition = false;
        RemainingHull = 0;
    }

    // JsonUtility serializes an optional inline class as a default object instead of null.
    // Only the InstalledHull slot may interpret this exact empty shape as absent equipment.
    public static bool IsEmptyPlaceholder(HullEquipmentRuntimeState hull) => hull == null ||
        (string.IsNullOrEmpty(hull.InstanceId) && string.IsNullOrEmpty(hull.ConfigId) &&
         hull.CargoSize == 0 && hull.CargoCapacity == 0 && hull.MaximumDurability == 0 && hull.CurrentDurability == 0 &&
         hull.MaximumHull == 0 && !hull.HasExactCondition && hull.RemainingHull == 0 &&
         (hull.DurabilityPercent == -1 || hull.DurabilityPercent == 0 || hull.DurabilityPercent == 100));
}
[Serializable]
public sealed class MarketHullListing
{
    public HullEquipmentRuntimeState Hull;
    public int NewPrice;
    public double ExpiresAtSeconds;
}
