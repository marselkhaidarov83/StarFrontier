using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcLootGeneratedKind
{
    Item = 0,
    Equipment = 10,
    Credits = 20,
    Diamonds = 30,
    Astralite = 40
}

[Serializable]
public sealed class NpcLootCargoDefinition
{
    public ItemConfig Item;
    [Min(1)] public int Quantity = 1;
}

[Serializable]
public sealed class NpcLootCargoChance
{
    [Min(1)] public int BatchNumber = 1;
    [Range(0f, 100f)] public float ChancePercent;
}

[Serializable]
public sealed class NpcLootEquipmentChance
{
    [Min(0)] public int SlotIndex;
    [Range(0f, 100f)] public float ChancePercent;
}

[Serializable]
public sealed class NpcLootGeneratedEntry
{
    public NpcLootGeneratedKind Kind;
    public ItemConfig Item;
    public WeaponConfig EquipmentConfig;
    [Min(1)] public int MinAmount = 1;
    [Min(1)] public int MaxAmount = 1;
    [Range(0f, 100f)] public float ChancePercent;
}

[Serializable]
public sealed class NpcLootTriggerRule
{
    [Tooltip("При гибели корабля. Для порога здоровья оставьте выключенным.")]
    public bool IsDeath;
    [Range(1, 99)] public int HullThresholdPercent = 50;
    public NpcLootCargoChance[] CargoBatches = Array.Empty<NpcLootCargoChance>();
    public NpcLootEquipmentChance[] Equipment = Array.Empty<NpcLootEquipmentChance>();
    public NpcLootGeneratedEntry[] Generated = Array.Empty<NpcLootGeneratedEntry>();
}

[CreateAssetMenu(fileName = "npc_loot_table_", menuName = "StarFrontier/Configs/Npc/Loot Table")]
public sealed class NpcLootTableConfig : BaseConfig
{
    [Header("Initial cargo; each entry becomes one whole batch")]
    public NpcLootCargoDefinition[] InitialCargo = Array.Empty<NpcLootCargoDefinition>();

    [Header("Equipment instance defaults")]
    [SerializeField, Range(0, 100)] private int initialEquipmentDurabilityPercent = 100;

    [Header("Loot rules")]
    public NpcLootTriggerRule[] Rules = Array.Empty<NpcLootTriggerRule>();

    [Header("Shared Astralite mapping by enemy species")]
    public EnemyAstraliteCatalogConfig AstraliteCatalog;

    public int InitialEquipmentDurabilityPercent => initialEquipmentDurabilityPercent;

    public int GetInitialCargoQuantity()
    {
        int total = 0;
        if (InitialCargo == null) return total;
        foreach (NpcLootCargoDefinition batch in InitialCargo)
            if (batch != null && batch.Item != null)
                total += Mathf.Max(0, batch.Quantity);
        return total;
    }

    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();
        if (initialEquipmentDurabilityPercent < 0 || initialEquipmentDurabilityPercent > 100)
            errors.Add("Equipment durability must be 0–100%.");

        if (InitialCargo != null)
        {
            for (int i = 0; i < InitialCargo.Length; i++)
            {
                NpcLootCargoDefinition batch = InitialCargo[i];
                if (batch == null || batch.Item == null || batch.Quantity <= 0)
                    errors.Add($"Initial cargo batch {i + 1} needs an item and positive quantity.");
            }
        }

        var thresholds = new HashSet<int>();
        bool hasDeath = false;
        if (Rules == null) return errors;
        for (int i = 0; i < Rules.Length; i++)
        {
            NpcLootTriggerRule rule = Rules[i];
            if (rule == null)
            {
                errors.Add($"Rule {i + 1} is empty.");
                continue;
            }
            if (rule.IsDeath)
            {
                if (hasDeath) errors.Add("Only one death rule is allowed.");
                hasDeath = true;
            }
            else if (rule.HullThresholdPercent <= 0 || rule.HullThresholdPercent >= 100 ||
                     !thresholds.Add(rule.HullThresholdPercent))
            {
                errors.Add($"Rule {i + 1} has an invalid or duplicate hull threshold.");
            }
            var batches = new HashSet<int>();
            if (rule.CargoBatches != null)
                foreach (NpcLootCargoChance chance in rule.CargoBatches)
                    if (chance == null || chance.BatchNumber <= 0 ||
                        !batches.Add(chance.BatchNumber) || !ValidChance(chance.ChancePercent))
                        errors.Add($"Rule {i + 1} has an invalid or duplicate cargo batch chance.");
            var slots = new HashSet<int>();
            if (rule.Equipment != null)
                foreach (NpcLootEquipmentChance chance in rule.Equipment)
                    if (chance == null || chance.SlotIndex < 0 ||
                        !slots.Add(chance.SlotIndex) || !ValidChance(chance.ChancePercent))
                        errors.Add($"Rule {i + 1} has an invalid or duplicate equipment slot chance.");
            if (rule.Generated != null)
                foreach (NpcLootGeneratedEntry entry in rule.Generated)
                {
                    if (entry == null || !ValidChance(entry.ChancePercent) ||
                        entry.MinAmount <= 0 || entry.MaxAmount < entry.MinAmount)
                    {
                        errors.Add($"Rule {i + 1} has an invalid generated drop range/chance.");
                        continue;
                    }
                    if (entry.Kind == NpcLootGeneratedKind.Item && entry.Item == null)
                        errors.Add($"Rule {i + 1} has a generated item without ItemConfig.");
                    if (entry.Kind == NpcLootGeneratedKind.Equipment && entry.EquipmentConfig == null)
                        errors.Add($"Rule {i + 1} has generated equipment without WeaponConfig.");
                    if (entry.Kind == NpcLootGeneratedKind.Astralite && AstraliteCatalog == null)
                        errors.Add($"Rule {i + 1} has Astralite without a species catalog.");
                }
        }
        return errors;
    }

    private static bool ValidChance(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 100f;
}
