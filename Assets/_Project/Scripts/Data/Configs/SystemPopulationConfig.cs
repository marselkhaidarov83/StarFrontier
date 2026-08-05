using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemPopulationConfig",
    menuName = "StarFrontier/Configs/System Population")]
public sealed class SystemPopulationConfig : BaseConfig
{
    [Header("Profiles by Galaxy Level")]
    [Tooltip(
        "Exactly one profile is required for every GalaxyLevel from 1 to 10.")]
    [SerializeField] private SystemPopulationProfile[] levelProfiles =
        new SystemPopulationProfile[0];

    [Header("Legacy Fallback - Remove After Migration")]
    [SerializeField] private AllySpawnRuleConfig[] allySpawnRules;
    [SerializeField] private EnemyGroupSpawnRuleConfig[] enemyGroupSpawnRules;

    public SystemPopulationProfile[] LevelProfiles =>
        levelProfiles;

    // Legacy read-only properties are retained so older code and assets compile.
    public AllySpawnRuleConfig[] AllySpawnRules => allySpawnRules;
    public EnemyGroupSpawnRuleConfig[] EnemyGroupSpawnRules =>
        enemyGroupSpawnRules;

    public SystemPopulationProfile GetProfileForGalaxyLevel(
        int galaxyLevel)
    {
        int normalizedLevel = Mathf.Clamp(galaxyLevel, 1, 10);

        if (levelProfiles == null)
            return null;

        for (int i = 0; i < levelProfiles.Length; i++)
        {
            SystemPopulationProfile profile = levelProfiles[i];

            if (profile == null)
                continue;

            if (profile.GalaxyLevel == normalizedLevel)
                return profile;
        }

        return null;
    }

    public bool HasProfileForGalaxyLevel(int galaxyLevel)
    {
        return GetProfileForGalaxyLevel(galaxyLevel) != null;
    }

    public static bool IsEnemyConfigAllowedForGalaxyLevel(
        EnemyConfig enemyConfig,
        int galaxyLevel)
    {
        if (enemyConfig == null)
            return false;

        int normalizedLevel = Mathf.Clamp(galaxyLevel, 1, 10);
        return enemyConfig.Level == normalizedLevel;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelProfiles == null)
            levelProfiles = new SystemPopulationProfile[0];

        for (int i = 0; i < levelProfiles.Length; i++)
        {
            if (levelProfiles[i] == null)
                levelProfiles[i] = new SystemPopulationProfile();

            levelProfiles[i].EnsureLevel(i + 1);
        }
    }
#endif
}