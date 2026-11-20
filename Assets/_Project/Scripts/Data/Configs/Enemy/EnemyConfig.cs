using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Конфиг описывает врага: характеристики, награды, поведение, оружие и внешний вид.
[CreateAssetMenu(
    fileName = "EnemyConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy")]
public class EnemyConfig : BaseConfig
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

    [Header("Runtime Names")]
    [SerializeField]
    [Tooltip("Набор имён для этого типа корабля.")]
    private EnemyNamePoolConfig namePool;

    [Header("Behavior Scenarios")]
    [SerializeField]
    [Tooltip("Список сценариев поведения и связанных профилей.")]
    private NpcBehaviourScenarioEntry[] behaviorScenarios =
        new NpcBehaviourScenarioEntry[0];

    [Header("Cargo and Loot")]
    [SerializeField] private EnemyFaction2A lootSpecies = EnemyFaction2A.AI;
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
    public EnemyFaction2A LootSpecies => lootSpecies;
    public NpcLootTableConfig LootTable => lootTable;
    // public EnemyArchetype AiArchetype => archetype;
    public EnemyNamePoolConfig NamePool => namePool;

    public IReadOnlyList<NpcBehaviourScenarioEntry> BehaviorScenarios =>
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
                WeaponConfig weaponConfig =
                    weapons[i];

                if (weaponConfig != null)
                    return weaponConfig;
            }

            return null;
        }
    }


    public Sprite CombatSprite => HullConfig != null ? HullConfig.Icon : Icon;
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
            NpcBehaviourScenarioEntry entry =
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
        if (!TryGetBehaviorScenario(
                scenario,
                out NpcBehaviourScenarioConfig behaviorConfig))
        {
            return null;
        }

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
            NpcBehaviourScenarioEntry entry =
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
                WeaponGroupConfig group =
                    weaponGroups[i];

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
            int count = 0;

            if (weaponGroups != null)
            {
                for (int i = 0; i < weaponGroups.Length; i++)
                {
                    WeaponGroupConfig group =
                        weaponGroups[i];

                    if (group == null)
                        continue;

                    count += group.WeaponCount;
                }
            }

            return count;
        }
    }

    private IReadOnlyList<WeaponConfig>
        GetFirstValidWeaponGroupWeapons()
    {
        if (weaponGroups != null)
        {
            for (int i = 0; i < weaponGroups.Length; i++)
            {
                WeaponGroupConfig group =
                    weaponGroups[i];

                if (group == null)
                    continue;

                if (!group.IsValid())
                    continue;

                return group.WeaponConfigs;
            }
        }

        return EmptyWeaponConfigs;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        behaviorScenarios ??= new NpcBehaviourScenarioEntry[0];
        weaponGroups ??= new WeaponGroupConfig[0];
        level = Mathf.Clamp(level, 1, 10);
    }
#endif
}
