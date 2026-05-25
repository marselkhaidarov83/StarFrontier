using UnityEngine;

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "StarFrontier/Configs/Weapon")]
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

    [Header("Combat Behavior")]
    [SerializeField] private bool isHitscan;
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private WeaponDamageType damageType;
    [SerializeField] private WeaponTargetingMode targetingMode;

    [Header("Visuals")]
    [SerializeField] private GameObject projectilePrefabRef;

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
}