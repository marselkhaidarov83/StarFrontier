using NUnit.Framework;
using System.IO;

public sealed class S04_04_T03_PlayerThreatListHudTests
{
    [Test]
    public void PlayerThreatListHud_UsesHudBindingContract()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            text.Contains("ISystemHudWidgetBinder2A"),
            "Threat list HUD must be bound through SystemHudCompositionRoot2A.");

        Assert.IsTrue(
            text.Contains("Bind(SystemHudBindingContext2A context)"),
            "Threat list HUD must receive services through HUD binding context.");

        Assert.IsTrue(
            text.Contains("Unbind()"),
            "Threat list HUD must clean up subscriptions.");
    }

    [Test]
    public void PlayerThreatListHud_UsesNpcStatePlayerTargetAndWeaponRange()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            text.Contains("ISystemNpcRuntimeService"),
            "Threat list HUD must read NPC runtime state.");

        Assert.IsTrue(
            text.Contains("IPlayerCombatTargetService"),
            "Threat list HUD must compare NPCs against player position.");

        Assert.IsTrue(
            text.Contains("weaponRangeMultiplier = 5f"),
            "Threat list HUD must use 5x enemy weapon range as visibility radius.");

        Assert.IsTrue(
            text.Contains("ShotDistance"),
            "Threat list HUD must use enemy weapon shot distance.");

        Assert.IsTrue(
            text.Contains("fallbackShotDistance"),
            "Threat list HUD must keep working when restored NPC weapon range is missing.");

        Assert.IsTrue(
            text.Contains("CurrentTargetRuntimeNpcId"),
            "Threat list HUD must know whether an enemy is targeting another NPC.");

        Assert.IsTrue(
            text.Contains("IsHostileInThreatZone"),
            "Threat list HUD must separate panel visibility range from row inclusion.");

        Assert.IsTrue(
            text.Contains("hasNearbyHostile = true"),
            "Threat list HUD panel must become visible when a hostile enemy is within warning range.");

        Assert.IsTrue(
            text.Contains("_threats.Add(npc)"),
            "Threat list rows must include every hostile enemy within warning range.");

        Assert.IsTrue(
            text.Contains("IsNpcAimingAtPlayer(npc)"),
            "Threat list HUD must still detect enemies aiming at the player.");
    }

    [Test]
    public void PlayerThreatListHud_CanBeCollapsedByHeader()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            text.Contains("headerButton"),
            "Threat list HUD must have a clickable header.");

        Assert.IsTrue(
            text.Contains("ToggleCollapsed"),
            "Threat list HUD must support collapsing by header click.");

        Assert.IsTrue(
            text.Contains("contentRoot.SetActive(!isCollapsed)"),
            "Threat list HUD must hide content while collapsed.");

        Assert.IsFalse(
            text.Contains("new GameObject("),
            "Threat list HUD must not create the panel UI at runtime.");

        Assert.IsFalse(
            text.Contains("Instantiate("),
            "Threat list HUD must not create list rows at runtime.");
    }

    [Test]
    public void PlayerThreatListHud_RestoresEnemyNamePoolDisplayName()
    {
        string factoryText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeFactory.cs");

        Assert.IsTrue(
            factoryText.Contains("DisplayName = config.PickRuntimeDisplayName(runtimeNpcId)"),
            "New enemies must pick runtime display names from EnemyNamePoolConfig through EnemyConfig.");

        string saveServiceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcSimulationSaveService.cs");

        Assert.IsTrue(
            saveServiceText.Contains("RestoreRuntimeDisplayName"),
            "Restored enemies must rebuild display names for the threat list.");

        Assert.IsTrue(
            saveServiceText.Contains("config.PickRuntimeDisplayName(npc.RuntimeNpcId)"),
            "Restored enemy names must come from the name pool with the runtime NPC id as stable seed.");
    }

    [Test]
    public void RestoredNpcWeapons_KeepShotDistanceForThreatHudRange()
    {
        string saveDataText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Data/Save/Npc/SystemNpcWeaponSaveData.cs");

        Assert.IsTrue(
            saveDataText.Contains("public float ShotDistance"),
            "NPC weapon saves must store runtime shot distance.");

        string saveServiceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcSimulationSaveService.cs");

        Assert.IsTrue(
            saveServiceText.Contains("ShotDistance = weapon.ShotDistance"),
            "NPC simulation save must capture weapon shot distance.");

        Assert.IsTrue(
            saveServiceText.Contains("ResolveRestoredWeaponShotDistance"),
            "NPC simulation restore must rebuild missing shot distance for old saves.");

        Assert.IsTrue(
            saveServiceText.Contains("return weaponConfig.RangeMax"),
            "Old saves without shot distance must use weapon config range for threat HUD visibility.");
    }

    [Test]
    public void PlayerThreatListHud_ShowsEnemyNameWithoutDistanceNumbers()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            text.Contains("displayName = threat.DisplayName.Trim();"),
            "Threat rows must show the enemy display name.");

        Assert.IsTrue(
            text.Contains("#FFFFFF"),
            "Threat rows must show nearby enemies in white.");

        Assert.IsTrue(
            text.Contains("#FF4A4A"),
            "Threat rows must show enemies aiming at the player in red.");

        Assert.IsTrue(
            text.Contains("BuildEnemyStatusText"),
            "Threat rows must show enemy hull/shield in parentheses.");

        Assert.IsFalse(
            text.Contains("Mathf.CeilToInt(distance)"),
            "Threat rows must not show distance numbers like 49/60.");
    }

    [Test]
    public void PlayerThreatListHud_UsesRussianTitleDynamicHeightAndClickableRows()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            text.Contains("УГРОЗЫ"),
            "Threat list HUD header must use Russian title text.");

        Assert.IsTrue(
            text.Contains("RefreshPanelHeight"),
            "Threat list HUD must resize the panel when row count changes.");

        Assert.IsTrue(
            text.Contains("rowCount * Mathf.Max(1f, rowHeight)"),
            "Threat list HUD height must depend on the number of visible threat rows.");

        Assert.IsTrue(
            text.Contains("rowsButton"),
            "Threat list rows must have a clickable authored UI button.");

        Assert.IsTrue(
            text.Contains("OnThreatRowsClicked"),
            "Threat list HUD must route row clicks to a handler hook.");
    }

    [Test]
    public void SystemHudBottomPrefab_HasThreatListComponentAndAuthoredUi()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/UI/Hud/PF_SystemHudBottomRoot.prefab");

        Assert.IsTrue(
            prefabText.Contains("SystemPlayerThreatListHud2A"),
            "System HUD bottom prefab must have SystemPlayerThreatListHud2A component.");

        Assert.IsTrue(
            prefabText.Contains("PlayerThreatListPanel"),
            "System HUD bottom prefab must contain an authored threat list panel object.");

        Assert.IsTrue(
            prefabText.Contains("ThreatListHeaderButton"),
            "Threat list panel must contain an authored header button.");

        Assert.IsTrue(
            prefabText.Contains("ThreatListContent"),
            "Threat list panel must contain an authored content root.");

        Assert.IsTrue(
            prefabText.Contains("ThreatRowTemplate"),
            "Threat list panel must contain an authored row template.");

        Assert.IsTrue(
            prefabText.Contains("panelRoot: {fileID: 9100001}"),
            "Threat list HUD component must bind the authored panel root.");

        Assert.IsTrue(
            prefabText.Contains("threatRowsText: {fileID: 9100044}"),
            "Threat list HUD component must bind the authored rows text.");

        Assert.IsTrue(
            prefabText.Contains("rowsButton: {fileID: 9100045}"),
            "Threat list HUD component must bind the authored clickable rows button.");

        Assert.IsTrue(
            prefabText.Contains("m_text: УГРОЗЫ - 0"),
            "Threat list HUD prefab must show Russian title text in the authored header.");

        Assert.IsTrue(
            prefabText.Contains("m_fontSize: 26"),
            "Threat list row font must be large enough to read in play mode.");

        Assert.IsTrue(
            prefabText.Contains("weaponRangeMultiplier: 5"),
            "Threat list HUD prefab binding must use 5x enemy weapon range.");

        Assert.IsTrue(
            prefabText.Contains("fallbackShotDistance: 12"),
            "Threat list HUD prefab binding must define fallback enemy shot distance.");

        Assert.IsTrue(
            prefabText.Contains("logThreatDebug: 0"),
            "Threat list HUD debug logging must be disabled by default but left available in the component.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(
            File.Exists(projectPath),
            "Missing file: " + projectPath);

        return File.ReadAllText(projectPath);
    }
}
