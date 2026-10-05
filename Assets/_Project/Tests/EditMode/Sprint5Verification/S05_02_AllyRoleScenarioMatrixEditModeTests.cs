using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class S05_02_AllyRoleScenarioMatrixEditModeTests
{
    private static readonly AllyRoleExpectation[] RequiredRoles =
    {
        new AllyRoleExpectation(
            "military",
            AllyRole2A.Military,
            true),

        new AllyRoleExpectation(
            "ranger",
            AllyRole2A.Ranger,
            true),

        new AllyRoleExpectation(
            "trader",
            AllyRole2A.Trader,
            false),

        new AllyRoleExpectation(
            "science",
            AllyRole2A.Science,
            false),

        new AllyRoleExpectation(
            "medic",
            AllyRole2A.Medic,
            false)
    };

    [Test]
    public void AllyRole2A_ContainsApprovedSprint5Roles()
    {
        Assert.That(
            Enum.IsDefined(typeof(AllyRole2A), AllyRole2A.Military),
            Is.True);

        Assert.That(
            Enum.IsDefined(typeof(AllyRole2A), AllyRole2A.Ranger),
            Is.True);

        Assert.That(
            Enum.IsDefined(typeof(AllyRole2A), AllyRole2A.Trader),
            Is.True);

        Assert.That(
            Enum.IsDefined(typeof(AllyRole2A), AllyRole2A.Science),
            Is.True);

        Assert.That(
            Enum.IsDefined(typeof(AllyRole2A), AllyRole2A.Medic),
            Is.True);

        string[] roleNames =
            Enum.GetNames(typeof(AllyRole2A));

        Assert.That(
            roleNames,
            Does.Contain("Science"),
            "Sprint 5 accepts Science as the civilization traffic role instead of CivilianTransport.");

        Assert.That(
            roleNames,
            Does.Not.Contain("CivilianTransport"),
            "CivilianTransport must not be required while Science is the accepted role.");
    }

    [Test]
    public void AllyBehaviourScenario_ContainsApprovedSprint5Scenarios()
    {
        Assert.That(
            Enum.IsDefined(typeof(AllyBehaviourScenario), AllyBehaviourScenario.Normal),
            Is.True);

        Assert.That(
            Enum.IsDefined(typeof(AllyBehaviourScenario), AllyBehaviourScenario.EnemyInvasion),
            Is.True);

        Assert.That(
            Enum.IsDefined(typeof(AllyBehaviourScenario), AllyBehaviourScenario.EnemySystemInvasion),
            Is.True);
    }

    [Test]
    public void AllyConfigs_ExistForEveryApprovedRoleAndLevel()
    {
        for (int roleIndex = 0; roleIndex < RequiredRoles.Length; roleIndex++)
        {
            AllyRoleExpectation roleExpectation =
                RequiredRoles[roleIndex];

            for (int level = 1; level <= 10; level++)
            {
                string id =
                    $"ally_{roleExpectation.RoleKey}_L{level:00}_01";

                AllyConfig config =
                    FindConfigById<AllyConfig>(id);

                Assert.That(
                    config,
                    Is.Not.Null,
                    "Missing AllyConfig: " + id);

                Assert.That(
                    config.Role,
                    Is.EqualTo(roleExpectation.Role),
                    config.Id + " has wrong AllyRole2A.");

                Assert.That(
                    config.Level,
                    Is.EqualTo(level),
                    config.Id + " has wrong level.");

                Assert.That(
                    config.MapSprite,
                    Is.Not.Null,
                    config.Id + " must have a role visual or marker sprite.");
            }
        }
    }

    [Test]
    public void AllyConfigs_HaveRequiredScenarioMatrix()
    {
        for (int roleIndex = 0; roleIndex < RequiredRoles.Length; roleIndex++)
        {
            AllyRoleExpectation roleExpectation =
                RequiredRoles[roleIndex];

            for (int level = 1; level <= 10; level++)
            {
                string id =
                    $"ally_{roleExpectation.RoleKey}_L{level:00}_01";

                AllyConfig config =
                    FindConfigById<AllyConfig>(id);

                Assert.That(
                    config,
                    Is.Not.Null,
                    "Missing AllyConfig: " + id);

                Assert.That(
                    config.HasDuplicateBehaviorScenarios(),
                    Is.False,
                    config.Id + " has duplicate behavior scenarios.");

                Assert.That(
                    config.HasBehaviorScenario(AllyBehaviourScenario.Normal),
                    Is.True,
                    config.Id + " is missing normal scenario.");

                Assert.That(
                    config.HasBehaviorScenario(AllyBehaviourScenario.EnemyInvasion),
                    Is.True,
                    config.Id + " is missing enemy_invasion scenario.");

                Assert.That(
                    config.HasBehaviorScenario(AllyBehaviourScenario.EnemySystemInvasion),
                    Is.EqualTo(roleExpectation.RequiresEnemySystemInvasion),
                    config.Id + " has wrong enemy_system_invasion availability.");
            }
        }
    }

    [Test]
    public void AllyConfigs_UseSharedNpcBehaviourScenarioConfigProfiles()
    {
        for (int roleIndex = 0; roleIndex < RequiredRoles.Length; roleIndex++)
        {
            AllyRoleExpectation roleExpectation =
                RequiredRoles[roleIndex];

            for (int level = 1; level <= 10; level++)
            {
                string id =
                    $"ally_{roleExpectation.RoleKey}_L{level:00}_01";

                AllyConfig config =
                    FindConfigById<AllyConfig>(id);

                Assert.That(
                    config,
                    Is.Not.Null,
                    "Missing AllyConfig: " + id);

                Assert.That(
                    config.BehaviorScenarios,
                    Is.Not.Null,
                    config.Id + " BehaviorScenarios list is null.");

                int expectedScenarioCount =
                    roleExpectation.RequiresEnemySystemInvasion ? 3 : 2;

                Assert.That(
                    config.BehaviorScenarios.Count,
                    Is.EqualTo(expectedScenarioCount),
                    config.Id + " has wrong scenario count.");

                for (int scenarioIndex = 0;
                     scenarioIndex < config.BehaviorScenarios.Count;
                     scenarioIndex++)
                {
                    AllyBehaviourScenarioEntry entry =
                        config.BehaviorScenarios[scenarioIndex];

                    Assert.That(
                        entry,
                        Is.Not.Null,
                        config.Id + " has null scenario entry at index " + scenarioIndex);

                    Assert.That(
                        entry.IsValid(),
                        Is.True,
                        config.Id + " has invalid scenario entry at index " + scenarioIndex);

                    Assert.That(
                        entry.BehaviorConfig,
                        Is.Not.Null,
                        config.Id + " has null NpcBehaviourScenarioConfig at index " + scenarioIndex);

                    Assert.That(
                        entry.BehaviorConfig,
                        Is.TypeOf<NpcBehaviourScenarioConfig>(),
                        config.Id + " must use shared NpcBehaviourScenarioConfig profiles.");
                }
            }
        }
    }

    [Test]
    public void AllyRoleVisuals_AreDifferentBetweenApprovedRolesAtLevelOne()
    {
        HashSet<Sprite> sprites =
            new HashSet<Sprite>();

        for (int roleIndex = 0; roleIndex < RequiredRoles.Length; roleIndex++)
        {
            AllyRoleExpectation roleExpectation =
                RequiredRoles[roleIndex];

            string id =
                $"ally_{roleExpectation.RoleKey}_L01_01";

            AllyConfig config =
                FindConfigById<AllyConfig>(id);

            Assert.That(
                config,
                Is.Not.Null,
                "Missing AllyConfig: " + id);

            Assert.That(
                config.MapSprite,
                Is.Not.Null,
                config.Id + " must have a role visual or marker sprite.");

            Assert.That(
                sprites.Add(config.MapSprite),
                Is.True,
                config.Id + " reuses another role sprite. Roles must be visually distinguishable.");
        }
    }

    private static T FindConfigById<T>(string id)
        where T : BaseConfig
    {
        string[] guids =
            AssetDatabase.FindAssets("t:" + typeof(T).Name);

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            T config =
                AssetDatabase.LoadAssetAtPath<T>(path);

            if (config == null)
                continue;

            if (config.Id == id)
                return config;
        }

        return null;
    }

    private sealed class AllyRoleExpectation
    {
        public AllyRoleExpectation(
            string roleKey,
            AllyRole2A role,
            bool requiresEnemySystemInvasion)
        {
            RoleKey = roleKey;
            Role = role;
            RequiresEnemySystemInvasion = requiresEnemySystemInvasion;
        }

        public string RoleKey { get; }
        public AllyRole2A Role { get; }
        public bool RequiresEnemySystemInvasion { get; }
    }
}