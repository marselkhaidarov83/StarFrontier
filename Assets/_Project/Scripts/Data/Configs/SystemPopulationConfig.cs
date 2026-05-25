using System;
using UnityEngine;

    [Serializable]
    [CreateAssetMenu(fileName = "SystemPopulationConfig", menuName = "StarFrontier/Configs/System Population")]
    public sealed class SystemPopulationConfig : BaseConfig
    {
        [Header("Allies")]
        [SerializeField] private AllySpawnRuleConfig[] allySpawnRules;

        [Header("Enemies")]
        [SerializeField] private EnemyGroupSpawnRuleConfig[] enemyGroupSpawnRules;

        public AllySpawnRuleConfig[] AllySpawnRules => allySpawnRules;
        public EnemyGroupSpawnRuleConfig[] EnemyGroupSpawnRules => enemyGroupSpawnRules;
    }
