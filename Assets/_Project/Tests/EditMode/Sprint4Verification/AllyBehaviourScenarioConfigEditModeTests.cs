using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AllyBehaviourScenarioConfigEditModeTests
{
    [Test]
    public void AllyBehaviourScenario_ContainsRequiredValues()
    {
        Assert.That(Enum.IsDefined(typeof(AllyBehaviourScenario), AllyBehaviourScenario.Normal), Is.True);
        Assert.That(Enum.IsDefined(typeof(AllyBehaviourScenario), AllyBehaviourScenario.EnemyInvasion), Is.True);
        Assert.That(Enum.IsDefined(typeof(AllyBehaviourScenario), AllyBehaviourScenario.EnemySystemInvasion), Is.True);
    }

    [Test]
    public void AllyBehaviourScenarioConfig_InheritsFromBaseConfig()
    {
        Assert.That(
            typeof(BaseConfig).IsAssignableFrom(typeof(AllyBehaviourScenarioConfig)),
            Is.True);
    }

    [Test]
    public void AllyBehaviourScenarioConfig_DoesNotStoreScenario()
    {
        FieldInfo scenarioIdField = typeof(AllyBehaviourScenarioConfig).GetField(
            "scenarioId",
            BindingFlags.Instance | BindingFlags.NonPublic);

        FieldInfo scenarioField = typeof(AllyBehaviourScenarioConfig).GetField(
            "scenario",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(scenarioIdField, Is.Null);
        Assert.That(scenarioField, Is.Null);
    }

    [Test]
    public void AllyConfig_ResolvesBehaviorProfileByEnum()
    {
        AllyConfig allyConfig = ScriptableObject.CreateInstance<AllyConfig>();

        NpcBehaviourScenarioConfig normal =
            ScriptableObject.CreateInstance<NpcBehaviourScenarioConfig>();

        NpcBehaviourScenarioConfig enemyInvasion =
            ScriptableObject.CreateInstance<NpcBehaviourScenarioConfig>();

        NpcBehaviourScenarioConfig enemySystemInvasion =
            ScriptableObject.CreateInstance<NpcBehaviourScenarioConfig>();

        AllyBehaviourScenarioEntry normalEntry = CreateEntry(
            AllyBehaviourScenario.Normal,
            normal);

        AllyBehaviourScenarioEntry enemyInvasionEntry = CreateEntry(
            AllyBehaviourScenario.EnemyInvasion,
            enemyInvasion);

        AllyBehaviourScenarioEntry enemySystemInvasionEntry = CreateEntry(
            AllyBehaviourScenario.EnemySystemInvasion,
            enemySystemInvasion);

        SetPrivateField(
            allyConfig,
            "behaviorScenarios",
            new[]
            {
                normalEntry,
                enemyInvasionEntry,
                enemySystemInvasionEntry
            });

        try
        {
            Assert.That(
                allyConfig.TryGetBehaviorScenario(
                    AllyBehaviourScenario.Normal,
                    out NpcBehaviourScenarioConfig normalResult),
                Is.True);

            Assert.That(normalResult, Is.SameAs(normal));

            Assert.That(
                allyConfig.TryGetBehaviorScenario(
                    AllyBehaviourScenario.EnemyInvasion,
                    out NpcBehaviourScenarioConfig invasionResult),
                Is.True);

            Assert.That(invasionResult, Is.SameAs(enemyInvasion));

            Assert.That(
                allyConfig.GetBehaviorScenario(
                    AllyBehaviourScenario.EnemySystemInvasion),
                Is.SameAs(enemySystemInvasion));

            Assert.That(
                allyConfig.HasDuplicateBehaviorScenarios(),
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(allyConfig);
            UnityEngine.Object.DestroyImmediate(normal);
            UnityEngine.Object.DestroyImmediate(enemyInvasion);
            UnityEngine.Object.DestroyImmediate(enemySystemInvasion);
        }
    }

    [Test]
    public void AllyConfig_DetectsDuplicateScenarios()
    {
        AllyConfig allyConfig = ScriptableObject.CreateInstance<AllyConfig>();

        NpcBehaviourScenarioConfig firstProfile =
            ScriptableObject.CreateInstance<NpcBehaviourScenarioConfig>();

        NpcBehaviourScenarioConfig secondProfile =
            ScriptableObject.CreateInstance<NpcBehaviourScenarioConfig>();

        AllyBehaviourScenarioEntry firstEntry = CreateEntry(
            AllyBehaviourScenario.Normal,
            firstProfile);

        AllyBehaviourScenarioEntry secondEntry = CreateEntry(
            AllyBehaviourScenario.Normal,
            secondProfile);

        SetPrivateField(
            allyConfig,
            "behaviorScenarios",
            new[]
            {
                firstEntry,
                secondEntry
            });

        try
        {
            Assert.That(
                allyConfig.HasDuplicateBehaviorScenarios(),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(allyConfig);
            UnityEngine.Object.DestroyImmediate(firstProfile);
            UnityEngine.Object.DestroyImmediate(secondProfile);
        }
    }

    [Test]
    public void ExistingAllyConfigAssets_HaveExpectedScenarios()
    {
        AllyConfigScenarioExpectation[] expectations =
        {
            new AllyConfigScenarioExpectation(
                "ally_military_L01_01",
                true),

            new AllyConfigScenarioExpectation(
                "ally_ranger_L01_01",
                true),

            new AllyConfigScenarioExpectation(
                "ally_trader_L01_01",
                false),

            new AllyConfigScenarioExpectation(
                "ally_medic_L01_01",
                false),

            new AllyConfigScenarioExpectation(
                "ally_science_L01_01",
                false)
        };

        for (int i = 0; i < expectations.Length; i++)
        {
            AllyConfigScenarioExpectation expectation = expectations[i];

            AllyConfig config =
                FindConfigById<AllyConfig>(
                    expectation.Id);

            Assert.That(
                config,
                Is.Not.Null,
                "Missing AllyConfig: " + expectation.Id);

            Assert.That(
                config.HasDuplicateBehaviorScenarios(),
                Is.False,
                config.Id + " has duplicate scenarios.");

            Assert.That(
                config.HasBehaviorScenario(
                    AllyBehaviourScenario.Normal),
                Is.True,
                config.Id + " is missing normal.");

            Assert.That(
                config.HasBehaviorScenario(
                    AllyBehaviourScenario.EnemyInvasion),
                Is.True,
                config.Id + " is missing enemy_invasion.");

            Assert.That(
                config.HasBehaviorScenario(
                    AllyBehaviourScenario.EnemySystemInvasion),
                Is.EqualTo(expectation.RequiresEnemySystemInvasion),
                config.Id + " has wrong enemy_system_invasion availability.");
        }
    }

    private static AllyBehaviourScenarioEntry CreateEntry(
        AllyBehaviourScenario scenario,
        NpcBehaviourScenarioConfig behaviorConfig)
    {
        AllyBehaviourScenarioEntry entry =
            new AllyBehaviourScenarioEntry();

        SetPrivateField(
            entry,
            "scenario",
            scenario);

        SetPrivateField(
            entry,
            "behaviorConfig",
            behaviorConfig);

        return entry;
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

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.That(
            field,
            Is.Not.Null,
            "Private field not found: " + fieldName);

        field.SetValue(target, value);
    }

    private sealed class AllyConfigScenarioExpectation
    {
        public AllyConfigScenarioExpectation(
            string id,
            bool requiresEnemySystemInvasion)
        {
            Id = id;
            RequiresEnemySystemInvasion = requiresEnemySystemInvasion;
        }

        public string Id { get; }
        public bool RequiresEnemySystemInvasion { get; }
    }
}