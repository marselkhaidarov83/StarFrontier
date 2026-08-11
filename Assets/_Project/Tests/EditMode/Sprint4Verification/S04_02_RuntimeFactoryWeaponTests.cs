using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class S04_02_RuntimeFactoryWeaponTests
{
    private static readonly string[] EnemyConfigIds =
    {
        "enemy_ancients_L01_01",
        "enemy_ai_L01_01",
        "enemy_infected_L01_01"
    };

    private static readonly string[] AllyConfigIds =
    {
        "ally_ranger_L01_01",
        "ally_military_L01_01",
        "ally_trader_L01_01"
    };

    [Test]
    public void EnemyRuntimeFactory_CopiesOneValidConfiguredWeaponGroup()
    {
        for (int i = 0; i < EnemyConfigIds.Length; i++)
        {
            EnemyConfig config =
                FindConfigById<EnemyConfig>(
                    EnemyConfigIds[i]);

            Assert.NotNull(
                config,
                "Missing EnemyConfig: " + EnemyConfigIds[i]);

            SystemNpcRuntimeState npc =
                SystemNpcRuntimeFactory.CreateEnemy(
                    config,
                    "test_system",
                    "test_system",
                    Vector3.zero,
                    "test_enemy_rule",
                    "test_group");

            Assert.NotNull(npc);

            Assert.AreEqual(
                SystemNpcType.Enemy,
                npc.NpcType);

            AssertRuntimeWeaponsMatchOneValidWeaponGroup(
                config.Id,
                config.WeaponGroups,
                config.WeaponConfigs,
                npc.Weapons);
        }
    }

    [Test]
    public void AllyRuntimeFactory_CopiesOneValidConfiguredWeaponGroup()
    {
        for (int i = 0; i < AllyConfigIds.Length; i++)
        {
            AllyConfig config =
                FindConfigById<AllyConfig>(
                    AllyConfigIds[i]);

            Assert.NotNull(
                config,
                "Missing AllyConfig: " + AllyConfigIds[i]);

            SystemNpcRuntimeState npc =
                SystemNpcRuntimeFactory.CreateAlly(
                    config,
                    "test_system",
                    "test_system",
                    "test_planet",
                    Vector3.zero,
                    "test_ally_rule");

            Assert.NotNull(npc);

            Assert.AreEqual(
                SystemNpcType.Ally,
                npc.NpcType);

            AssertRuntimeWeaponsMatchOneValidWeaponGroup(
                config.Id,
                config.WeaponGroups,
                config.WeaponConfigs,
                npc.Weapons);
        }
    }

    private static void AssertRuntimeWeaponsMatchOneValidWeaponGroup(
        string ownerConfigId,
        IReadOnlyList<WeaponGroupConfig> weaponGroups,
        IReadOnlyList<WeaponConfig> fallbackConfigWeapons,
        IReadOnlyList<SystemNpcWeaponRuntimeState> runtimeWeapons)
    {
        Assert.NotNull(
            runtimeWeapons,
            ownerConfigId + " runtime weapon list is null.");

        Assert.Greater(
            runtimeWeapons.Count,
            0,
            ownerConfigId + " runtime must have at least one weapon.");

        HashSet<string> actualWeaponIds =
            BuildRuntimeWeaponIdSet(
                ownerConfigId,
                runtimeWeapons);

        if (weaponGroups != null)
        {
            for (int i = 0; i < weaponGroups.Count; i++)
            {
                WeaponGroupConfig group =
                    weaponGroups[i];

                if (group == null)
                    continue;

                if (!group.IsValid())
                    continue;

                HashSet<string> groupWeaponIds =
                    BuildConfigWeaponIdSet(
                        group.Id,
                        group.WeaponConfigs);

                if (SetsAreEqual(groupWeaponIds, actualWeaponIds))
                    return;
            }
        }

        HashSet<string> fallbackWeaponIds =
            BuildConfigWeaponIdSet(
                ownerConfigId,
                fallbackConfigWeapons);

        if (SetsAreEqual(fallbackWeaponIds, actualWeaponIds))
            return;

        Assert.Fail(
            ownerConfigId +
            " runtime weapons do not match any valid configured WeaponGroupConfig.");
    }

    private static HashSet<string> BuildRuntimeWeaponIdSet(
        string ownerConfigId,
        IReadOnlyList<SystemNpcWeaponRuntimeState> runtimeWeapons)
    {
        HashSet<string> result =
            new HashSet<string>();

        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState runtimeWeapon =
                runtimeWeapons[i];

            Assert.NotNull(
                runtimeWeapon,
                ownerConfigId + " has null runtime weapon at index " + i);

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(runtimeWeapon.WeaponConfigId),
                ownerConfigId + " has runtime weapon without WeaponConfigId.");

            Assert.IsTrue(
                result.Add(runtimeWeapon.WeaponConfigId),
                ownerConfigId + " has duplicated runtime weapon: " + runtimeWeapon.WeaponConfigId);

            Assert.GreaterOrEqual(
                runtimeWeapon.ShotDistance,
                0f,
                ownerConfigId + " has negative ShotDistance for " + runtimeWeapon.WeaponConfigId);
        }

        return result;
    }

    private static HashSet<string> BuildConfigWeaponIdSet(
        string ownerConfigId,
        IReadOnlyList<WeaponConfig> configWeapons)
    {
        HashSet<string> result =
            new HashSet<string>();

        if (configWeapons == null)
            return result;

        for (int i = 0; i < configWeapons.Count; i++)
        {
            WeaponConfig weaponConfig =
                configWeapons[i];

            if (weaponConfig == null)
                continue;

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(weaponConfig.Id),
                ownerConfigId + " has configured weapon without Id.");

            result.Add(
                weaponConfig.Id);
        }

        return result;
    }

    private static bool SetsAreEqual(
        HashSet<string> expected,
        HashSet<string> actual)
    {
        if (expected == null || actual == null)
            return false;

        if (expected.Count == 0)
            return false;

        if (expected.Count != actual.Count)
            return false;

        foreach (string expectedId in expected)
        {
            if (!actual.Contains(expectedId))
                return false;
        }

        return true;
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