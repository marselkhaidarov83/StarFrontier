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
        "ally_warrior_L01_01",
        "ally_trader_L01_01"
    };

    [Test]
    public void EnemyRuntimeFactory_CopiesAllConfiguredWeapons()
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

            Assert.AreEqual(
                config.WeaponCount,
                npc.Weapons.Count,
                config.Id + " runtime weapon count must match EnemyConfig.WeaponCount.");

            AssertRuntimeWeaponsMatchConfigWeapons(
                config.Id,
                config.WeaponConfigs,
                npc.Weapons);
        }
    }

    [Test]
    public void AllyRuntimeFactory_CopiesAllConfiguredWeapons()
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

            Assert.AreEqual(
                config.WeaponCount,
                npc.Weapons.Count,
                config.Id + " runtime weapon count must match AllyConfig.WeaponCount.");

            AssertRuntimeWeaponsMatchConfigWeapons(
                config.Id,
                config.WeaponConfigs,
                npc.Weapons);
        }
    }

    private static void AssertRuntimeWeaponsMatchConfigWeapons(
        string ownerConfigId,
        IReadOnlyList<WeaponConfig> configWeapons,
        IReadOnlyList<SystemNpcWeaponRuntimeState> runtimeWeapons)
    {
        Assert.NotNull(
            configWeapons,
            ownerConfigId + " config weapon list is null.");

        Assert.NotNull(
            runtimeWeapons,
            ownerConfigId + " runtime weapon list is null.");

        HashSet<string> expectedWeaponIds =
            new HashSet<string>();

        for (int i = 0; i < configWeapons.Count; i++)
        {
            WeaponConfig weaponConfig =
                configWeapons[i];

            if (weaponConfig == null)
                continue;

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(weaponConfig.Id),
                ownerConfigId + " has configured weapon without Id.");

            expectedWeaponIds.Add(
                weaponConfig.Id);
        }

        HashSet<string> actualWeaponIds =
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
                actualWeaponIds.Add(runtimeWeapon.WeaponConfigId),
                ownerConfigId + " has duplicated runtime weapon: " + runtimeWeapon.WeaponConfigId);

            Assert.GreaterOrEqual(
                runtimeWeapon.ShotDistance,
                0f,
                ownerConfigId + " has negative ShotDistance for " + runtimeWeapon.WeaponConfigId);
        }

        Assert.AreEqual(
            expectedWeaponIds.Count,
            actualWeaponIds.Count,
            ownerConfigId + " runtime weapon id count does not match config weapon id count.");

        foreach (string expectedId in expectedWeaponIds)
        {
            Assert.IsTrue(
                actualWeaponIds.Contains(expectedId),
                ownerConfigId + " runtime weapons missing: " + expectedId);
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
}