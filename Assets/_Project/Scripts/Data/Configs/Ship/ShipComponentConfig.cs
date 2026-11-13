using System;
using UnityEngine;

public enum ShipComponentSlot
{
    Engine,
    FuelTank,
    Radar,
    Scanner,
    RepairDroid,
    Grabber,
    ShieldGenerator
}

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

[CreateAssetMenu(menuName = "StarFrontier/Configs/Ship/Component")]
public sealed class ShipComponentConfig : ItemConfig
{
    public ShipComponentSlot Slot;
    [Min(1)] public int MaximumDurability = 100;
    [Min(0)] public float Speed;
    [Tooltip("Дальность для карточки двигателя.")]
    [Min(0)] public float EngineRange;
    [Min(0)] public int FuelCapacity;
    [Min(0)] public float RadarRange;
    [Min(0)] public float ScannerRange;
    [Min(0)] public int RepairPerCycle;
    public GrabberConfig Grabber;
    [Min(0)] public float ShieldMultiplier = 1f;
}
