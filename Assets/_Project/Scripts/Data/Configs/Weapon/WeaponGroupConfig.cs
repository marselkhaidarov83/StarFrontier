using System.Collections.Generic;
using UnityEngine;

public enum WeaponGroupKind
{
    Ally = 0,
    Enemy = 10
}

public enum WeaponGroupAllyType
{
    None = 0,
    Military = 10,
    Ranger = 20,
    Medic = 30,
    Science = 40,
    Trader = 50
}

public enum WeaponGroupEnemyFaction
{
    None = 0,
    AI = 10,
    Ancients = 20,
    Infected = 30
}

[CreateAssetMenu(
    fileName = "weapon_group_",
    menuName = "STAR FRONTIER/Configs/Weapon Group Config")]
public sealed class WeaponGroupConfig : BaseConfig
{
    [Header("Identity")]
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField] private WeaponGroupKind groupKind = WeaponGroupKind.Ally;
    [SerializeField] private WeaponGroupAllyType allyType = WeaponGroupAllyType.None;
    [SerializeField] private WeaponGroupEnemyFaction enemyFaction = WeaponGroupEnemyFaction.None;
    [SerializeField, Min(0)] private int strengthRank;
    [SerializeField] private string strengthOrder = string.Empty;

    [Header("Weapon owner")]
    [SerializeField] private string weaponOwner = string.Empty;
    [SerializeField] private string weaponOwnerKey = string.Empty;

    [Header("Variant")]
    [SerializeField, Min(1)] private int variant = 1;
    [SerializeField, Min(1)] private int weaponCount = 1;
    [SerializeField, Min(0)] private int powerScore;
    [SerializeField] private string tierPattern = string.Empty;
    [SerializeField] private string familyPattern = string.Empty;

    [Header("Weapon refs")]
    [SerializeField] private List<WeaponConfig> weaponConfigs = new List<WeaponConfig>(5);

    public int Level => level;
    public WeaponGroupKind GroupKind => groupKind;
    public WeaponGroupAllyType AllyType => allyType;
    public WeaponGroupEnemyFaction EnemyFaction => enemyFaction;
    public int StrengthRank => strengthRank;
    public string StrengthOrder => strengthOrder;
    public string WeaponOwner => weaponOwner;
    public string WeaponOwnerKey => weaponOwnerKey;
    public int Variant => variant;
    public int WeaponCount => weaponCount;
    public int PowerScore => powerScore;
    public string TierPattern => tierPattern;
    public string FamilyPattern => familyPattern;
    public IReadOnlyList<WeaponConfig> WeaponConfigs => weaponConfigs;

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return false;

        if (level < 1 || level > 10)
            return false;

        if (weaponCount < 1)
            return false;

        if (weaponConfigs == null)
            return false;

        int actualWeaponCount = 0;

        for (int i = 0; i < weaponConfigs.Count; i++)
        {
            if (weaponConfigs[i] != null)
                actualWeaponCount++;
        }

        return actualWeaponCount == weaponCount;
    }
}