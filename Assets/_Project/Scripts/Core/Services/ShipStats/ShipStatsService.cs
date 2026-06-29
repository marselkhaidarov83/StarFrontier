using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рассчитывает итоговые параметры корабля
/// из ShipConfig и установленных модулей.
///
/// Сервис не зависит от Bootstrapper,
/// не обращается к сцене и не является ITickable.
/// </summary>
public sealed class ShipStatsService : IShipStatsService
{
    public ShipFinalStats CalculateFromConfig(
        ShipConfig shipConfig)
    {
        return Calculate(
            shipConfig,
            null);
    }

    public ShipFinalStats Calculate(
        ShipConfig shipConfig,
        IEnumerable<ModuleConfig> equippedModules)
    {
        if (shipConfig == null)
        {
            throw new ArgumentNullException(
                nameof(shipConfig));
        }

        ShipStatsAccumulator accumulator =
            ShipStatsAccumulator.FromConfig(shipConfig);

        if (equippedModules != null)
        {
            foreach (ModuleConfig moduleConfig in equippedModules)
            {
                ApplyModule(
                    accumulator,
                    moduleConfig);
            }
        }

        accumulator.Clamp();

        return accumulator.ToFinalStats();
    }

    private static void ApplyModule(
        ShipStatsAccumulator accumulator,
        ModuleConfig moduleConfig)
    {
        if (moduleConfig == null)
            return;

        StatModifierData[] modifiers =
            moduleConfig.StatModifiers;

        if (modifiers == null)
            return;

        for (int index = 0;
             index < modifiers.Length;
             index++)
        {
            ApplyModifier(
                accumulator,
                modifiers[index]);
        }
    }

    private static void ApplyModifier(
        ShipStatsAccumulator accumulator,
        StatModifierData modifier)
    {
        switch (modifier.ModifierType)
        {
            case StatModifierType.Additive:
                ApplyAdditiveModifier(
                    accumulator,
                    modifier.StatType,
                    modifier.Value);
                break;

            case StatModifierType.Multiplicative:
                ApplyMultiplicativeModifier(
                    accumulator,
                    modifier.StatType,
                    modifier.Value);
                break;

            default:
                break;
        }
    }

    private static void ApplyAdditiveModifier(
        ShipStatsAccumulator accumulator,
        ShipStatType statType,
        float value)
    {
        switch (statType)
        {
            case ShipStatType.Hull:
                accumulator.MaxHull += value;
                break;

            case ShipStatType.Shield:
                accumulator.MaxShield += value;
                break;

            case ShipStatType.Speed:
                accumulator.MaxSpeed += value;
                break;

            case ShipStatType.Acceleration:
                accumulator.Acceleration += value;
                break;

            case ShipStatType.TurnRate:
                accumulator.TurnRate += value;
                break;

            case ShipStatType.CargoCapacity:
                accumulator.CargoCapacity += value;
                break;

            /*
             * В проекте нет ресурса энергии корабля.
             * Legacy-модификаторы энергии игнорируются.
             */
            case ShipStatType.Energy:
            case ShipStatType.EnergyRegen:
                break;

            default:
                break;
        }
    }

    private static void ApplyMultiplicativeModifier(
        ShipStatsAccumulator accumulator,
        ShipStatType statType,
        float value)
    {
        /*
         * value = 0.2 означает +20%.
         * value = -0.1 означает -10%.
         */
        float multiplier =
            1f + value;

        switch (statType)
        {
            case ShipStatType.Hull:
                accumulator.MaxHull *= multiplier;
                break;

            case ShipStatType.Shield:
                accumulator.MaxShield *= multiplier;
                break;

            case ShipStatType.Speed:
                accumulator.MaxSpeed *= multiplier;
                break;

            case ShipStatType.Acceleration:
                accumulator.Acceleration *= multiplier;
                break;

            case ShipStatType.TurnRate:
                accumulator.TurnRate *= multiplier;
                break;

            case ShipStatType.CargoCapacity:
                accumulator.CargoCapacity *= multiplier;
                break;

            /*
             * В проекте нет ресурса энергии корабля.
             * Legacy-модификаторы энергии игнорируются.
             */
            case ShipStatType.Energy:
            case ShipStatType.EnergyRegen:
                break;

            default:
                break;
        }
    }

    private sealed class ShipStatsAccumulator
    {
        private ShipStatsAccumulator()
        {
        }

        public string ShipConfigId { get; private set; }

        public float MaxHull { get; set; }

        public float MaxShield { get; set; }

        public float MaxSpeed { get; set; }

        public float Acceleration { get; set; }

        public float TurnRate { get; set; }

        public float CargoCapacity { get; set; }

        public int WeaponSlotCount { get; private set; }

        public int ModuleSlotCount { get; private set; }

        public Sprite CombatSprite { get; private set; }

        public static ShipStatsAccumulator FromConfig(
            ShipConfig shipConfig)
        {
            return new ShipStatsAccumulator
            {
                ShipConfigId = shipConfig.Id,
                MaxHull = shipConfig.BaseHull,
                MaxShield = shipConfig.BaseShield,
                MaxSpeed = shipConfig.BaseSpeed,
                Acceleration = shipConfig.BaseAcceleration,
                TurnRate = shipConfig.BaseTurnRate,
                CargoCapacity = shipConfig.BaseCargoCapacity,
                WeaponSlotCount = shipConfig.WeaponSlotCount,
                ModuleSlotCount = shipConfig.ModuleSlotCount,
                CombatSprite = shipConfig.CombatSprite
            };
        }

        public void Clamp()
        {
            MaxHull = Mathf.Max(0f, MaxHull);
            MaxShield = Mathf.Max(0f, MaxShield);

            MaxSpeed = Mathf.Max(0f, MaxSpeed);
            Acceleration = Mathf.Max(0f, Acceleration);
            TurnRate = Mathf.Max(0f, TurnRate);

            CargoCapacity = Mathf.Max(0f, CargoCapacity);
        }

        public ShipFinalStats ToFinalStats()
        {
            return new ShipFinalStats(
                ShipConfigId,
                Mathf.RoundToInt(MaxHull),
                Mathf.RoundToInt(MaxShield),
                MaxSpeed,
                Acceleration,
                TurnRate,
                Mathf.RoundToInt(CargoCapacity),
                WeaponSlotCount,
                ModuleSlotCount,
                CombatSprite);
        }
    }
}