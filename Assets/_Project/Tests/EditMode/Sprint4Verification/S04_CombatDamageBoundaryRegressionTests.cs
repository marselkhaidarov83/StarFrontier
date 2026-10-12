using System;
using NUnit.Framework;

/// <summary>
/// Sprint 4: behavioral damage invariants and the five canonical shot types.
/// Does not replace the existing projectile/beam/wave/scene tests.
/// </summary>
public sealed class S04_CombatDamageBoundaryRegressionTests
{
    private readonly DamageService2A _service = new DamageService2A();

    [TestCase(0, 10, 0)]
    [TestCase(0, 10, 3)]
    [TestCase(4, 10, 2)]
    [TestCase(4, 10, 4)]
    [TestCase(4, 10, 5)]
    [TestCase(4, 10, 100)]
    [TestCase(999, 10, 100)]
    public void Damage_AlwaysConsumesShieldBeforeHull_AndConservesAppliedDamage(
        int shield, int hull, int damage)
    {
        CombatDamageResult2A result = _service.ApplyDamage(shield, hull, damage);
        int expectedShieldDamage = Math.Min(shield, damage);
        int expectedHullDamage = Math.Min(hull, damage - expectedShieldDamage);

        Assert.That(result.ShieldDamage, Is.EqualTo(expectedShieldDamage));
        Assert.That(result.HullDamage, Is.EqualTo(expectedHullDamage));
        Assert.That(result.AppliedDamage, Is.EqualTo(expectedShieldDamage + expectedHullDamage));
        Assert.That(result.CurrentShield, Is.EqualTo(shield - expectedShieldDamage));
        Assert.That(result.CurrentHull, Is.EqualTo(hull - expectedHullDamage));
        Assert.That(result.IsDestroyed, Is.EqualTo(result.CurrentHull == 0));
    }

    [TestCase(-1)]
    [TestCase(0)]
    public void NonPositiveDamage_CannotChangeAliveShip(int damage)
    {
        CombatDamageResult2A result = _service.ApplyDamage(6, 10, damage);

        Assert.That(result.CurrentShield, Is.EqualTo(6));
        Assert.That(result.CurrentHull, Is.EqualTo(10));
        Assert.That(result.AppliedDamage, Is.Zero);
        Assert.That(result.IsDestroyed, Is.False);
    }

    [Test]
    public void ShotTypeContract_ContainsOnlyFiveDistinctCanonicalValues()
    {
        Array types = Enum.GetValues(typeof(WeaponShotType2A));
        Assert.That(types.Length, Is.EqualTo(5));
        Assert.That((int)WeaponShotType2A.Beam, Is.EqualTo(0));
        Assert.That((int)WeaponShotType2A.RapidEnergyVolley, Is.EqualTo(1));
        Assert.That((int)WeaponShotType2A.MissileSwarm, Is.EqualTo(2));
        Assert.That((int)WeaponShotType2A.HeavyProjectile, Is.EqualTo(3));
        Assert.That((int)WeaponShotType2A.Wave, Is.EqualTo(4));
    }
}
