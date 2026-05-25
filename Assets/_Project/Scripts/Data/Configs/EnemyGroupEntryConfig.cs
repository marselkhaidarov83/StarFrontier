using System;
using UnityEngine;

[Serializable]
public sealed class EnemyGroupEntryConfig
{
    [Header("Enemy")]
    [SerializeField] private EnemyConfig enemyConfig;

    [Header("Count")]
    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 1;

    public EnemyConfig EnemyConfig => enemyConfig;
    public int MinCount => minCount;
    public int MaxCount => maxCount;
}