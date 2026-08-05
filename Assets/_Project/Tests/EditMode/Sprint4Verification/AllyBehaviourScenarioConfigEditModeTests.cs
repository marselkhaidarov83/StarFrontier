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
        Assert.That(
            Enum.IsDefined(
                typeof(AllyBehaviourScenario),
                AllyBehaviourScenario.Normal),
            Is.True);

        Assert.That(
            Enum.IsDefined(
                typeof(AllyBehaviourScenario),
                AllyBehaviourScenario.EnemyInvasion),
            Is.True);

        Assert.That(
            Enum.IsDefined(
                typeof(AllyBehaviourScenario),
                AllyBehaviourScenario.EnemySystemInvasion),
            Is.True);
    }

    [Test]
    public void AllyBehaviourScenarioConfig_InheritsFromBaseConfig()
    {
        Assert.That(
            typeof(BaseConfig).IsAssignableFrom(
                typeof(AllyBehaviourScenarioConfig)),
            Is.True);
    }

    [Test]
    public void AllyBehaviourScenarioConfig_DoesNotStoreScenario()
    {
        FieldInfo scenarioIdField =
            typeof(AllyBehaviourScenarioConfig).GetField(
                "scenarioId",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        FieldInfo scenarioField =
            typeof(AllyBehaviourScenarioConfig).GetField(
                "scenario",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.That(scenarioIdField, Is.Null);
        Assert.That(scenarioField, Is.Null);
    }

    [Test]
    public void AllyConfig_ResolvesBehaviorProfileByEnum()
    {
        AllyConfig allyConfig =
            ScriptableObject.CreateInstance<AllyConfig>();

        AllyBehaviourScenarioConfig normal =
            ScriptableObject.CreateInstance<
                AllyBehaviourScenarioConfig>();

        AllyBehaviourScenarioConfig enemyInvasion =
            ScriptableObject.CreateInstance<
                AllyBehaviourScenarioConfig>();

        AllyBehaviourScenarioConfig enemySystemInvasion =
            ScriptableObject.CreateInstance<
                AllyBehaviourScenarioConfig>();

        AllyBehaviourScenarioEntry normalEntry =
            new AllyBehaviourScenarioEntry();

        AllyBehaviourScenarioEntry enemyInvasionEntry =
            new AllyBehaviourScenarioEntry();

        AllyBehaviourScenarioEntry enemySystemInvasionEntry =
            new AllyBehaviourScenarioEntry();

        SetPrivateField(
            normalEntry,
            "scenario",
            AllyBehaviourScenario.Normal);

        SetPrivateField(
            normalEntry,
            "behaviorConfig",
            normal);

        SetPrivateField(
            enemyInvasionEntry,
            "scenario",
            AllyBehaviourScenario.EnemyInvasion);

        SetPrivateField(
            enemyInvasionEntry,
            "behaviorConfig",
            enemyInvasion);

        SetPrivateField(
            enemySystemInvasionEntry,
            "scenario",
            AllyBehaviourScenario.EnemySystemInvasion);

        SetPrivateField(
            enemySystemInvasionEntry,
            "behaviorConfig",
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
                    out AllyBehaviourScenarioConfig normalResult),
                Is.True);

            Assert.That(
                normalResult,
                Is.SameAs(normal));

            Assert.That(
                allyConfig.TryGetBehaviorScenario(
                    AllyBehaviourScenario.EnemyInvasion,
                    out AllyBehaviourScenarioConfig invasionResult),
                Is.True);

            Assert.That(
                invasionResult,
                Is.SameAs(enemyInvasion));

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
            UnityEngine.Object.DestroyImmediate(
                allyConfig);

            UnityEngine.Object.DestroyImmediate(
                normal);

            UnityEngine.Object.DestroyImmediate(
                enemyInvasion);

            UnityEngine.Object.DestroyImmediate(
                enemySystemInvasion);
        }
    }

    [Test]
    public void AllyConfig_DetectsDuplicateScenarios()
    {
        AllyConfig allyConfig =
            ScriptableObject.CreateInstance<AllyConfig>();

        AllyBehaviourScenarioConfig firstProfile =
            ScriptableObject.CreateInstance<
                AllyBehaviourScenarioConfig>();

        AllyBehaviourScenarioConfig secondProfile =
            ScriptableObject.CreateInstance<
                AllyBehaviourScenarioConfig>();

        AllyBehaviourScenarioEntry firstEntry =
            new AllyBehaviourScenarioEntry();

        AllyBehaviourScenarioEntry secondEntry =
            new AllyBehaviourScenarioEntry();

        SetPrivateField(
            firstEntry,
            "scenario",
            AllyBehaviourScenario.Normal);

        SetPrivateField(
            firstEntry,
            "behaviorConfig",
            firstProfile);

        SetPrivateField(
            secondEntry,
            "scenario",
            AllyBehaviourScenario.Normal);

        SetPrivateField(
            secondEntry,
            "behaviorConfig",
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
            UnityEngine.Object.DestroyImmediate(
                allyConfig);

            UnityEngine.Object.DestroyImmediate(
                firstProfile);

            UnityEngine.Object.DestroyImmediate(
                secondProfile);
        }
    }

    [Test]
    public void ExistingAllyConfigAssets_HaveAllThreeScenarios()
    {
        string[] expectedIds =
        {
            "ally_civilian_L01_01",
            "ally_medical_L01_01",
            "ally_warrior_L01_01",
            "ally_ranger_L01_01",
            "ally_trader_L01_01"
        };

        for (int i = 0; i < expectedIds.Length; i++)
        {
            AllyConfig config =
                FindConfigById<AllyConfig>(
                    expectedIds[i]);

            Assert.That(
                config,
                Is.Not.Null,
                "Missing AllyConfig: " + expectedIds[i]);

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
                Is.True,
                config.Id +
                " is missing enemy_system_invasion.");
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
}