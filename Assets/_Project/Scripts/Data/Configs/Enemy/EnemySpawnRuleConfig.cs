using System;
using System.Collections.Generic;
using UnityEngine;

// Конфиг EnemySpawnRuleConfig содержит правила появления врагов одной фракции по уровням галактики.
[Serializable]
[CreateAssetMenu(
    fileName = "EnemySpawnRuleConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy spawn rule")]
public sealed class EnemySpawnRuleConfig : BaseConfig
{
    [Header("Faction")]
    [Tooltip("Фракция врагов, для которой действует это правило.")]
    [SerializeField] private EnemyFaction2A faction = EnemyFaction2A.AI;

    [Header("Profiles by Galaxy Level")]
    [SerializeField]
    [Tooltip("Правила по уровням галактики.")]
    private EnemySpawnLevelEntryConfig[] levelEntries =
        new EnemySpawnLevelEntryConfig[0];

    public EnemyFaction2A Faction => faction;

    public IReadOnlyList<EnemySpawnLevelEntryConfig> LevelEntries =>
        levelEntries;

    public EnemySpawnLevelEntryConfig GetEntryForGalaxyLevel(
        int galaxyLevel)
    {
        int normalizedLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

        if (levelEntries == null)
            return null;

        for (int i = 0; i < levelEntries.Length; i++)
        {
            EnemySpawnLevelEntryConfig entry =
                levelEntries[i];

            if (entry == null)
                continue;

            if (entry.GalaxyLevel == normalizedLevel)
                return entry;
        }

        return null;
    }

    public IReadOnlyList<EnemyGroupEntryConfig> GetEnemiesForGalaxyLevel(
        int galaxyLevel)
    {
        EnemySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return Array.Empty<EnemyGroupEntryConfig>();

        return entry.Enemies;
    }

    public float GetSpawnIntervalSeconds(
        int galaxyLevel)
    {
        EnemySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 0f;

        return entry.SpawnIntervalSeconds;
    }

    public int GetMaxAliveGroupsForGalaxyLevel(
        int galaxyLevel)
    {
        EnemySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 1;

        return entry.MaxAliveGroupsFromThisRule;
    }

    public bool HasValidEnemiesForGalaxyLevel(
        int galaxyLevel)
    {
        EnemySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return false;

        return entry.HasValidEnemies();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelEntries == null)
            levelEntries = new EnemySpawnLevelEntryConfig[0];

        for (int i = 0; i < levelEntries.Length; i++)
        {
            if (levelEntries[i] == null)
                levelEntries[i] = new EnemySpawnLevelEntryConfig();

            levelEntries[i].Validate(i + 1);
        }
    }
#endif
}

[Serializable]
public sealed class EnemySpawnLevelEntryConfig
{
    [Header("Galaxy Level")]
    [SerializeField]
    [Range(1, 10)]
    [Tooltip("Уровень галактики, для которого действует эта запись.")]
    private int galaxyLevel = 1;

    [Header("Population")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Пауза между попытками появления группы в секундах.")]
    private float spawnIntervalSeconds = 180f;

    [Header("Spawn Limits")]
    [SerializeField]
    [Min(1)]
    [Tooltip("Максимум живых групп, созданных этим правилом одновременно.")]
    private int maxAliveGroupsFromThisRule = 1;

    [Header("Enemies")]
    [SerializeField]
    [Tooltip("Список врагов этой фракции для появления.")]
    private EnemyGroupEntryConfig[] enemies =
        new EnemyGroupEntryConfig[0];

    public int GalaxyLevel => galaxyLevel;
    public float SpawnIntervalSeconds => spawnIntervalSeconds;
    public int MaxAliveGroupsFromThisRule => maxAliveGroupsFromThisRule;
    public IReadOnlyList<EnemyGroupEntryConfig> Enemies => enemies;

    public bool HasValidEnemies()
    {
        if (enemies == null || enemies.Length == 0)
            return false;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
                continue;

            if (enemies[i].IsValid())
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    public void Validate(
        int fallbackGalaxyLevel)
    {
        galaxyLevel =
            Mathf.Clamp(
                galaxyLevel <= 0 ? fallbackGalaxyLevel : galaxyLevel,
                1,
                10);

        spawnIntervalSeconds =
            Mathf.Max(0f, spawnIntervalSeconds);

        maxAliveGroupsFromThisRule =
            Mathf.Max(1, maxAliveGroupsFromThisRule);

        if (enemies == null)
            enemies = new EnemyGroupEntryConfig[0];

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
                enemies[i] = new EnemyGroupEntryConfig();

            enemies[i].Validate();
        }
    }
#endif
}