using System;
using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Configs/Market/Hull")]
public sealed class HullConfig : BaseConfig
{
    [Range(0, 10)] public int Level = 1;
    public AllyConfig Hull;
    public EnemyConfig EnemyHull;
    public bool EnemyOnly => EnemyHull != null;
    public bool MarketAvailable => !EnemyOnly && Level > 0 && Hull != null && Hull.Level > 0;
    [Min(0f)] public float AccelerationMin;
    [Min(0f)] public float AccelerationMax;
    [Min(0f)] public float TurnRadius = 300f;
    [Min(0f)] public float VisualSize = 48f;
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
            CargoCapacity = Roll(random, CargoCapacityMin, CargoCapacityMax),
            Acceleration = AccelerationMin + (float)random.NextDouble() * (Math.Max(AccelerationMin, AccelerationMax) - AccelerationMin)
        };
    }

    private static int Roll(System.Random random, int min, int max) =>
        (int)(min + Math.Floor(random.NextDouble() * ((long)Math.Max(min, max) - min + 1)));
}
