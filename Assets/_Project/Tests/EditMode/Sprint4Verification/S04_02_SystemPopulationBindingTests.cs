using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_SystemPopulationBindingTests
{
    private const string AllySpawnRuleId =
        "allySpawnRule_s04_02_basic_allies_01";

    private const string EnemyGroupSpawnRuleId =
        "enemyGroupSpawnRule_s04_02_factions_basic_01";

    [Test]
    public void SomeStarSystem_HasPopulationWithSprint4Rules()
    {
        StarSystemConfig system =
            FindStarSystemWithRequiredPopulationRules();

        Assert.NotNull(
            system,
            "No StarSystemConfig has SystemPopulation with required S04-02 ally and enemy rules.");
    }

    private static StarSystemConfig FindStarSystemWithRequiredPopulationRules()
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

            if (!HasAllyRule(system.SystemPopulation))
                continue;

            if (!HasEnemyRule(system.SystemPopulation))
                continue;

            return system;
        }

        return null;
    }

    private static bool HasAllyRule(
        SystemPopulationConfig population)
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

            if (rule.Id == AllySpawnRuleId)
                return true;
        }

        return false;
    }

    private static bool HasEnemyRule(
        SystemPopulationConfig population)
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

            if (rule.Id == EnemyGroupSpawnRuleId)
                return true;
        }

        return false;
    }
}