using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    private int baseHullMin = 50;

    [SerializeField]
    private int baseHullMax = 50;

    [FormerlySerializedAs("baseShield")]
    [SerializeField]
    private int baseShieldMin = 20;

    [SerializeField]
    private int baseShieldMax = 20;

    [FormerlySerializedAs("baseEnergy")]
    [SerializeField]
    private int baseEnergyMin = 50;

    [SerializeField]
    private int baseEnergyMax = 50;

    [SerializeField]
    private float baseEnergyRegen = 0f;

    [FormerlySerializedAs("baseSpeed")]
    [SerializeField]
    private float baseSpeedMin = 2f;

    [SerializeField]
    private float baseSpeedMax = 2f;

    [Header("Movement")]
    [SerializeField]
    private float baseAcceleration = 1f;

    [SerializeField]
    private float baseTurnRate = 90f;

    [Header("Capacity")]
    [SerializeField]
    private int baseCargoCapacity = 0;

    [Header("Slots")]
    [SerializeField]
    private int weaponSlotCount = 1;

    [SerializeField]
    private int moduleSlotCount = 0;

    [SerializeField]
    [Range(0, 10)]
    private int level = 1;

    [Header("Ally Role")]
    [SerializeField]
    private AllyRole2A role = AllyRole2A.Ranger;

    [Header("Runtime Names")]
    [SerializeField]
    private AllyNamePoolConfig namePool;

    [Header("Behavior Scenarios")]
    [Tooltip("Scenario enum and reference to the behavior profile.")]
    [SerializeField]
    private AllyBehaviourScenarioEntry[] behaviorScenarios =
        new AllyBehaviourScenarioEntry[0];

    [Header("Weapon Groups")]
    [Tooltip("One random group is selected when a real ally NPC is created.")]
    [SerializeField]
    private WeaponGroupConfig[] weaponGroups =
        new WeaponGroupConfig[0];

    [Header("Visuals")]
    [SerializeField]
    private Sprite mapSprite;

    [SerializeField]
    private Sprite combatSprite;

    public int BaseHullMin => baseHullMin;
    public int BaseHullMax => baseHullMax;
    public int BaseShieldMin => baseShieldMin;
    public int BaseShieldMax => baseShieldMax;
    public int BaseEnergyMin => baseEnergyMin;
    public int BaseEnergyMax => baseEnergyMax;
    public float BaseSpeedMin => baseSpeedMin;
    public float BaseSpeedMax => baseSpeedMax;

    public int BaseHull => baseHullMin;
    public int BaseShield => baseShieldMin;
    public int BaseEnergy => baseEnergyMin;
    public float BaseEnergyRegen => baseEnergyRegen;
    public float BaseSpeed => baseSpeedMin;
    public float BaseAcceleration => baseAcceleration;
    public float BaseTurnRate => baseTurnRate;
    public int BaseCargoCapacity => baseCargoCapacity;
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

        baseSpeedMin = Mathf.Max(0f, baseSpeedMin);
        baseSpeedMax = Mathf.Max(baseSpeedMin, baseSpeedMax);

        baseAcceleration = Mathf.Max(0f, baseAcceleration);
        baseTurnRate = Mathf.Max(0f, baseTurnRate);
        baseCargoCapacity = Mathf.Max(0, baseCargoCapacity);
        weaponSlotCount = Mathf.Max(0, weaponSlotCount);
        moduleSlotCount = Mathf.Max(0, moduleSlotCount);

        level = Mathf.Clamp(level, 0, 10);
    }
#endif
}
