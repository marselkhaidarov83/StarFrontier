using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MarketGalaxyLevelSettings
{
    [Min(1)] public int GalaxyLevel = 1;
    [Min(0.01f)] public float IntervalSeconds = 60;
    public List<MarketItemEntry> Items = new List<MarketItemEntry>();
    public List<MarketEquipmentRule> Equipment = new List<MarketEquipmentRule>();
    public List<MarketHullRule> Hulls = new List<MarketHullRule>();
}

[Serializable]
public sealed class MarketEquipmentRule
{
    public WeaponConfig Weapon;
    [Tooltip("Только союзное оборудование доступно для покупки и обратного выкупа.")]
    public bool Allied = true;
    [Min(1)] public int NewPrice = 100;
    [Min(0.01f)] public float SpawnIntervalSeconds = 300;
    [Min(0.01f)] public float LifetimeSeconds = 600;
    [Min(1)] public int MaximumListings = 2;
}

[Serializable]
public sealed class MarketHullRule
{
    public HullEquipmentConfig Hull;
    [Tooltip("Добавить один корпус при первом появлении этого предложения на рынке.")]
    public bool StartInStock;
    [Min(1)] public int NewPrice = 1000;
    [Min(0.01f)] public float SpawnIntervalSeconds = 300;
    [Min(0.01f)] public float LifetimeSeconds = 900;
    [Min(1)] public int MaximumListings = 1;
}

[CreateAssetMenu(fileName = "MarketProfile", menuName = "StarFrontier/Configs/Market/MarketProfile")]
public class MarketProfileConfig : BaseConfig
{
    [SerializeField] private MarketSpecializationType specialization;
    [Tooltip("Старый ассортимент. Используется только профилями без настроек уровней.")]
    [SerializeField] private List<MarketItemEntry> items = new List<MarketItemEntry>();
    public List<MarketGalaxyLevelSettings> GalaxyLevels = new List<MarketGalaxyLevelSettings>();
    public MarketSpecializationType Specialization => specialization;
    public List<MarketItemEntry> Items => items;

    // Highest configured level <= current; sparse tables intentionally inherit the previous tier.
    public MarketGalaxyLevelSettings GetLevel(int level)
    {
        MarketGalaxyLevelSettings found = null;
        int requested = Math.Max(1, level);
        if (GalaxyLevels != null)
            for (int i = 0; i < GalaxyLevels.Count; i++)
            {
                var row = GalaxyLevels[i];
                if (row != null && row.GalaxyLevel <= requested && (found == null || row.GalaxyLevel > found.GalaxyLevel)) found = row;
            }
        return found;
    }
}
