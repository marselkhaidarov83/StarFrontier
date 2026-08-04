using NUnit.Framework;
using UnityEditor;

public sealed class S04_02_WeaponConfigModelTests
{
    private static readonly string[] RequiredWeaponConfigPaths =
    {
        "Assets/_Project/Content/Configs/Weapons/weapon_common_laser_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_common_missile_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_common_pulse_L1_01.asset",

        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_ai_laser_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_ai_missile_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_ai_pulse_L1_01.asset",

        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_ancients_laser_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_ancients_missile_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_ancients_pulse_L1_01.asset",

        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_infected_laser_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_infected_missile_L1_01.asset",
        "Assets/_Project/Content/Configs/Weapons/weapon_enemy_infected_pulse_L1_01.asset"
    };

    [Test]
    public void WeaponRuntimeKind_HasThreeCanonicalKinds()
    {
        Assert.AreEqual(
            0,
            (int)WeaponRuntimeKind2A.Pulse);

        Assert.AreEqual(
            1,
            (int)WeaponRuntimeKind2A.Laser);

        Assert.AreEqual(
            2,
            (int)WeaponRuntimeKind2A.Missile);
    }

    [Test]
    public void RequiredWeaponConfigs_Exist()
    {
        for (int i = 0; i < RequiredWeaponConfigPaths.Length; i++)
        {
            WeaponConfig config =
                AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                    RequiredWeaponConfigPaths[i]);

            Assert.NotNull(
                config,
                "Missing WeaponConfig: " + RequiredWeaponConfigPaths[i]);
        }
    }

    [Test]
    public void RequiredWeaponConfigs_HaveValidSprint4Model()
    {
        for (int i = 0; i < RequiredWeaponConfigPaths.Length; i++)
        {
            WeaponConfig config =
                AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                    RequiredWeaponConfigPaths[i]);

            Assert.NotNull(
                config,
                "Missing WeaponConfig: " + RequiredWeaponConfigPaths[i]);

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(config.Id),
                RequiredWeaponConfigPaths[i] + " must have Id.");

            Assert.GreaterOrEqual(
                config.DamagePerCharge,
                1,
                config.Id + " must have positive damage per charge.");

            Assert.GreaterOrEqual(
                config.ChargesPerTickMin,
                1,
                config.Id + " must have at least one charge.");

            Assert.GreaterOrEqual(
                config.ChargesPerTickMax,
                config.ChargesPerTickMin,
                config.Id + " max charges must be >= min charges.");

            if (config.RuntimeKind2A == WeaponRuntimeKind2A.Pulse)
                AssertPulseWeapon(config);

            if (config.RuntimeKind2A == WeaponRuntimeKind2A.Laser)
                AssertLaserWeapon(config);

            if (config.RuntimeKind2A == WeaponRuntimeKind2A.Missile)
                AssertMissileWeapon(config);
        }
    }

    [Test]
    public void RequiredWeaponConfigs_HaveVisualPrefab()
    {
        for (int i = 0; i < RequiredWeaponConfigPaths.Length; i++)
        {
            WeaponConfig config =
                AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                    RequiredWeaponConfigPaths[i]);

            Assert.NotNull(
                config,
                "Missing WeaponConfig: " + RequiredWeaponConfigPaths[i]);

            Assert.NotNull(
                config.ProjectilePrefabRef,
                config.Id + " must have ProjectilePrefabRef assigned.");
        }
    }

    private static void AssertPulseWeapon(
        WeaponConfig config)
    {
        Assert.LessOrEqual(
            config.ChargesPerTickMax,
            10,
            config.Id + " pulse weapon must have max 10 charges.");

        Assert.AreEqual(
            1,
            config.ActiveTicks,
            config.Id + " pulse weapon must be active for 1 tick.");

        Assert.IsTrue(
            config.ResolvesWithinCurrentTick,
            config.Id + " pulse weapon must resolve within current tick.");

        Assert.IsFalse(
            config.UsesAmmo,
            config.Id + " pulse weapon must not use ammo.");
    }

    private static void AssertLaserWeapon(
        WeaponConfig config)
    {
        Assert.AreEqual(
            1,
            config.ChargesPerTickMin,
            config.Id + " laser min charges must be 1.");

        Assert.AreEqual(
            1,
            config.ChargesPerTickMax,
            config.Id + " laser max charges must be 1.");

        Assert.AreEqual(
            1,
            config.ActiveTicks,
            config.Id + " laser must be active for 1 tick.");

        Assert.IsTrue(
            config.ResolvesWithinCurrentTick,
            config.Id + " laser must resolve within current tick.");

        Assert.IsFalse(
            config.UsesAmmo,
            config.Id + " laser weapon must not use ammo.");
    }

    private static void AssertMissileWeapon(
        WeaponConfig config)
    {
        Assert.LessOrEqual(
            config.ChargesPerTickMax,
            5,
            config.Id + " missile weapon must have max 5 missiles.");

        Assert.IsFalse(
            config.ResolvesWithinCurrentTick,
            config.Id + " missile weapon must not resolve within current tick.");

        Assert.IsTrue(
            config.UsesAmmo,
            config.Id + " missile weapon must use ammo.");

        Assert.IsTrue(
            config.ReloadOnlyOnPlanet,
            config.Id + " missile weapon must reload only on planet.");

        Assert.GreaterOrEqual(
            config.MaxAmmoCharges,
            config.ChargesPerTickMax,
            config.Id + " missile ammo must cover at least one full volley.");

        Assert.GreaterOrEqual(
            config.ProjectileLifetimeTicks,
            1,
            config.Id + " missile lifetime must be at least 1 tick.");

        Assert.Greater(
            config.ProjectileSpeedPerTick,
            0f,
            config.Id + " missile speed must be positive.");
    }
}