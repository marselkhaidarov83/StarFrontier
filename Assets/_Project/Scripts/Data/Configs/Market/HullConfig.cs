using System;
using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Configs/Market/Hull")]
public sealed class HullConfig : BaseConfig
{
    [Range(1, 10)] public int Level = 1;
    public AllyConfig Hull;
    [Range(0, 5)] public int WeaponSlotCount = 1;
    public ShipComponentSlots AvailableComponentSlots;
    [Min(1)] public int MaximumHullMin = 100;
    [Min(1)] public int MaximumHullMax = 100;
    [Min(0)] public int CargoCapacityMin;
    [Min(0)] public int CargoCapacityMax;

    public HullEquipmentRuntimeState CreateRuntimeState(string instanceId)
    {
        var random = new System.Random(ShipEquipmentConfig.StableSeed(instanceId));
        int durability = Roll(random, MaximumHullMin, MaximumHullMax);
        return new HullEquipmentRuntimeState
        {
            InstanceId = instanceId, ConfigId = Id,
            CurrentDurability = durability, MaximumDurability = durability,
            CargoCapacity = Roll(random, CargoCapacityMin, CargoCapacityMax)
        };
    }

    private static int Roll(System.Random random, int min, int max) =>
        (int)(min + Math.Floor(random.NextDouble() * ((long)Math.Max(min, max) - min + 1)));
}
