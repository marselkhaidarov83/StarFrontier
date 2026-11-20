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
    [SerializeField, Tooltip("Фракция для правил спавна и визуального представления врагов.")]
    private WeaponGroupEnemyFaction enemyFaction = WeaponGroupEnemyFaction.None;

    [SerializeField, Min(1), Tooltip("Количество оружия, которое должна содержать группа.")]
    private int weaponCount = 1;

    [SerializeField, Tooltip("Оружие группы в заданном порядке.")]
    private List<WeaponConfig> weaponConfigs = new List<WeaponConfig>(5);

    public WeaponGroupEnemyFaction EnemyFaction => enemyFaction;
    public int WeaponCount => weaponCount;
    public IReadOnlyList<WeaponConfig> WeaponConfigs => weaponConfigs;

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Id) || weaponCount < 1 || weaponConfigs == null)
            return false;

        int actualWeaponCount = 0;
        for (int i = 0; i < weaponConfigs.Count; i++)
            if (weaponConfigs[i] != null)
                actualWeaponCount++;

        return actualWeaponCount == weaponCount;
    }
}
