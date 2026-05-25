using System.Collections.Generic;

public class ShipStatCalculator
{
    private readonly IConfigService _configService;

    public ShipStatCalculator()
    {
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public ShipStats Calculate(ShipConfig shipData, List<string> equippedModuleIds)
    {
        if (shipData == null)
            return null;

        var stats = new ShipStats
        {
            MaxHull = shipData.BaseHull,
            MaxShield = shipData.BaseShield,
            MaxEnergy = shipData.BaseEnergy,
            Speed = shipData.BaseSpeed,
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

            case ModuleStatType.Speed:
                stats.Speed = ApplyFloatModifier(stats.Speed, moduleData);
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

    private float ApplyFloatModifier(float baseValue, ModuleConfig moduleData)
    {
        var value = baseValue + moduleData.FlatBonus;
        value += value * moduleData.PercentBonus;
        return value;
    }
}