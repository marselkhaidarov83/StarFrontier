using System;
using UnityEngine;

/// <summary>
/// Итоговые параметры корабля после расчёта из ShipConfig
/// и дополнительных модификаторов.
///
/// Важно:
/// здесь нет энергии корабля и нет регенерации щита.
/// </summary>
[Serializable]
public sealed class ShipFinalStats
{
    public ShipFinalStats(
        string shipConfigId,
        int maxHull,
        int maxShield,
        float maxSpeed,
        float acceleration,
        float turnRate,
        float turnRadius,
        int cargoCapacity,
        int weaponSlotCount,
        int moduleSlotCount,
        Sprite combatSprite)
    {
        ShipConfigId = shipConfigId ?? string.Empty;

        MaxHull = Mathf.Max(0, maxHull);
        MaxShield = Mathf.Max(0, maxShield);

        MaxSpeed = Mathf.Max(0f, maxSpeed);
        Acceleration = Mathf.Max(0f, acceleration);
        TurnRate = Mathf.Max(0f, turnRate);
        TurnRadius = Mathf.Max(0f, turnRadius);

        CargoCapacity = Mathf.Max(0, cargoCapacity);
        WeaponSlotCount = Mathf.Max(0, weaponSlotCount);
        ModuleSlotCount = Mathf.Max(0, moduleSlotCount);

        CombatSprite = combatSprite;
    }

    public string ShipConfigId { get; }

    public int MaxHull { get; }

    public int MaxShield { get; }

    public float MaxSpeed { get; }

    public float Acceleration { get; }

    public float TurnRate { get; }

    public float TurnRadius { get; }

    public int CargoCapacity { get; }

    public int WeaponSlotCount { get; }

    public int ModuleSlotCount { get; }

    public Sprite CombatSprite { get; }
}
