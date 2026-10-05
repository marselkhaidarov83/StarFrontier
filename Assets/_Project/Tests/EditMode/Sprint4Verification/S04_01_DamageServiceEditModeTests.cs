using NUnit.Framework;

public sealed class S04_01_DamageServiceEditModeTests
{
    private IDamageService2A _damageService;

    [SetUp]
    public void SetUp()
    {
        _damageService = new DamageService2A();
    }

    [Test]
    public void Damage_FirstConsumesShield()
    {
        CombatDamageResult2A result =
            _damageService.ApplyDamage(
                10,
                20,
                6);

        Assert.AreEqual(
            4,
            result.CurrentShield);

        Assert.AreEqual(
            20,
            result.CurrentHull);

        Assert.AreEqual(
            6,
            result.AppliedDamage);

        Assert.AreEqual(
            6,
            result.ShieldDamage);

        Assert.AreEqual(
            0,
            result.HullDamage);

        Assert.IsFalse(
            result.IsDestroyed);
    }

    [Test]
    public void Damage_SpillsIntoHull()
    {
        CombatDamageResult2A result =
            _damageService.ApplyDamage(
                5,
                20,
                8);

        Assert.AreEqual(
            0,
            result.CurrentShield);

        Assert.AreEqual(
            17,
            result.CurrentHull);

        Assert.AreEqual(
            8,
            result.AppliedDamage);

        Assert.AreEqual(
            5,
            result.ShieldDamage);

        Assert.AreEqual(
            3,
            result.HullDamage);

        Assert.IsFalse(
            result.IsDestroyed);
    }

    [Test]
    public void Damage_DestroysHull()
    {
        CombatDamageResult2A result =
            _damageService.ApplyDamage(
                0,
                5,
                10);

        Assert.AreEqual(
            0,
            result.CurrentShield);

        Assert.AreEqual(
            0,
            result.CurrentHull);

        Assert.AreEqual(
            5,
            result.AppliedDamage);

        Assert.AreEqual(
            0,
            result.ShieldDamage);

        Assert.AreEqual(
            5,
            result.HullDamage);

        Assert.IsTrue(
            result.IsDestroyed);
    }

    [Test]
    public void Damage_IgnoresInvalidDamage()
    {
        CombatDamageResult2A result =
            _damageService.ApplyDamage(
                5,
                10,
                0);

        Assert.AreEqual(
            5,
            result.CurrentShield);

        Assert.AreEqual(
            10,
            result.CurrentHull);

        Assert.AreEqual(
            0,
            result.AppliedDamage);

        Assert.AreEqual(
            0,
            result.ShieldDamage);

        Assert.AreEqual(
            0,
            result.HullDamage);

        Assert.IsFalse(
            result.IsDestroyed);
    }
}