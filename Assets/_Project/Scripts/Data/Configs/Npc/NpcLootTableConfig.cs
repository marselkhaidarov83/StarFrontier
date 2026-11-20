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
    [Tooltip("Товар в начальном трюме корабля.")]
    public ItemConfig Item;
    [Tooltip("Количество единиц в отдельной партии груза.")]
    [Min(1)] public int Quantity = 1;
}

[Serializable]
public sealed class NpcLootCargoChance
{
    [Tooltip("Номер начальной партии груза, начиная с 1.")]
    [Min(1)] public int BatchNumber = 1;
    [Range(0f, 100f)] public float ChancePercent;
}

[Serializable]
public sealed class NpcLootEquipmentChance
{
    [Tooltip("Включено — компонент корабля; выключено — оружие. Выпадение разрешено только при 100% урона.")]
    public bool IsComponent;
    [Min(0)] public int SlotIndex;
    [Range(0f, 100f)] public float ChancePercent;
}

[Serializable]
public sealed class NpcLootGeneratedEntry
{
    [Tooltip("Что создать: товар, оружие, кредиты, алмазы или астралит.")]
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
    [Tooltip("Потерянная доля суммарного максимального запаса корпуса и щита. 100% — уничтожение корабля.")]
    [Range(20, 100)] public int DamageThresholdPercent = 20;
    public bool IsDeath => DamageThresholdPercent == 100;
    public NpcLootCargoChance[] CargoBatches = Array.Empty<NpcLootCargoChance>();
    public NpcLootEquipmentChance[] Equipment = Array.Empty<NpcLootEquipmentChance>();
    public NpcLootGeneratedEntry[] Generated = Array.Empty<NpcLootGeneratedEntry>();
}

[CreateAssetMenu(fileName = "npc_loot_table_", menuName = "StarFrontier/Configs/Npc/Loot Table")]
public sealed class NpcLootTableConfig : BaseConfig
{
    [Header("Начальный груз: каждая запись — отдельная партия")]
    public NpcLootCargoDefinition[] InitialCargo = Array.Empty<NpcLootCargoDefinition>();

    [Header("Начальная прочность установленного оснащения")]
    [Tooltip("-1 — полная прочность; иначе указанное количество единиц прочности оружия и компонентов.")]
    [SerializeField, Min(-1)] private int initialEquipmentDurability = -1;

    [Header("Правила выпадения при 20, 40, 60, 80 и 100% урона")]
    public NpcLootTriggerRule[] Rules = {
        new NpcLootTriggerRule { DamageThresholdPercent = 20 }, new NpcLootTriggerRule { DamageThresholdPercent = 40 },
        new NpcLootTriggerRule { DamageThresholdPercent = 60 }, new NpcLootTriggerRule { DamageThresholdPercent = 80 },
        new NpcLootTriggerRule { DamageThresholdPercent = 100 } };

    [Header("Каталог астралитов по фракциям врагов")]
    public EnemyAstraliteCatalogConfig AstraliteCatalog;

    public int InitialEquipmentDurability => initialEquipmentDurability;

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
        if (initialEquipmentDurability < -1)
            errors.Add("Начальная прочность оснащения: -1 (полная) либо неотрицательное количество единиц.");

        if (InitialCargo != null)
        {
            for (int i = 0; i < InitialCargo.Length; i++)
            {
                NpcLootCargoDefinition batch = InitialCargo[i];
                if (batch == null || batch.Item == null || batch.Quantity <= 0)
                    errors.Add($"Начальная партия {i + 1}: укажите товар и положительное количество.");
            }
        }

        var thresholds = new HashSet<int>();
        if (Rules == null || Rules.Length != 5)
        { errors.Add("Должно быть ровно пять правил: 20, 40, 60, 80 и 100% урона."); return errors; }
        for (int i = 0; i < Rules.Length; i++)
        {
            NpcLootTriggerRule rule = Rules[i];
            if (rule == null)
            {
                errors.Add($"Правило {i + 1} не заполнено.");
                continue;
            }
            if (rule.DamageThresholdPercent != (i + 1) * 20 || !thresholds.Add(rule.DamageThresholdPercent))
            {
                errors.Add($"Правило {i + 1}: ожидается порог {(i + 1) * 20}% урона.");
            }
            if (!rule.IsDeath && rule.Equipment != null && rule.Equipment.Length > 0)
                errors.Add($"Правило {i + 1}: оснащение может выпадать только при 100% урона.");
            var batches = new HashSet<int>();
            if (rule.CargoBatches != null)
                foreach (NpcLootCargoChance chance in rule.CargoBatches)
                    if (chance == null || chance.BatchNumber <= 0 ||
                        !batches.Add(chance.BatchNumber) || !ValidChance(chance.ChancePercent))
                        errors.Add($"Правило {i + 1}: неверная либо повторная запись партии груза или её вероятности.");
            var slots = new HashSet<int>();
            if (rule.Equipment != null)
                foreach (NpcLootEquipmentChance chance in rule.Equipment)
                    if (chance == null || chance.SlotIndex < 0 ||
                        chance.SlotIndex >= (chance.IsComponent ? ShipLoadoutRules.ComponentSlots : ShipLoadoutRules.WeaponSlots) ||
                        !slots.Add(chance.SlotIndex + (chance.IsComponent ? ShipLoadoutRules.WeaponSlots : 0)) || !ValidChance(chance.ChancePercent))
                        errors.Add($"Правило {i + 1}: неверный либо повторный слот оснащения или его вероятность.");
            if (rule.Generated != null)
                foreach (NpcLootGeneratedEntry entry in rule.Generated)
                {
                    if (entry == null || !Enum.IsDefined(typeof(NpcLootGeneratedKind), entry.Kind) || !ValidChance(entry.ChancePercent) ||
                        entry.MinAmount <= 0 || entry.MaxAmount < entry.MinAmount)
                    {
                        errors.Add($"Правило {i + 1}: неверный тип, диапазон количества или вероятность создаваемой добычи.");
                        continue;
                    }
                    if (entry.Kind == NpcLootGeneratedKind.Item && entry.Item == null)
                        errors.Add($"Правило {i + 1}: создаваемый товар не указан.");
                    if (entry.Kind == NpcLootGeneratedKind.Equipment && entry.EquipmentConfig == null)
                        errors.Add($"Правило {i + 1}: создаваемое оружие не указано.");
                    if (entry.Kind == NpcLootGeneratedKind.Equipment && !rule.IsDeath)
                        errors.Add($"Правило {i + 1}: создаваемое оружие может выпадать только при 100% урона.");
                    if (entry.Kind == NpcLootGeneratedKind.Astralite && AstraliteCatalog == null)
                        errors.Add($"Правило {i + 1}: для астралитов отсутствует каталог фракций.");
                }
        }
        return errors;
    }

    private static bool ValidChance(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 100f;
}
