using UnityEngine;

[CreateAssetMenu(fileName = "ShipConfig", menuName = "StarFrontier/Configs/Ship")]
public class ShipConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseHull;
    [SerializeField] private int baseShield;
    [SerializeField] private int baseEnergy;
    [SerializeField] private float baseEnergyRegen;

    [Header("Movement")]
    [SerializeField] private float baseSpeed;
    [SerializeField] private float baseAcceleration;
    [SerializeField] private float baseTurnRate;

    [Header("Capacity")]
    [SerializeField] private int baseCargoCapacity;

    [Header("Slots")]
    [SerializeField] private int weaponSlotCount;
    [SerializeField] private int moduleSlotCount;

    [Header("Visuals")]
    [SerializeField] private Sprite combatSprite;

    public int BaseHull => baseHull;
    public int BaseShield => baseShield;
    public int BaseEnergy => baseEnergy;
    public float BaseEnergyRegen => baseEnergyRegen;
    public float BaseSpeed => baseSpeed;
    public float BaseAcceleration => baseAcceleration;
    public float BaseTurnRate => baseTurnRate;
    public int BaseCargoCapacity => baseCargoCapacity;
    public int WeaponSlotCount => weaponSlotCount;
    public int ModuleSlotCount => moduleSlotCount;
    public Sprite CombatSprite => combatSprite;
}