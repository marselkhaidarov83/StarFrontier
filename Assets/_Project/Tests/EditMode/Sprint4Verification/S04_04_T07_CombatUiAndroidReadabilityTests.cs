using NUnit.Framework;
using System.IO;

public sealed class S04_04_T07_CombatUiAndroidReadabilityTests
{
    [Test]
    public void SystemScene_UsesPortraitReferenceResolutionAndSafeArea()
    {
        string sceneText =
            ReadProjectFile(
                "Assets/_Project/Scenes/SystemScene.unity");

        Assert.IsTrue(
            sceneText.Contains("m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.CanvasScaler"),
            "SystemScene must have CanvasScaler.");

        Assert.IsTrue(
            sceneText.Contains("m_ReferenceResolution: {x: 1080, y: 1920}"),
            "SystemScene must use portrait 1080x1920 reference resolution.");

        Assert.IsTrue(
            sceneText.Contains("SystemHudSafeAreaAdapter2A"),
            "SystemScene must use safe area adapter for Android/touch screens.");
    }

    [Test]
    public void CombatHudPrefab_HasTouchReadablePanels()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/UI/Hud/PF_SystemHudBottomRoot.prefab");

        Assert.IsTrue(
            prefabText.Contains("m_Name: PlayerThreatListPanel"),
            "Combat HUD must have right threat list panel.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: PlayerWeaponListPanel"),
            "Combat HUD must have left player weapon list panel.");

        Assert.IsTrue(
            prefabText.Contains("headerHeight: 48"),
            "Combat HUD panel headers must be touch-readable.");

        Assert.IsTrue(
            prefabText.Contains("rowHeight: 40"),
            "Combat HUD rows must be touch-readable.");

        Assert.IsTrue(
            prefabText.Contains("m_SizeDelta: {x: 500, y: 260}") &&
            prefabText.Contains("m_SizeDelta: {x: 500, y: 180}"),
            "Combat HUD panels must be wide enough for Russian text.");

        Assert.IsTrue(
            prefabText.Contains("threatRowsText: {fileID: 9100044}"),
            "Threat list must use authored clickable text rows.");

        Assert.IsTrue(
            prefabText.Contains("rowsText: {fileID: 9200404}"),
            "Weapon list must use authored clickable text rows.");
    }

    [Test]
    public void CameraActionButtons_MoveAboveThreatPanel()
    {
        string sceneText =
            ReadProjectFile(
                "Assets/_Project/Scenes/SystemScene.unity");

        Assert.IsTrue(
            sceneText.Contains("m_Name: ActionButtonsBlock"),
            "SystemScene must keep camera action buttons in a dedicated block.");

        Assert.IsTrue(
            sceneText.Contains("m_Name: ReturnToShipButton"),
            "SystemScene must have return-to-ship camera button.");

        Assert.IsTrue(
            sceneText.Contains("m_Name: CenterOnTargetButton"),
            "SystemScene must have center-on-target camera button.");

        Assert.IsTrue(
            sceneText.Contains("SystemCameraActionButtonsThreatPanelOffset2A"),
            "Camera action buttons must move above right threat panel.");
    }

    [Test]
    public void CombatMarkers_UseAndroidReadableSortingAndScaling()
    {
        string targetMarkerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatWeaponTargetMarkerView2A.cs");

        string statusMarkerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatEnemyStatusMarkerView2A.cs");

        string scalerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatMarkerVisibilityScaler2A.cs");

        Assert.IsTrue(
            targetMarkerText.Contains("SystemShipFX"),
            "Target marker must render in SystemShipFX sorting layer.");

        Assert.IsTrue(
            statusMarkerText.Contains("SystemShipFX"),
            "Enemy HP/shield marker must render in SystemShipFX sorting layer.");

        Assert.IsTrue(
            targetMarkerText.Contains("WorldSize"),
            "Target marker size must depend on NPC ship world size.");

        Assert.IsTrue(
            scalerText.Contains("ResolvePlayerMaxWeaponRange"),
            "Marker visibility distance must depend on player weapon range.");

        Assert.IsFalse(
            scalerText.Contains("ApplyRootScale(targetMarkerRoot"),
            "Distance scaler must not override target marker size.");
    }

    private static string ReadProjectFile(string path)
    {
        Assert.IsTrue(
            File.Exists(path),
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}
