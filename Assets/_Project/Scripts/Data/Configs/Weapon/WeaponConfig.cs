using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "StarFrontier/Configs/Combat/Weapon")]
[Serializable]
public class WeaponConfig : BaseConfig
{
    [Header("Progression")]
    [SerializeField][Min(1)] private int level = 1;
    [SerializeField] private WeaponEquipmentTier equipmentTier = WeaponEquipmentTier.Base;

    [Header("Cargo")]
    [SerializeField][Min(1)] private int cargoSize = 1;

    [Header("Base Stats / Randomized Runtime Ranges")]
    [SerializeField][Min(1)] private int baseDamageMin = 1;
    [SerializeField][Min(1)] private int baseDamageMax = 1;

    [SerializeField][Min(0f)] private float rangeMin = 1f;
    [SerializeField][Min(0f)] private float rangeMax = 1f;

    [SerializeField][Min(0)] private int energyCostMin = 0;
    [SerializeField][Min(0)] private int energyCostMax = 0;

    [SerializeField][Min(1)] private int projectileLifetimeMin = 1;
    [SerializeField][Min(1)] private int projectileLifetimeMax = 1;

    [Header("Combat Behavior")]
    [SerializeField] private bool isHitscan;
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private WeaponDamageType damageType;
    [SerializeField] private WeaponTargetingMode targetingMode = WeaponTargetingMode.SelectedTarget;
    [SerializeField] private bool autoDetectShotType = true;
    [SerializeField] private WeaponShotType2A shotType = WeaponShotType2A.Beam;
    [SerializeField][Min(1)][InspectorName("Кол-во выстрелов")] private int shotCount = 1;

    [Header("Visuals")]
    [SerializeField] private GameObject projectilePrefabRef;

    [Header("Ammo")]
    [SerializeField] private bool usesAmmo = false;
    [SerializeField][Min(0)] private int maxAmmoChargesMin = 0;
    [SerializeField][Min(0)] private int maxAmmoChargesMax = 0;

    public int Level => level;
    public WeaponEquipmentTier EquipmentTier => equipmentTier;

    public int CargoSize => cargoSize;

    public int BaseDamageMin => baseDamageMin;
    public int BaseDamageMax => baseDamageMax;

    public float RangeMin => rangeMin;
    public float RangeMax => rangeMax;

    public int EnergyCostMin => energyCostMin;
    public int EnergyCostMax => energyCostMax;

    public int ProjectileLifetimeMin => projectileLifetimeMin;
    public int ProjectileLifetimeMax => projectileLifetimeMax;

    public bool IsHitscan => isHitscan;
    public WeaponType WeaponType => weaponType;
    public WeaponDamageType DamageType => damageType;
    public WeaponTargetingMode TargetingMode => targetingMode;

    public GameObject ProjectilePrefabRef => projectilePrefabRef;

    public bool UsesAmmo => usesAmmo;
    public int MaxAmmoChargesMin => maxAmmoChargesMin;
    public int MaxAmmoChargesMax => maxAmmoChargesMax;

    public bool AutoDetectShotType => autoDetectShotType;
    public WeaponShotType2A ShotType => autoDetectShotType ? ResolveShotType() : shotType;
    public int ShotCount => shotCount;

    public WeaponRuntimeStats RollRuntimeStats()
    {
        return RollRuntimeStats(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
    }

    public WeaponRuntimeStats RollRuntimeStats(int seed)
    {
        System.Random random = new System.Random(seed);
        return RollRuntimeStats(random);
    }

    public WeaponRuntimeStats RollRuntimeStats(System.Random random)
    {
        if (random == null)
            random = new System.Random();

        int damage = RollIntInclusive(random, baseDamageMin, baseDamageMax);
        float range = RollFloat(random, rangeMin, rangeMax);
        int energyCost = RollIntInclusive(random, energyCostMin, energyCostMax);
        int projectileLifetime = RollIntInclusive(random, projectileLifetimeMin, projectileLifetimeMax);

        int maxAmmoCharges = usesAmmo
            ? RollIntInclusive(random, maxAmmoChargesMin, maxAmmoChargesMax)
            : 0;

        return new WeaponRuntimeStats(
            Id,
            level,
            equipmentTier,
            cargoSize,
            damage,
            range,
            energyCost,
            projectileLifetime,
            isHitscan,
            weaponType,
            ShotType,
            damageType,
            targetingMode,
            shotCount,
            usesAmmo,
            maxAmmoCharges
        );
    }

    public bool IsLaser()
    {
        return weaponType == WeaponType.Laser ||
               weaponType == WeaponType.Beam;
    }

    public bool IsMissile()
    {
        return weaponType == WeaponType.Missile;
    }

    public bool IsPulseLike()
    {
        return weaponType == WeaponType.Pulse ||
               weaponType == WeaponType.Disruptor ||
               weaponType == WeaponType.Spore ||
               weaponType == WeaponType.Swarm ||
               weaponType == WeaponType.Burst;
    }

    public bool IsProjectileLike()
    {
        return !isHitscan;
    }

    private static int RollIntInclusive(System.Random random, int min, int max)
    {
        if (max < min)
            max = min;

        return random.Next(min, max + 1);
    }

    private static float RollFloat(System.Random random, float min, float max)
    {
        if (max < min)
            max = min;

        double value01 = random.NextDouble();
        return min + (float)value01 * (max - min);
    }

    private WeaponShotType2A ResolveShotType()
    {
        string normalizedId = string.IsNullOrWhiteSpace(Id)
            ? string.Empty
            : Id.ToLowerInvariant();

        if (weaponType == WeaponType.Laser ||
            weaponType == WeaponType.Beam ||
            normalizedId.Contains("_laser_") ||
            normalizedId.Contains("_lance_") ||
            normalizedId.Contains("_tendril_"))
        {
            return WeaponShotType2A.Beam;
        }

        if (weaponType == WeaponType.Missile ||
            normalizedId.Contains("_missile_") ||
            normalizedId.Contains("_swarm_missile_") ||
            normalizedId.Contains("_orb_") ||
            normalizedId.Contains("_swarm_"))
        {
            return WeaponShotType2A.MissileSwarm;
        }

        if (weaponType == WeaponType.Plasma ||
            normalizedId.Contains("_plasma_") ||
            normalizedId.Contains("_core_bolt_") ||
            normalizedId.Contains("_acid_"))
        {
            return WeaponShotType2A.HeavyProjectile;
        }

        if (weaponType == WeaponType.Disruptor ||
            weaponType == WeaponType.Singularity ||
            weaponType == WeaponType.Spore ||
            normalizedId.Contains("_wave_cannon_") ||
            normalizedId.Contains("_disruptor_") ||
            normalizedId.Contains("_singularity_") ||
            normalizedId.Contains("_spore_"))
        {
            return WeaponShotType2A.Wave;
        }

        return WeaponShotType2A.RapidEnergyVolley;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        cargoSize = Mathf.Max(1, cargoSize);

        baseDamageMin = Mathf.Max(1, baseDamageMin);
        baseDamageMax = Mathf.Max(baseDamageMin, baseDamageMax);

        rangeMin = Mathf.Max(0f, rangeMin);
        rangeMax = Mathf.Max(rangeMin, rangeMax);

        energyCostMin = Mathf.Max(0, energyCostMin);
        energyCostMax = Mathf.Max(energyCostMin, energyCostMax);

        projectileLifetimeMin = Mathf.Max(1, projectileLifetimeMin);
        projectileLifetimeMax = Mathf.Max(projectileLifetimeMin, projectileLifetimeMax);

        if (weaponType == WeaponType.Missile)
            usesAmmo = true;

        if (!usesAmmo)
        {
            maxAmmoChargesMin = 0;
            maxAmmoChargesMax = 0;
        }
        else
        {
            maxAmmoChargesMin = Mathf.Max(1, maxAmmoChargesMin);
            maxAmmoChargesMax = Mathf.Max(maxAmmoChargesMin, maxAmmoChargesMax);
        }

        shotCount = Mathf.Max(1, shotCount);
    }
#endif
}