using NUnit.Framework;
using System.IO;

public sealed class S04_04_T04_PlayerWeaponListHudTests
{
    [Test]
    public void PlayerAttackService_SeparatesTargetSelectionFromWeaponAssignment()
    {
        string serviceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            serviceText.Contains("public void SetTarget(string targetNpcId)"),
            "PlayerAttackService must keep explicit target selection.");

        Assert.IsTrue(
            serviceText.Contains("PublishAssignmentsChanged();"),
            "Target selection must publish marker state for aiming stage.");

        Assert.IsTrue(
            serviceText.Contains("AssignWeaponSlotToTarget"),
            "PlayerAttackService must support assigning a specific weapon slot.");

        Assert.IsTrue(
            serviceText.Contains("ClearWeaponSlotTarget"),
            "PlayerAttackService must support clearing a specific weapon slot target.");

        Assert.IsTrue(
            serviceText.Contains("_weaponTargetNpcIdsBySlot[weaponSlotIndex] = targetNpcId"),
            "Specific weapon slot assignment must move that weapon to the selected enemy group.");

        Assert.IsTrue(
            serviceText.Contains("OnNpcDestroyed"),
            "Weapon assignments must be cleared when target enemy dies.");
    }

    [Test]
    public void WeaponListHud_ShowsGroupsAndRangeColors()
    {
        string hudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerWeaponListHud2A.cs");

        Assert.IsTrue(
            hudText.Contains("Без цели"),
            "Unassigned weapons must be grouped under Без цели.");

        Assert.IsTrue(
            hudText.IndexOf("foreach (var pair in groupedSlots)") <
            hudText.IndexOf("bool hasUntargetedWeapons"),
            "Без цели group must be appended after assigned enemy groups.");

        Assert.IsTrue(
            hudText.Contains("if (!hasUntargetedWeapons)"),
            "Без цели group must be hidden when every weapon already has a target.");

        Assert.IsTrue(
            hudText.Contains("Враг "),
            "Assigned weapons must be grouped under enemy display name.");

        Assert.IsTrue(
            hudText.Contains("#FFFFFF"),
            "Unassigned weapon rows must be white.");

        Assert.IsTrue(
            hudText.Contains("#61FF7A"),
            "Assigned weapons in range must be green.");

        Assert.IsTrue(
            hudText.Contains("#FF4A4A"),
            "Assigned weapons out of range must be red.");

        Assert.IsTrue(
            hudText.Contains("AssignWeaponSlotToTarget"),
            "Clicking an untargeted weapon row must assign that weapon to the selected enemy.");

        Assert.IsTrue(
            hudText.Contains("ClearWeaponSlotTarget"),
            "Weapon list HUD must support clearing a weapon target.");

        Assert.IsTrue(
            hudText.Contains("ResolveClickedLinkId"),
            "Weapon list clicks must resolve the clicked TMP link directly.");

        Assert.IsTrue(
            hudText.Contains("TMP_TextUtilities.FindIntersectingLink"),
            "Weapon row clicks must be detected by pressing the weapon text link.");

        Assert.IsTrue(
            hudText.Contains("<link=\\\"weapon:"),
            "Weapon rows must be wrapped into TMP links.");

        Assert.IsTrue(
            hudText.Contains("<link=\\\"untargeted\\\""),
            "Clicking Без цели must assign all untargeted weapons to the selected target.");

        Assert.IsTrue(
            hudText.Contains("<link=\\\"target:"),
            "Clicking an enemy weapon group header must clear all weapons from that target.");

        Assert.IsTrue(
            hudText.Contains("AssignAllUntargetedWeaponsToSelectedTarget"),
            "Weapon list HUD must support assigning every untargeted weapon at once.");

        Assert.IsTrue(
            hudText.Contains("ClearAllWeaponsFromTarget"),
            "Weapon list HUD must support clearing every weapon from a clicked enemy group.");

        Assert.IsFalse(
            hudText.Contains("\"W\" + (slotIndex + 1)"),
            "Weapon rows must not show W1, W2 and similar slot labels.");

        Assert.IsTrue(
            hudText.Contains("eventData.position"),
            "Weapon row clicks must use PointerEventData position for Input System compatibility.");

        Assert.IsTrue(
            hudText.Contains("ResolveWeaponDamage"),
            "Weapon rows must show expected damage in parentheses.");

        Assert.IsTrue(
            hudText.Contains("HasNearbyHostile"),
            "Weapon list HUD must hide/show by the same nearby hostile zone logic as the threat list.");

        Assert.IsTrue(
            hudText.Contains("weaponRangeMultiplier = 5f"),
            "Weapon list HUD must use 5x enemy shot distance for visibility.");

        Assert.IsTrue(
            hudText.Contains("ToggleCollapsed"),
            "Weapon list HUD header must collapse and expand the panel.");

        Assert.IsTrue(
            hudText.Contains("CountTargetedWeapons"),
            "Weapon list HUD header must show targeted/total weapon count.");

        Assert.IsTrue(
            hudText.Contains("\"ЦЕЛЬ \" + selectedName"),
            "Weapon list HUD header must show only selected enemy name while a target is selected.");

        Assert.IsFalse(
            hudText.Contains("string arrow = isCollapsed"),
            "Weapon list HUD header must not show plus or minus collapse symbols.");

        Assert.IsTrue(
            hudText.Contains("contentRoot.SetActive(!isCollapsed)"),
            "Weapon list HUD must hide content while collapsed.");

        Assert.IsFalse(
            hudText.Contains("new GameObject("),
            "Weapon list HUD must use authored prefab UI, not runtime-created UI.");

        Assert.IsFalse(
            hudText.Contains("Instantiate("),
            "Weapon list HUD must not instantiate rows at runtime.");
    }

    [Test]
    public void ThreatListAndNpcClick_SelectTargetForWeaponPanel()
    {
        string threatHudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            threatHudText.Contains("IPlayerAttackService"),
            "Threat list HUD must be able to select attack target.");

        Assert.IsTrue(
            threatHudText.Contains("_playerAttackService.SetTarget(threat.RuntimeNpcId)"),
            "Clicking threat row must select that enemy for weapon assignment.");

        string npcViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        Assert.IsTrue(
            npcViewText.Contains("_playerAttackService.SetTarget(runtimeNpcId)"),
            "Clicking an enemy ship must select that enemy for weapon assignment.");
    }

    [Test]
    public void WeaponTargetMarker_ShowsForSelectedTargetOrAssignedWeapons()
    {
        string markerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatWeaponTargetMarkerView2A.cs");

        Assert.IsTrue(
            markerText.Contains("_isSelectedForAiming"),
            "Target marker must show during aiming stage before a weapon is assigned.");

        Assert.IsTrue(
            markerText.Contains("_hasAnyAssignedWeapon || _isSelectedForAiming"),
            "Target marker must show when target is selected or has assigned weapons.");

        Assert.IsFalse(
            markerText.Contains("text += \"W\""),
            "Target marker must not show W1, W2 and similar weapon labels above the ship.");
    }

    [Test]
    public void SystemHudBottomPrefab_HasAuthoredWeaponListPanel()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/UI/Hud/PF_SystemHudBottomRoot.prefab");

        Assert.IsTrue(
            prefabText.Contains("SystemPlayerWeaponListHud2A"),
            "System bottom HUD prefab must contain weapon list HUD component.");

        Assert.IsTrue(
            prefabText.Contains("PlayerWeaponListPanel"),
            "System bottom HUD prefab must contain authored player weapon panel.");

        Assert.IsTrue(
            prefabText.Contains("m_SizeDelta: {x: 520, y: 180}"),
            "Weapon panel must be wide enough for weapon names and damage text.");

        Assert.IsTrue(
            prefabText.Contains("m_SizeDelta: {x: 460, y: 260}"),
            "Threat panel must be wide enough for enemy names and status text.");

        Assert.IsTrue(
            prefabText.Contains("PlayerWeaponListHeaderText"),
            "Weapon panel must contain authored header text.");

        Assert.IsTrue(
            prefabText.Contains("headerButton: {fileID: 9200205}"),
            "Weapon panel header must be wired as a clickable collapse button.");

        Assert.IsTrue(
            prefabText.Contains("PlayerWeaponListHeaderBackground"),
            "Weapon panel header must have a colored background like the threat panel header.");

        Assert.IsTrue(
            prefabText.Contains("m_text: ОРУЖИЕ 0/0"),
            "Weapon panel header prefab text must use weapon count format.");

        Assert.IsTrue(
            prefabText.Contains("PlayerWeaponRowsText"),
            "Weapon panel must contain authored clickable rows text.");
    }

    [Test]
    public void CameraActionButtons_StayAboveThreatPanel()
    {
        string sceneText =
            ReadProjectFile(
                "Assets/_Project/Scenes/SystemScene.unity");

        Assert.IsTrue(
            sceneText.Contains("m_Name: ActionButtonsBlock"),
            "System scene must keep camera action buttons in one block.");

        Assert.IsTrue(
            sceneText.Contains("m_Name: ReturnToShipButton"),
            "Camera action buttons block must include the return-to-ship button.");

        Assert.IsTrue(
            sceneText.Contains("m_Name: CenterOnTargetButton"),
            "Camera action buttons block must include the center-on-target button.");

        Assert.IsTrue(
            sceneText.Contains("SystemCameraActionButtonsThreatPanelOffset2A"),
            "Camera action buttons block must adjust above threat panel.");

        string offsetText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/StarSystem/Camera/SystemCameraActionButtonsThreatPanelOffset2A.cs");

        Assert.IsTrue(
            offsetText.Contains("PlayerThreatListPanel"),
            "Camera action buttons offset must track the right threat list panel.");

        Assert.IsTrue(
            offsetText.Contains("threatPanel.rect.height"),
            "Camera action buttons offset must depend on dynamic threat panel height.");

        Assert.IsTrue(
            offsetText.Contains("panelGap"),
            "Camera action buttons must keep a small gap above the panel.");
    }

    [Test]
    public void ClicksOutsideEnemy_ClearOnlyUnassignedSelectedTarget()
    {
        string serviceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            serviceText.Contains("ClearSelectedTargetIfNoAssignedWeapons"),
            "PlayerAttackService must clear temporary selected target only when no weapon is assigned to it.");

        string shipClickText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Input/ShipClickCancelMovement2A.cs");

        Assert.IsTrue(
            shipClickText.Contains("ClearSelectedTargetIfNoAssignedWeapons"),
            "Clicking the player ship must clear temporary selected combat target.");

        string planetClickText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Planet/PlanetSelectableView2.cs");

        Assert.IsTrue(
            planetClickText.Contains("ClearSelectedTargetIfNoAssignedWeapons"),
            "Clicking a planet must clear temporary selected combat target.");

        string mapClickText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Travel/SystemMapClickArea2.cs");

        Assert.IsTrue(
            mapClickText.Contains("ClearSelectedTargetIfNoAssignedWeapons"),
            "Clicking empty system map must clear temporary selected combat target.");
    }

    [Test]
    public void SelectingNpcTarget_BuildsTickLockedTravelRouteToNpc()
    {
        string travelInterfaceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Travel/ISystemTravelService.cs");

        Assert.IsTrue(
            travelInterfaceText.Contains("SetNpcDestination"),
            "System travel service must expose NPC destination selection.");

        string destinationTypeText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Data/Enums/Travel/TravelDestinationType.cs");

        Assert.IsTrue(
            destinationTypeText.Contains("Npc"),
            "Travel destination type must include NPC targets.");

        string travelServiceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Travel/SystemTravelService.cs");

        Assert.IsTrue(
            travelServiceText.Contains("Subscribe<GameTickStartedEvent>"),
            "NPC route must refresh from the temporal tick start event.");

        Assert.IsTrue(
            travelServiceText.Contains("Subscribe<GameTimeQuantumAdvancedEvent>"),
            "NPC route must refresh on the tick boundary after moving NPCs.");

        Assert.IsTrue(
            travelServiceText.Contains("RefreshNpcDestinationAtTickStart"),
            "NPC route must recalculate only at the beginning of a temporal tick.");

        Assert.IsTrue(
            travelServiceText.Contains("evt.CurrentDay + 1"),
            "NPC route must prepare the next tick route after the current quantum advances.");

        Assert.IsTrue(
            travelServiceText.Contains("BuildCurrentTravelPath"),
            "NPC route must use the existing system travel path algorithm.");

        Assert.IsTrue(
            travelServiceText.Contains("CalculateNextTravelPositionByTickLockedNpcPath"),
            "During a tick, player travel must follow the route locked for that tick.");

        Assert.IsTrue(
            travelServiceText.Contains("IsNpcDestination()"),
            "Arriving at an NPC must keep the route-follow destination active.");

        Assert.IsTrue(
            travelServiceText.Contains("_targetService?.ClearTarget()"),
            "Selecting an NPC destination must clear planet, map point and system exit target markers.");

        Assert.IsTrue(
            travelServiceText.Contains("TravelDestinationType.Planet ||") &&
            travelServiceText.Contains("TravelDestinationType.Npc))"),
            "NPC route preview must refresh live like planet route preview.");

        string playerAttackText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            playerAttackText.Contains("_travelService?.SetNpcDestination(targetNpcId)"),
            "Selecting an enemy target must also build a route to that NPC.");

        string npcViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        Assert.IsTrue(
            npcViewText.Contains("_systemTravelService?.SetNpcDestination(runtimeNpcId)"),
            "Clicking a non-hostile NPC must build a route to that NPC.");

        string markerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatWeaponTargetMarkerView2A.cs");

        Assert.IsTrue(
            markerText.Contains("npcTargetMarkerColor"),
            "NPC travel target marker must support blue color.");

        Assert.IsTrue(
            markerText.Contains("DestinationSelectedEvent"),
            "NPC travel target marker must react to NPC route selection.");

        Assert.IsTrue(
            markerText.Contains("_isSelectedForTravel"),
            "NPC travel target marker must show while NPC is selected for travel.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(
            File.Exists(projectPath),
            "Missing file: " + projectPath);

        return File.ReadAllText(projectPath);
    }
}
