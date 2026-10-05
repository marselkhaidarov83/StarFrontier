using NUnit.Framework;
using System.IO;

public sealed class S05_04_NpcIdentificationAndStatusPanelTests
{
    [Test]
    public void SystemNpcViewBinder_UsesShipSpritesAsNpcIdentity()
    {
        string binderText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcViewBinder.cs");

        Assert.IsTrue(
            binderText.Contains("private Sprite ResolveSprite(SystemNpcRuntimeState npc)"),
            "NPC view binding must resolve ship sprite from NPC state/config.");

        Assert.IsTrue(
            binderText.Contains("GetAllyConfigById(npc.ConfigId)") &&
            binderText.Contains("return allyConfig.MapSprite"),
            "Ally NPC identity must be shown through AllyConfig.MapSprite.");

        Assert.IsTrue(
            binderText.Contains("GetEnemyConfigById(npc.ConfigId)") &&
            binderText.Contains("return enemyConfig.CombatSprite"),
            "Enemy NPC identity must be shown through EnemyConfig.CombatSprite.");

        Assert.IsTrue(
            binderText.Contains("GetPirateConfigById(npc.ConfigId)") &&
            binderText.Contains("return pirateConfig.CombatSprite"),
            "Pirate NPC identity must be shown through PirateConfig.CombatSprite.");

        Assert.IsTrue(
            binderText.Contains("ResolveWorldSize(npc)") &&
            binderText.Contains("view.Bind("),
            "NPC sprite binding must also pass production visual size into the view.");
    }

    [Test]
    public void SystemNpcView_ClickOpensShipStatusPanelForAllLiveNpcTypes()
    {
        string npcViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        Assert.IsTrue(
            npcViewText.Contains("IPointerClickHandler"),
            "NPC view must receive player click/tap input.");

        Assert.IsTrue(
            npcViewText.Contains("SystemObjectsPanelCloseRequestedEvent2A"),
            "NPC click must close the system objects panel before showing ship status.");

        Assert.IsTrue(
            npcViewText.Contains("SystemSelectedTargetInfoPanelRequestedEvent2A"),
            "NPC click must open the ship status panel in the System Scene canvas.");

        Assert.IsTrue(
            npcViewText.Contains("npc.IsHostileToPlayer") &&
            npcViewText.Contains("SystemGameplayTargetType.Enemy") &&
            npcViewText.Contains("SystemGameplayTargetType.Ally"),
            "NPC click must classify hostile NPCs as Enemy targets and civilian/allied NPCs as Ally targets.");

        Assert.IsTrue(
            npcViewText.Contains("!npc.IsAlive") &&
            npcViewText.Contains("return;"),
            "Dead NPCs must not open a stale status panel.");

        Assert.IsTrue(
            npcViewText.Contains("_playerAttackService.SetTarget(runtimeNpcId)"),
            "Enemy and pirate click must keep combat target selection behavior.");

        Assert.IsTrue(
            npcViewText.Contains("_systemTravelService?.SetNpcDestination(runtimeNpcId)"),
            "Non-hostile NPC click must keep NPC destination/follow behavior.");
    }

    [Test]
    public void ShipStatusPanel_RendersStatusWithoutDangerMarkerRequirement()
    {
        string hudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemSelectedTargetInfoHud2A.cs");

        Assert.IsTrue(
            hudText.Contains("SystemSelectedTargetInfoPanelRequestedEvent2A"),
            "Ship status panel must open through the selected target info request event.");

        Assert.IsTrue(
            hudText.Contains("RefreshNpc(SystemNpcRuntimeState npc)") &&
            hudText.Contains("RefreshEnemy(SystemEnemyRuntimeState enemy)"),
            "Ship status panel must support both new NPC runtime targets and legacy enemy targets.");

        Assert.IsTrue(
            hudText.Contains("AppendStatLine(\"Здоровье\"") &&
            hudText.Contains("AppendStatLine(\"Щит\""),
            "Ship status panel must render visible ship status rows.");

        Assert.IsTrue(
            hudText.Contains("AppendNpcWeapons(npc)") &&
            hudText.Contains("AppendLegacyEnemyWeapon(enemy)"),
            "Ship status panel must render weapon status for NPC and legacy enemy paths.");

        Assert.IsFalse(
            hudText.Contains("DangerTier") ||
            hudText.Contains("Danger Tier") ||
            hudText.Contains("Опасность"),
            "Danger tier must not be required as a separate visible status or marker in 2A-S05-04.");
    }

    [Test]
    public void ShipStatusPanel_CleansUpOnLifecycleAndPanelSwitchEvents()
    {
        string hudText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/HUD/SystemSelectedTargetInfoHud2A.cs");

        Assert.IsTrue(
            hudText.Contains("SystemSelectedTargetInfoPanelCloseRequestedEvent2A"),
            "Ship status panel must support explicit close events.");

        Assert.IsTrue(
            hudText.Contains("SystemObjectsPanelCloseRequestedEvent2A") &&
            hudText.Contains("SystemObjectsPanelRequestedEvent2A"),
            "Ship status panel must close when another System Scene panel is requested or closed.");

        Assert.IsTrue(
            hudText.Contains("SystemNpcDestroyedEvent") &&
            hudText.Contains("OnNpcDestroyed"),
            "Ship status panel must subscribe to NPC destroyed events.");

        Assert.IsTrue(
            hudText.Contains("SystemEnemyDestroyedEvent") &&
            hudText.Contains("OnEnemyDestroyed"),
            "Ship status panel must subscribe to legacy enemy destroyed events.");

        Assert.IsTrue(
            hudText.Contains("HidePanel();") &&
            hudText.Contains("_currentTargetId = string.Empty"),
            "Ship status panel cleanup must clear current target state and hide the panel.");
    }

    [Test]
    public void NpcIdentificationScope_DoesNotRequirePermanentRoleFactionDangerMarkers()
    {
        string npcViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        string binderText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcViewBinder.cs");

        Assert.IsFalse(
            npcViewText.Contains("roleText") ||
            npcViewText.Contains("factionText") ||
            npcViewText.Contains("dangerText"),
            "NPC view must not require permanent role/faction/danger text labels.");

        Assert.IsFalse(
            binderText.Contains("roleMarker") ||
            binderText.Contains("factionMarker") ||
            binderText.Contains("dangerMarker"),
            "NPC view binder must not require permanent role/faction/danger marker objects.");
    }

    private static string ReadProjectFile(string path)
    {
        Assert.IsTrue(
            File.Exists(path),
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}