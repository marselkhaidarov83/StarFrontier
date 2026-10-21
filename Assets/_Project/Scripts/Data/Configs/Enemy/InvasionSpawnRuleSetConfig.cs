using UnityEngine;

[CreateAssetMenu(fileName = "InvasionSpawnRuleSetConfig", menuName = "StarFrontier/Configs/War/Invasion spawn rules")]
public sealed class InvasionSpawnRuleSetConfig : BaseConfig
{
    [Tooltip("Только группы вторжений. Эти правила не используются для обычной популяции системы.")]
    [SerializeField] private SystemPopulationEnemyGroupRuleEntry[] entries =
        new SystemPopulationEnemyGroupRuleEntry[0];

    public SystemPopulationEnemyGroupRuleEntry[] Entries => entries;

    public bool ContainsRule(EnemyGroupSpawnRuleConfig rule)
    {
        if (rule == null || entries == null)
            return false;
        foreach (SystemPopulationEnemyGroupRuleEntry entry in entries)
        {
            if (entry != null && entry.IsValid() && entry.EnemyGroupSpawnRule == rule)
                return true;
        }
        return false;
    }
}
