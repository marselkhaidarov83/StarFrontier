#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class Stage2A_S03_03_02_FuelHudEditModeTests
{
    [SetUp]
    public void OpenScene()
    {
        Stage2A_S03_03_TestUtility.OpenSystemScene();
    }

    [Test]
    public void FuelHudStateUtility2A_Exists()
    {
        Assert.NotNull(
            Stage2A_S03_03_TestUtility
                .FindType("FuelHudStateUtility2A"),
            "FuelHudStateUtility2A is missing. T05 fuel thresholds are not testable.");
    }

    [TestCase(100f, "Normal")]
    [TestCase(75f, "Normal")]
    [TestCase(74.99f, "Medium")]
    [TestCase(50f, "Medium")]
    [TestCase(25f, "Medium")]
    [TestCase(24.99f, "Low")]
    [TestCase(0f, "Low")]
    public void FuelHudStateUtility2A_ReturnsExpectedStateByPercent(
        float percent,
        string expectedStateName)
    {
        Type utilityType =
            Stage2A_S03_03_TestUtility
                .FindType("FuelHudStateUtility2A");

        Assert.NotNull(utilityType);

        object result =
            Stage2A_S03_03_TestUtility
                .InvokeStatic(
                    utilityType,
                    "GetStateByPercent",
                    percent);

        Assert.AreEqual(
            expectedStateName,
            result.ToString(),
            "Fuel HUD state threshold mismatch for percent = " + percent);
    }

    [Test]
    public void FuelHudStateUtility2A_ReturnsLow_WhenCapacityIsZero()
    {
        Type utilityType =
            Stage2A_S03_03_TestUtility
                .FindType("FuelHudStateUtility2A");

        Assert.NotNull(utilityType);

        object result =
            Stage2A_S03_03_TestUtility
                .InvokeStatic(
                    utilityType,
                    "GetState",
                    10,
                    0);

        Assert.AreEqual(
            "Low",
            result.ToString(),
            "Fuel HUD must be Low when capacity is zero.");
    }

    [TestCase("icon_fuel_low_01")]
    [TestCase("icon_fuel_medium_01")]
    [TestCase("icon_fuel_normal_01")]
    public void ProductionFuelIcon_IsImportedAsSprite(
        string iconName)
    {
        string[] paths =
            Stage2A_S03_03_TestUtility
                .FindAssetPathsByName(iconName);

        Assert.IsNotEmpty(
            paths,
            "Fuel icon asset not found: " + iconName);

        bool hasSpriteTexture =
            false;

        foreach (string path in paths)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null &&
                importer.textureType == TextureImporterType.Sprite)
            {
                hasSpriteTexture = true;
                break;
            }
        }

        Assert.IsTrue(
            hasSpriteTexture,
            "Fuel icon must be imported as Sprite: " + iconName);
    }

    [Test]
    public void SystemScene_HasFuelHudObjects()
    {
        bool hasFuelObject =
            Stage2A_S03_03_TestUtility
                .FindGameObjectsInOpenScenes(gameObject =>
                    gameObject.name.ToLowerInvariant().Contains("fuel"))
                .Count > 0;

        Assert.IsTrue(
            hasFuelObject,
            "SystemScene must contain Fuel HUD objects.");
    }

    [Test]
    public void SystemTopHudFuelIndicator2A_ExistsOrSceneHasFuelBindingController()
    {
        Type explicitController =
            Stage2A_S03_03_TestUtility
                .FindType("SystemTopHudFuelIndicator2A");

        bool hasSceneFuelController =
            Stage2A_S03_03_TestUtility
                .FindGameObjectsInOpenScenes(gameObject =>
                    gameObject.name.ToLowerInvariant().Contains("fuel"))
                .Count > 0;

        Assert.IsTrue(
            explicitController != null || hasSceneFuelController,
            "Fuel HUD must have either SystemTopHudFuelIndicator2A or existing scene fuel binding.");
    }
}
#endif
