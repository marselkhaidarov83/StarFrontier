using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_ConfigAssetTests
{
    private const string AllySpawnRuleId =
        "allySpawnRule_ranger_01";

    private const string EnemyGroupSpawnRuleId =
        "enemyGroupSpawnRule_ai_01";

    private static readonly string[] RequiredAllyConfigIds =
    {
        "ally_ranger_L01_01",
        "ally_trader_L01_01",
        "ally_warrior_L01_01"
    };

    private static readonly AllyRole2A[] RequiredAllyRoles =
    {
        AllyRole2A.Ranger,
        AllyRole2A.Warrior,
        AllyRole2A.Trader
    };

    private static readonly string[] RequiredEnemyConfigIds =
    {
        "enemy_ancients_L01_01",
        "enemy_ai_L01_01",
        "enemy_infected_L01_01"
    };

    [Test]
    public void AllyConfigs_ExistAndHaveCorrectRoles()
    {
        for (int i = 0; i < RequiredAllyConfigIds.Length; i++)
        {
            AllyConfig config =
                FindConfigById<AllyConfig>(
                    RequiredAllyConfigIds[i]);

            Assert.NotNull(
                config,
                "Missing AllyConfig: " + RequiredAllyConfigIds[i]);

            Assert.AreEqual(
                RequiredAllyRoles[i],
                config.Role,
                config.Id + " has wrong ally role.");

            Assert.IsTrue(
                config.HasWeapons(),
                config.Id + " must have weapons.");

            Assert.GreaterOrEqual(
                config.WeaponCount,
                2,
                config.Id + " must have at least two weapons.");
        }
    }

    [Test]
    public void EnemyConfigs_ExistAndHaveWeapons()
    {
        for (int i = 0; i < RequiredEnemyConfigIds.Length; i++)
        {
            EnemyConfig config =
                FindConfigById<EnemyConfig>(
                    RequiredEnemyConfigIds[i]);

            Assert.NotNull(
                config,
                "Missing EnemyConfig: " + RequiredEnemyConfigIds[i]);

            Assert.IsTrue(
                config.HasWeapons(),
                config.Id + " must have weapons.");

            Assert.GreaterOrEqual(
                config.WeaponCount,
                3,
                config.Id + " must have three weapons.");
        }
    }

    [Test]
    public void AllySpawnRule_HasRangerWarriorTrader()
    {
        AllySpawnRuleConfig rule =
            FindConfigById<AllySpawnRuleConfig>(
                AllySpawnRuleId);

        Assert.NotNull(
            rule,
            "Missing AllySpawnRuleConfig: " + AllySpawnRuleId);

        Assert.IsTrue(
            rule.HasValidAllies(),
            rule.Id + " must have valid allies.");

        Assert.AreEqual(
            3,
            rule.Allies.Count,
            rule.Id + " must contain exactly three ally entries.");

        AssertAllyEntry(
            rule,
            "ally_ranger_basic_s04_02",
            1,
            1);

        AssertAllyEntry(
            rule,
            "ally_warrior_basic_s04_02",
            1,
            1);

        AssertAllyEntry(
            rule,
            "ally_trader_basic_s04_02",
            1,
            1);
    }

    [Test]
    public void EnemyGroupSpawnRule_HasThreeEnemyFactions()
    {
        EnemyGroupSpawnRuleConfig rule =
            FindConfigById<EnemyGroupSpawnRuleConfig>(
                EnemyGroupSpawnRuleId);

        Assert.NotNull(
            rule,
            "Missing EnemyGroupSpawnRuleConfig: " + EnemyGroupSpawnRuleId);

        Assert.IsTrue(
            rule.HasValidEnemies(),
            rule.Id + " must have valid enemies.");

        Assert.AreEqual(
            3,
            rule.Enemies.Count,
            rule.Id + " must contain exactly three enemy entries.");

        AssertEnemyEntry(
            rule,
            "enemy_ancients_basic_s04_02",
            1,
            1);

        AssertEnemyEntry(
            rule,
            "enemy_ai_basic_s04_02",
            1,
            1);

        AssertEnemyEntry(
            rule,
            "enemy_infected_basic_s04_02",
            1,
            1);
    }

    private static void AssertAllyEntry(
        AllySpawnRuleConfig rule,
        string allyConfigId,
        int expectedMin,
        int expectedMax)
    {
        AllyGroupEntryConfig entry =
            FindAllyEntry(
                rule,
                allyConfigId);

        Assert.NotNull(
            entry,
            rule.Id + " must contain ally: " + allyConfigId);

        Assert.AreEqual(
            expectedMin,
            entry.MinCount,
            allyConfigId + " has wrong MinCount.");

        Assert.AreEqual(
            expectedMax,
            entry.MaxCount,
            allyConfigId + " has wrong MaxCount.");
    }

    private static void AssertEnemyEntry(
        EnemyGroupSpawnRuleConfig rule,
        string enemyConfigId,
        int expectedMin,
        int expectedMax)
    {
        EnemyGroupEntryConfig entry =
            FindEnemyEntry(
                rule,
                enemyConfigId);

        Assert.NotNull(
            entry,
            rule.Id + " must contain enemy: " + enemyConfigId);

        Assert.AreEqual(
            expectedMin,
            entry.MinCount,
            enemyConfigId + " has wrong MinCount.");

        Assert.AreEqual(
            expectedMax,
            entry.MaxCount,
            enemyConfigId + " has wrong MaxCount.");
    }

    private static AllyGroupEntryConfig FindAllyEntry(
        AllySpawnRuleConfig rule,
        string allyConfigId)
    {
        if (rule == null || rule.Allies == null)
            return null;

        for (int i = 0; i < rule.Allies.Count; i++)
        {
            AllyGroupEntryConfig entry =
                rule.Allies[i];

            if (entry == null || entry.AllyConfig == null)
                continue;

            if (entry.AllyConfig.Id == allyConfigId)
                return entry;
        }

        return null;
    }

    private static EnemyGroupEntryConfig FindEnemyEntry(
        EnemyGroupSpawnRuleConfig rule,
        string enemyConfigId)
    {
        if (rule == null || rule.Enemies == null)
            return null;

        for (int i = 0; i < rule.Enemies.Count; i++)
        {
            EnemyGroupEntryConfig entry =
                rule.Enemies[i];

            if (entry == null || entry.EnemyConfig == null)
                continue;

            if (entry.EnemyConfig.Id == enemyConfigId)
                return entry;
        }

        return null;
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