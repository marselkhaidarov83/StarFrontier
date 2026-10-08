using UnityEngine;

// Конфиг PirateConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "PirateConfig", menuName = "StarFrontier/Configs/Npc/Pirate")]
public class PirateConfig : BaseConfig
{
    [Header("Base Stats")]
    [Tooltip("Базовая прочность корпуса. Используется как запасное или старое значение характеристик корабля.")]
    [SerializeField] private int baseHull;
    [Tooltip("Базовый запас щита. Используется как запасное или старое значение характеристик корабля.")]
    [SerializeField] private int baseShield;
    [Tooltip("Базовый запас энергии. Используется как запасное или старое значение характеристик корабля.")]
    [SerializeField] private int baseEnergy;
    [Tooltip("Базовая скорость движения корабля.")]
    [SerializeField] private float baseSpeed;
    [Tooltip("Радиус поворота корабля. Используется при движении и построении плавного маршрута.")]
    [SerializeField] [Min(0f)] private float turnRadius = 60f;

    [Header("Combat Role")]
    [Tooltip("Параметр archetype. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private EnemyArchetype archetype;
    [Tooltip("Параметр weaponConfig. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponConfig weaponConfig;

    [Header("Rewards")]
    [Tooltip("Параметр creditReward. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private int creditReward;
    [Tooltip("Параметр xpReward. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private int xpReward;
    [Tooltip("Параметр dangerTier. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] [Range(1, 5)] private int dangerTier = 1;

    [Header("Visuals")]
    [Tooltip("Спрайт корабля в боевой визуализации.")]
    [SerializeField] private Sprite combatSprite;
    [Tooltip("Визуальный размер объекта на сцене.")]
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
