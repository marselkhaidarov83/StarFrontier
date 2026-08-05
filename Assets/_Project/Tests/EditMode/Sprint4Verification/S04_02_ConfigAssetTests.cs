using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_ConfigAssetTests
{
    private const string AllySpawnRuleId =
        "allySpawnRule_ranger_01";

    private static readonly AllyExpectation[] RequiredAllies =
    {
        new AllyExpectation(
            "ally_ranger_L01_01",
            AllyRole2A.Ranger,
            1),

        new AllyExpectation(
            "ally_warrior_L01_01",
            AllyRole2A.Military,
            2),

        new AllyExpectation(
            "ally_trader_L01_01",
            AllyRole2A.Trader,
            0)
    };

    private static readonly EnemyRuleExpectation[] RequiredEnemyRules =
    {
        new EnemyRuleExpectation(
            "enemyGroupSpawnRule_ancients_01",
            "enemy_ancients_L01_01",
            1,
            5),

        new EnemyRuleExpectation(
            "enemyGroupSpawnRule_ai_01",
            "enemy_ai_L01_01",
            1,
            5),

        new EnemyRuleExpectation(
            "enemyGroupSpawnRule_infected_01",
            "enemy_infected_L01_01",
            1,
            5)
    };

    [Test]
    public void AllyConfigs_ExistAndMatchLatestGithubConfig()
    {
        for (int i = 0; i < RequiredAllies.Length; i++)
        {
            AllyExpectation expectation =
                RequiredAllies[i];

            AllyConfig config =
                FindConfigById<AllyConfig>(
                    expectation.Id);

            Assert.NotNull(
                config,
                "Missing AllyConfig: " + expectation.Id);

            Assert.AreEqual(
                expectation.Role,
                config.Role,
                config.Id + " has wrong AllyRole2A.");

            Assert.GreaterOrEqual(
                config.WeaponCount,
                expectation.MinWeaponCount,
                config.Id + " has less weapons than expected.");
        }
    }

    [Test]
    public void EnemyConfigs_ExistAndHaveAtLeastOneWeapon()
    {
        for (int i = 0; i < RequiredEnemyRules.Length; i++)
        {
            EnemyRuleExpectation expectation =
                RequiredEnemyRules[i];

            EnemyConfig config =
                FindConfigById<EnemyConfig>(
                    expectation.EnemyConfigId);

            Assert.NotNull(
                config,
                "Missing EnemyConfig: " + expectation.EnemyConfigId);

            Assert.IsTrue(
                config.HasWeapons(),
                config.Id + " must have at least one weapon.");

            Assert.GreaterOrEqual(
                config.WeaponCount,
                1,
                config.Id + " must have at least one WeaponConfig.");
        }
    }

    [Test]
    public void AllySpawnRule_ExistsAndHasNoEmptyPlaceholders()
    {
        AllySpawnRuleConfig rule =
            FindConfigById<AllySpawnRuleConfig>(
                AllySpawnRuleId);

        Assert.NotNull(
            rule,
            "Missing AllySpawnRuleConfig: " + AllySpawnRuleId);

        Assert.NotNull(
            rule.Allies,
            rule.Id + " Allies list is null.");

        Assert.Greater(
            rule.Allies.Count,
            0,
            rule.Id + " must contain ally entries.");

        Assert.IsTrue(
            rule.HasValidAllies(),
            rule.Id + " must have at least one valid AllyGroupEntryConfig.");

        for (int i = 0; i < rule.Allies.Count; i++)
        {
            AllyGroupEntryConfig entry =
                rule.Allies[i];

            Assert.NotNull(
                entry,
                rule.Id + " has null ally entry at index " + i);

            Assert.NotNull(
                entry.AllyConfig,
                rule.Id + " has empty AllyConfig at index " + i);

            Assert.IsTrue(
                entry.IsValid(),
                rule.Id + " has invalid ally entry at index " + i);

            Assert.Greater(
                entry.MaxCount,
                0,
                rule.Id + " ally entry maxCount must be greater than zero at index " + i);

            Assert.GreaterOrEqual(
                entry.MaxCount,
                entry.MinCount,
                rule.Id + " ally entry MaxCount must be >= MinCount at index " + i);
        }
    }

    [Test]
    public void EnemyGroupSpawnRules_ExistAndReferenceExpectedEnemies()
    {
        for (int i = 0; i < RequiredEnemyRules.Length; i++)
        {
            EnemyRuleExpectation expectation =
                RequiredEnemyRules[i];

            EnemyGroupSpawnRuleConfig rule =
                FindConfigById<EnemyGroupSpawnRuleConfig>(
                    expectation.RuleId);

            Assert.NotNull(
                rule,
                "Missing EnemyGroupSpawnRuleConfig: " + expectation.RuleId);

            Assert.IsTrue(
                rule.HasValidEnemies(),
                rule.Id + " must have valid enemies.");

            Assert.NotNull(
                rule.Enemies,
                rule.Id + " Enemies list is null.");

            Assert.AreEqual(
                1,
                rule.Enemies.Count,
                rule.Id + " must contain exactly one enemy entry in latest GitHub config.");

            EnemyGroupEntryConfig entry =
                rule.Enemies[0];

            Assert.NotNull(
                entry,
                rule.Id + " has null enemy entry.");

            Assert.NotNull(
                entry.EnemyConfig,
                rule.Id + " has empty EnemyConfig.");

            Assert.AreEqual(
                expectation.EnemyConfigId,
                entry.EnemyConfig.Id,
                rule.Id + " references wrong EnemyConfig.");

            Assert.AreEqual(
                expectation.ExpectedMinCount,
                entry.MinCount,
                rule.Id + " has wrong MinCount.");

            Assert.AreEqual(
                expectation.ExpectedMaxCount,
                entry.MaxCount,
                rule.Id + " has wrong MaxCount.");
        }
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

    private sealed class AllyExpectation
    {
        public AllyExpectation(
            string id,
            AllyRole2A role,
            int minWeaponCount)
        {
            Id = id;
            Role = role;
            MinWeaponCount = minWeaponCount;
        }

        public string Id { get; }
        public AllyRole2A Role { get; }
        public int MinWeaponCount { get; }
    }

    private sealed class EnemyRuleExpectation
    {
        public EnemyRuleExpectation(
            string ruleId,
            string enemyConfigId,
            int expectedMinCount,
            int expectedMaxCount)
        {
            RuleId = ruleId;
            EnemyConfigId = enemyConfigId;
            ExpectedMinCount = expectedMinCount;
            ExpectedMaxCount = expectedMaxCount;
        }

        public string RuleId { get; }
        public string EnemyConfigId { get; }
        public int ExpectedMinCount { get; }
        public int ExpectedMaxCount { get; }
    }
}