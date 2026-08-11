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

    public float SpawnIntervalSeconds =>
        GetSpawnIntervalSeconds(1);

    public int MaxAliveGroupsFromThisRule =>
        GetMaxAliveGroupsForGalaxyLevel(1);

    public IReadOnlyList<EnemyGroupEntryConfig> PickEnemiesForGalaxyLevel(
        int galaxyLevel)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return EmptyEnemies;

        IReadOnlyList<EnemyGroupEntryConfig> pickedEnemies =
            entry.PickEnemies();

        if (pickedEnemies == null)
            return EmptyEnemies;

        return pickedEnemies;
    }

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
    private static readonly EnemyGroupEntryConfig[] EmptyEnemies =
        new EnemyGroupEntryConfig[0];

    [SerializeField] [Range(1, 10)] private int galaxyLevel = 1;

    [Header("Spawn Timing")]
    [SerializeField] [Min(0f)] private float spawnIntervalSeconds = 180f;

    [Header("Spawn Limits")]
    [SerializeField] [Min(1)] private int maxAliveGroupsFromThisRule = 1;

    [Header("Enemy Groups")]
    [SerializeField] private EnemyGroupSpawnOptionConfig[] enemyGroups =
        new EnemyGroupSpawnOptionConfig[0];

    public int GalaxyLevel => galaxyLevel;

    public float SpawnIntervalSeconds => spawnIntervalSeconds;

    public int MaxAliveGroupsFromThisRule => maxAliveGroupsFromThisRule;

    public IReadOnlyList<EnemyGroupSpawnOptionConfig> EnemyGroups => enemyGroups;

    public bool HasValidEnemies()
    {
        return HasValidEnemyGroups();
    }

    public bool HasValidEnemyGroups()
    {
        if (enemyGroups == null || enemyGroups.Length == 0)
            return false;

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            EnemyGroupSpawnOptionConfig group =
                enemyGroups[i];

            if (group == null)
                continue;

            if (!group.HasValidEnemies())
                continue;

            return true;
        }

        return false;
    }

    public IReadOnlyList<EnemyGroupEntryConfig> PickEnemies()
    {
        EnemyGroupSpawnOptionConfig group =
            PickEnemyGroup();

        if (group != null && group.HasValidEnemies())
            return group.Enemies;

        return EmptyEnemies;
    }

    public EnemyGroupSpawnOptionConfig PickEnemyGroup()
    {
        if (!HasValidEnemyGroups())
            return null;

        int totalWeight = 0;

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            EnemyGroupSpawnOptionConfig group =
                enemyGroups[i];

            if (group == null || !group.HasValidEnemies())
                continue;

            totalWeight += Mathf.Max(1, group.Weight);
        }

        if (totalWeight <= 0)
            return null;

        int roll =
            UnityEngine.Random.Range(0, totalWeight);

        int cumulative = 0;

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            EnemyGroupSpawnOptionConfig group =
                enemyGroups[i];

            if (group == null || !group.HasValidEnemies())
                continue;

            cumulative += Mathf.Max(1, group.Weight);

            if (roll < cumulative)
                return group;
        }

        return null;
    }

    public int GetMinEnemyCount()
    {
        return GetMinEnemyGroupCount();
    }

    public int GetMaxEnemyCount()
    {
        return GetMaxEnemyGroupCount();
    }

    private int GetMinEnemyGroupCount()
    {
        int count = 0;
        bool hasValidGroup = false;

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            EnemyGroupSpawnOptionConfig group =
                enemyGroups[i];

            if (group == null || !group.HasValidEnemies())
                continue;

            if (!hasValidGroup || group.GetMinEnemyCount() < count)
                count = group.GetMinEnemyCount();

            hasValidGroup = true;
        }

        return hasValidGroup ? count : 0;
    }

    private int GetMaxEnemyGroupCount()
    {
        int count = 0;

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            EnemyGroupSpawnOptionConfig group =
                enemyGroups[i];

            if (group == null || !group.HasValidEnemies())
                continue;

            count = Mathf.Max(
                count,
                group.GetMaxEnemyCount());
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

        if (enemyGroups == null)
            enemyGroups = new EnemyGroupSpawnOptionConfig[0];

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            if (enemyGroups[i] == null)
                enemyGroups[i] = new EnemyGroupSpawnOptionConfig();

            enemyGroups[i].Validate();
        }
    }
#endif
}

[System.Serializable]
public sealed class EnemyGroupSpawnOptionConfig
{
    [SerializeField] [Min(1)] private int weight = 1;

    [SerializeField] private EnemyGroupEntryConfig[] enemies =
        new EnemyGroupEntryConfig[0];

    public int Weight => weight;

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
        return GetEnemyCount(useMaxCount: false);
    }

    public int GetMaxEnemyCount()
    {
        return GetEnemyCount(useMaxCount: true);
    }

    private int GetEnemyCount(
        bool useMaxCount)
    {
        if (enemies == null)
            return 0;

        int count = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGroupEntryConfig entry =
                enemies[i];

            if (entry == null || !entry.IsValid())
                continue;

            count += useMaxCount
                ? entry.MaxCount
                : entry.MinCount;
        }

        return count;
    }

#if UNITY_EDITOR
    public void Validate()
    {
        weight =
            Mathf.Max(1, weight);

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
