using System;
using UnityEngine;

// Конфиг EnemySpawnRuleSetConfig собирает правила спавна врагов разных фракций в один ассет для системы.
[Serializable]
[CreateAssetMenu(
    fileName = "EnemySpawnRuleSetConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy spawn rule set")]
public sealed class EnemySpawnRuleSetConfig : BaseConfig
{
    [Header("Faction Rules")]
    [Tooltip("Правила спавна врагов по фракциям.")]
    [SerializeField] private EnemySpawnRuleSetEntry[] factionRules =
        new EnemySpawnRuleSetEntry[0];

    public EnemySpawnRuleSetEntry[] FactionRules => factionRules;

    public EnemySpawnRuleConfig GetRuleForFaction(
        EnemyFaction2A faction)
    {
        if (factionRules == null)
            return null;

        for (int i = 0; i < factionRules.Length; i++)
        {
            EnemySpawnRuleSetEntry entry =
                factionRules[i];

            if (entry == null)
                continue;

            if (entry.Faction != faction)
                continue;

            return entry.SpawnRule;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (factionRules == null)
            factionRules = new EnemySpawnRuleSetEntry[0];

        for (int i = 0; i < factionRules.Length; i++)
        {
            if (factionRules[i] == null)
                factionRules[i] = new EnemySpawnRuleSetEntry();
        }
    }
#endif
}

[Serializable]
public sealed class EnemySpawnRuleSetEntry
{
    [Tooltip("Фракция врагов.")]
    [SerializeField] private EnemyFaction2A faction = EnemyFaction2A.AI;

    [Tooltip("Правило спавна врагов этой фракции.")]
    [SerializeField] private EnemySpawnRuleConfig spawnRule;

    public EnemyFaction2A Faction => faction;
    public EnemySpawnRuleConfig SpawnRule => spawnRule;
}