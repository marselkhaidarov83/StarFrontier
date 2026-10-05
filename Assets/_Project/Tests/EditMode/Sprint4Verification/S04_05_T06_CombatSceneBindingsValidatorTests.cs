using NUnit.Framework;
using System.IO;

public sealed class S04_05_T06_CombatSceneBindingsValidatorTests
{
    [Test]
    public void ValidatorScript_ExistsAndExposesUnityMenu()
    {
        string validatorText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Editor/Combat/CombatSceneBindingsValidator2A.cs");

        Assert.IsTrue(
            validatorText.Contains("STAR FRONTIER/Combat/Validate Combat Scene Bindings"),
            "Validator must be available from the Unity menu.");

        Assert.IsTrue(
            validatorText.Contains("[MenuItem(MenuPath)]"),
            "Validator must expose a MenuItem entry.");

        Assert.IsTrue(
            validatorText.Contains("ValidateProjectBindings"),
            "Validator must expose a reusable validation method.");
    }

    [Test]
    public void ValidatorScript_ChecksRequiredCombatAssets()
    {
        string validatorText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Editor/Combat/CombatSceneBindingsValidator2A.cs");

        Assert.IsTrue(
            validatorText.Contains("Assets/_Project/Scenes/SystemScene.unity"),
            "Validator must check SystemScene.");

        Assert.IsTrue(
            validatorText.Contains("Assets/_Project/Scenes/CombatScene.unity"),
            "Validator must check CombatScene.");

        Assert.IsTrue(
            validatorText.Contains("Assets/_Project/Prefabs/UI/Npc/NpcPrefab_01.prefab"),
            "Validator must check NPC prefab.");

        Assert.IsTrue(
            validatorText.Contains("Assets/_Project/Prefabs/UI/Npc/NpcProjectilePrefab_01.prefab"),
            "Validator must check projectile prefab.");
    }

    [Test]
    public void ValidatorScript_ChecksCombatBindingComponents()
    {
        string validatorText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Editor/Combat/CombatSceneBindingsValidator2A.cs");

        Assert.IsTrue(
            validatorText.Contains("SystemHudCompositionRoot2A"),
            "Validator must check System HUD composition root.");

        Assert.IsTrue(
            validatorText.Contains("SystemEncounterRuntimeRoot"),
            "Validator must check combat runtime roots.");

        Assert.IsTrue(
            validatorText.Contains("SystemNpcCombatVisualController"),
            "Validator must check combat visual controller.");

        Assert.IsTrue(
            validatorText.Contains("CombatWeaponTargetMarkerView2A"),
            "Validator must check weapon target marker.");

        Assert.IsTrue(
            validatorText.Contains("CombatEnemyStatusMarkerView2A"),
            "Validator must check enemy status marker.");

        Assert.IsTrue(
            validatorText.Contains("CombatMarkerVisibilityScaler2A"),
            "Validator must check marker visibility scaler.");

        Assert.IsTrue(
            validatorText.Contains("SystemNpcProjectileVisual"),
            "Validator must check projectile visual prefab.");
    }

    private static string ReadProjectFile(
        string path)
    {
        Assert.IsTrue(
            File.Exists(path),
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}
