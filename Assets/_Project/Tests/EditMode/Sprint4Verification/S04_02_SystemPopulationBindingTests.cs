using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_SystemPopulationBindingTests
{
    private const string SolariaSystemId =
        "s01_system_solaria_02";

    private const string SolariaPopulationId =
        "system_population_s01_solaria_01";

    private const string AllySpawnRuleId =
        "allySpawnRule_ranger_01";

    private static readonly string[] RequiredEnemyGroupRuleIds =
    {
        "enemyGroupSpawnRule_ancients_01",
        "enemyGroupSpawnRule_ai_01",
        "enemyGroupSpawnRule_infected_01"
    };

    [Test]
    public void Solaria_HasExpectedSystemPopulation()
    {
        StarSystemConfig system =
            FindConfigById<StarSystemConfig>(
                SolariaSystemId);

        Assert.NotNull(
            system,
            "Missing StarSystemConfig: " + SolariaSystemId);

        Assert.NotNull(
            system.SystemPopulation,
            SolariaSystemId + " must have SystemPopulation assigned.");

        Assert.AreEqual(
            SolariaPopulationId,
            system.SystemPopulation.Id,
            SolariaSystemId + " must reference " + SolariaPopulationId);
    }

    [Test]
    public void SolariaPopulation_HasGalaxyLevelProfiles()
    {
        SystemPopulationConfig population =
            FindConfigById<SystemPopulationConfig>(
                SolariaPopulationId);

        Assert.NotNull(
            population,
            "Missing SystemPopulationConfig: " + SolariaPopulationId);

        Assert.NotNull(
            population.LevelProfiles,
            SolariaPopulationId + " LevelProfiles is null.");

        Assert.AreEqual(
            10,
            population.LevelProfiles.Length,
            SolariaPopulationId + " must have exactly 10 GalaxyLevel profiles.");

        for (int i = 0; i < population.LevelProfiles.Length; i++)
        {
            SystemPopulationProfile profile =
                population.LevelProfiles[i];

            Assert.NotNull(
                profile,
                SolariaPopulationId + " has null profile at index " + i);

            Assert.AreEqual(
                i + 1,
                profile.GalaxyLevel,
                SolariaPopulationId + " profile at index " + i +
                " must have GalaxyLevel " + (i + 1));
        }
    }

    [Test]
    public void SolariaPopulation_ReferencesAllySpawnRuleThroughProfiles()
    {
        SystemPopulationConfig population =
            FindConfigById<SystemPopulationConfig>(
                SolariaPopulationId);

        Assert.NotNull(
            population,
            "Missing SystemPopulationConfig: " + SolariaPopulationId);

        Assert.IsTrue(
            HasAllyRuleInAnyProfile(
                population,
                AllySpawnRuleId),
            SolariaPopulationId + " must reference " + AllySpawnRuleId +
            " through one of its SystemPopulationProfile entries.");
    }

    [Test]
    public void AtLeastOneStarSystem_ReferencesEachRequiredEnemyGroupRuleThroughProfiles()
    {
        for (int i = 0; i < RequiredEnemyGroupRuleIds.Length; i++)
        {
            string ruleId =
                RequiredEnemyGroupRuleIds[i];

            StarSystemConfig system =
                FindStarSystemReferencingEnemyGroupRule(
                    ruleId);

            Assert.NotNull(
                system,
                "No StarSystemConfig references EnemyGroupSpawnRuleConfig " +
                "through SystemPopulationProfile: " + ruleId);
        }
    }

    [Test]
    public void SolariaPopulation_HasAtLeastOneEnemyGroupRuleThroughProfiles()
    {
        SystemPopulationConfig population =
            FindConfigById<SystemPopulationConfig>(
                SolariaPopulationId);

        Assert.NotNull(
            population,
            "Missing SystemPopulationConfig: " + SolariaPopulationId);

        Assert.IsTrue(
            HasAnyEnemyRuleInAnyProfile(
                population),
            SolariaPopulationId +
            " must have at least one enemy group rule through LevelProfiles.");
    }

    private static StarSystemConfig FindStarSystemReferencingEnemyGroupRule(
        string enemyGroupRuleId)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:StarSystemConfig");

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            StarSystemConfig system =
                AssetDatabase.LoadAssetAtPath<StarSystemConfig>(
                    path);

            if (system == null)
                continue;

            if (system.SystemPopulation == null)
                continue;

            if (HasEnemyRuleInAnyProfile(
                    system.SystemPopulation,
                    enemyGroupRuleId))
            {
                return system;
            }
        }

        return null;
    }

    private static bool HasAllyRuleInAnyProfile(
        SystemPopulationConfig population,
        string allyRuleId)
    {
        if (population == null)
            return false;

        if (population.LevelProfiles == null)
            return false;

        for (int i = 0; i < population.LevelProfiles.Length; i++)
        {
            SystemPopulationProfile profile =
                population.LevelProfiles[i];

            if (profile == null)
                continue;

            if (HasAllyRule(
                    profile,
                    allyRuleId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAllyRule(
        SystemPopulationProfile profile,
        string allyRuleId)
    {
        if (profile == null)
            return false;

        if (profile.AllySpawnRules == null)
            return false;

        for (int i = 0; i < profile.AllySpawnRules.Length; i++)
        {
            AllySpawnRuleConfig rule =
                profile.AllySpawnRules[i];

            if (rule == null)
                continue;

            if (rule.Id == allyRuleId)
                return true;
        }

        return false;
    }

    private static bool HasAnyEnemyRuleInAnyProfile(
        SystemPopulationConfig population)
    {
        if (population == null)
            return false;

        if (population.LevelProfiles == null)
            return false;

        for (int i = 0; i < population.LevelProfiles.Length; i++)
        {
            SystemPopulationProfile profile =
                population.LevelProfiles[i];

            if (profile == null)
                continue;

            if (profile.EnemyGroupSpawnRules == null)
                continue;

            if (profile.EnemyGroupSpawnRules.Length > 0)
                return true;
        }

        return false;
    }

    private static bool HasEnemyRuleInAnyProfile(
        SystemPopulationConfig population,
        string enemyGroupRuleId)
    {
        if (population == null)
            return false;

        if (population.LevelProfiles == null)
            return false;

        for (int i = 0; i < population.LevelProfiles.Length; i++)
        {
            SystemPopulationProfile profile =
                population.LevelProfiles[i];

            if (profile == null)
                continue;

            if (HasEnemyRule(
                    profile,
                    enemyGroupRuleId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEnemyRule(
        SystemPopulationProfile profile,
        string enemyGroupRuleId)
    {
        if (profile == null)
            return false;

        if (profile.EnemyGroupSpawnRules == null)
            return false;

        for (int i = 0; i < profile.EnemyGroupSpawnRules.Length; i++)
        {
            EnemyGroupSpawnRuleConfig rule =
                profile.EnemyGroupSpawnRules[i];

            if (rule == null)
                continue;

            if (rule.Id == enemyGroupRuleId)
                return true;
        }

        return false;
    }

    private static T FindConfigById<T>(string id)
        where T : BaseConfig
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:" + typeof(T).Name);

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            T config =
                AssetDatabase.LoadAssetAtPath<T>(
                    path);

            if (config == null)
                continue;

            if (config.Id == id)
                return config;
        }

        return null;
    }
}