using System;
using UnityEngine;

[Serializable]
public sealed class MarketResearchEffect
{
    [Range(0, 10)] public int Level;
    [Range(0, 1)] public float WarProductionLoss = 0.5f;
    [Min(0)] public float WarConsumptionIncrease = 0.5f;
    [Range(0, 1)] public float MinimumEquipmentBuyoutPercent = 0.1f;
}

[CreateAssetMenu(menuName = "StarFrontier/Configs/Market/Economy Balance")]
public sealed class MarketEconomyBalanceConfig : BaseConfig
{
    [Min(1)] public int HullRepairUnitPrice = 2;
    [Tooltip("Цена восстановления одной единицы прочности оборудования.")]
    [Min(1)] public int EquipmentRepairUnitPrice = 2;
    [Min(1)] public int FuelUnitPrice = 10;
    public System.Collections.Generic.List<MarketEquipmentRule> EquipmentCatalog = new System.Collections.Generic.List<MarketEquipmentRule>();
    public System.Collections.Generic.List<MarketHullRule> HullCatalog = new System.Collections.Generic.List<MarketHullRule>();
    public ResearchUpgrade WarProtectionResearch = ResearchUpgrade.None;
    public ResearchUpgrade EquipmentBuyoutResearch = ResearchUpgrade.None;
    [Tooltip("0 — без исследования. Проценты записываются долями: 0.1 = 10%.")]
    public MarketResearchEffect[] ResearchLevels = Array.Empty<MarketResearchEffect>();
    public MarketResearchEffect GetEffect(int level)
    {
        MarketResearchEffect found = null;
        foreach (var row in ResearchLevels ?? Array.Empty<MarketResearchEffect>())
            if (row != null && row.Level <= Math.Max(0, level) && (found == null || row.Level > found.Level)) found = row;
        return found;
    }
}
