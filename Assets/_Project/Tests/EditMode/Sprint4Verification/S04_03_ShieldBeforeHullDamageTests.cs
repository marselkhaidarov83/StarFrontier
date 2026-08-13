using NUnit.Framework;
using System.IO;

public sealed class S04_03_ShieldBeforeHullDamageTests
{
    [Test]
    public void DamageService_ConsumesShieldBeforeHull()
    {
        IDamageService2A damageService = new DamageService2A();

        CombatDamageResult2A result = damageService.ApplyDamage(
            currentShield: 10,
            currentHull: 20,
            damage: 6);

        Assert.AreEqual(4, result.CurrentShield);
        Assert.AreEqual(20, result.CurrentHull);
        Assert.AreEqual(6, result.ShieldDamage);
        Assert.AreEqual(0, result.HullDamage);
        Assert.AreEqual(6, result.AppliedDamage);
        Assert.IsFalse(result.IsDestroyed);
    }

    [Test]
    public void DamageService_SpillsRemainingDamageIntoHull()
    {
        IDamageService2A damageService = new DamageService2A();

        CombatDamageResult2A result = damageService.ApplyDamage(
            currentShield: 5,
            currentHull: 20,
            damage: 8);

        Assert.AreEqual(0, result.CurrentShield);
        Assert.AreEqual(17, result.CurrentHull);
        Assert.AreEqual(5, result.ShieldDamage);
        Assert.AreEqual(3, result.HullDamage);
        Assert.AreEqual(8, result.AppliedDamage);
        Assert.IsFalse(result.IsDestroyed);
    }

    [Test]
    public void DamageService_DestroysHullOnlyAfterHullReachesZero()
    {
        IDamageService2A damageService = new DamageService2A();

        CombatDamageResult2A result = damageService.ApplyDamage(
            currentShield: 3,
            currentHull: 5,
            damage: 20);

        Assert.AreEqual(0, result.CurrentShield);
        Assert.AreEqual(0, result.CurrentHull);
        Assert.AreEqual(3, result.ShieldDamage);
        Assert.AreEqual(5, result.HullDamage);
        Assert.AreEqual(8, result.AppliedDamage);
        Assert.IsTrue(result.IsDestroyed);
    }

    [Test]
    public void PlayerCombatEntity_UsesSharedDamageService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Combat/Player/PlayerCombatEntity.cs");

        Assert.IsTrue(
            text.Contains("IDamageService2A"),
            "PlayerCombatEntity must use shared IDamageService2A.");

        Assert.IsTrue(
            text.Contains("_damageService.ApplyDamage"),
            "PlayerCombatEntity must apply damage through shared damage service.");
    }

    [Test]
    public void SystemAllyService_UsesSharedDamageService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Combat/SystemAllyService.cs");

        Assert.IsTrue(
            text.Contains("IDamageService2A"),
            "SystemAllyService must use shared IDamageService2A.");

        Assert.IsTrue(
            text.Contains("_damageService.ApplyDamage"),
            "SystemAllyService must apply damage through shared damage service.");
    }

    [Test]
    public void SystemNpcRuntimeService_UsesSharedDamageService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        Assert.IsTrue(
            text.Contains("IDamageService2A"),
            "SystemNpcRuntimeService must use shared IDamageService2A.");

        Assert.IsTrue(
            text.Contains("_damageService.ApplyDamage"),
            "SystemNpcRuntimeService must apply damage through shared damage service.");
    }

    [Test]
    public void LegacySystemEnemyService_UsesSharedDamageService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Enemy/SystemEnemyService.cs");

        Assert.IsTrue(
            text.Contains("IDamageService2A"),
            "SystemEnemyService must use shared IDamageService2A.");

        Assert.IsTrue(
            text.Contains("_damageService.ApplyDamage"),
            "SystemEnemyService must apply damage through shared damage service.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }
}