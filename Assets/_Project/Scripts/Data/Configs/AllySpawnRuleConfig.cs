using System;
using System.Collections.Generic;
using UnityEngine;

    [Serializable]
    [CreateAssetMenu(fileName = "AllySpawnRuleConfig", menuName = "StarFrontier/Configs/Ally spawn rule")]
    public sealed class AllySpawnRuleConfig : BaseConfig
    {
        [Header("Ally")]
        [SerializeField] private AllyConfig allyConfig;

        [Header("Population")]
        [SerializeField] private int minCount = 1;
        [SerializeField] private int maxCount = 3;
        [SerializeField] private float spawnIntervalSeconds = 60f;

        [Header("Behavior")]
        [SerializeField] private List<SystemNpcBehaviorWeight> behaviorWeights = new();

        [Header("4Enemy")]
        [SerializeField] private float engageEnemiesWeight = 100;

        public AllyConfig AllyConfig => allyConfig;
        public int MinCount => minCount;
        public int MaxCount => maxCount;
        public float SpawnIntervalSeconds => spawnIntervalSeconds;
        public IReadOnlyList<SystemNpcBehaviorWeight> BehaviorWeights => behaviorWeights;
        public float EngageEnemiesWeight => engageEnemiesWeight;
    }