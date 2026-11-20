using System;
using UnityEngine;

public enum ShipComponentSlot { Engine, FuelTank, Radar, Scanner, RepairDroid, Grabber, ShieldGenerator }

[Flags]
public enum ShipComponentSlots
{
    None = 0,
    Engine = 1 << (int)ShipComponentSlot.Engine,
    FuelTank = 1 << (int)ShipComponentSlot.FuelTank,
    Radar = 1 << (int)ShipComponentSlot.Radar,
    Scanner = 1 << (int)ShipComponentSlot.Scanner,
    RepairDroid = 1 << (int)ShipComponentSlot.RepairDroid,
    Grabber = 1 << (int)ShipComponentSlot.Grabber,
    ShieldGenerator = 1 << (int)ShipComponentSlot.ShieldGenerator,
    All = Engine | FuelTank | Radar | Scanner | RepairDroid | Grabber | ShieldGenerator
}

public enum ShipEquipmentVariant { Industrial, Expedition, Advanced }

[Serializable]
public sealed class ShipEquipmentRuntimeStats
{
    public int CargoSize;
    public float Speed;
    public float EngineRange;
    public int FuelCapacity;
    public float RadarRange;
    public float ScannerRange;
    public int RepairPerCycle;
    public int Strength;
    public float Range;
    public int ShieldCapacity;
    public float PullSpeedPerTick;
    public ShipEquipmentRuntimeStats Copy() => (ShipEquipmentRuntimeStats)MemberwiseClone();
}

[CreateAssetMenu(menuName = "StarFrontier/Configs/Ship/Equipment")]
public sealed class ShipEquipmentConfig : ItemConfig
{
    [SerializeField, Tooltip("Оборудование врагов. Не попадает в предложения рынка и обратный выкуп.")]
    private bool enemyOnly;
    public bool EnemyOnly => enemyOnly;
    [Range(1, 10)] public int Level = 1;
    public ShipEquipmentVariant Variant;
    public ShipComponentSlot Slot;
    [Min(1)] public int MaximumDurability = 100;
    [Min(1)] public int CargoSizeMin = 1;
    [Min(1)] public int CargoSizeMax = 1;
    [Min(0)] public int SpeedMin;
    [Min(0)] public int SpeedMax;
    [Min(0)] public int EngineRangeMin;
    [Min(0)] public int EngineRangeMax;
    [Min(0)] public int FuelCapacityMin;
    [Min(0)] public int FuelCapacityMax;
    [Min(0)] public int RadarRangeMin;
    [Min(0)] public int RadarRangeMax;
    [Min(0)] public int ScannerRangeMin;
    [Min(0)] public int ScannerRangeMax;
    [Min(0)] public int RepairPerCycleMin;
    [Min(0)] public int RepairPerCycleMax;
    [Min(0)] public int GrabberStrengthMin;
    [Min(0)] public int GrabberStrengthMax;
    [Min(0)] public int GrabberRangeMin;
    [Min(0)] public int GrabberRangeMax;
    [Min(0)] public int ShieldMin;
    [Min(0)] public int ShieldMax;

    // Config callers use the lower bound. Individual equipment uses its saved roll.
    public float Speed { get => SpeedMin; set { SpeedMin = Mathf.RoundToInt(value); SpeedMax = SpeedMin; } }
    public float EngineRange { get => EngineRangeMin; set { EngineRangeMin = Mathf.RoundToInt(value); EngineRangeMax = EngineRangeMin; } }
    public int FuelCapacity { get => FuelCapacityMin; set { FuelCapacityMin = value; FuelCapacityMax = value; } }
    public float RadarRange => RadarRangeMin;
    public float ScannerRange => ScannerRangeMin;
    public int RepairPerCycle => RepairPerCycleMin;
    public int Strength => GrabberStrengthMin;
    public float Range => GrabberRangeMin;

    public ShipEquipmentRuntimeStats RollRuntimeStats(int seed)
    {
        var random = new System.Random(seed);
        return new ShipEquipmentRuntimeStats
        {
            CargoSize = RollInt(random, CargoSizeMin, CargoSizeMax),
            Speed = RollInt(random, SpeedMin, SpeedMax),
            EngineRange = RollInt(random, EngineRangeMin, EngineRangeMax),
            FuelCapacity = RollInt(random, FuelCapacityMin, FuelCapacityMax),
            RadarRange = RollInt(random, RadarRangeMin, RadarRangeMax),
            ScannerRange = RollInt(random, ScannerRangeMin, ScannerRangeMax),
            RepairPerCycle = RollInt(random, RepairPerCycleMin, RepairPerCycleMax),
            Strength = RollInt(random, GrabberStrengthMin, GrabberStrengthMax),
            Range = RollInt(random, GrabberRangeMin, GrabberRangeMax),
            ShieldCapacity = RollInt(random, ShieldMin, ShieldMax)
        };
    }

    public static int StableSeed(string identity)
    {
        unchecked
        {
            int seed = 17;
            foreach (char c in identity ?? string.Empty) seed = seed * 31 + c;
            return seed;
        }
    }

    private static int RollInt(System.Random random, int min, int max) =>
        (int)(min + Math.Floor(random.NextDouble() * ((long)Math.Max(min, max) - min + 1)));

}
