using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_WeaponConfigModelTests
{
    private const string WeaponConfigFolder =
        "Assets/_Project/Content/Configs/Weapons";

    [Test]
    public void WeaponEquipmentTier_HasExpectedCanonicalValues()
    {
        Assert.AreEqual(0, (int)WeaponEquipmentTier.Base);
        Assert.AreEqual(1, (int)WeaponEquipmentTier.Bronze);
        Assert.AreEqual(2, (int)WeaponEquipmentTier.Silver);
        Assert.AreEqual(3, (int)WeaponEquipmentTier.Gold);
        Assert.AreEqual(4, (int)WeaponEquipmentTier.Epic);
    }

    [Test]
    public void WeaponType_HasExpectedCanonicalValues()
    {
        Assert.AreEqual(0, (int)WeaponType.Laser);
        Assert.AreEqual(1, (int)WeaponType.Pulse);
        Assert.AreEqual(2, (int)WeaponType.Plasma);
        Assert.AreEqual(3, (int)WeaponType.Railgun);
        Assert.AreEqual(4, (int)WeaponType.Missile);
        Assert.AreEqual(5, (int)WeaponType.Disruptor);
        Assert.AreEqual(6, (int)WeaponType.Beam);
        Assert.AreEqual(7, (int)WeaponType.Lance);
        Assert.AreEqual(8, (int)WeaponType.Orb);
        Assert.AreEqual(9, (int)WeaponType.Singularity);
        Assert.AreEqual(10, (int)WeaponType.Spore);
        Assert.AreEqual(11, (int)WeaponType.Acid);
        Assert.AreEqual(12, (int)WeaponType.Swarm);
        Assert.AreEqual(13, (int)WeaponType.Tendril);
        Assert.AreEqual(14, (int)WeaponType.Burst);
    }

    [Test]
    public void WeaponDamageType_HasExpectedCanonicalValues()
    {
        Assert.AreEqual(0, (int)WeaponDamageType.Energy);
        Assert.AreEqual(1, (int)WeaponDamageType.Kinetic);
        Assert.AreEqual(2, (int)WeaponDamageType.Explosive);
        Assert.AreEqual(3, (int)WeaponDamageType.Corrosive);
        Assert.AreEqual(4, (int)WeaponDamageType.Biological);
        Assert.AreEqual(5, (int)WeaponDamageType.Gravity);
    }

    [Test]
    public void WeaponConfigs_ExistInWeaponConfigFolder()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:WeaponConfig",
                new[] { WeaponConfigFolder });

        Assert.IsNotNull(guids);
        Assert.Greater(
            guids.Length,
            0,
            "No WeaponConfig assets found in " + WeaponConfigFolder);
    }

    [Test]
    public void WeaponConfigs_HaveValidMinMaxModel()
    {
        WeaponConfig[] configs = LoadAllWeaponConfigs();

        Assert.Greater(
            configs.Length,
            0,
            "No WeaponConfig assets found in " + WeaponConfigFolder);

        for (int i = 0; i < configs.Length; i++)
        {
            WeaponConfig config = configs[i];

            Assert.NotNull(config);

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(config.Id),
                "WeaponConfig must have Id.");

            Assert.Greater(
                config.Level,
                0,
                config.Id + " Level must be > 0.");

            Assert.Greater(
                config.CargoSize,
                0,
                config.Id + " CargoSize must be > 0.");

            Assert.Greater(
                config.BaseDamageMin,
                0,
                config.Id + " BaseDamageMin must be > 0.");

            Assert.GreaterOrEqual(
                config.BaseDamageMax,
                config.BaseDamageMin,
                config.Id + " BaseDamageMax must be >= BaseDamageMin.");

            Assert.Greater(
                config.RangeMin,
                0f,
                config.Id + " RangeMin must be > 0.");

            Assert.GreaterOrEqual(
                config.RangeMax,
                config.RangeMin,
                config.Id + " RangeMax must be >= RangeMin.");

            Assert.GreaterOrEqual(
                config.EnergyCostMin,
                0,
                config.Id + " EnergyCostMin must be >= 0.");

            Assert.GreaterOrEqual(
                config.EnergyCostMax,
                config.EnergyCostMin,
                config.Id + " EnergyCostMax must be >= EnergyCostMin.");

            Assert.GreaterOrEqual(
                config.ProjectileLifetimeMin,
                1,
                config.Id + " ProjectileLifetimeMin must be >= 1.");

            Assert.GreaterOrEqual(
                config.ProjectileLifetimeMax,
                config.ProjectileLifetimeMin,
                config.Id + " ProjectileLifetimeMax must be >= ProjectileLifetimeMin.");

            AssertAmmoModel(config);
        }
    }

    [Test]
    public void WeaponConfigs_RollRuntimeStatsInsideConfiguredRanges()
    {
        WeaponConfig[] configs = LoadAllWeaponConfigs();

        Assert.Greater(
            configs.Length,
            0,
            "No WeaponConfig assets found in " + WeaponConfigFolder);

        for (int i = 0; i < configs.Length; i++)
        {
            WeaponConfig config = configs[i];

            Assert.NotNull(config);

            WeaponRuntimeStats stats =
                config.RollRuntimeStats(1000 + i);

            Assert.AreEqual(
                config.Id,
                stats.WeaponConfigId,
                config.Id + " rolled stats must keep config id.");

            Assert.AreEqual(
                config.Level,
                stats.Level,
                config.Id + " rolled Level must match config.");

            Assert.AreEqual(
                config.EquipmentTier,
                stats.EquipmentTier,
                config.Id + " rolled EquipmentTier must match config.");

            Assert.AreEqual(
                config.CargoSize,
                stats.CargoSize,
                config.Id + " rolled CargoSize must match config.");

            Assert.GreaterOrEqual(
                stats.Damage,
                config.BaseDamageMin,
                config.Id + " rolled damage must be >= BaseDamageMin.");

            Assert.LessOrEqual(
                stats.Damage,
                config.BaseDamageMax,
                config.Id + " rolled damage must be <= BaseDamageMax.");

            Assert.GreaterOrEqual(
                stats.Range,
                config.RangeMin,
                config.Id + " rolled range must be >= RangeMin.");

            Assert.LessOrEqual(
                stats.Range,
                config.RangeMax + 0.0001f,
                config.Id + " rolled range must be <= RangeMax.");

            Assert.GreaterOrEqual(
                stats.EnergyCost,
                config.EnergyCostMin,
                config.Id + " rolled energy cost must be >= EnergyCostMin.");

            Assert.LessOrEqual(
                stats.EnergyCost,
                config.EnergyCostMax,
                config.Id + " rolled energy cost must be <= EnergyCostMax.");

            Assert.GreaterOrEqual(
                stats.ProjectileLifetime,
                config.ProjectileLifetimeMin,
                config.Id + " rolled projectile lifetime must be >= ProjectileLifetimeMin.");

            Assert.LessOrEqual(
                stats.ProjectileLifetime,
                config.ProjectileLifetimeMax,
                config.Id + " rolled projectile lifetime must be <= ProjectileLifetimeMax.");

            Assert.AreEqual(
                config.IsHitscan,
                stats.IsHitscan,
                config.Id + " rolled IsHitscan must match config.");

            Assert.AreEqual(
                config.WeaponType,
                stats.WeaponType,
                config.Id + " rolled WeaponType must match config.");

            Assert.AreEqual(
                config.DamageType,
                stats.DamageType,
                config.Id + " rolled DamageType must match config.");

            Assert.AreEqual(
                config.TargetingMode,
                stats.TargetingMode,
                config.Id + " rolled TargetingMode must match config.");

            Assert.AreEqual(
                config.UsesAmmo,
                stats.UsesAmmo,
                config.Id + " rolled UsesAmmo must match config.");

            if (config.UsesAmmo)
            {
                Assert.GreaterOrEqual(
                    stats.MaxAmmoCharges,
                    config.MaxAmmoChargesMin,
                    config.Id + " rolled ammo must be >= MaxAmmoChargesMin.");

                Assert.LessOrEqual(
                    stats.MaxAmmoCharges,
                    config.MaxAmmoChargesMax,
                    config.Id + " rolled ammo must be <= MaxAmmoChargesMax.");
            }
            else
            {
                Assert.AreEqual(
                    0,
                    stats.MaxAmmoCharges,
                    config.Id + " rolled ammo must be 0 when UsesAmmo is false.");
            }
        }
    }

    [Test]
    public void MissileWeapons_UseAmmo()
    {
        WeaponConfig[] configs = LoadAllWeaponConfigs();

        for (int i = 0; i < configs.Length; i++)
        {
            WeaponConfig config = configs[i];

            if (config == null)
                continue;

            if (config.WeaponType != WeaponType.Missile)
                continue;

            Assert.IsTrue(
                config.UsesAmmo,
                config.Id + " missile weapon must use ammo.");

            Assert.Greater(
                config.MaxAmmoChargesMin,
                0,
                config.Id + " missile MaxAmmoChargesMin must be > 0.");

            Assert.GreaterOrEqual(
                config.MaxAmmoChargesMax,
                config.MaxAmmoChargesMin,
                config.Id + " missile MaxAmmoChargesMax must be >= MaxAmmoChargesMin.");
        }
    }

    [Test]
    public void NonAmmoWeapons_HaveZeroAmmoRange()
    {
        WeaponConfig[] configs = LoadAllWeaponConfigs();

        for (int i = 0; i < configs.Length; i++)
        {
            WeaponConfig config = configs[i];

            if (config == null)
                continue;

            if (config.UsesAmmo)
                continue;

            Assert.AreEqual(
                0,
                config.MaxAmmoChargesMin,
                config.Id + " MaxAmmoChargesMin must be 0 when UsesAmmo is false.");

            Assert.AreEqual(
                0,
                config.MaxAmmoChargesMax,
                config.Id + " MaxAmmoChargesMax must be 0 when UsesAmmo is false.");
        }
    }

    private static void AssertAmmoModel(WeaponConfig config)
    {
        if (config.UsesAmmo)
        {
            Assert.Greater(
                config.MaxAmmoChargesMin,
                0,
                config.Id + " MaxAmmoChargesMin must be > 0 when UsesAmmo is true.");

            Assert.GreaterOrEqual(
                config.MaxAmmoChargesMax,
                config.MaxAmmoChargesMin,
                config.Id + " MaxAmmoChargesMax must be >= MaxAmmoChargesMin when UsesAmmo is true.");

            return;
        }

        Assert.AreEqual(
            0,
            config.MaxAmmoChargesMin,
            config.Id + " MaxAmmoChargesMin must be 0 when UsesAmmo is false.");

        Assert.AreEqual(
            0,
            config.MaxAmmoChargesMax,
            config.Id + " MaxAmmoChargesMax must be 0 when UsesAmmo is false.");
    }

    private static WeaponConfig[] LoadAllWeaponConfigs()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:WeaponConfig",
                new[] { WeaponConfigFolder });

        var configs = new WeaponConfig[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            configs[i] =
                AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
        }

        return configs;
    }
}