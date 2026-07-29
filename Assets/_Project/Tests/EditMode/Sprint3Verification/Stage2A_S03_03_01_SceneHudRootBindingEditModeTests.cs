#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class Stage2A_S03_03_01_SceneHudRootBindingEditModeTests
{
    [SetUp]
    public void OpenScene()
    {
        Stage2A_S03_03_TestUtility.OpenSystemScene();
    }

    [Test]
    public void SystemScene_HasTopAndBottomSafeAreaHudRoots()
    {
        Assert.NotNull(
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_TopHud"),
            "SafeAreaRoot_TopHud is missing.");

        Assert.NotNull(
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_BottomHud"),
            "SafeAreaRoot_BottomHud is missing.");
    }

    [Test]
    public void SafeAreaHudRoots_HaveSystemHudSafeAreaAdapter2A()
    {
        GameObject top =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_TopHud");

        GameObject bottom =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_BottomHud");

        Assert.IsTrue(
            Stage2A_S03_03_TestUtility
                .HasMonoBehaviourNamed(top, "SystemHudSafeAreaAdapter2A"),
            "Top HUD safe area root must have SystemHudSafeAreaAdapter2A.");

        Assert.IsTrue(
            Stage2A_S03_03_TestUtility
                .HasMonoBehaviourNamed(bottom, "SystemHudSafeAreaAdapter2A"),
            "Bottom HUD safe area root must have SystemHudSafeAreaAdapter2A.");
    }

    [Test]
    public void BottomHudRoot_ContainsProductionBottomHudPrefabInstance()
    {
        GameObject bottom =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_BottomHud");

        Assert.NotNull(bottom);

        bool containsBottomPrefab =
            Stage2A_S03_03_TestUtility
                .GetChildrenRecursive(bottom)
                .Exists(child =>
                    child.name == "PF_SystemHudBottomRoot");

        Assert.IsTrue(
            containsBottomPrefab,
            "SafeAreaRoot_BottomHud must contain PF_SystemHudBottomRoot.");
    }

    [Test]
    public void HudCanvas_UsesScaleWithScreenSize_1080x1920()
    {
        GameObject top =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_TopHud");

        Assert.NotNull(top);

        Canvas canvas =
            top.GetComponentInParent<Canvas>();

        Assert.NotNull(
            canvas,
            "HUD safe area root must be under a Canvas.");

        CanvasScaler scaler =
            canvas.GetComponent<CanvasScaler>();

        Assert.NotNull(
            scaler,
            "HUD Canvas must have CanvasScaler.");

        Assert.AreEqual(
            CanvasScaler.ScaleMode.ScaleWithScreenSize,
            scaler.uiScaleMode,
            "HUD CanvasScaler must use Scale With Screen Size.");

        Assert.AreEqual(
            1080f,
            scaler.referenceResolution.x,
            0.01f,
            "HUD reference resolution X must be 1080.");

        Assert.AreEqual(
            1920f,
            scaler.referenceResolution.y,
            0.01f,
            "HUD reference resolution Y must be 1920.");

        Assert.AreEqual(
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
            scaler.screenMatchMode,
            "HUD CanvasScaler must use MatchWidthOrHeight.");
    }
}
#endif
