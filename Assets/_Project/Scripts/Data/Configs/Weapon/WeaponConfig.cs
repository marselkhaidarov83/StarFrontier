using System;
using UnityEngine;

// Конфиг описывает оружие: уровень, урон, дальность, стоимость энергии, тип выстрела, боеприпасы и визуальные настройки.
[CreateAssetMenu(fileName = "WeaponConfig", menuName = "StarFrontier/Configs/Combat/Weapon")]
[Serializable]
public class WeaponConfig : SpaceLootCombatConfig
{
    [SerializeField, Tooltip("Оружие врагов. Не попадает в предложения рынка и обратный выкуп.")]
    private bool enemyOnly;
    public bool EnemyOnly => enemyOnly;
    [Header("Progression")]
    [Tooltip("Уровень конфигурации. Используется для баланса, генерации и подбора подходящих записей.")]
    [SerializeField][Min(1)] private int level = 1;

    [Header("Cargo")]
    [Tooltip("Занимает места в трюме за один экземпляр оружия.")]
    [SerializeField][Min(1)] private int cargoSizeMin = 1;
    [SerializeField][Min(1)] private int cargoSizeMax = 2;

    [Header("Base Stats / Randomized Runtime Ranges")]
    [Tooltip("Минимальное значение параметра baseDamageMin. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(1)] private int baseDamageMin = 1;
    [Tooltip("Максимальное значение параметра baseDamageMax. Используется как верхняя граница диапазона.")]
    [SerializeField][Min(1)] private int baseDamageMax = 1;

    [Tooltip("Минимальное значение параметра rangeMin. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(0f)] private int rangeMin = 1;
    [Tooltip("Максимальное значение параметра rangeMax. Используется как верхняя граница диапазона.")]
    [SerializeField][Min(0f)] private int rangeMax = 1;

    [Tooltip("Минимальное значение параметра energyCostMin. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(0)] private int energyCostMin = 0;
    [Tooltip("Максимальное значение параметра energyCostMax. Используется как верхняя граница диапазона.")]
    [SerializeField][Min(0)] private int energyCostMax = 0;

    [Tooltip("Минимальное значение параметра projectileLifetimeMin. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(1)] private int projectileLifetimeMin = 1;
    [Tooltip("Максимальное значение параметра projectileLifetimeMax. Используется как верхняя граница диапазона.")]
    [SerializeField][Min(1)] private int projectileLifetimeMax = 1;

    [Header("Combat Behavior")]
    [Tooltip("Параметр weaponType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponType weaponType;
    [Tooltip("Параметр damageType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponDamageType damageType;
    [Tooltip("Параметр shotType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponShotType2A shotType = WeaponShotType2A.Beam;
    [Tooltip("Количество для параметра shotCount. Используется соответствующей системой при генерации или расчёте.")]
    [SerializeField][Min(1)][InspectorName("Кол-во выстрелов")] private int shotCount = 1;

    [Header("Visuals")]
    [Tooltip("Параметр projectilePrefabRef. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private GameObject projectilePrefabRef;

    [Header("Ammo")]
    [Tooltip("Переключатель usesAmmo. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool usesAmmo = false;
    [Tooltip("Минимальное значение параметра maxAmmoChargesMin. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(0)] private int maxAmmoChargesMin = 0;
    [Tooltip("Максимальное значение параметра maxAmmoChargesMax. Используется как верхняя граница диапазона.")]
    [SerializeField][Min(0)] private int maxAmmoChargesMax = 0;

    public int Level => level;

    [SerializeField, Min(1)] private int maximumDurability = 100;
    public int MaximumDurability => maximumDurability > 0 ? maximumDurability : 100;
    public int CargoSize => cargoSizeMin;
    public int CargoSizeMin => cargoSizeMin;
    public int CargoSizeMax => cargoSizeMax;

    public int BaseDamageMin => baseDamageMin;
    public int BaseDamageMax => baseDamageMax;

    public float RangeMin => rangeMin;
    public float RangeMax => rangeMax;

    public int EnergyCostMin => energyCostMin;
    public int EnergyCostMax => energyCostMax;

    public int ProjectileLifetimeMin => projectileLifetimeMin;
    public int ProjectileLifetimeMax => projectileLifetimeMax;

    public WeaponType WeaponType => weaponType;
    public WeaponDamageType DamageType => damageType;

    public GameObject ProjectilePrefabRef => projectilePrefabRef;

    public bool UsesAmmo => usesAmmo;
    public int MaxAmmoChargesMin => maxAmmoChargesMin;
    public int MaxAmmoChargesMax => maxAmmoChargesMax;

    public WeaponShotType2A ShotType => shotType;
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

        int cargoSize = RollIntInclusive(random, cargoSizeMin, cargoSizeMax);
        int damage = RollIntInclusive(random, baseDamageMin, baseDamageMax);
        int range = RollIntInclusive(random, rangeMin, rangeMax);
        int energyCost = RollIntInclusive(random, energyCostMin, energyCostMax);
        int projectileLifetime = RollIntInclusive(random, projectileLifetimeMin, projectileLifetimeMax);

        int maxAmmoCharges = usesAmmo
            ? RollIntInclusive(random, maxAmmoChargesMin, maxAmmoChargesMax)
            : 0;

        return new WeaponRuntimeStats(
            Id,
            level,
            cargoSize,
            damage,
            range,
            energyCost,
            projectileLifetime,
            weaponType,
            ShotType,
            damageType,
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


    private static int RollIntInclusive(System.Random random, int min, int max)
    {
        if (max < min)
            max = min;

        return random.Next(min, max + 1);
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        cargoSizeMin = Mathf.Max(1, cargoSizeMin);
        cargoSizeMax = Mathf.Max(cargoSizeMin, cargoSizeMax);

        baseDamageMin = Mathf.Max(1, baseDamageMin);
        baseDamageMax = Mathf.Max(baseDamageMin, baseDamageMax);

        rangeMin = Mathf.Max(0, rangeMin);
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
