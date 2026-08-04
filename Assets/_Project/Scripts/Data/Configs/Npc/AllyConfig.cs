using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AllyConfig", menuName = "StarFrontier/Configs/Npc/Ally")]
public sealed class AllyConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseHull = 50;
    [SerializeField] private int baseShield = 20;
    [SerializeField] private int baseEnergy = 50;
    [SerializeField] private float baseSpeed = 2f;
    [SerializeField] [Range(1, 10)] private int level = 1;

    [Header("Ally Role")]
    [SerializeField] private AllyRole2A role = AllyRole2A.Ranger;

    [Header("Weapons")]
    [SerializeField] private WeaponConfig[] weaponConfigs = new WeaponConfig[0];

    [Header("Visuals")]
    [SerializeField] private Sprite mapSprite;

    public int BaseHull => baseHull;
    public int BaseShield => baseShield;
    public int BaseEnergy => baseEnergy;
    public float BaseSpeed => baseSpeed;
    public int Level => level;

    public AllyRole2A Role => role;

    public IReadOnlyList<WeaponConfig> WeaponConfigs => weaponConfigs;

    /*
     * Старое свойство оставляем для совместимости.
     * Старый код, который ожидает AllyConfig.WeaponConfig,
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

    public Sprite MapSprite => mapSprite;

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
    }
#endif
}