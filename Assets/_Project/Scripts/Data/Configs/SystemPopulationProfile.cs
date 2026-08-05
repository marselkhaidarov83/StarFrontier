using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(
    fileName = "SystemPopulationProfile",
    menuName = "StarFrontier/Configs/System Population Profile")]
public sealed class SystemPopulationProfile : BaseConfig
{
    [Header("Galaxy Level")]
    [SerializeField] [Range(1, 10)] private int galaxyLevel = 1;

    [Header("Allies")]
    [SerializeField] private AllySpawnRuleConfig[] allySpawnRules =
        new AllySpawnRuleConfig[0];

    [Header("Enemies")]
    [SerializeField] private EnemyGroupSpawnRuleConfig[] enemyGroupSpawnRules =
        new EnemyGroupSpawnRuleConfig[0];

    public int GalaxyLevel => galaxyLevel;
    public AllySpawnRuleConfig[] AllySpawnRules => allySpawnRules;
    public EnemyGroupSpawnRuleConfig[] EnemyGroupSpawnRules =>
        enemyGroupSpawnRules;

#if UNITY_EDITOR
    public void EnsureLevel(int level)
    {
        galaxyLevel = Mathf.Clamp(level, 1, 10);

        if (allySpawnRules == null)
            allySpawnRules = new AllySpawnRuleConfig[0];

        if (enemyGroupSpawnRules == null)
            enemyGroupSpawnRules = new EnemyGroupSpawnRuleConfig[0];
    }
#endif
}