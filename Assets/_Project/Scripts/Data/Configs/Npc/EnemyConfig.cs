using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "StarFrontier/Configs/Npc/Enemy")]
public class EnemyConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseHull;
    [SerializeField] private int baseShield;
    [SerializeField] private int baseEnergy;
    [SerializeField] private float baseSpeed;
    [SerializeField] [Range(1, 10)] private int level;

    [Header("Combat Role")]
    [SerializeField] private EnemyArchetype archetype;

    [Header("Weapons")]
    [SerializeField] private WeaponConfig[] weaponConfigs = new WeaponConfig[0];

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
    public int Level => level;

    public EnemyArchetype AiArchetype => archetype;

    public IReadOnlyList<WeaponConfig> WeaponConfigs => weaponConfigs;

    /*
     * Старое свойство оставляем для совместимости.
     * Старый код, который ожидает EnemyConfig.WeaponConfig,
     * получит первое непустое оружие из массива.
     */
    public WeaponConfig WeaponConfig
    {
        get
        {
            if (weaponConfigs == null)
                return null;

            for (int i = 0; i < weaponConfigs.Length; i++)
            {
                if (weaponConfigs[i] != null)
                    return weaponConfigs[i];
            }

            return null;
        }
    }

    public int CreditReward => creditReward;
    public int XpReward => xpReward;
    public int DangerTier => dangerTier;
    public Sprite CombatSprite => combatSprite;

    public bool HasWeapons()
    {
        return WeaponCount > 0;
    }

    public int WeaponCount
    {
        get
        {
            if (weaponConfigs == null)
                return 0;

            int count = 0;

            for (int i = 0; i < weaponConfigs.Length; i++)
            {
                if (weaponConfigs[i] != null)
                    count++;
            }

            return count;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (weaponConfigs == null)
            weaponConfigs = new WeaponConfig[0];

        baseHull = Mathf.Max(1, baseHull);
        baseShield = Mathf.Max(0, baseShield);
        baseEnergy = Mathf.Max(0, baseEnergy);
        baseSpeed = Mathf.Max(0f, baseSpeed);
        level = Mathf.Max(1, level);
        creditReward = Mathf.Max(0, creditReward);
        xpReward = Mathf.Max(0, xpReward);
    }
#endif
}