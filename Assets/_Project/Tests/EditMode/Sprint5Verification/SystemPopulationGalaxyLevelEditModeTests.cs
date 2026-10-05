using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SystemPopulationGalaxyLevelEditModeTests
{
    [Test]
    public void SystemPopulationRule_IsValidWhenItHasAnySpawnRule()
    {
        SystemPopulationRule populationRule =
            ScriptableObject.CreateInstance<SystemPopulationRule>();

        AllySpawnRuleConfig allyRule =
            ScriptableObject.CreateInstance<AllySpawnRuleConfig>();

        SetPrivateField(
            populationRule,
            "allySpawnRules",
            new[]
            {
                allyRule
            });

        Assert.That(
            populationRule.HasAnySpawnRule(),
            Is.True);

        Assert.That(
            populationRule.HasAnyAllySpawnRule(),
            Is.True);

        Object.DestroyImmediate(allyRule);
        Object.DestroyImmediate(populationRule);
    }

    [Test]
    public void SystemPopulationRule_IsInvalidWhenItHasNoSpawnRules()
    {
        SystemPopulationRule populationRule =
            ScriptableObject.CreateInstance<SystemPopulationRule>();

        Assert.That(
            populationRule.HasAnySpawnRule(),
            Is.False);

        Object.DestroyImmediate(populationRule);
    }

    [Test]
    public void EnemyConfig_LevelCanBeComparedToGalaxyLevel()
    {
        EnemyConfig enemy =
            ScriptableObject.CreateInstance<EnemyConfig>();

        SetPrivateField(enemy, "level", 4);

        Assert.That(
            enemy.Level == 4,
            Is.True);

        Assert.That(
            enemy.Level == 3,
            Is.False);

        Assert.That(
            enemy.Level == 5,
            Is.False);

        Object.DestroyImmediate(enemy);
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

        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
