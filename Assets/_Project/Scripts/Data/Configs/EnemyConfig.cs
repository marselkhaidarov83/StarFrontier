using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "StarFrontier/Configs/Enemy")]
public class EnemyConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseHull;
    [SerializeField] private int baseShield;
    [SerializeField] private int baseEnergy;
    [SerializeField] private float baseSpeed;

    [Header("Combat Role")]
    [SerializeField] private EnemyArchetype archetype;
    [SerializeField] private WeaponConfig weaponConfig;

    [Header("Rewards")]
    [SerializeField] private int creditReward;
    [SerializeField] private int xpReward;
    [SerializeField] [Range(1, 5)] private int dangerTier = 1;

    [Header("Visuals")]
    [SerializeField] private Sprite combatSprite;

    public int BaseHull => baseHull;
    public int BaseShield => baseShield;
    public int BaseEnergy => baseEnergy;
    public float BaseSpeed => baseSpeed;
    public EnemyArchetype AiArchetype => archetype;
    public WeaponConfig WeaponConfig => weaponConfig;
    public int CreditReward => creditReward;
    public int XpReward => xpReward;
    public int DangerTier => dangerTier;
    public Sprite CombatSprite => combatSprite;
}