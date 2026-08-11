using NUnit.Framework;
using System.IO;

public sealed class S04_03_NpcAttackUnifiedPathTests
{
    [Test]
    public void SystemNpcCombatService_IsProductionNpcAttackPath()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcCombatService.cs");

        Assert.IsTrue(
            text.Contains("TryCreateProjectile"),
            "SystemNpcCombatService must create NPC runtime projectiles.");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileRuntimeState"),
            "NPC attack must create GalaxyNpcProjectileRuntimeState.");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileCreatedEvent"),
            "NPC attack must publish projectile created event.");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileImpactEvent"),
            "NPC attack must publish projectile impact event.");
    }

    [Test]
    public void LegacyEnemyWeaponController_FileDoesNotExist()
    {
        Assert.IsFalse(
            File.Exists("Assets/_Project/Scripts/Presentation/Combat/SystemEncounter/EnemyWeaponController.cs"),
            "Legacy EnemyWeaponController.cs must be removed from production code.");
    }

    [Test]
    public void GalaxyNpcCombatVisualController_UsesRuntimeProjectileEvents()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Npc/GalaxyNpcCombatVisualController.cs");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileCreatedEvent"),
            "Visual controller must listen to runtime projectile creation.");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileImpactEvent"),
            "Visual controller must listen to runtime projectile impact.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }
}