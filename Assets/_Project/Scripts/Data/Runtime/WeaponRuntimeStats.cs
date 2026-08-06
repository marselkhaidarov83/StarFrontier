using System;

[Serializable]
public struct WeaponRuntimeStats
{
    public string WeaponConfigId;

    public int Level;
    public WeaponEquipmentTier EquipmentTier;
    public int CargoSize;

    public int Damage;
    public float Range;
    public int EnergyCost;
    public int ProjectileLifetime;

    public bool IsHitscan;
    public WeaponType WeaponType;
    public WeaponDamageType DamageType;
    public WeaponTargetingMode TargetingMode;

    public bool UsesAmmo;
    public int MaxAmmoCharges;

    public WeaponRuntimeStats(
        string weaponConfigId,
        int level,
        WeaponEquipmentTier equipmentTier,
        int cargoSize,
        int damage,
        float range,
        int energyCost,
        int projectileLifetime,
        bool isHitscan,
        WeaponType weaponType,
        WeaponDamageType damageType,
        WeaponTargetingMode targetingMode,
        bool usesAmmo,
        int maxAmmoCharges)
    {
        WeaponConfigId = weaponConfigId;

        Level = level;
        EquipmentTier = equipmentTier;
        CargoSize = cargoSize;

        Damage = damage;
        Range = range;
        EnergyCost = energyCost;
        ProjectileLifetime = projectileLifetime;

        IsHitscan = isHitscan;
        WeaponType = weaponType;
        DamageType = damageType;
        TargetingMode = targetingMode;

        UsesAmmo = usesAmmo;
        MaxAmmoCharges = maxAmmoCharges;
    }
}