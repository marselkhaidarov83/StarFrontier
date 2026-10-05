using NUnit.Framework;
using System.IO;

public sealed class S04_03_ProjectileVisualLifecycleTests
{
    [Test]
    public void GalaxyNpcProjectileView_FollowsRuntimeProjectileWithoutDamageLogic()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Npc/GalaxyNpcProjectileView.cs");

        Assert.IsTrue(
            text.Contains("TryGetProjectile"),
            "GalaxyNpcProjectileView must follow runtime projectile state.");

        Assert.IsTrue(
            text.Contains("Complete()"),
            "GalaxyNpcProjectileView must expose cleanup method.");

        Assert.IsFalse(
            text.Contains("OnTriggerEnter2D"),
            "GalaxyNpcProjectileView must not deal damage through physics triggers.");

        Assert.IsFalse(
            text.Contains("ApplyDamage"),
            "GalaxyNpcProjectileView must not apply damage directly.");
    }

    [Test]
    public void GalaxyNpcCombatVisualController_UsesRuntimeProjectileEvents()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Npc/GalaxyNpcCombatVisualController.cs");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileCreatedEvent"),
            "Visual controller must subscribe to projectile created event.");

        Assert.IsTrue(
            text.Contains("GalaxyNpcProjectileImpactEvent"),
            "Visual controller must subscribe to projectile impact event.");

        Assert.IsTrue(
            text.Contains("OnProjectileCreated"),
            "Visual controller must spawn visuals on created event.");

        Assert.IsTrue(
            text.Contains("OnProjectileImpact"),
            "Visual controller must cleanup visuals on impact event.");
    }

    [Test]
    public void SystemNpcCombatService_PublishesProjectileLifecycleEvents()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcCombatService.cs");

        Assert.IsTrue(
            text.Contains("new GalaxyNpcProjectileCreatedEvent"),
            "Runtime combat service must publish projectile created event.");

        Assert.IsTrue(
            text.Contains("new GalaxyNpcProjectileImpactEvent"),
            "Runtime combat service must publish projectile impact event.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }
}