using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_SystemPopulationBindingTests
{
    private const string SolariaSystemId =
        "s01_system_solaria_02";

    private const string ExpectedSolariaPopulationRuleId =
        "system_population_rule_trade_no_enemies_01";

    private const string ExpectedSolariaAllySpawnRuleId =
        "ally_spawn_trade_01";

    private const string VegaReachSystemId =
        "s01_system_vega_reach_02";

    private const string ExpectedSciencePopulationRuleId =
        "system_population_rule_science_no_enemies_01";

    private const string ExpectedScienceAllySpawnRuleId =
        "ally_spawn_science_01";

    private static readonly string[] RequiredEnemyGroupRuleIds =
    {
        "enemy_group_spawn_ancients_01",
        "enemy_group_spawn_ai_01",
        "enemy_group_spawn_infected_01"
    };

    [Test]
    public void Solaria_HasExpectedSystemPopulationRule()
    {
        StarSystemConfig system =
            FindConfigById<StarSystemConfig>(
                SolariaSystemId);

        Assert.NotNull(
            system,
            "Missing StarSystemConfig: " + SolariaSystemId);

        Assert.NotNull(
            system.SystemPopulationRule,
            SolariaSystemId + " must have SystemPopulationRule assigned.");

        Assert.AreEqual(
            ExpectedSolariaPopulationRuleId,
            system.SystemPopulationRule.Id,
            SolariaSystemId + " must reference " +
            ExpectedSolariaPopulationRuleId);
    }

    [Test]
    public void SolariaPopulationRule_ReferencesExpectedAllySpawnRule()
    {
        StarSystemConfig system =
            FindConfigById<StarSystemConfig>(
                SolariaSystemId);

        Assert.NotNull(
            system,
            "Missing StarSystemConfig: " + SolariaSystemId);

        Assert.NotNull(
            system.SystemPopulationRule,
            SolariaSystemId + " must have SystemPopulationRule assigned.");

        Assert.IsTrue(
            HasAllyRule(
                system.SystemPopulationRule,
                ExpectedSolariaAllySpawnRuleId),
            system.SystemPopulationRule.Id + " must reference " +
            ExpectedSolariaAllySpawnRuleId + ".");
    }

    [Test]
    public void ScienceSystem_UsesExpectedNoEnemiesPopulationRule()
    {
        StarSystemConfig system =
            FindConfigById<StarSystemConfig>(
                VegaReachSystemId);

        Assert.NotNull(
            system,
            "Missing StarSystemConfig: " + VegaReachSystemId);

        Assert.NotNull(
            system.SystemPopulationRule,
            VegaReachSystemId + " must have SystemPopulationRule assigned.");

        Assert.AreEqual(
            ExpectedSciencePopulationRuleId,
            system.SystemPopulationRule.Id,
            VegaReachSystemId + " must reference " +
            ExpectedSciencePopulationRuleId);

        Assert.IsTrue(
            HasAllyRule(
                system.SystemPopulationRule,
                ExpectedScienceAllySpawnRuleId),
            system.SystemPopulationRule.Id + " must reference " +
            ExpectedScienceAllySpawnRuleId + ".");
    }

    [Test]
    public void AtLeastOneStarSystem_ReferencesEachRequiredEnemyGroupRuleThroughPopulationRule()
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
                "through SystemPopulationRule: " + ruleId);
        }
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

            if (HasEnemyRule(
                    system.SystemPopulationRule,
                    enemyGroupRuleId))
            {
                return system;
            }
        }

        return null;
    }

    private static bool HasAllyRule(
        SystemPopulationRule populationRule,
        string allyRuleId)
    {
        if (populationRule == null)
            return false;

        if (populationRule.AllySpawnRules == null)
            return false;

        for (int i = 0; i < populationRule.AllySpawnRules.Length; i++)
        {
            AllySpawnRuleConfig rule =
                populationRule.AllySpawnRules[i];

            if (rule == null)
                continue;

            if (rule.Id == allyRuleId)
                return true;
        }

        return false;
    }

    private static bool HasEnemyRule(
        SystemPopulationRule populationRule,
        string enemyGroupRuleId)
    {
        if (populationRule == null)
            return false;

        if (populationRule.EnemyGroupSpawnRules == null)
            return false;

        for (int i = 0; i < populationRule.EnemyGroupSpawnRules.Length; i++)
        {
            EnemyGroupSpawnRuleConfig rule =
                populationRule.EnemyGroupSpawnRules[i];

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