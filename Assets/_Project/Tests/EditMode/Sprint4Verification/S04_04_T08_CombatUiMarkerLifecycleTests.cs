using NUnit.Framework;
using System.IO;

public sealed class S04_04_T08_CombatUiMarkerLifecycleTests
{
    [Test]
    public void NpcPrefab_HasAllCombatMarkerComponentsBound()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/UI/Npc/NpcPrefab_01.prefab");

        Assert.IsTrue(
            prefabText.Contains("m_Name: CombatWeaponTargetMarker"),
            "Npc prefab must contain authored target marker object.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: WeaponAssignmentText"),
            "Npc prefab must contain authored target marker text object.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: CombatEnemyStatusMarker"),
            "Npc prefab must contain authored enemy status marker object.");

        Assert.IsTrue(
            prefabText.Contains("CombatWeaponTargetMarkerView2A"),
            "Npc prefab must have target marker view component.");

        Assert.IsTrue(
            prefabText.Contains("CombatEnemyStatusMarkerView2A"),
            "Npc prefab must have enemy HP/shield marker view component.");

        Assert.IsTrue(
            prefabText.Contains("CombatMarkerVisibilityScaler2A"),
            "Npc prefab must have marker visibility scaler component.");

        Assert.IsTrue(
            prefabText.Contains("markerRoot: {fileID: 6712718687247919678}"),
            "Target marker view must bind CombatWeaponTargetMarker root.");

        Assert.IsTrue(
            prefabText.Contains("weaponAssignmentsText: {fileID: 8594063311080642004}"),
            "Target marker view must bind authored TMP text.");

        Assert.IsTrue(
            prefabText.Contains("statusMarkerRoot: {fileID: 7220002}"),
            "Marker visibility scaler must bind enemy status marker root.");
    }

    [Test]
    public void SystemHudPrefab_HasCombatPanelsAndClickableRowsBound()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/UI/Hud/PF_SystemHudBottomRoot.prefab");

        Assert.IsTrue(
            prefabText.Contains("SystemPlayerThreatListHud2A"),
            "System HUD prefab must have threat list HUD component.");

        Assert.IsTrue(
            prefabText.Contains("SystemPlayerWeaponListHud2A"),
            "System HUD prefab must have player weapon list HUD component.");

        Assert.IsTrue(
            prefabText.Contains("PlayerThreatListPanel"),
            "System HUD prefab must contain threat list panel.");

        Assert.IsTrue(
            prefabText.Contains("PlayerWeaponListPanel"),
            "System HUD prefab must contain weapon list panel.");

        Assert.IsTrue(
            prefabText.Contains("rowsClickRelay: {fileID: 9100046}"),
            "Threat rows must use pointer click relay.");

        Assert.IsTrue(
            prefabText.Contains("rowsClickRelay: {fileID: 9200406}"),
            "Weapon rows must use pointer click relay.");

        Assert.IsTrue(
            prefabText.Contains("threatRowsText: {fileID: 9100044}"),
            "Threat HUD must bind authored rows text.");

        Assert.IsTrue(
            prefabText.Contains("rowsText: {fileID: 9200404}"),
            "Weapon HUD must bind authored rows text.");
    }

    [Test]
    public void TargetMarkerLifecycle_UsesAssignmentsTravelAndDestroyedEvents()
    {
        string markerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatWeaponTargetMarkerView2A.cs");

        Assert.IsTrue(
            markerText.Contains("CombatWeaponTargetAssignmentsChangedEvent2A"),
            "Target marker must react to weapon assignment changes.");

        Assert.IsTrue(
            markerText.Contains("DestinationSelectedEvent"),
            "Target marker must react to NPC travel target selection.");

        Assert.IsTrue(
            markerText.Contains("SystemTravelCancelledEvent"),
            "Target marker must clear NPC travel selection when travel is cancelled.");

        Assert.IsTrue(
            markerText.Contains("SystemTravelCompletedEvent"),
            "Target marker must clear NPC travel selection when travel is completed.");

        Assert.IsTrue(
            markerText.Contains("SystemNpcDestroyedEvent"),
            "Target marker must hide when its NPC is destroyed.");

        Assert.IsTrue(
            markerText.Contains("SetVisible(false)"),
            "Target marker must support explicit cleanup hiding.");
    }

    [Test]
    public void EnemyStatusMarkerLifecycle_UsesStateDamageAndDestroyedEvents()
    {
        string statusText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatEnemyStatusMarkerView2A.cs");

        Assert.IsTrue(
            statusText.Contains("SystemNpcDamagedEvent"),
            "Enemy status marker must refresh when NPC receives damage.");

        Assert.IsTrue(
            statusText.Contains("SystemNpcDestroyedEvent"),
            "Enemy status marker must hide when NPC is destroyed.");

        Assert.IsTrue(
            statusText.Contains("!npc.IsAlive"),
            "Enemy status marker must hide when NPC state is no longer alive.");

        Assert.IsTrue(
            statusText.Contains("!npc.IsHostileToPlayer"),
            "Enemy status marker must hide for non-hostile NPC.");
    }

    [Test]
    public void PlayerAttackService_CleansInvalidTargetsAndPublishesCleanup()
    {
        string serviceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            serviceText.Contains("RemoveInvalidAssignments"),
            "Player attack service must remove invalid weapon target assignments.");

        Assert.IsTrue(
            serviceText.Contains("SystemNpcDestroyedEvent"),
            "Player attack service must react to NPC destruction.");

        Assert.IsTrue(
            serviceText.Contains("SystemEncounterResolvedEvent"),
            "Player attack service must cleanup after encounter victory/resolution.");

        Assert.IsTrue(
            serviceText.Contains("SystemEncounterDefeatedEvent"),
            "Player attack service must cleanup after encounter defeat.");

        Assert.IsTrue(
            serviceText.Contains("PlayerDestroyedEvent"),
            "Player attack service must cleanup after player death.");

        Assert.IsTrue(
            serviceText.Contains("CombatWeaponTargetAssignmentsChangedEvent2A.Cleared()"),
            "Player attack cleanup must publish cleared marker assignments.");
    }

    [Test]
    public void TravelSelection_ClearsOldPlanetMapAndExitMarkersBeforeNpcTarget()
    {
        string travelText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Travel/SystemTravelService.cs");

        int npcDestinationIndex =
            travelText.IndexOf(
                "public void SetNpcDestination",
                System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(
            npcDestinationIndex,
            0,
            "SystemTravelService must support NPC destination selection.");

        string npcDestinationText =
            travelText.Substring(npcDestinationIndex);

        Assert.IsTrue(
            npcDestinationText.Contains("ClearPreviousTravelSelectionForNpcDestination"),
            "NPC destination selection must clear old planet/map/exit markers first.");

        Assert.IsTrue(
            npcDestinationText.Contains("SystemTravelCancelledEvent"),
            "NPC destination selection must publish travel cancel event for marker cleanup.");

        Assert.IsTrue(
            npcDestinationText.Contains("_targetService?.ClearTarget();"),
            "NPC destination selection must clear previous target service marker state.");
    }

    [Test]
    public void MarkerVisibilityScaler_DoesNotOverrideTargetMarkerOwnership()
    {
        string scalerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatMarkerVisibilityScaler2A.cs");

        Assert.IsTrue(
            scalerText.Contains("ResolvePlayerMaxWeaponRange"),
            "Marker visibility scaler must use player weapon range.");

        Assert.IsTrue(
            scalerText.Contains("SetRenderersVisible(statusMarkerRoot"),
            "Marker visibility scaler may hide enemy status marker.");

        Assert.IsFalse(
            scalerText.Contains("SetRenderersVisible(targetMarkerRoot"),
            "Marker visibility scaler must not hide target marker renderers.");

        Assert.IsFalse(
            scalerText.Contains("SetRenderersVisible(targetLabelRoot"),
            "Marker visibility scaler must not hide target marker text.");

        Assert.IsFalse(
            scalerText.Contains("ApplyRootScale(targetMarkerRoot"),
            "Marker visibility scaler must not override target marker size.");

        Assert.IsFalse(
            scalerText.Contains("ApplyRootScale(targetLabelRoot"),
            "Marker visibility scaler must not override target marker text layout.");
    }

    [Test]
    public void Sprint404_AllTaskVerificationFilesExist()
    {
        string[] requiredTests =
        {
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T01_WeaponTargetMarkerTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T02_EnemyStatusMarkerTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T03_PlayerThreatListHudTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T04_PlayerWeaponListHudTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T05_CombatMarkerVisibilityTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T06_CombatUiCleanupTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T07_CombatUiAndroidReadabilityTests.cs"
        };

        for (int i = 0; i < requiredTests.Length; i++)
        {
            Assert.IsTrue(
                File.Exists(requiredTests[i]),
                "Missing Sprint 4 combat UI verification test: " +
                requiredTests[i]);
        }
    }

    private static string ReadProjectFile(string path)
    {
        Assert.IsTrue(
            File.Exists(path),
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}
