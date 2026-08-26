using NUnit.Framework;
using System.IO;

public sealed class S04_04_T06_CombatUiCleanupTests
{
    [Test]
    public void PlayerAttackService_ClearsCombatUiOnDeathAndEncounterFinish()
    {
        string serviceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            serviceText.Contains("ClearAllCombatUiTargets"),
            "PlayerAttackService must expose full combat UI cleanup.");

        Assert.IsTrue(
            serviceText.Contains("SystemNpcDestroyedEvent"),
            "PlayerAttackService must clear dead NPC target assignments.");

        Assert.IsTrue(
            serviceText.Contains("SystemEncounterResolvedEvent"),
            "PlayerAttackService must clear combat UI when encounter is resolved.");

        Assert.IsTrue(
            serviceText.Contains("SystemEncounterDefeatedEvent"),
            "PlayerAttackService must clear combat UI when encounter is defeated.");

        Assert.IsTrue(
            serviceText.Contains("PlayerDestroyedEvent"),
            "PlayerAttackService must clear combat UI when player is destroyed.");

        Assert.IsTrue(
            serviceText.Contains("CombatWeaponTargetAssignmentsChangedEvent2A.Cleared()"),
            "Full cleanup must publish cleared weapon target assignments.");
    }

    [Test]
    public void PlayerAttackInterface_ExposesFullCombatUiCleanup()
    {
        string interfaceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/IPlayerAttackService.cs");

        Assert.IsTrue(
            interfaceText.Contains("void ClearAllCombatUiTargets()"),
            "IPlayerAttackService must expose full combat UI cleanup.");
    }

    [Test]
    public void NpcTravelSelection_CancelsPreviousTravelMarkers()
    {
        string serviceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Travel/SystemTravelService.cs");

        int methodIndex =
            serviceText.IndexOf(
                "public void SetNpcDestination",
                System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(
            methodIndex,
            0,
            "SystemTravelService must support NPC destinations.");

        string methodText =
            serviceText.Substring(methodIndex);

        Assert.IsTrue(
            methodText.Contains("CancelTravel();"),
            "Selecting an NPC destination must cancel previous planet/map/exit route markers first.");

        Assert.IsTrue(
            methodText.Contains("_targetService?.ClearTarget();"),
            "Selecting an NPC destination must clear previous target marker state.");
    }

    [Test]
    public void MarkerVisibilityScaler_DoesNotHideSelectedTargetByDistance()
    {
        string scalerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatMarkerVisibilityScaler2A.cs");

        Assert.IsFalse(
            scalerText.Contains("SetRenderersVisible(targetMarkerRoot"),
            "Marker scaler must not directly hide selected enemy/NPC target marker renderers.");

        Assert.IsFalse(
            scalerText.Contains("SetRenderersVisible(targetLabelRoot"),
            "Marker scaler must not directly hide selected enemy/NPC target marker text.");

        Assert.IsFalse(
            scalerText.Contains("ApplyRootScale(targetMarkerRoot"),
            "Marker scaler must not override target marker size calculated from NPC ship size.");

        Assert.IsFalse(
            scalerText.Contains("ApplyRootScale(targetLabelRoot"),
            "Marker scaler must not override target marker text layout calculated from NPC ship size.");
    }

    private static string ReadProjectFile(string path)
    {
        Assert.IsTrue(
            File.Exists(path),
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}
