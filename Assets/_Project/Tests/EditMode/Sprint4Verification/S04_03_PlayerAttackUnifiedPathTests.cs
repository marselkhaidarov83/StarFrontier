using NUnit.Framework;
using System.IO;

public sealed class S04_03_PlayerAttackUnifiedPathTests
{
    [Test]
    public void SystemNpcView_SelectsPlayerAttackServiceTarget()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Npc/SystemNpcView.cs");

        Assert.IsTrue(
            text.Contains("IPlayerAttackService"),
            "SystemNpcView must use IPlayerAttackService.");

        Assert.IsTrue(
            text.Contains("_playerAttackService.SetTarget(runtimeNpcId)"),
            "Clicking an enemy NPC must select the target through PlayerAttackService.");
    }

    [Test]
    public void PlayerAttackService_CreatesRuntimeProjectileThroughNpcCombatService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Combat/PlayerAttackService.cs");

        Assert.IsTrue(
            text.Contains("ISystemNpcCombatService"),
            "PlayerAttackService must depend on ISystemNpcCombatService.");

        Assert.IsTrue(
            text.Contains("TryCreatePlayerProjectile"),
            "PlayerAttackService must create player projectiles through the runtime combat service.");
    }

    [Test]
    public void RuntimeCombatService_PlayerProjectileMarksShooterAsPlayer()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcCombatService.cs");

        Assert.IsTrue(
            text.Contains("ShooterType = CombatShooterType.Player"),
            "Player projectile runtime state must mark ShooterType as Player.");
    }

    [Test]
    public void ProductionScene_DoesNotDependOnWeaponFireControllerForPlayerDamage()
    {
        string sceneText = ReadProjectFile(
            "Assets/_Project/Scenes/SystemScene.unity");

        Assert.IsFalse(
            sceneText.Contains("WeaponFireController"),
            "SystemScene must not use WeaponFireController as the production player attack path.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }
}