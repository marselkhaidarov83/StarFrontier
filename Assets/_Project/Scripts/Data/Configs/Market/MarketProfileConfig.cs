using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MarketGalaxyLevelSettings
{
    [InspectorName("Уровень рынка"), Min(1)] public int GalaxyLevel = 1;
    [Min(0.01f)] public float IntervalSeconds = 60;
    [Min(0)] public int HullOfferCount;
    [Min(0)] public int WeaponOfferCount;
    [Min(0)] public int EquipmentOfferCount;
    public List<MarketItemEntry> Items = new List<MarketItemEntry>();
    public List<MarketEquipmentRule> Equipment = new List<MarketEquipmentRule>();
    public List<MarketHullRule> Hulls = new List<MarketHullRule>();
}

[Serializable]
public sealed class MarketEquipmentRule
{
    public WeaponConfig Weapon;
    public ShipEquipmentConfig Component;

    public string EquipmentId => Component != null ? Component.Id : Weapon != null ? Weapon.Id : null;

    public bool Matches(SystemNpcWeaponRuntimeState item) =>
        item != null && (Component != null ? item.ComponentConfigId == Component.Id : Weapon != null && item.WeaponConfigId == Weapon.Id);

    [Tooltip("Только союзное оборудование доступно для покупки и обратного выкупа.")]
    public bool Allied = true;
    [Min(0)] public int Weight = 100;
    [Min(1)] public int NewPrice = 100;
}

[Serializable]
public sealed class MarketHullRule
{
    public HullConfig Hull;
    [Min(0)] public int Weight = 100;
    [Min(1)] public int NewPrice = 1000;
}

[CreateAssetMenu(fileName = "MarketProfile", menuName = "StarFrontier/Configs/Market/MarketProfile")]
public class MarketProfileConfig : BaseConfig
{
    [Tooltip("Период полной замены непроданных корпусов, оружия и оборудования.")]
    [Min(1)] public int OfferRefreshIntervalSeconds = 300;
    public List<MarketGalaxyLevelSettings> GalaxyLevels = new List<MarketGalaxyLevelSettings>();

    // Highest configured level <= current; sparse tables intentionally inherit the previous tier.
    public MarketGalaxyLevelSettings GetLevel(int level) => FindLevel(GalaxyLevels, level);

    private static MarketGalaxyLevelSettings FindLevel(List<MarketGalaxyLevelSettings> levels, int level)
    {
        MarketGalaxyLevelSettings found = null;
        int requested = Math.Max(1, level);
        if (levels != null)
            for (int i = 0; i < levels.Count; i++)
            {
                var row = levels[i];
                if (row != null && row.GalaxyLevel <= requested && (found == null || row.GalaxyLevel > found.GalaxyLevel)) found = row;
            }
        return found;
    }
}
