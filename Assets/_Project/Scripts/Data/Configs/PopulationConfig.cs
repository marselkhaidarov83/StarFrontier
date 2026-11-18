using System;
using UnityEngine;

// Правила населения звёздной системы или уровней станции.
[CreateAssetMenu(
    fileName = "PopulationConfig",
    menuName = "StarFrontier/Configs/Galaxy/Population")]
public sealed class PopulationConfig : BaseConfig
{
    private static readonly EnemyGroupSpawnRuleConfig[] EmptyEnemyGroupSpawnRules =
        new EnemyGroupSpawnRuleConfig[0];

    [Header("Allies")]
    [Tooltip("Параметр allySpawnRules. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private AllySpawnRuleConfig[] allySpawnRules =
        new AllySpawnRuleConfig[0];

    [Header("Invasion Rules")]
    [SerializeField] private InvasionSpawnRuleSetConfig invasionSpawnRuleSet;
    public InvasionSpawnRuleSetConfig InvasionSpawnRuleSet =>
        StationLevels != null && StationLevels.Length > 0 ? null : invasionSpawnRuleSet;

    [HideInInspector]
    [Tooltip("Параметр enemyGroupSpawnRuleEntries. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private SystemPopulationEnemyGroupRuleEntry[] enemyGroupSpawnRuleEntries =
        new SystemPopulationEnemyGroupRuleEntry[0];

    [Header("Уровни станции")]
    [Tooltip("Правила появления союзников по уровням станции. Для систем список пуст.")]
    public StationPopulationLevel[] StationLevels = Array.Empty<StationPopulationLevel>();

    public AllySpawnRuleConfig[] AllySpawnRules => allySpawnRules;

    public AllySpawnRuleConfig[] GetAllySpawnRules(int stationLevel)
    {
        foreach (var level in StationLevels ?? Array.Empty<StationPopulationLevel>())
        {
            if (level != null && level.Level == stationLevel)
                return level.AllySpawnRules ?? Array.Empty<AllySpawnRuleConfig>();
        }

        return Array.Empty<AllySpawnRuleConfig>();
    }

    public EnemyGroupSpawnRuleConfig[] EnemyGroupSpawnRules =>
        BuildEnemyGroupSpawnRulesSnapshot();

    // Compatibility accessors for older editor tools; production uses InvasionSpawnRuleSet directly.
    public SystemPopulationEnemyGroupRuleEntry[] EnemyGroupSpawnRuleEntries =>
        InvasionSpawnRuleSet != null ? InvasionSpawnRuleSet.Entries : Array.Empty<SystemPopulationEnemyGroupRuleEntry>();

    public bool HasAnySpawnRule()
    {
        if (HasAnyAllySpawnRule())
            return true;

        if (HasAnyEnemyGroupSpawnRule())
            return true;

        return false;
    }

    public bool HasAnyAllySpawnRule()
    {
        if (allySpawnRules == null || allySpawnRules.Length == 0)
            return false;

        for (int i = 0; i < allySpawnRules.Length; i++)
        {
            if (allySpawnRules[i] != null)
                return true;
        }

        return false;
    }

    public bool HasAnyEnemyGroupSpawnRule()
    {
        if (EnemyGroupSpawnRuleEntries == null ||
            EnemyGroupSpawnRuleEntries.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < EnemyGroupSpawnRuleEntries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                EnemyGroupSpawnRuleEntries[i];

            if (entry != null && entry.IsValid())
                return true;
        }

        return false;
    }

    private EnemyGroupSpawnRuleConfig[] BuildEnemyGroupSpawnRulesSnapshot()
    {
        if (EnemyGroupSpawnRuleEntries == null ||
            EnemyGroupSpawnRuleEntries.Length == 0)
        {
            return EmptyEnemyGroupSpawnRules;
        }

        int count = 0;

        for (int i = 0; i < EnemyGroupSpawnRuleEntries.Length; i++)
        {
            if (EnemyGroupSpawnRuleEntries[i] != null &&
                EnemyGroupSpawnRuleEntries[i].EnemyGroupSpawnRule != null)
            {
                count++;
            }
        }

        if (count <= 0)
            return EmptyEnemyGroupSpawnRules;

        EnemyGroupSpawnRuleConfig[] result =
            new EnemyGroupSpawnRuleConfig[count];

        int index = 0;

        for (int i = 0; i < EnemyGroupSpawnRuleEntries.Length; i++)
        {
            if (EnemyGroupSpawnRuleEntries[i] == null)
                continue;

            EnemyGroupSpawnRuleConfig rule =
                EnemyGroupSpawnRuleEntries[i].EnemyGroupSpawnRule;

            if (rule == null)
                continue;

            result[index] = rule;
            index++;
        }

        return result;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (allySpawnRules == null)
            allySpawnRules = new AllySpawnRuleConfig[0];

        if (enemyGroupSpawnRuleEntries == null)
            enemyGroupSpawnRuleEntries = new SystemPopulationEnemyGroupRuleEntry[0];

        for (int i = 0; i < enemyGroupSpawnRuleEntries.Length; i++)
        {
            if (enemyGroupSpawnRuleEntries[i] == null)
                enemyGroupSpawnRuleEntries[i] = new SystemPopulationEnemyGroupRuleEntry();

            enemyGroupSpawnRuleEntries[i].Validate();
        }
    }
#endif
}

[System.Serializable]
public sealed class SystemPopulationEnemyGroupRuleEntry
{
    [Tooltip("Параметр enemyGroupSpawnRule. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private EnemyGroupSpawnRuleConfig enemyGroupSpawnRule;
    [Tooltip("Относительный вес выбора этой записи. Чем больше значение, тем чаще запись выбирается среди других подходящих.")]
    [SerializeField] [Min(1)] private int weight = 1;

    public EnemyGroupSpawnRuleConfig EnemyGroupSpawnRule => enemyGroupSpawnRule;

    public int Weight => weight;

    public bool IsValid()
    {
        if (enemyGroupSpawnRule == null)
            return false;

        if (weight <= 0)
            return false;

        return true;
    }

#if UNITY_EDITOR
    public void Validate()
    {
        weight =
            Mathf.Max(1, weight);
    }
#endif
}

[Serializable]
public sealed class StationPopulationLevel
{
    [Range(1, 10)] public int Level = 1;
    public AllySpawnRuleConfig[] AllySpawnRules = Array.Empty<AllySpawnRuleConfig>();
}
