using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyGroupSpawnRuleConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy Group Spawn Rule")]
public sealed class EnemyGroupSpawnRuleConfig : BaseConfig
{
    [Header("Spawn Timing")]
    [SerializeField] [Min(0f)] private float spawnIntervalSeconds = 180f;

    [Header("Spawn Limits")]
    [SerializeField] [Min(1)] private int maxAliveGroupsFromThisRule = 1;

    [Header("Enemies")]
    [SerializeField] private EnemyGroupEntryConfig[] enemies =
        new EnemyGroupEntryConfig[0];

    [Header("Spawn Position")]
    [SerializeField] private Vector3 startPosition =
        new Vector3(6f, 0f, 0f);

    public float SpawnIntervalSeconds => spawnIntervalSeconds;

    public int MaxAliveGroupsFromThisRule => maxAliveGroupsFromThisRule;

    public IReadOnlyList<EnemyGroupEntryConfig> Enemies => enemies;

    public Vector3 StartPosition => startPosition;

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
    private void OnValidate()
    {
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