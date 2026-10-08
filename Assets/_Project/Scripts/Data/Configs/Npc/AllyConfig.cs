using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Конфиг описывает союзный корабль: характеристики, роль, имя, поведение, оружие и внешний вид.
[CreateAssetMenu(
    fileName = "AllyConfig",
    menuName = "StarFrontier/Configs/Npc/Ally")]
public sealed class AllyConfig : BaseConfig
{
    private static readonly WeaponConfig[] EmptyWeaponConfigs =
        new WeaponConfig[0];

    [Header("Base Stats Range")]
    [FormerlySerializedAs("baseHull")]
    [SerializeField]
    [Tooltip("Минимальная базовая прочность корпуса. Используется при создании корабля для случайного выбора значения.")]
    private int baseHullMin = 50;

    [SerializeField]
    [Tooltip("Максимальная базовая прочность корпуса. Используется при создании корабля для случайного выбора значения.")]
    private int baseHullMax = 50;

    [FormerlySerializedAs("baseShield")]
    [SerializeField]
    [Tooltip("Минимальный базовый запас щита. Используется при создании корабля для случайного выбора значения.")]
    private int baseShieldMin = 20;

    [SerializeField]
    [Tooltip("Максимальный базовый запас щита. Используется при создании корабля для случайного выбора значения.")]
    private int baseShieldMax = 20;

    [FormerlySerializedAs("baseEnergy")]
    [SerializeField]
    [Tooltip("Минимальный базовый запас энергии. Используется при создании корабля для случайного выбора значения.")]
    private int baseEnergyMin = 50;

    [SerializeField]
    [Tooltip("Максимальный базовый запас энергии. Используется при создании корабля для случайного выбора значения.")]
    private int baseEnergyMax = 50;

    [SerializeField]
    [Tooltip("Базовое восстановление энергии. Используется в расчётах боевой выносливости корабля.")]
    private float baseEnergyRegen = 0f;

    [FormerlySerializedAs("baseSpeed")]
    [SerializeField]
    [Tooltip("Минимальная базовая скорость. Используется при создании корабля для случайного выбора значения.")]
    private int baseSpeedMin = 2;

    [SerializeField]
    [Tooltip("Максимальная базовая скорость. Используется при создании корабля для случайного выбора значения.")]
    private int baseSpeedMax = 2;

    [Header("Movement")]
    [FormerlySerializedAs("baseAcceleration")]
    [SerializeField]
    [Tooltip("Минимальное базовое ускорение корабля.")]
    private float baseAccelerationMin = 1f;

    [SerializeField]
    [Tooltip("Максимальное базовое ускорение корабля.")]
    private float baseAccelerationMax = 1f;

    [FormerlySerializedAs("baseTurnRate")]
    [SerializeField]
    [Tooltip("Минимальная базовая скорость поворота корабля.")]
    private float baseTurnRateMin = 90f;

    [SerializeField]
    [Tooltip("Максимальная базовая скорость поворота корабля.")]
    private float baseTurnRateMax = 90f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Радиус поворота корабля. Используется при движении и построении плавного маршрута.")]
    private float turnRadius = 60f;

    [Header("Capacity")]
    [FormerlySerializedAs("baseCargoCapacity")]
    [SerializeField]
    [Tooltip("Минимальная вместимость грузового отсека.")]
    private int baseCargoCapacityMin = 0;

    [SerializeField]
    [Tooltip("Максимальная вместимость грузового отсека.")]
    private int baseCargoCapacityMax = 0;

    [Header("Slots")]
    [SerializeField]
    [Tooltip("Количество ячеек оружия у корабля.")]
    private int weaponSlotCount = 1;

    [SerializeField]
    [Tooltip("Количество ячеек модулей у корабля.")]
    private int moduleSlotCount = 0;

    [SerializeField]
    [Range(0, 10)]
    [Tooltip("Уровень конфигурации. Используется для баланса, генерации и подбора подходящих записей.")]
    private int level = 1;

    [Header("Ally Role")]
    [SerializeField]
    [Tooltip("Роль союзника. Используется при создании союзных кораблей и выборе поведения.")]
    private AllyRole2A role = AllyRole2A.Ranger;

    [Header("Runtime Names")]
    [SerializeField]
    [Tooltip("Набор имён для этого типа корабля.")]
    private AllyNamePoolConfig namePool;

    [Header("Behavior Scenarios")]
    [SerializeField]
    [Tooltip("Список сценариев поведения и связанных профилей.")]
    private AllyBehaviourScenarioEntry[] behaviorScenarios =
        new AllyBehaviourScenarioEntry[0];

    [Header("Weapon Groups")]
    [SerializeField]
    [Tooltip("Группы оружия, из которых выбирается оснащение реального корабля.")]
    private WeaponGroupConfig[] weaponGroups =
        new WeaponGroupConfig[0];

    [Header("Visuals")]
    [SerializeField]
    [Tooltip("Спрайт корабля на карте системы.")]
    private Sprite mapSprite;

    [SerializeField]
    [Tooltip("Спрайт корабля в боевой визуализации.")]
    private Sprite combatSprite;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Визуальный размер объекта на сцене.")]
    private float visualSize = 48f;

    public int BaseHullMin => baseHullMin;
    public int BaseHullMax => baseHullMax;
    public int BaseShieldMin => baseShieldMin;
    public int BaseShieldMax => baseShieldMax;
    public int BaseEnergyMin => baseEnergyMin;
    public int BaseEnergyMax => baseEnergyMax;
    public int BaseSpeedMin => baseSpeedMin;
    public int BaseSpeedMax => baseSpeedMax;

    public int BaseHull => baseHullMin;
    public int BaseShield => baseShieldMin;
    public int BaseEnergy => baseEnergyMin;
    public float BaseEnergyRegen => baseEnergyRegen;
    public int BaseSpeed => baseSpeedMin;
    public float BaseAccelerationMin => baseAccelerationMin;
    public float BaseAccelerationMax => baseAccelerationMax;
    public float BaseTurnRateMin => baseTurnRateMin;
    public float BaseTurnRateMax => baseTurnRateMax;
    public float TurnRadius => turnRadius;
    public int BaseCargoCapacityMin => baseCargoCapacityMin;
    public int BaseCargoCapacityMax => baseCargoCapacityMax;

    public float BaseAcceleration => baseAccelerationMin;
    public float BaseTurnRate => baseTurnRateMin;
    public int BaseCargoCapacity => baseCargoCapacityMin;
    public int WeaponSlotCount => weaponSlotCount;
    public int ModuleSlotCount => moduleSlotCount;

    public int Level => level;
    public AllyRole2A Role => role;
    public AllyNamePoolConfig NamePool => namePool;

    public IReadOnlyList<AllyBehaviourScenarioEntry> BehaviorScenarios =>
        behaviorScenarios;

    public IReadOnlyList<WeaponGroupConfig> WeaponGroups =>
        weaponGroups;

    public IReadOnlyList<WeaponConfig> WeaponConfigs =>
        GetFirstValidWeaponGroupWeapons();

    [Tooltip("Параметр WeaponConfig. Используется связанными игровыми системами этого конфига.")]
    public WeaponConfig WeaponConfig
    {
        get
        {
            IReadOnlyList<WeaponConfig> weapons =
                GetFirstValidWeaponGroupWeapons();

            if (weapons == null)
                return null;

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null)
                    return weapons[i];
            }

            return null;
        }
    }

    public Sprite MapSprite => mapSprite;
    public Sprite CombatSprite => combatSprite != null ? combatSprite : mapSprite;
    public float VisualSize => visualSize;

    public string PickRuntimeDisplayName(string runtimeNpcId)
    {
        if (namePool != null)
        {
            string pickedName =
                namePool.PickName(runtimeNpcId);

            if (!string.IsNullOrWhiteSpace(pickedName))
                return pickedName;
        }

        if (!string.IsNullOrWhiteSpace(DisplayName))
            return DisplayName;

        return Id;
    }

    public bool TryGetBehaviorScenario(
        AllyBehaviourScenario scenario,
        out NpcBehaviourScenarioConfig behaviorConfig)
    {
        behaviorConfig = null;

        if (behaviorScenarios == null)
            return false;

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            AllyBehaviourScenarioEntry entry =
                behaviorScenarios[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            if (entry.Scenario != scenario)
                continue;

            behaviorConfig = entry.BehaviorConfig;
            return true;
        }

        return false;
    }

    public NpcBehaviourScenarioConfig GetBehaviorScenario(
        AllyBehaviourScenario scenario)
    {
        if (!TryGetBehaviorScenario(scenario, out NpcBehaviourScenarioConfig behaviorConfig))
            return null;

        return behaviorConfig;
    }

    public bool HasBehaviorScenario(AllyBehaviourScenario scenario)
    {
        return TryGetBehaviorScenario(scenario, out _);
    }

    public bool HasDuplicateBehaviorScenarios()
    {
        if (behaviorScenarios == null)
            return false;

        HashSet<AllyBehaviourScenario> scenarios =
            new HashSet<AllyBehaviourScenario>();

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            AllyBehaviourScenarioEntry entry =
                behaviorScenarios[i];

            if (entry == null)
                continue;

            if (!scenarios.Add(entry.Scenario))
                return true;
        }

        return false;
    }

    public bool HasWeapons()
    {
        return WeaponCount > 0;
    }

    public bool HasWeaponGroups()
    {
        return WeaponGroupCount > 0;
    }

    [Tooltip("Количество для параметра WeaponGroupCount. Используется соответствующей системой при генерации или расчёте.")]
    public int WeaponGroupCount
    {
        get
        {
            if (weaponGroups == null)
                return 0;

            int count = 0;

            for (int i = 0; i < weaponGroups.Length; i++)
            {
                WeaponGroupConfig group = weaponGroups[i];

                if (group == null)
                    continue;

                if (!group.IsValid())
                    continue;

                count++;
            }

            return count;
        }
    }

    [Tooltip("Количество для параметра WeaponCount. Используется соответствующей системой при генерации или расчёте.")]
    public int WeaponCount
    {
        get
        {
            if (weaponGroups == null)
                return 0;

            int count = 0;

            for (int i = 0; i < weaponGroups.Length; i++)
            {
                WeaponGroupConfig group = weaponGroups[i];

                if (group == null)
                    continue;

                count += group.WeaponCount;
            }

            return count;
        }
    }

    private IReadOnlyList<WeaponConfig> GetFirstValidWeaponGroupWeapons()
    {
        if (weaponGroups == null)
            return EmptyWeaponConfigs;

        for (int i = 0; i < weaponGroups.Length; i++)
        {
            WeaponGroupConfig group = weaponGroups[i];

            if (group == null)
                continue;

            if (!group.IsValid())
                continue;

            return group.WeaponConfigs;
        }

        return EmptyWeaponConfigs;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (behaviorScenarios == null)
            behaviorScenarios = new AllyBehaviourScenarioEntry[0];

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            if (behaviorScenarios[i] == null)
                behaviorScenarios[i] = new AllyBehaviourScenarioEntry();
        }

        if (weaponGroups == null)
            weaponGroups = new WeaponGroupConfig[0];

        for (int i = 0; i < weaponGroups.Length; i++)
        {
            if (weaponGroups[i] == null)
                weaponGroups[i] = new WeaponGroupConfig();
        }

        baseHullMin = Mathf.Max(1, baseHullMin);
        baseHullMax = Mathf.Max(baseHullMin, baseHullMax);

        baseShieldMin = Mathf.Max(0, baseShieldMin);
        baseShieldMax = Mathf.Max(baseShieldMin, baseShieldMax);

        baseEnergyMin = Mathf.Max(0, baseEnergyMin);
        baseEnergyMax = Mathf.Max(baseEnergyMin, baseEnergyMax);
        baseEnergyRegen = Mathf.Max(0f, baseEnergyRegen);

        baseSpeedMin = Mathf.Max(0, baseSpeedMin);
        baseSpeedMax = Mathf.Max(baseSpeedMin, baseSpeedMax);

        baseAccelerationMin = Mathf.Max(0f, baseAccelerationMin);
        baseAccelerationMax = Mathf.Max(baseAccelerationMin, baseAccelerationMax);

        baseTurnRateMin = Mathf.Max(0f, baseTurnRateMin);
        baseTurnRateMax = Mathf.Max(baseTurnRateMin, baseTurnRateMax);

        baseCargoCapacityMin = Mathf.Max(0, baseCargoCapacityMin);
        baseCargoCapacityMax = Mathf.Max(baseCargoCapacityMin, baseCargoCapacityMax);
        weaponSlotCount = Mathf.Max(0, weaponSlotCount);
        moduleSlotCount = Mathf.Max(0, moduleSlotCount);

        level = Mathf.Clamp(level, 0, 10);
    }
#endif
}
