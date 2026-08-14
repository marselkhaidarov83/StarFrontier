using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "EnemyConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy")]
public class EnemyConfig : BaseConfig
{
    private static readonly WeaponConfig[] EmptyWeaponConfigs =
        new WeaponConfig[0];

    [Header("Base Stats Range")]
    [FormerlySerializedAs("baseHull")]
    [SerializeField]
    private int baseHullMin = 1;

    [SerializeField]
    private int baseHullMax = 1;

    [FormerlySerializedAs("baseShield")]
    [SerializeField]
    private int baseShieldMin = 0;

    [SerializeField]
    private int baseShieldMax = 0;

    [FormerlySerializedAs("baseEnergy")]
    [SerializeField]
    private int baseEnergyMin = 0;

    [SerializeField]
    private int baseEnergyMax = 0;

    [FormerlySerializedAs("baseSpeed")]
    [SerializeField]
    private float baseSpeedMin = 0f;

    [SerializeField]
    private float baseSpeedMax = 0f;

    [SerializeField]
    [Range(1, 10)]
    private int level = 1;

    // [Header("Combat Role")]
    // [SerializeField]
    // private EnemyArchetype archetype;

    [Header("Runtime Names")]
    [SerializeField]
    private EnemyNamePoolConfig namePool;

    [Header("Behavior Scenarios")]
    [Tooltip("Scenario enum and reference to the behavior profile.")]
    [SerializeField]
    private NpcBehaviourScenarioEntry[] behaviorScenarios =
        new NpcBehaviourScenarioEntry[0];

    [Header("Weapon Groups")]
    [SerializeField]
    private WeaponGroupConfig[] weaponGroups =
        new WeaponGroupConfig[0];

    [Header("Legacy Weapons - Migration Fallback")]
    [FormerlySerializedAs("weaponConfigs")]
    [SerializeField]
    [HideInInspector]
    private WeaponConfig[] legacyWeaponConfigs =
        new WeaponConfig[0];

    [Header("Rewards Range")]
    [FormerlySerializedAs("creditReward")]
    [SerializeField]
    private int creditRewardMin = 0;

    [SerializeField]
    private int creditRewardMax = 0;

    [FormerlySerializedAs("xpReward")]
    [SerializeField]
    private int xpRewardMin = 0;

    [SerializeField]
    private int xpRewardMax = 0;

    [SerializeField]
    [Range(1, 5)]
    private int dangerTier = 1;

    [Header("Visuals")]
    [SerializeField]
    private Sprite combatSprite;

    [SerializeField]
    [Min(0f)]
    private float visualSize = 48f;

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
    public float BaseSpeed => baseSpeedMin;

    public int Level => level;
    // public EnemyArchetype AiArchetype => archetype;
    public EnemyNamePoolConfig NamePool => namePool;

    public IReadOnlyList<NpcBehaviourScenarioEntry> BehaviorScenarios =>
        behaviorScenarios;

    public IReadOnlyList<WeaponGroupConfig> WeaponGroups =>
        weaponGroups;

    public IReadOnlyList<WeaponConfig> WeaponConfigs =>
        GetFirstValidWeaponGroupWeaponsOrLegacy();

    public WeaponConfig WeaponConfig
    {
        get
        {
            IReadOnlyList<WeaponConfig> weapons =
                GetFirstValidWeaponGroupWeaponsOrLegacy();

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

    public int CreditRewardMin => creditRewardMin;
    public int CreditRewardMax => creditRewardMax;
    public int XpRewardMin => xpRewardMin;
    public int XpRewardMax => xpRewardMax;

    public int CreditReward => creditRewardMin;
    public int XpReward => xpRewardMin;
    public int DangerTier => dangerTier;
    public Sprite CombatSprite => combatSprite;
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

            if (count > 0)
                return count;

            if (legacyWeaponConfigs == null)
                return 0;

            for (int i = 0; i < legacyWeaponConfigs.Length; i++)
            {
                if (legacyWeaponConfigs[i] != null)
                    count++;
            }

            return count;
        }
    }

    private IReadOnlyList<WeaponConfig>
        GetFirstValidWeaponGroupWeaponsOrLegacy()
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

        if (legacyWeaponConfigs != null)
            return legacyWeaponConfigs;

        return EmptyWeaponConfigs;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (behaviorScenarios == null)
            behaviorScenarios = new NpcBehaviourScenarioEntry[0];

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            if (behaviorScenarios[i] == null)
                behaviorScenarios[i] = new NpcBehaviourScenarioEntry();
        }

        if (weaponGroups == null)
            weaponGroups = new WeaponGroupConfig[0];

        if (legacyWeaponConfigs == null)
            legacyWeaponConfigs = new WeaponConfig[0];

        baseHullMin = Mathf.Max(1, baseHullMin);
        baseHullMax = Mathf.Max(baseHullMin, baseHullMax);

        baseShieldMin = Mathf.Max(0, baseShieldMin);
        baseShieldMax = Mathf.Max(baseShieldMin, baseShieldMax);

        baseEnergyMin = Mathf.Max(0, baseEnergyMin);
        baseEnergyMax = Mathf.Max(baseEnergyMin, baseEnergyMax);

        baseSpeedMin = Mathf.Max(0f, baseSpeedMin);
        baseSpeedMax = Mathf.Max(baseSpeedMin, baseSpeedMax);

        level = Mathf.Clamp(level, 1, 10);

        creditRewardMin = Mathf.Max(0, creditRewardMin);
        creditRewardMax = Mathf.Max(creditRewardMin, creditRewardMax);

        xpRewardMin = Mathf.Max(0, xpRewardMin);
        xpRewardMax = Mathf.Max(xpRewardMin, xpRewardMax);

        dangerTier = Mathf.Clamp(dangerTier, 1, 5);
    }
#endif
}
