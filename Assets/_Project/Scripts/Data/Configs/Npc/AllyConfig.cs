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

    [SerializeField, Range(0, 10)] private int level = 1;
    [Header("Loadout Groups")]
    [SerializeField] private HullGroupConfig hullGroup;
    [SerializeField] private EquipmentGroupConfig equipmentGroup;
    public HullGroupConfig HullGroup => hullGroup;
    public EquipmentGroupConfig EquipmentGroup => equipmentGroup;

    // Preview values are only for config inspection and legacy adapters.
    // Spawning selects and rolls one actual hull and one component per available slot.
    public HullConfig HullConfig => hullGroup != null ? hullGroup.PreviewHull : null;
    public ShipComponentSlots AvailableComponentSlots => HullConfig != null ? HullConfig.AvailableComponentSlots : ShipComponentSlots.None;
    public int WeaponSlotCount => HullConfig != null ? HullConfig.WeaponSlotCount : 0;
    public IReadOnlyList<ShipEquipmentConfig> DefaultComponents => equipmentGroup != null
        ? equipmentGroup.PreviewComponents(AvailableComponentSlots) : new ShipEquipmentConfig[0];
    public ShipEquipmentConfig DefaultEngine => Component(ShipComponentSlot.Engine);
    private ShipEquipmentConfig Component(ShipComponentSlot slot) =>
        (AvailableComponentSlots & (ShipComponentSlots)(1 << (int)slot)) != 0 ? equipmentGroup?.PreviewComponent(slot) : null;

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

    [Header("Travel Rules")]
    [SerializeField]
    [Tooltip("Если включено, этот союзный NPC не выбирает для межсистемного перелёта системы, принадлежащие врагам.")]
    private bool avoidEnemyOwnedTravelDestinations;

    [Header("Loot")]
    [SerializeField] private NpcLootTableConfig lootTable;

    [Header("Weapon Groups")]
    [SerializeField]
    [Tooltip("Группы оружия, из которых выбирается оснащение реального корабля.")]
    private WeaponGroupConfig[] weaponGroups =
        new WeaponGroupConfig[0];



    public int BaseHullMin => HullConfig != null ? HullConfig.MaximumHullMin : 1;
    public int BaseHullMax => HullConfig != null ? HullConfig.MaximumHullMax : 1;
    public int BaseShieldMin => Component(ShipComponentSlot.ShieldGenerator)?.ShieldMin ?? 0;
    public int BaseShieldMax => Component(ShipComponentSlot.ShieldGenerator)?.ShieldMax ?? 0;
    public int BaseHull => BaseHullMin;
    public int BaseShield => BaseShieldMin;
    public int BaseSpeedMin => DefaultEngine?.SpeedMin ?? 0;
    public int BaseSpeedMax => DefaultEngine?.SpeedMax ?? 0;
    public int BaseSpeed => BaseSpeedMin;
    public float TurnRadius => HullConfig != null ? HullConfig.TurnRadius : 0f;
    public int BaseCargoCapacityMin => HullConfig != null ? HullConfig.CargoCapacityMin : 0;
    public int BaseCargoCapacityMax => HullConfig != null ? HullConfig.CargoCapacityMax : 0;
    public int BaseCargoCapacity => BaseCargoCapacityMin;
    public float BaseAccelerationMin => HullConfig != null ? HullConfig.AccelerationMin : 0f;
    public float BaseAccelerationMax => HullConfig != null ? HullConfig.AccelerationMax : 0f;
    public float BaseAcceleration => BaseAccelerationMin;
    public int Level => level;
    public AllyRole2A Role => role;
    public NpcLootTableConfig LootTable => lootTable;
    public ShipEquipmentConfig Grabber => Component(ShipComponentSlot.Grabber);
    public AllyNamePoolConfig NamePool => namePool;

    public IReadOnlyList<AllyBehaviourScenarioEntry> BehaviorScenarios =>
        behaviorScenarios;

    public IReadOnlyList<WeaponGroupConfig> WeaponGroups =>
        weaponGroups;

    public bool AvoidEnemyOwnedTravelDestinations => avoidEnemyOwnedTravelDestinations;

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

    public Sprite MapSprite => HullConfig != null ? HullConfig.Icon : Icon;
    public Sprite CombatSprite => MapSprite;
    public float VisualSize => HullConfig != null ? HullConfig.VisualSize : 0f;

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
        behaviorScenarios ??= new AllyBehaviourScenarioEntry[0];
        weaponGroups ??= new WeaponGroupConfig[0];
        level = Mathf.Clamp(level, 0, 10);
    }
#endif
}
