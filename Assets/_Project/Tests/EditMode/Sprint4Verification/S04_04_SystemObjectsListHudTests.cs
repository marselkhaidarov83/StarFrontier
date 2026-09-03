using NUnit.Framework;
using System.IO;

public sealed class S04_04_SystemObjectsListHudTests
{
    [Test]
    public void TopHudPrefab_HasSystemObjectsPanelAndClickRelays()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/Hud/PF_HudTopRoot.prefab");

        Assert.IsTrue(
            prefabText.Contains("SystemObjectsListHud2A"),
            "Top HUD prefab must have system objects list HUD binder.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: SystemObjectsListPanel"),
            "Top HUD prefab must contain authored system objects panel.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: HeaderText"),
            "System objects panel must contain authored header text.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: HeaderBackground"),
            "System objects panel must contain red header background.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: RowsText"),
            "System objects panel must contain authored rows text.");

        Assert.IsTrue(
            prefabText.Contains("rowsClickRelay: {fileID: 9300024}"),
            "System objects panel rows must be clickable through pointer relay.");

        Assert.IsTrue(
            prefabText.Contains("systemTitleClickRelay: {fileID: 9300030}"),
            "System title must open the system objects panel through pointer relay.");

        Assert.IsTrue(
            prefabText.Contains("m_AnchoredPosition: {x: 20, y: -210}"),
            "System objects panel must sit below the top HUD.");

        Assert.IsTrue(
            prefabText.Contains("m_Color: {r: 0.32, g: 0.04, b: 0.035, a: 0.96}"),
            "System objects panel header must use the same red background as lower panels.");

        Assert.IsTrue(
            prefabText.Contains("maxPanelHeight: 680"),
            "System objects panel must be tall enough to show planets, station and exits.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: HeaderText") &&
            prefabText.Contains("m_fontStyle: 0"),
            "System objects panel header must use regular font style.");
    }

    [Test]
    public void SystemObjectsListHud_BuildsGroupsAndRoutesThroughExistingTravelEvents()
    {
        string hudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemObjectsListHud2A.cs");

        Assert.IsTrue(
            hudText.Contains("AppendGroup(\"Планеты\")"),
            "System objects panel must render planets group.");

        Assert.IsTrue(
            hudText.Contains("AppendGroup(\"Станция\")"),
            "System objects panel must render station group.");

        Assert.IsTrue(
            hudText.Contains("AppendGroup(\"Точки выхода\")"),
            "System objects panel must render system exits group.");

        Assert.IsTrue(
            hudText.Contains("new PlanetSelectedEvent(planet)"),
            "Planet row click must use existing planet selection event.");

        Assert.IsTrue(
            hudText.Contains("new StationSelectedEvent(station)"),
            "Station row click must use existing station selection event.");

        Assert.IsTrue(
            hudText.Contains("new RouteExitMapChangedEvent"),
            "Exit row click must use existing route exit event.");

        Assert.IsTrue(
            hudText.Contains("TrySelectTarget") &&
            hudText.Contains("SystemGameplayTargetType.Planet") &&
            hudText.Contains("SystemGameplayTargetType.Station") &&
            hudText.Contains("SystemGameplayTargetType.TravelPoint"),
            "Rows must also select gameplay target marker for planet, station and exit.");

        Assert.IsTrue(
            hudText.Contains("GetAllStarSystems()"),
            "Exit rows must be collected like the system map collects routes.");

        Assert.IsFalse(
            hudText.Contains("LinkedSystems"),
            "Exit rows must use StarSystemConfig routes, not linked systems.");

        Assert.IsTrue(
            hudText.Contains("TMP_TextUtilities.FindIntersectingLink"),
            "Rows must be clickable by TMP link text.");

        Assert.IsTrue(
            hudText.Contains("ToUpperInvariant"),
            "System objects panel header must show system title in uppercase.");

        Assert.IsFalse(
            hudText.Contains("Current StarSystemConfig.Routes count"),
            "System objects panel must not keep temporary route debug logs.");
    }

    [Test]
    public void SystemObjectsListHud_IsLimitedToSystemScene()
    {
        string hudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemObjectsListHud2A.cs");

        Assert.IsTrue(
            hudText.Contains("UnityEngine.SceneManagement"),
            "System objects panel must know the active scene.");

        Assert.IsTrue(
            hudText.Contains(".name == \"SystemScene\""),
            "System objects panel must not open on GalaxyScene.");
    }

    [Test]
    public void SunPrefab_CanOpenSystemObjectsPanel()
    {
        string sunScriptText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Planet/SunNodeView.cs");

        string sunSelectableText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Planet/SunSelectableView2A.cs");

        string sunPrefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/Sun/PF_SystemMapSun_Pseudo3D.prefab");

        Assert.IsTrue(
            sunSelectableText.Contains("IPointerClickHandler"),
            "Sun selectable view must receive pointer clicks like planet selectable view.");

        Assert.IsTrue(
            sunScriptText.Contains("SystemObjectsPanelRequestedEvent2A"),
            "Sun click must request system objects panel.");

        Assert.IsTrue(
            sunScriptText.Contains("_onClick.Invoke"),
            "Sun click must use callback passed by SystemMapController2.");

        Assert.IsTrue(
            sunPrefabText.Contains("SunSelectableView2A"),
            "Sun image must receive clicks through selectable view like planets.");

        Assert.IsTrue(
            sunScriptText.Contains("OpenSystemObjectsPanelFromSun"),
            "Sun selectable view must open system objects panel through SunNodeView.");

        Assert.IsTrue(
            sunSelectableText.Contains("OpenSystemObjectsPanelFromSun"),
            "Sun selectable view must delegate click to SunNodeView.");

        Assert.IsFalse(
            sunScriptText.Contains("OnMouseUpAsButton") ||
            sunScriptText.Contains("ScreenToWorldPoint"),
            "Sun selection must not be opened through manual mouse or point/camera conversion.");

        Assert.IsTrue(
            sunPrefabText.Contains("CircleCollider2D"),
            "Sun prefab must have a collider so pointer clicks can reach SunNodeView.");
    }

    [Test]
    public void SystemMapController_PassesPanelOpenCallbackToSun()
    {
        string controllerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/StarSystem/SystemMapController2.cs");

        Assert.IsTrue(
            controllerText.Contains("OnSunClicked"),
            "System map controller must have sun click callback.");

        Assert.IsTrue(
            controllerText.Contains("SystemObjectsPanelRequestedEvent2A"),
            "Sun click callback must request system objects panel.");

        Assert.IsTrue(
            controllerText.Contains("starSystem.Sun,") &&
            controllerText.Contains("OnSunClicked,"),
            "System map controller must pass panel callback into SunNodeView.Initialize.");
    }

    [Test]
    public void SceneObjectClicks_RequestSystemObjectsPanelClose()
    {
        string[] clickScripts =
        {
            "Assets/_Project/Scripts/Presentation/Planet/PlanetSelectableView2.cs",
            "Assets/_Project/Scripts/Presentation/Station/StationSelectableView2A.cs",
            "Assets/_Project/Scripts/Presentation/SystemExit/SystemExitNodeView2A.cs",
            "Assets/_Project/Scripts/Presentation/Travel/SystemMapClickArea2.cs",
            "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs",
            "Assets/_Project/Scripts/Presentation/Combat/EnemySystemMapEntity.cs",
            "Assets/_Project/Scripts/Presentation/Combat/Player/PlayerSystemMapShipView.cs"
        };

        for (int i = 0; i < clickScripts.Length; i++)
        {
            string scriptText =
                ReadProjectFile(clickScripts[i]);

            Assert.IsTrue(
                scriptText.Contains("SystemObjectsPanelCloseRequestedEvent2A"),
                "Scene object click must close system objects panel: " +
                clickScripts[i]);
        }
    }

    [Test]
    public void TopHudPrefab_HasSelectedTargetInfoPanel()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/Hud/PF_HudTopRoot.prefab");

        Assert.IsTrue(
            prefabText.Contains("SystemSelectedTargetInfoHud2A"),
            "Top HUD prefab must have selected enemy or NPC info HUD binder.");

        Assert.IsTrue(
            prefabText.Contains("m_Name: SelectedTargetInfoPanel"),
            "Top HUD prefab must contain authored selected target info panel.");

        Assert.IsTrue(
            prefabText.Contains("panelRoot: {fileID: 9310001}") &&
            prefabText.Contains("headerText: {fileID: 9310013}") &&
            prefabText.Contains("rowsText: {fileID: 9310023}"),
            "Selected target info HUD must be wired to authored panel text objects.");

        Assert.IsTrue(
            prefabText.Contains("m_AnchoredPosition: {x: 20, y: -210}"),
            "Selected target info panel top-left corner must match system objects panel.");

        Assert.IsTrue(
            prefabText.Contains("m_text: \"\\u0426\\u0435\\u043B\\u044C\""),
            "Selected target info panel must have authored header text.");

        Assert.IsTrue(
            prefabText.Contains("maxPanelHeight: 680"),
            "Selected target info panel must be tall enough to show target weapons.");
    }

    [Test]
    public void SelectedTargetInfoHud_ShowsStatsAndWeapons()
    {
        string hudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemSelectedTargetInfoHud2A.cs");

        Assert.IsTrue(
            hudText.Contains("SystemSelectedTargetInfoPanelRequestedEvent2A"),
            "Selected target info HUD must open by target info request event.");

        Assert.IsTrue(
            hudText.Contains("SystemObjectsPanelCloseRequestedEvent2A") &&
            hudText.Contains("SystemObjectsPanelRequestedEvent2A"),
            "Selected target info HUD must hide when other system panels or scene clicks happen.");

        Assert.IsTrue(
            hudText.Contains("AppendStatLine(\"Здоровье\"") &&
            hudText.Contains("AppendStatLine(\"Щит\""),
            "Selected target info HUD must render health and shield rows.");

        Assert.IsTrue(
            hudText.Contains("AppendGroup(\"Оружие\")"),
            "Selected target info HUD must render weapon subgroup.");

        Assert.IsTrue(
            hudText.Contains("CountGroupSpacerRows"),
            "Selected target info HUD height must include spacer rows before weapon group.");

        Assert.IsTrue(
            hudText.Contains("WeaponConfigId") &&
            hudText.Contains("ShotDistance") &&
            hudText.Contains("BaseAttackDamage") &&
            hudText.Contains("AttackRange"),
            "Selected target info HUD must support NPC weapons and legacy enemy attack stats.");

        Assert.IsTrue(
            hudText.Contains("ToUpperInvariant"),
            "Selected target info HUD header must render selected enemy or NPC name in uppercase.");
    }

    [Test]
    public void EnemyAndNpcClicks_OpenSelectedTargetInfoPanel()
    {
        string npcViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        string enemyViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Combat/EnemySystemMapEntity.cs");

        string threatHudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemPlayerThreatListHud2A.cs");

        Assert.IsTrue(
            npcViewText.Contains("SystemSelectedTargetInfoPanelRequestedEvent2A"),
            "NPC click must open selected target info panel.");

        Assert.IsTrue(
            enemyViewText.Contains("SystemSelectedTargetInfoPanelRequestedEvent2A"),
            "Legacy enemy click must open selected target info panel.");

        Assert.IsTrue(
            threatHudText.Contains("SystemSelectedTargetInfoPanelRequestedEvent2A"),
            "Threat list row click must open selected target info panel.");

        Assert.IsFalse(
            enemyViewText.Contains("Pointer click received"),
            "Temporary enemy click debug log must remain disabled.");
    }

    private static string ReadProjectFile(string path)
    {
        Assert.IsTrue(
            File.Exists(path),
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}
