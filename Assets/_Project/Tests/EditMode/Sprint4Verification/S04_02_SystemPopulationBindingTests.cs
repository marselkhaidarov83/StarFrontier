using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_SystemPopulationBindingTests
{
    private const string SolariaSystemId =
        "s01_system_solaria_02";

    private const string TraderPopulationId =
        "systemPopulation_trader_01";

    private const string AllySpawnRuleId =
        "allySpawnRule_ranger_01";

    private static readonly string[] RequiredEnemyGroupRuleIds =
    {
        "enemyGroupSpawnRule_ancients_01",
        "enemyGroupSpawnRule_ai_01",
        "enemyGroupSpawnRule_infected_01"
    };

    [Test]
    public void Solaria_HasTraderSystemPopulation()
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
            TraderPopulationId,
            system.SystemPopulation.Id,
            SolariaSystemId + " must reference " + TraderPopulationId);
    }

    [Test]
    public void TraderPopulation_ReferencesAllySpawnRule()
    {
        SystemPopulationConfig population =
            FindConfigById<SystemPopulationConfig>(
                TraderPopulationId);

        Assert.NotNull(
            population,
            "Missing SystemPopulationConfig: " + TraderPopulationId);

        Assert.IsTrue(
            HasAllyRule(population, AllySpawnRuleId),
            TraderPopulationId + " must reference " + AllySpawnRuleId);
    }

    [Test]
    public void AtLeastOneStarSystem_ReferencesEachRequiredEnemyGroupRule()
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
                "No StarSystemConfig references EnemyGroupSpawnRuleConfig: " + ruleId);
        }
    }

    [Test]
    public void TraderPopulation_HasAtLeastOneEnemyGroupRule()
    {
        SystemPopulationConfig population =
            FindConfigById<SystemPopulationConfig>(
                TraderPopulationId);

        Assert.NotNull(
            population,
            "Missing SystemPopulationConfig: " + TraderPopulationId);

        Assert.NotNull(
            population.EnemyGroupSpawnRules,
            TraderPopulationId + " EnemyGroupSpawnRules is null.");

        Assert.Greater(
            population.EnemyGroupSpawnRules.Length,
            0,
            TraderPopulationId + " must have at least one enemy group rule.");
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

            if (HasEnemyRule(system.SystemPopulation, enemyGroupRuleId))
                return system;
        }

        return null;
    }

    private static bool HasAllyRule(
        SystemPopulationConfig population,
        string allyRuleId)
    {
        if (population == null)
            return false;

        if (population.AllySpawnRules == null)
            return false;

        for (int i = 0; i < population.AllySpawnRules.Length; i++)
        {
            AllySpawnRuleConfig rule =
                population.AllySpawnRules[i];

            if (rule == null)
                continue;

            if (rule.Id == allyRuleId)
                return true;
        }

        return false;
    }

    private static bool HasEnemyRule(
        SystemPopulationConfig population,
        string enemyGroupRuleId)
    {
        if (population == null)
            return false;

        if (population.EnemyGroupSpawnRules == null)
            return false;

        for (int i = 0; i < population.EnemyGroupSpawnRules.Length; i++)
        {
            EnemyGroupSpawnRuleConfig rule =
                population.EnemyGroupSpawnRules[i];

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