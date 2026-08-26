using NUnit.Framework;
using System.IO;

public sealed class S04_04_T01_WeaponTargetMarkerTests
{
    [Test]
    public void PlayerAttackService_SupportsWeaponSlotAssignments()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            text.Contains("WeaponTargetNpcIdsBySlot"),
            "PlayerAttackService must expose weapon target assignments by slot.");

        Assert.IsTrue(
            text.Contains("AssignNextWeaponSlotToTarget"),
            "PlayerAttackService must assign weapon slots to clicked targets.");

        Assert.IsTrue(
            text.Contains("TryCreatePlayerProjectile("),
            "PlayerAttackService must still fire through the runtime combat service.");
    }

    [Test]
    public void CombatWeaponTargetAssignmentsChangedEvent_Exists()
    {
        Assert.IsTrue(
            File.Exists(
                "Assets/_Project/Scripts/Data/Events/Combat/CombatWeaponTargetAssignmentsChangedEvent2A.cs"),
            "CombatWeaponTargetAssignmentsChangedEvent2A file is missing.");
    }

    [Test]
    public void CombatWeaponTargetMarkerView_Exists()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/CombatWeaponTargetMarkerView2A.cs");

        Assert.IsFalse(
            text.Contains("AddComponent<TextMeshPro>"),
            "Combat marker text must be a prefab binding, not a runtime-created object.");

        Assert.IsTrue(
            text.Contains("SystemShipFX"),
            "Combat marker text must use the SystemShipFX sorting layer.");

        Assert.IsTrue(
            text.Contains("WorldSize"),
            "Combat marker size must depend on the NPC ship world size.");
    }

    [Test]
    public void SystemNpcView_ExposesRuntimeIdAndWorldSize()
    {
        string text =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        Assert.IsTrue(
            text.Contains("public string RuntimeNpcId => runtimeNpcId;"),
            "SystemNpcView must expose RuntimeNpcId for combat marker binding.");

        Assert.IsTrue(
            text.Contains("public float WorldSize"),
            "SystemNpcView must expose WorldSize from config binding.");

        Assert.IsTrue(
            text.Contains("WorldSize = worldSize;"),
            "SystemNpcView must store the configured world size during Bind.");
    }

    [Test]
    public void NpcPrefab_HasCombatWeaponTargetMarkerBinding()
    {
        string prefabText =
            ReadProjectFile(
                "Assets/_Project/Prefabs/UI/Npc/NpcPrefab_01.prefab");

        Assert.IsTrue(
            prefabText.Contains("CombatWeaponTargetMarkerView2A"),
            "NpcPrefab_01 must have CombatWeaponTargetMarkerView2A component.");

        Assert.IsTrue(
            prefabText.Contains("WeaponAssignmentText"),
            "NpcPrefab_01 must contain a prefab-authored weapon assignment text object.");

        Assert.IsTrue(
            prefabText.Contains("weaponAssignmentsText: {fileID:"),
            "CombatWeaponTargetMarkerView2A must bind the prefab-authored TMP text.");

        Assert.IsTrue(
            prefabText.Contains("m_SortingLayerID: 1258504597"),
            "WeaponAssignmentText MeshRenderer must use SystemShipFX sorting layer.");
    }

    [Test]
    public void CombatWeaponTargetMarkerSprite_IsImported()
    {
        Assert.IsTrue(
            File.Exists(
                "Assets/Art/Marker/combat_weapon_target_marker_enemy_01.png"),
            "Combat weapon target marker sprite is missing.");

        Assert.IsTrue(
            File.Exists(
                "Assets/Art/Marker/combat_weapon_target_marker_enemy_01.png.meta"),
            "Combat weapon target marker sprite meta file is missing.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(
            File.Exists(projectPath),
            "Missing file: " + projectPath);

        return File.ReadAllText(projectPath);
    }
}
