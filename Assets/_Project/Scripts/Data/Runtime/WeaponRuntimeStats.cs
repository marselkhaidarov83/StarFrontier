using System;

[Serializable]
public struct WeaponRuntimeStats
{
    public string WeaponConfigId;

    public int Level;
    public int CargoSize;

    public int Damage;
    public float Range;
    public int EnergyCost;
    public int ProjectileLifetime;

    public WeaponType WeaponType;
    public WeaponShotType2A ShotType;
    public WeaponDamageType DamageType;

    public int ShotCount;

    public bool UsesAmmo;
    public int MaxAmmoCharges;

    public WeaponRuntimeStats(
        string weaponConfigId,
        int level,
        int cargoSize,
        int damage,
        float range,
        int energyCost,
        int projectileLifetime,
        WeaponType weaponType,
        WeaponShotType2A shotType,
        WeaponDamageType damageType,
        int shotCount,
        bool usesAmmo,
        int maxAmmoCharges)
    {
        WeaponConfigId = weaponConfigId;

        Level = level;
        CargoSize = cargoSize;

        Damage = damage;
        Range = range;
        EnergyCost = energyCost;
        ProjectileLifetime = projectileLifetime;

        WeaponType = weaponType;
        ShotType = shotType;
        DamageType = damageType;

        ShotCount = Math.Max(1, shotCount);

        UsesAmmo = usesAmmo;
        MaxAmmoCharges = maxAmmoCharges;
    }
}