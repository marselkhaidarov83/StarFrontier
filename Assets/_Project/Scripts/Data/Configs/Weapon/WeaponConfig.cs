using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "StarFrontier/Configs/Combat/Weapon")]
[Serializable]
public class WeaponConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseDamage;
    [SerializeField] private float range;
    [SerializeField] private float cooldown;
    [SerializeField] private float projectileSpeed;
    [SerializeField] private int energyCost;
    [SerializeField] private float fireRate;
    [SerializeField] private float projectileLifetime;
    [SerializeField] private int level;

    [Header("Combat Behavior")]
    [SerializeField] private bool isHitscan;
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private WeaponDamageType damageType;
    [SerializeField] private WeaponTargetingMode targetingMode;

    [Header("Visuals")]
    [SerializeField] private GameObject projectilePrefabRef;

    [Header("Sprint 4 / Runtime Kind")]
    [SerializeField] private WeaponRuntimeKind2A runtimeKind2A =
        WeaponRuntimeKind2A.Pulse;

    [SerializeField] private WeaponOwnerProfile2A ownerProfile2A =
        WeaponOwnerProfile2A.PlayerAndRangers;

    [Header("Sprint 4 / Tick Damage")]
    [SerializeField] [Min(1)] private int damagePerCharge = 1;

    [SerializeField] [Min(1)] private int chargesPerTickMin = 1;
    [SerializeField] [Min(1)] private int chargesPerTickMax = 1;

    [SerializeField] [Min(1)] private int activeTicks = 1;

    [SerializeField] private bool resolvesWithinCurrentTick = true;

    [Header("Sprint 4 / Missile Ammo")]
    [SerializeField] private bool usesAmmo = false;
    [SerializeField] [Min(0)] private int maxAmmoCharges = 0;
    [SerializeField] private bool reloadOnlyOnPlanet = false;

    [Header("Sprint 4 / Missile Flight")]
    [SerializeField] [Min(0f)] private float projectileSpeedPerTick = 0f;
    [SerializeField] [Min(1)] private int projectileLifetimeTicks = 1;

    public int BaseDamage => baseDamage;
    public float Range => range;
    public float Cooldown => cooldown;
    public float ProjectileSpeed => projectileSpeed;
    public int EnergyCost => energyCost;
    public float FireRate => fireRate;
    public float ProjectileLifetime => projectileLifetime;
    public bool IsHitscan => isHitscan;
    public WeaponType WeaponType => weaponType;
    public WeaponDamageType DamageType => damageType;
    public WeaponTargetingMode TargetingMode => targetingMode;
    public GameObject ProjectilePrefabRef => projectilePrefabRef;

    public WeaponRuntimeKind2A RuntimeKind2A => runtimeKind2A;
    public WeaponOwnerProfile2A OwnerProfile2A => ownerProfile2A;

    public int DamagePerCharge => damagePerCharge;
    public int ChargesPerTickMin => chargesPerTickMin;
    public int ChargesPerTickMax => chargesPerTickMax;
    public int ActiveTicks => activeTicks;
    public bool ResolvesWithinCurrentTick => resolvesWithinCurrentTick;

    public bool UsesAmmo => usesAmmo;
    public int MaxAmmoCharges => maxAmmoCharges;
    public bool ReloadOnlyOnPlanet => reloadOnlyOnPlanet;

    public float ProjectileSpeedPerTick => projectileSpeedPerTick;
    public int ProjectileLifetimeTicks => projectileLifetimeTicks;

    public int MaxDamagePerTick =>
        damagePerCharge * chargesPerTickMax;

    public int GetClampedChargesPerTick(int requestedCharges)
    {
        return Mathf.Clamp(
            requestedCharges,
            chargesPerTickMin,
            chargesPerTickMax);
    }

    public bool IsPulse()
    {
        return runtimeKind2A == WeaponRuntimeKind2A.Pulse;
    }

    public bool IsLaser()
    {
        return runtimeKind2A == WeaponRuntimeKind2A.Laser;
    }

    public bool IsMissile()
    {
        return runtimeKind2A == WeaponRuntimeKind2A.Missile;
    }

    public bool CanSpendAmmo(
        int currentAmmoCharges,
        int requestedCharges)
    {
        if (!usesAmmo)
            return true;

        int clampedCharges =
            GetClampedChargesPerTick(requestedCharges);

        return currentAmmoCharges >= clampedCharges;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        damagePerCharge =
            Mathf.Max(1, damagePerCharge);

        activeTicks =
            Mathf.Max(1, activeTicks);

        projectileLifetimeTicks =
            Mathf.Max(1, projectileLifetimeTicks);

        switch (runtimeKind2A)
        {
            case WeaponRuntimeKind2A.Pulse:
                ValidatePulse();
                break;

            case WeaponRuntimeKind2A.Laser:
                ValidateLaser();
                break;

            case WeaponRuntimeKind2A.Missile:
                ValidateMissile();
                break;
        }
    }

    private void ValidatePulse()
    {
        chargesPerTickMin =
            Mathf.Clamp(chargesPerTickMin, 1, 10);

        chargesPerTickMax =
            Mathf.Clamp(chargesPerTickMax, chargesPerTickMin, 10);

        activeTicks = 1;
        resolvesWithinCurrentTick = true;

        usesAmmo = false;
        maxAmmoCharges = 0;
        reloadOnlyOnPlanet = false;

        projectileLifetimeTicks = 1;
    }

    private void ValidateLaser()
    {
        chargesPerTickMin = 1;
        chargesPerTickMax = 1;

        activeTicks = 1;
        resolvesWithinCurrentTick = true;

        usesAmmo = false;
        maxAmmoCharges = 0;
        reloadOnlyOnPlanet = false;

        projectileLifetimeTicks = 1;
    }

    private void ValidateMissile()
    {
        chargesPerTickMin =
            Mathf.Clamp(chargesPerTickMin, 1, 5);

        chargesPerTickMax =
            Mathf.Clamp(chargesPerTickMax, chargesPerTickMin, 5);

        activeTicks =
            Mathf.Max(1, projectileLifetimeTicks);

        resolvesWithinCurrentTick = false;

        usesAmmo = true;
        reloadOnlyOnPlanet = true;

        maxAmmoCharges =
            Mathf.Max(chargesPerTickMax, maxAmmoCharges);

        projectileSpeedPerTick =
            Mathf.Max(0.01f, projectileSpeedPerTick);
    }
#endif
}