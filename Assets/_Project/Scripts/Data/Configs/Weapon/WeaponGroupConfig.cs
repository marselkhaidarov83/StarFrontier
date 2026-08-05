using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(
    fileName = "WeaponGroupConfig",
    menuName = "StarFrontier/Configs/Npc/WeaponGroupConfig")]
public sealed class WeaponGroupConfig : BaseConfig
{
    [Header("Weapons")]
    [SerializeField]
    private WeaponConfig[] weaponConfigs =
        new WeaponConfig[0];

    public IReadOnlyList<WeaponConfig> WeaponConfigs =>
        weaponConfigs;

    public bool IsValid()
    {
        if (weaponConfigs == null)
            return false;

        for (int i = 0; i < weaponConfigs.Length; i++)
        {
            WeaponConfig weaponConfig =
                weaponConfigs[i];

            if (weaponConfig == null)
                continue;

            if (string.IsNullOrWhiteSpace(weaponConfig.Id))
                continue;

            return true;
        }

        return false;
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
    }
#endif
}