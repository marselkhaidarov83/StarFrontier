using UnityEngine;

[CreateAssetMenu(fileName = "PirateConfig", menuName = "StarFrontier/Configs/Npc/Pirate")]
public class PirateConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseHull;
    [SerializeField] private int baseShield;
    [SerializeField] private int baseEnergy;
    [SerializeField] private float baseSpeed;
    [SerializeField] [Min(0f)] private float turnRadius = 60f;

    [Header("Combat Role")]
    [SerializeField] private EnemyArchetype archetype;
    [SerializeField] private WeaponConfig weaponConfig;

    [Header("Rewards")]
    [SerializeField] private int creditReward;
    [SerializeField] private int xpReward;
    [SerializeField] [Range(1, 5)] private int dangerTier = 1;

    [Header("Visuals")]
    [SerializeField] private Sprite combatSprite;
    [SerializeField] [Min(0f)] private float visualSize = 48f;

    public int BaseHull => baseHull;
    public int BaseShield => baseShield;
    public int BaseEnergy => baseEnergy;
    public float BaseSpeed => baseSpeed;
    public float TurnRadius => turnRadius;
    public EnemyArchetype AiArchetype => archetype;
    public WeaponConfig WeaponConfig => weaponConfig;
    public int CreditReward => creditReward;
    public int XpReward => xpReward;
    public int DangerTier => dangerTier;
    public Sprite CombatSprite => combatSprite;
    public float VisualSize => visualSize;
}
