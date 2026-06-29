using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ShipStatsServiceEditModeTests
{
    [Test]
    public void CalculateFromConfig_CopiesBaseShipStats()
    {
        ShipStatsService service =
            new ShipStatsService();

        ShipConfig shipConfig =
            CreateShipConfig(
                "ship_test",
                hull: 100,
                shield: 50,
                speed: 7f,
                acceleration: 18f,
                turnRate: 240f,
                cargoCapacity: 20,
                weaponSlots: 2,
                moduleSlots: 3);

        ShipFinalStats stats =
            service.CalculateFromConfig(shipConfig);

        Assert.AreEqual("ship_test", stats.ShipConfigId);
        Assert.AreEqual(100, stats.MaxHull);
        Assert.AreEqual(50, stats.MaxShield);
        Assert.AreEqual(7f, stats.MaxSpeed);
        Assert.AreEqual(18f, stats.Acceleration);
        Assert.AreEqual(240f, stats.TurnRate);
        Assert.AreEqual(20, stats.CargoCapacity);
        Assert.AreEqual(2, stats.WeaponSlotCount);
        Assert.AreEqual(3, stats.ModuleSlotCount);
    }

    [Test]
    public void Calculate_WithNullShipConfig_Throws()
    {
        ShipStatsService service =
            new ShipStatsService();

        Assert.Throws<System.ArgumentNullException>(
            () => service.CalculateFromConfig(null));
    }

    [Test]
    public void Calculate_WithAdditiveModule_AppliesBonus()
    {
        ShipStatsService service =
            new ShipStatsService();

        ShipConfig shipConfig =
            CreateShipConfig(
                "ship_test",
                hull: 100,
                shield: 50,
                speed: 7f,
                acceleration: 18f,
                turnRate: 240f,
                cargoCapacity: 20,
                weaponSlots: 2,
                moduleSlots: 3);

        ModuleConfig moduleConfig =
            CreateModuleConfig(
                CreateModifier(
                    ShipStatType.Hull,
                    StatModifierType.Additive,
                    25f),
                CreateModifier(
                    ShipStatType.Speed,
                    StatModifierType.Additive,
                    3f));

        ShipFinalStats stats =
            service.Calculate(
                shipConfig,
                new[] { moduleConfig });

        Assert.AreEqual(125, stats.MaxHull);
        Assert.AreEqual(10f, stats.MaxSpeed);
    }

    [Test]
    public void Calculate_WithMultiplicativeModule_AppliesPercentBonus()
    {
        ShipStatsService service =
            new ShipStatsService();

        ShipConfig shipConfig =
            CreateShipConfig(
                "ship_test",
                hull: 100,
                shield: 50,
                speed: 10f,
                acceleration: 20f,
                turnRate: 100f,
                cargoCapacity: 20,
                weaponSlots: 2,
                moduleSlots: 3);

        ModuleConfig moduleConfig =
            CreateModuleConfig(
                CreateModifier(
                    ShipStatType.Speed,
                    StatModifierType.Multiplicative,
                    0.5f));

        ShipFinalStats stats =
            service.Calculate(
                shipConfig,
                new[] { moduleConfig });

        Assert.AreEqual(15f, stats.MaxSpeed);
    }

    [Test]
    public void Calculate_WithEnergyModifiers_IgnoresThem()
    {
        ShipStatsService service =
            new ShipStatsService();

        ShipConfig shipConfig =
            CreateShipConfig(
                "ship_test",
                hull: 100,
                shield: 50,
                speed: 10f,
                acceleration: 20f,
                turnRate: 100f,
                cargoCapacity: 20,
                weaponSlots: 2,
                moduleSlots: 3);

        ModuleConfig moduleConfig =
            CreateModuleConfig(
                CreateModifier(
                    ShipStatType.Energy,
                    StatModifierType.Additive,
                    999f),
                CreateModifier(
                    ShipStatType.EnergyRegen,
                    StatModifierType.Multiplicative,
                    999f));

        ShipFinalStats stats =
            service.Calculate(
                shipConfig,
                new[] { moduleConfig });

        Assert.AreEqual(100, stats.MaxHull);
        Assert.AreEqual(50, stats.MaxShield);
        Assert.AreEqual(10f, stats.MaxSpeed);
    }

    [Test]
    public void Calculate_WithNullModule_SkipsIt()
    {
        ShipStatsService service =
            new ShipStatsService();

        ShipConfig shipConfig =
            CreateShipConfig(
                "ship_test",
                hull: 100,
                shield: 50,
                speed: 10f,
                acceleration: 20f,
                turnRate: 100f,
                cargoCapacity: 20,
                weaponSlots: 2,
                moduleSlots: 3);

        ShipFinalStats stats =
            service.Calculate(
                shipConfig,
                new ModuleConfig[] { null });

        Assert.AreEqual(100, stats.MaxHull);
        Assert.AreEqual(10f, stats.MaxSpeed);
    }

    [Test]
    public void Calculate_WithNegativeResult_ClampsToZero()
    {
        ShipStatsService service =
            new ShipStatsService();

        ShipConfig shipConfig =
            CreateShipConfig(
                "ship_test",
                hull: 100,
                shield: 50,
                speed: 10f,
                acceleration: 20f,
                turnRate: 100f,
                cargoCapacity: 20,
                weaponSlots: 2,
                moduleSlots: 3);

        ModuleConfig moduleConfig =
            CreateModuleConfig(
                CreateModifier(
                    ShipStatType.Hull,
                    StatModifierType.Additive,
                    -500f),
                CreateModifier(
                    ShipStatType.Speed,
                    StatModifierType.Additive,
                    -500f));

        ShipFinalStats stats =
            service.Calculate(
                shipConfig,
                new[] { moduleConfig });

        Assert.AreEqual(0, stats.MaxHull);
        Assert.AreEqual(0f, stats.MaxSpeed);
    }

    [Test]
    public void ShipStatsService_IsNotTickable()
    {
        bool isTickable =
            typeof(ITickable).IsAssignableFrom(
                typeof(ShipStatsService));

        Assert.IsFalse(
            isTickable,
            "ShipStatsService не должен обновляться каждый кадр.");
    }

    private static ShipConfig CreateShipConfig(
        string id,
        int hull,
        int shield,
        float speed,
        float acceleration,
        float turnRate,
        int cargoCapacity,
        int weaponSlots,
        int moduleSlots)
    {
        ShipConfig config =
            ScriptableObject.CreateInstance<ShipConfig>();

        SetPrivateField(config, "id", id);
        SetPrivateField(config, "baseHull", hull);
        SetPrivateField(config, "baseShield", shield);

        /*
         * Legacy-поля энергии заполняем,
         * чтобы убедиться, что сервис от них не зависит.
         */
        SetPrivateField(config, "baseEnergy", 999);
        SetPrivateField(config, "baseEnergyRegen", 999f);

        SetPrivateField(config, "baseSpeed", speed);
        SetPrivateField(config, "baseAcceleration", acceleration);
        SetPrivateField(config, "baseTurnRate", turnRate);
        SetPrivateField(config, "baseCargoCapacity", cargoCapacity);
        SetPrivateField(config, "weaponSlotCount", weaponSlots);
        SetPrivateField(config, "moduleSlotCount", moduleSlots);

        return config;
    }

    private static ModuleConfig CreateModuleConfig(
        params StatModifierData[] modifiers)
    {
        ModuleConfig config =
            ScriptableObject.CreateInstance<ModuleConfig>();

        SetPrivateField(config, "statModifiers", modifiers);

        return config;
    }

    private static StatModifierData CreateModifier(
        ShipStatType statType,
        StatModifierType modifierType,
        float value)
    {
        StatModifierData modifier =
            new StatModifierData();

        object boxedModifier =
            modifier;

        SetPrivateField(
            boxedModifier,
            "statType",
            statType);

        SetPrivateField(
            boxedModifier,
            "modifierType",
            modifierType);

        SetPrivateField(
            boxedModifier,
            "value",
            value);

        return (StatModifierData)boxedModifier;
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field =
            FindField(
                target.GetType(),
                fieldName);

        Assert.IsNotNull(
            field,
            $"Field '{fieldName}' was not found on {target.GetType().Name}.");

        field.SetValue(target, value);
    }

    private static FieldInfo FindField(
        System.Type type,
        string fieldName)
    {
        while (type != null)
        {
            FieldInfo field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }
}
