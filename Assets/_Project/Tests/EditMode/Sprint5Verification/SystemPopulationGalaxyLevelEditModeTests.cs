using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SystemPopulationGalaxyLevelEditModeTests
{
    [Test]
    public void PopulationConfig_SelectsTheExactGalaxyLevelProfile()
    {
        SystemPopulationConfig config =
            ScriptableObject.CreateInstance<SystemPopulationConfig>();

        SystemPopulationProfile levelOne =
            new SystemPopulationProfile();

        SystemPopulationProfile levelFour =
            new SystemPopulationProfile();

        SetPrivateField(levelOne, "galaxyLevel", 1);
        SetPrivateField(levelFour, "galaxyLevel", 4);

        SetPrivateField(
            config,
            "levelProfiles",
            new[]
            {
                levelOne,
                levelFour
            });

        Assert.That(
            config.GetProfileForGalaxyLevel(1),
            Is.SameAs(levelOne));

        Assert.That(
            config.GetProfileForGalaxyLevel(4),
            Is.SameAs(levelFour));

        Assert.That(
            config.GetProfileForGalaxyLevel(5),
            Is.Null);

        Object.DestroyImmediate(config);
    }

    [Test]
    public void EnemyConfig_IsAllowedOnlyWhenItsLevelEqualsGalaxyLevel()
    {
        EnemyConfig enemy =
            ScriptableObject.CreateInstance<EnemyConfig>();

        SetPrivateField(enemy, "level", 4);

        Assert.That(
            SystemPopulationConfig
                .IsEnemyConfigAllowedForGalaxyLevel(enemy, 4),
            Is.True);

        Assert.That(
            SystemPopulationConfig
                .IsEnemyConfigAllowedForGalaxyLevel(enemy, 3),
            Is.False);

        Assert.That(
            SystemPopulationConfig
                .IsEnemyConfigAllowedForGalaxyLevel(enemy, 5),
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