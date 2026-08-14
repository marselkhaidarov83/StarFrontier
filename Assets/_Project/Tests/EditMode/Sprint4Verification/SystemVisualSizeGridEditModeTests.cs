using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SystemVisualSizeGridEditModeTests
{
    [Test]
    public void UnitsToWorldSize_UsesConfiguredPixelUnit()
    {
        SystemVisualConfig visualConfig =
            ScriptableObject.CreateInstance<SystemVisualConfig>();

        try
        {
            SetPrivateField(visualConfig, "sizeUnitPixels", 8f);

            Assert.AreEqual(
                24f,
                visualConfig.UnitsToWorldSize(3f));

            Assert.AreEqual(
                0f,
                visualConfig.UnitsToWorldSize(-3f));
        }
        finally
        {
            Object.DestroyImmediate(visualConfig);
        }
    }

    [Test]
    public void ObjectWorldSizes_UseUnifiedGridAndObjectUnits()
    {
        SystemVisualConfig visualConfig =
            ScriptableObject.CreateInstance<SystemVisualConfig>();

        SunConfig sun =
            ScriptableObject.CreateInstance<SunConfig>();

        PlanetConfig planet =
            ScriptableObject.CreateInstance<PlanetConfig>();

        StationTypeConfig stationType =
            ScriptableObject.CreateInstance<StationTypeConfig>();

        StationConfig station =
            ScriptableObject.CreateInstance<StationConfig>();

        AllyConfig ally =
            ScriptableObject.CreateInstance<AllyConfig>();

        EnemyConfig enemy =
            ScriptableObject.CreateInstance<EnemyConfig>();

        PirateConfig pirate =
            ScriptableObject.CreateInstance<PirateConfig>();

        RouteEndpointConfig endpoint =
            ScriptableObject.CreateInstance<RouteEndpointConfig>();

        try
        {
            SetPrivateField(visualConfig, "sizeUnitPixels", 2f);

            SetPrivateField(sun, "visualSize", 70f);
            SetPrivateField(planet, "visualSize", 42f);
            SetPrivateField(station, "overrideVisualSize", true);
            SetPrivateField(station, "visualSizeOverride", 88f);
            SetPrivateField(stationType, "visualSize", 88f);
            SetPrivateField(station, "stationTypeConfig", stationType);
            SetPrivateField(ally, "visualSize", 24f);
            SetPrivateField(enemy, "visualSize", 36f);
            SetPrivateField(pirate, "visualSize", 34f);
            SetPrivateField(endpoint, "visualSize", 18f);

            Assert.AreEqual(
                140f,
                visualConfig.GetSunWorldSize(sun));

            Assert.AreEqual(
                84f,
                visualConfig.GetPlanetWorldSize(planet));

            Assert.AreEqual(
                176f,
                visualConfig.GetStationWorldSize(station));

            Assert.AreEqual(
                48f,
                visualConfig.GetAllyWorldSize(ally));

            Assert.AreEqual(
                72f,
                visualConfig.GetEnemyWorldSize(enemy));

            Assert.AreEqual(
                68f,
                visualConfig.GetPirateWorldSize(pirate));

            Assert.AreEqual(
                36f,
                visualConfig.GetSystemExitWorldSize(endpoint));
        }
        finally
        {
            Object.DestroyImmediate(endpoint);
            Object.DestroyImmediate(pirate);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(station);
            Object.DestroyImmediate(stationType);
            Object.DestroyImmediate(planet);
            Object.DestroyImmediate(sun);
            Object.DestroyImmediate(visualConfig);
        }
    }

    [Test]
    public void ObjectWorldSizes_ReturnZeroWithoutObjectConfig()
    {
        SystemVisualConfig visualConfig =
            ScriptableObject.CreateInstance<SystemVisualConfig>();

        try
        {
            SetPrivateField(visualConfig, "sizeUnitPixels", 3f);

            Assert.AreEqual(
                0f,
                visualConfig.GetSunWorldSize(null));

            Assert.AreEqual(
                0f,
                visualConfig.GetPlanetWorldSize(null));

            Assert.AreEqual(
                0f,
                visualConfig.GetStationWorldSize(null));

            Assert.AreEqual(
                0f,
                visualConfig.GetEnemyWorldSize(null));

            Assert.AreEqual(
                0f,
                visualConfig.GetPirateWorldSize(null));

            Assert.AreEqual(
                0f,
                visualConfig.GetSystemExitWorldSize(null));
        }
        finally
        {
            Object.DestroyImmediate(visualConfig);
        }
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

        Assert.IsNotNull(
            field,
            "Missing field: " + fieldName);

        field.SetValue(
            target,
            value);
    }
}
