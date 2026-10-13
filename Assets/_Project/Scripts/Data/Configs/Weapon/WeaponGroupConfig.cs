using System.Collections.Generic;
using UnityEngine;

// Конфиг WeaponGroupConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
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
    menuName = "StarFrontier/Configs/Combat/Weapon Group Config")]
public sealed class WeaponGroupConfig : BaseConfig
{
    [Header("Identity")]
    [Tooltip("Уровень конфигурации. Используется для баланса, генерации и подбора подходящих записей.")]
    [SerializeField, Min(1)] private int level = 1;
    [Tooltip("Параметр groupKind. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponGroupKind groupKind = WeaponGroupKind.Ally;
    [Tooltip("Параметр allyType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponGroupAllyType allyType = WeaponGroupAllyType.None;
    [Tooltip("Параметр enemyFaction. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private WeaponGroupEnemyFaction enemyFaction = WeaponGroupEnemyFaction.None;
    [Tooltip("Параметр strengthRank. Используется связанными игровыми системами этого конфига.")]
    [SerializeField, Min(0)] private int strengthRank;
    [Tooltip("Параметр strengthOrder. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private string strengthOrder = string.Empty;

    [Header("Weapon owner")]
    [Tooltip("Параметр weaponOwner. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private string weaponOwner = string.Empty;
    [Tooltip("Параметр weaponOwnerKey. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private string weaponOwnerKey = string.Empty;

    [Header("Variant")]
    [Tooltip("Параметр variant. Используется связанными игровыми системами этого конфига.")]
    [SerializeField, Min(1)] private int variant = 1;
    [Tooltip("Количество для параметра weaponCount. Используется соответствующей системой при генерации или расчёте.")]
    [SerializeField, Min(1)] private int weaponCount = 1;
    [Tooltip("Параметр powerScore. Используется связанными игровыми системами этого конфига.")]
    [SerializeField, Min(0)] private int powerScore;
    [Tooltip("Параметр tierPattern. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private string tierPattern = string.Empty;
    [Tooltip("Параметр familyPattern. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private string familyPattern = string.Empty;

    [Header("Weapon refs")]
    [Tooltip("Список weaponConfigs. Используется связанными игровыми системами этого конфига.")]
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
