using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(
    fileName = "AllySpawnRuleConfig",
    menuName = "StarFrontier/Configs/Npc/Ally spawn rule")]
public sealed class AllySpawnRuleConfig : BaseConfig
{
    [Header("Profiles by Galaxy Level")]
    [Tooltip("Exactly one entry is expected for every galaxy level from 1 to 10.")]
    [SerializeField]
    private AllySpawnLevelEntryConfig[] levelEntries =
        new AllySpawnLevelEntryConfig[0];

    public IReadOnlyList<AllySpawnLevelEntryConfig> LevelEntries =>
        levelEntries;

    public AllySpawnLevelEntryConfig GetEntryForGalaxyLevel(
        int galaxyLevel)
    {
        int normalizedLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

        if (levelEntries == null)
            return null;

        for (int i = 0; i < levelEntries.Length; i++)
        {
            AllySpawnLevelEntryConfig entry =
                levelEntries[i];

            if (entry == null)
                continue;

            if (entry.GalaxyLevel == normalizedLevel)
                return entry;
        }

        return null;
    }

    public IReadOnlyList<AllyGroupEntryConfig> GetAlliesForGalaxyLevel(
        int galaxyLevel)
    {
        AllySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return Array.Empty<AllyGroupEntryConfig>();

        return entry.Allies;
    }

    public float GetSpawnIntervalSeconds(
        int galaxyLevel)
    {
        AllySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 0f;

        return entry.SpawnIntervalSeconds;
    }

    public bool HasValidAlliesForGalaxyLevel(
        int galaxyLevel)
    {
        AllySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return false;

        return entry.HasValidAllies();
    }

    public int GetMinAllyCount(
        int galaxyLevel)
    {
        AllySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 0;

        return entry.GetMinAllyCount();
    }

    public int GetMaxAllyCount(
        int galaxyLevel)
    {
        AllySpawnLevelEntryConfig entry =
            GetEntryForGalaxyLevel(galaxyLevel);

        if (entry == null)
            return 0;

        return entry.GetMaxAllyCount();
    }

    /*
     * Legacy compatibility.
     * Старый код без galaxy level получает набор для L01.
     */
    public IReadOnlyList<AllyGroupEntryConfig> Allies =>
        GetAlliesForGalaxyLevel(1);

    public float SpawnIntervalSeconds =>
        GetSpawnIntervalSeconds(1);

    public AllyConfig AllyConfig
    {
        get
        {
            IReadOnlyList<AllyGroupEntryConfig> allies =
                GetAlliesForGalaxyLevel(1);

            if (allies == null)
                return null;

            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i] == null)
                    continue;

                if (allies[i].AllyConfig != null)
                    return allies[i].AllyConfig;
            }

            return null;
        }
    }

    public int MinCount
    {
        get
        {
            IReadOnlyList<AllyGroupEntryConfig> allies =
                GetAlliesForGalaxyLevel(1);

            if (allies == null)
                return 0;

            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i] == null)
                    continue;

                if (!allies[i].IsValid())
                    continue;

                return allies[i].MinCount;
            }

            return 0;
        }
    }

    public int MaxCount
    {
        get
        {
            IReadOnlyList<AllyGroupEntryConfig> allies =
                GetAlliesForGalaxyLevel(1);

            if (allies == null)
                return 0;

            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i] == null)
                    continue;

                if (!allies[i].IsValid())
                    continue;

                return allies[i].MaxCount;
            }

            return 0;
        }
    }

    public bool HasValidAllies()
    {
        return HasValidAlliesForGalaxyLevel(1);
    }

    public int GetMinAllyCount()
    {
        return GetMinAllyCount(1);
    }

    public int GetMaxAllyCount()
    {
        return GetMaxAllyCount(1);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelEntries == null)
            levelEntries = new AllySpawnLevelEntryConfig[0];

        for (int i = 0; i < levelEntries.Length; i++)
        {
            if (levelEntries[i] == null)
                levelEntries[i] = new AllySpawnLevelEntryConfig();

            levelEntries[i].Validate(i + 1);
        }
    }
#endif
}

[Serializable]
public sealed class AllySpawnLevelEntryConfig
{
    [Header("Galaxy Level")]
    [SerializeField]
    [Range(1, 10)]
    private int galaxyLevel = 1;

    [Header("Population")]
    [SerializeField]
    [Min(0f)]
    private float spawnIntervalSeconds = 60f;

    [Header("Offline Population")]
    [Tooltip("Real-world hours between offline spawn checks while the player is not playing.")]
    [SerializeField]
    [Min(0f)]
    private float offlineSpawnIntervalHours = 6f;

    [Header("Allies")]
    [SerializeField]
    private AllyGroupEntryConfig[] allies =
        new AllyGroupEntryConfig[0];

    public int GalaxyLevel =>
        galaxyLevel;

    public float SpawnIntervalSeconds =>
        spawnIntervalSeconds;

    public float OfflineSpawnIntervalHours =>
        offlineSpawnIntervalHours;

    public IReadOnlyList<AllyGroupEntryConfig> Allies =>
        allies;

    public bool HasValidAllies()
    {
        if (allies == null || allies.Length == 0)
            return false;

        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] == null)
                continue;

            if (allies[i].IsValid())
                return true;
        }

        return false;
    }

    public int GetMinAllyCount()
    {
        if (allies == null)
            return 0;

        int count = 0;

        for (int i = 0; i < allies.Length; i++)
        {
            AllyGroupEntryConfig entry =
                allies[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            count += entry.MinCount;
        }

        return count;
    }

    public int GetMaxAllyCount()
    {
        if (allies == null)
            return 0;

        int count = 0;

        for (int i = 0; i < allies.Length; i++)
        {
            AllyGroupEntryConfig entry =
                allies[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            count += entry.MaxCount;
        }

        return count;
    }

#if UNITY_EDITOR
    public void Validate(int fallbackLevel)
    {
        galaxyLevel =
            Mathf.Clamp(fallbackLevel, 1, 10);

        spawnIntervalSeconds =
            Mathf.Max(0f, spawnIntervalSeconds);

        offlineSpawnIntervalHours =
            Mathf.Max(0f, offlineSpawnIntervalHours);

        if (allies == null)
            allies = new AllyGroupEntryConfig[0];

        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] == null)
                allies[i] = new AllyGroupEntryConfig();

            allies[i].Validate();
        }
    }
#endif
}

[Serializable]
public sealed class AllyGroupEntryConfig
{
    [SerializeField]
    private AllyConfig allyConfig;

    [SerializeField]
    [Min(0)]
    private int minCount = 1;

    [SerializeField]
    [Min(0)]
    private int maxCount = 1;

    public AllyConfig AllyConfig =>
        allyConfig;

    public int MinCount =>
        minCount;

    public int MaxCount =>
        maxCount;

    public bool IsValid()
    {
        if (allyConfig == null)
            return false;

        if (minCount < 0)
            return false;

        if (maxCount < minCount)
            return false;

        if (maxCount <= 0)
            return false;

        return true;
    }

#if UNITY_EDITOR
    public void Validate()
    {
        minCount =
            Mathf.Max(0, minCount);

        maxCount =
            Mathf.Max(minCount, maxCount);
    }
#endif
}
