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

        int playerAttackIndex = text.IndexOf(
            "public bool TryCreatePlayerProjectile",
            System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(
            playerAttackIndex,
            0,
            "SystemNpcCombatService must expose TryCreatePlayerProjectile for player attacks.");

        string playerAttackText = text.Substring(playerAttackIndex);

        Assert.IsTrue(
            playerAttackText.Contains("CombatShooterType.Player"),
            "Player projectile creation path must pass CombatShooterType.Player into the runtime combat factory.");

        Assert.IsTrue(
            playerAttackText.Contains("TryCreateSingleProjectile") ||
            playerAttackText.Contains("TryCreateTimedEnergyProjectiles") ||
            playerAttackText.Contains("TryCreateBeam") ||
            playerAttackText.Contains("TryCreateWave"),
            "Player attack must create runtime projectile/beam/wave through the shared combat runtime path.");

        Assert.IsTrue(
            text.Contains("ShooterType = shooterType"),
            "Runtime projectile state must copy the shooter type passed by the player/NPC creation path.");

        Assert.IsTrue(
            text.Contains("projectile.ShooterType == CombatShooterType.Player"),
            "Projectile publishing/damage logic must preserve player shooter identity.");
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