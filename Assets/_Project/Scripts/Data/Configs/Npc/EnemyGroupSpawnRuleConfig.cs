using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyGroupSpawnRuleConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy Group Spawn Rule")]
public sealed class EnemyGroupSpawnRuleConfig : BaseConfig
{
    private static readonly EnemyGroupEntryConfig[] EmptyEnemies =
        new EnemyGroupEntryConfig[0];

    [Header("Level Rules")]
    [SerializeField] private EnemyGroupSpawnLevelEntryConfig[] levelEntries =
        new EnemyGroupSpawnLevelEntryConfig[0];

    public IReadOnlyList<EnemyGroupSpawnLevelEntryConfig> LevelEntries =>
        levelEntries;

    /*
     * Legacy compatibility for old editor/debug code.
     * Runtime spawning should use GetEntryForGalaxyLevel(level).
     */
    public float SpawnIntervalSeconds =>
        GetSpawnIntervalSeconds(1);

    public int MaxAliveGroupsFromThisRule =>
        GetMaxAliveGroupsForGalaxyLevel(1);

    public IReadOnlyList<EnemyGroupEntryConfig> Enemies =>
        GetEnemiesForGalaxyLevel(1);

    public EnemyGroupSpawnLevelEntryConfig GetEntryForGalaxyLevel(
        int galaxyLevel)
    {
        int clampedLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

        if (levelEntries == null || levelEntries.Length == 0)
            return null;

        for (int i = 0; i < levelEntries.Length; i++)
        {
            EnemyGroupSpawnLevelEntryConfig entry =
                levelEntries[i];

            if (entry == null)
                continue;

            if (entry.GalaxyLevel == clampedLevel)
                return entry;
        }

        return null;
    }

    public IReadOnlyList<EnemyGroupEntryConfig> GetEnemiesForGalaxyLevel(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null || entry.Enemies == null)
            return EmptyEnemies;

        return entry.Enemies;
    }

    public float GetSpawnIntervalSeconds(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 180f;

        return entry.SpawnIntervalSeconds;
    }

    public int GetMaxAliveGroupsForGalaxyLevel(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 1;

        return entry.MaxAliveGroupsFromThisRule;
    }

    public bool HasValidEnemies()
    {
        return HasValidEnemiesForGalaxyLevel(1);
    }

    public bool HasValidEnemiesForGalaxyLevel(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return false;

        return entry.HasValidEnemies();
    }

    public int GetMinEnemyCount()
    {
        return GetMinEnemyCount(1);
    }

    public int GetMinEnemyCount(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 0;

        return entry.GetMinEnemyCount();
    }

    public int GetMaxEnemyCount()
    {
        return GetMaxEnemyCount(1);
    }

    public int GetMaxEnemyCount(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 0;

        return entry.GetMaxEnemyCount();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelEntries == null)
            levelEntries = new EnemyGroupSpawnLevelEntryConfig[0];

        for (int i = 0; i < levelEntries.Length; i++)
        {
            if (levelEntries[i] == null)
                levelEntries[i] = new EnemyGroupSpawnLevelEntryConfig();

            levelEntries[i].Validate();
        }
    }
#endif
}

[System.Serializable]
public sealed class EnemyGroupSpawnLevelEntryConfig
{
    [SerializeField] [Range(1, 10)] private int galaxyLevel = 1;

    [Header("Spawn Timing")]
    [SerializeField] [Min(0f)] private float spawnIntervalSeconds = 180f;

    [Header("Spawn Limits")]
    [SerializeField] [Min(1)] private int maxAliveGroupsFromThisRule = 1;

    [Header("Enemies")]
    [SerializeField] private EnemyGroupEntryConfig[] enemies =
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
            EnemyGroupEntryConfig entry =
                enemies[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            return true;
        }

        return false;
    }

    public int GetMinEnemyCount()
    {
        if (enemies == null)
            return 0;

        int count = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGroupEntryConfig entry =
                enemies[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            count += entry.MinCount;
        }

        return count;
    }

    public int GetMaxEnemyCount()
    {
        if (enemies == null)
            return 0;

        int count = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGroupEntryConfig entry =
                enemies[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            count += entry.MaxCount;
        }

        return count;
    }

#if UNITY_EDITOR
    public void Validate()
    {
        galaxyLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

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