using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(
    fileName = "AllySpawnRuleConfig",
    menuName = "StarFrontier/Configs/Npc/Ally spawn rule")]
public sealed class AllySpawnRuleConfig : BaseConfig
{
    [Header("Allies")]
    [SerializeField] private AllyGroupEntryConfig[] allies =
        new AllyGroupEntryConfig[0];

    [Header("Population")]
    [SerializeField] private float spawnIntervalSeconds = 60f;

    [Header("Behavior")]
    [SerializeField] private SystemNpcBehaviorWeight[] behaviorWeights =
        new SystemNpcBehaviorWeight[0];

    [Header("4Enemy")]
    [SerializeField] private float engageEnemiesWeight = 100f;

    public IReadOnlyList<AllyGroupEntryConfig> Allies => allies;

    public float SpawnIntervalSeconds => spawnIntervalSeconds;

    public IReadOnlyList<SystemNpcBehaviorWeight> BehaviorWeights =>
        behaviorWeights;

    public float EngageEnemiesWeight => engageEnemiesWeight;

    /*
     * Старое свойство оставляем для совместимости.
     * Старый код, который ожидает AllySpawnRuleConfig.AllyConfig,
     * получит первый непустой AllyConfig из массива Allies.
     */
    public AllyConfig AllyConfig
    {
        get
        {
            if (allies == null)
                return null;

            for (int i = 0; i < allies.Length; i++)
            {
                if (allies[i] == null)
                    continue;

                if (allies[i].AllyConfig != null)
                    return allies[i].AllyConfig;
            }

            return null;
        }
    }

    /*
     * Старое свойство оставляем для совместимости.
     * Возвращает MinCount первого валидного союзника.
     */
    public int MinCount
    {
        get
        {
            if (allies == null)
                return 0;

            for (int i = 0; i < allies.Length; i++)
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

    /*
     * Старое свойство оставляем для совместимости.
     * Возвращает MaxCount первого валидного союзника.
     */
    public int MaxCount
    {
        get
        {
            if (allies == null)
                return 0;

            for (int i = 0; i < allies.Length; i++)
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
    private void OnValidate()
    {
        spawnIntervalSeconds =
            Mathf.Max(0f, spawnIntervalSeconds);

        engageEnemiesWeight =
            Mathf.Max(0f, engageEnemiesWeight);

        if (allies == null)
            allies = new AllyGroupEntryConfig[0];

        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] == null)
                allies[i] = new AllyGroupEntryConfig();

            allies[i].Validate();
        }

        if (behaviorWeights == null)
            behaviorWeights = new SystemNpcBehaviorWeight[0];
    }
#endif
}

[Serializable]
public sealed class AllyGroupEntryConfig
{
    [SerializeField] private AllyConfig allyConfig;

    [SerializeField] [Min(0)] private int minCount = 1;
    [SerializeField] [Min(0)] private int maxCount = 1;

    public AllyConfig AllyConfig => allyConfig;

    public int MinCount => minCount;

    public int MaxCount => maxCount;

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