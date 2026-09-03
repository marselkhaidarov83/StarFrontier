using NUnit.Framework;
using System.IO;

public sealed class S04_04_T05_CombatMarkerVisibilityTests
{
    [Test]
    public void CombatMarkerVisibilityScalerScript_Exists()
    {
        Assert.IsTrue(
            File.Exists(
                "Assets/_Project/Scripts/Presentation/Npc/CombatMarkerVisibilityScaler2A.cs"),
            "T05 requires CombatMarkerVisibilityScaler2A script.");
    }

    [Test]
    public void CombatMarkerVisibilityScaler_UsesDistanceCameraAndDensityRules()
    {
        string text =
            File.ReadAllText(
                "Assets/_Project/Scripts/Presentation/Npc/CombatMarkerVisibilityScaler2A.cs");

        Assert.IsTrue(
            text.Contains("ResolvePlayerMaxWeaponRange"),
            "Marker visibility must use player weapon range.");

        Assert.IsTrue(
            text.Contains("WeaponConfig.RangeMax") ||
            text.Contains("weaponConfig.RangeMax"),
            "Marker visibility must read max range from player weapon configs.");

        Assert.IsTrue(
            text.Contains("hideRangeMultiplier"),
            "Marker visibility must hide by a multiplier of player weapon range.");

        Assert.IsTrue(
            text.Contains("WorldToViewportPoint"),
            "Marker visibility must check camera viewport visibility.");

        Assert.IsTrue(
            text.Contains("denseMarkerRadius"),
            "Marker visibility must have density rules.");

        Assert.IsTrue(
            text.Contains("maxNearbyVisibleStatusMarkers"),
            "Marker visibility must limit status markers in dense groups.");

        Assert.IsTrue(
            text.Contains("WeaponAssignmentText"),
            "Marker visibility must control the authored target marker text.");

        Assert.IsFalse(
            text.Contains("SetRenderersVisible(targetMarkerRoot"),
            "Marker scaler must not directly hide selected enemy/NPC target marker renderers.");

        Assert.IsFalse(
            text.Contains("SetRenderersVisible(targetLabelRoot"),
            "Marker scaler must not directly hide selected enemy/NPC target marker text.");

        Assert.IsFalse(
            text.Contains("ApplyRootScale(targetMarkerRoot"),
            "Marker scaler must not override target marker size calculated from NPC ship size.");

        Assert.IsFalse(
            text.Contains("ApplyRootScale(targetLabelRoot"),
            "Marker scaler must not override target marker text layout calculated from NPC ship size.");
    }

    [Test]
    public void NpcPrefab_HasCombatMarkerVisibilityScaler()
    {
        string text =
            File.ReadAllText(
                "Assets/_Project/Prefabs/Npc/NpcPrefab_01.prefab");

        Assert.IsTrue(
            text.Contains("CombatMarkerVisibilityScaler2A"),
            "Npc prefab must contain CombatMarkerVisibilityScaler2A.");

        Assert.IsTrue(
            text.Contains("fullSizeRangeMultiplier: 0.33"),
            "Npc prefab must show markers at full size inside one third of player weapon range.");

        Assert.IsTrue(
            text.Contains("fadeStartRangeMultiplier: 1"),
            "Npc prefab must start reducing markers at player weapon range.");

        Assert.IsTrue(
            text.Contains("hideRangeMultiplier: 1.67"),
            "Npc prefab must hide markers at roughly five thirds of player weapon range.");

        Assert.IsTrue(
            text.Contains("targetMarkerRoot: {fileID: 5791710429043291811}"),
            "Npc prefab must bind the target marker root.");

        Assert.IsTrue(
            text.Contains("targetLabelRoot: {fileID: 8594063311080642002}"),
            "Npc prefab must bind the target marker text root.");

        Assert.IsTrue(
            text.Contains("statusMarkerRoot: {fileID: 7220002}"),
            "Npc prefab must bind the enemy status marker root.");
    }
}
