using System.Collections.Generic;
using UnityEngine;

public class ShipStatCalculator
{
    private readonly IConfigService _configService;

    public ShipStatCalculator(IConfigService configService)
    {
        _configService = configService;
    }

    public static float GetEngineSpeed(ShipRuntimeData ship, IConfigService config)
    {
        if (ship == null) return 0f;
        if (!ship.HasModularLoadout)
            return Mathf.Max(0f, config.GetAllyConfigById(ship.AllyConfigId)?.DefaultEngine?.Speed ?? 0f);

        var instance = ShipLoadoutRules.Component(ship, ShipComponentSlot.Engine);
        var engine = config.GetItemConfigById(instance?.ComponentConfigId) as ShipEquipmentConfig;
        return engine != null && engine.Slot == ShipComponentSlot.Engine
            ? Mathf.Max(0f, instance.GetComponentStats(engine).Speed) : 0f;
    }

    public ShipStats Calculate(AllyConfig shipData, List<string> equippedModuleIds)
    {
        if (shipData == null)
            return null;

        var stats = new ShipStats
        {
            MaxHull = shipData.BaseHull,
            MaxShield = shipData.BaseShield,
            MaxEnergy = shipData.BaseEnergy,
            Speed = Mathf.Max(0f, shipData.DefaultEngine?.Speed ?? 0f),
            CargoCapacity = shipData.BaseCargoCapacity
        };

        if (equippedModuleIds == null)
            return stats;

        foreach (var moduleId in equippedModuleIds)
        {
            if (string.IsNullOrEmpty(moduleId))
                continue;

            ModuleConfig moduleData = _configService.GetModuleConfigById(moduleId);

            if (moduleData == null)
                continue;

            ApplyModule(stats, moduleData);
        }

        return stats;
    }

    private void ApplyModule(ShipStats stats, ModuleConfig moduleData)
    {
        if (stats == null || moduleData == null)
            return;

        switch (moduleData.StatType)
        {
            case ModuleStatType.Hull:
                stats.MaxHull = ApplyIntModifier(stats.MaxHull, moduleData);
                break;

            case ModuleStatType.Shield:
                stats.MaxShield = ApplyIntModifier(stats.MaxShield, moduleData);
                break;

            case ModuleStatType.Energy:
                stats.MaxEnergy = ApplyIntModifier(stats.MaxEnergy, moduleData);
                break;

            case ModuleStatType.Cargo:
                stats.CargoCapacity = ApplyIntModifier(stats.CargoCapacity, moduleData);
                break;
        }
    }

    private int ApplyIntModifier(int baseValue, ModuleConfig moduleData)
    {
        var value = baseValue + moduleData.FlatBonus;
        value += (int)(value * moduleData.PercentBonus);
        return value;
    }
}
