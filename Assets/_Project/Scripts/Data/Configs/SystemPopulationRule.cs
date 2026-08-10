using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemPopulationRule",
    menuName = "StarFrontier/Configs/System/System Population Rule")]
public sealed class SystemPopulationRule : BaseConfig
{
    private static readonly EnemyGroupSpawnRuleConfig[] EmptyEnemyGroupSpawnRules =
        new EnemyGroupSpawnRuleConfig[0];

    [Header("Allies")]
    [SerializeField] private AllySpawnRuleConfig[] allySpawnRules =
        new AllySpawnRuleConfig[0];

    [Header("Enemies")]
    [SerializeField] private SystemPopulationEnemyGroupRuleEntry[] enemyGroupSpawnRuleEntries =
        new SystemPopulationEnemyGroupRuleEntry[0];

    public AllySpawnRuleConfig[] AllySpawnRules => allySpawnRules;

    public EnemyGroupSpawnRuleConfig[] EnemyGroupSpawnRules =>
        BuildEnemyGroupSpawnRulesSnapshot();

    public SystemPopulationEnemyGroupRuleEntry[] EnemyGroupSpawnRuleEntries =>
        enemyGroupSpawnRuleEntries;

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
        if (enemyGroupSpawnRuleEntries == null ||
            enemyGroupSpawnRuleEntries.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < enemyGroupSpawnRuleEntries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                enemyGroupSpawnRuleEntries[i];

            if (entry != null && entry.IsValid())
                return true;
        }

        return false;
    }

    private EnemyGroupSpawnRuleConfig[] BuildEnemyGroupSpawnRulesSnapshot()
    {
        if (enemyGroupSpawnRuleEntries == null ||
            enemyGroupSpawnRuleEntries.Length == 0)
        {
            return EmptyEnemyGroupSpawnRules;
        }

        int count = 0;

        for (int i = 0; i < enemyGroupSpawnRuleEntries.Length; i++)
        {
            if (enemyGroupSpawnRuleEntries[i] != null &&
                enemyGroupSpawnRuleEntries[i].EnemyGroupSpawnRule != null)
            {
                count++;
            }
        }

        if (count <= 0)
            return EmptyEnemyGroupSpawnRules;

        EnemyGroupSpawnRuleConfig[] result =
            new EnemyGroupSpawnRuleConfig[count];

        int index = 0;

        for (int i = 0; i < enemyGroupSpawnRuleEntries.Length; i++)
        {
            if (enemyGroupSpawnRuleEntries[i] == null)
                continue;

            EnemyGroupSpawnRuleConfig rule =
                enemyGroupSpawnRuleEntries[i].EnemyGroupSpawnRule;

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
    [SerializeField] private EnemyGroupSpawnRuleConfig enemyGroupSpawnRule;
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
