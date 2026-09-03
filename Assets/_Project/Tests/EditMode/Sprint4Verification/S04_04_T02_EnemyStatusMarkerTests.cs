using NUnit.Framework;
using System.IO;

public sealed class S04_04_T02_EnemyStatusMarkerTests
{
    [Test]
    public void CombatEnemyStatusMarkerView_ReadsNpcStateAndDamageEvents()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatEnemyStatusMarkerView2A.cs");

        Assert.IsTrue(
            text.Contains("SystemNpcRuntimeState"),
            "Enemy status marker must read HP/shield from SystemNpcRuntimeState.");

        Assert.IsTrue(
            text.Contains("CurrentHull") && text.Contains("MaxHull"),
            "Enemy status marker must use current/max hull values.");

        Assert.IsTrue(
            text.Contains("CurrentShield") && text.Contains("MaxShield"),
            "Enemy status marker must use current/max shield values.");

        Assert.IsTrue(
            text.Contains("SystemNpcDamagedEvent"),
            "Enemy status marker must refresh after NPC damage events.");

        Assert.IsTrue(
            text.Contains("SystemNpcDestroyedEvent"),
            "Enemy status marker must hide after NPC destruction.");
    }

    [Test]
    public void CombatEnemyStatusMarkerView_UsesSystemShipFxAndShipSize()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatEnemyStatusMarkerView2A.cs");

        Assert.IsTrue(
            text.Contains("SystemShipFX"),
            "Enemy status marker renderers must use the SystemShipFX sorting layer.");

        Assert.IsTrue(
            text.Contains("WorldSize"),
            "Enemy status marker size must follow the NPC ship world size.");

        Assert.IsTrue(
            text.Contains("GetComponentsInChildren<SpriteRenderer>(true)"),
            "Enemy status marker must be able to resolve ship renderer bounds as fallback.");
    }

    [Test]
    public void NpcPrefab_HasEnemyStatusMarkerBinding()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/Npc/NpcPrefab_01.prefab");

        Assert.IsTrue(
            prefabText.Contains("CombatEnemyStatusMarkerView2A"),
            "NpcPrefab_01 must have CombatEnemyStatusMarkerView2A component.");

        Assert.IsTrue(
            prefabText.Contains("CombatEnemyStatusMarker"),
            "NpcPrefab_01 must contain the enemy status marker root.");

        Assert.IsTrue(
            prefabText.Contains("StatusBarBackground"),
            "NpcPrefab_01 must contain a status marker background bar.");

        Assert.IsTrue(
            prefabText.Contains("ShieldFill"),
            "NpcPrefab_01 must contain a shield fill bar.");

        Assert.IsTrue(
            prefabText.Contains("HullFill"),
            "NpcPrefab_01 must contain a hull fill bar.");

        Assert.IsTrue(
            prefabText.Contains("backgroundRenderer: {fileID:") &&
            prefabText.Contains("shieldFillRenderer: {fileID:") &&
            prefabText.Contains("hullFillRenderer: {fileID:"),
            "CombatEnemyStatusMarkerView2A must bind all prefab-authored SpriteRenderers.");

        Assert.IsTrue(
            prefabText.Contains("m_SortingLayerID: 1258504597"),
            "Enemy status marker SpriteRenderers must use SystemShipFX sorting layer.");
    }

    [Test]
    public void EnemyStatusMarkerPixelSprite_IsImported()
    {
        Assert.IsTrue(
            File.Exists("Assets/Art/Marker/combat_enemy_status_bar_pixel_01.png"),
            "Enemy status marker pixel sprite is missing.");

        Assert.IsTrue(
            File.Exists("Assets/Art/Marker/combat_enemy_status_bar_pixel_01.png.meta"),
            "Enemy status marker pixel sprite meta file is missing.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(
            File.Exists(projectPath),
            "Missing file: " + projectPath);

        return File.ReadAllText(projectPath);
    }
}
