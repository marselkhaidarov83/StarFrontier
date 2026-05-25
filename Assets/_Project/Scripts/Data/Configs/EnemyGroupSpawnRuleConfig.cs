using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "EnemyGroupSpawnRuleConfig", menuName = "StarFrontier/Configs/Enemy group spawn rule")]
public sealed class EnemyGroupSpawnRuleConfig : BaseConfig
{
    [Header("Spawn")]
    [SerializeField] private float spawnIntervalSeconds = 180f;
    [SerializeField] private int maxAliveGroupsFromThisRule = 1;

    [Header("Group Composition")]
    [SerializeField] private List<EnemyGroupEntryConfig> enemies = new();

    [Header("Position")]
    [SerializeField] private Vector3 startPosition = new Vector3(0, 0, -2);

    public float SpawnIntervalSeconds => spawnIntervalSeconds;
    public int MaxAliveGroupsFromThisRule => maxAliveGroupsFromThisRule;
    public IReadOnlyList<EnemyGroupEntryConfig> Enemies => enemies;
    public Vector3 StartPosition => startPosition;
}