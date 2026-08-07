using System;
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

    [FormerlySerializedAs("baseSpeed")]
    [SerializeField]
    private float baseSpeedMin = 2f;

    [SerializeField]
    private float baseSpeedMax = 2f;

    [SerializeField]
    [Range(1, 10)]
    private int level = 1;

    [Header("Ally Role")]
    [SerializeField]
    private AllyRole2A role =
        AllyRole2A.Ranger;

    [Header("Behavior Scenarios")]
    [Tooltip(
        "Scenario enum and reference to the behavior profile.")]
    [SerializeField]
    private NpcBehaviourScenarioEntry[] behaviorScenarios =
        new NpcBehaviourScenarioEntry[0];

    [Header("Weapon Groups")]
    [Tooltip(
        "One random group is selected when a real ally NPC is created.")]
    [SerializeField]
    private WeaponGroupConfig[] weaponGroups =
        new WeaponGroupConfig[0];

    [Header("Visuals")]
    [SerializeField]
    private Sprite mapSprite;

    public int BaseHullMin =>
        baseHullMin;

    public int BaseHullMax =>
        baseHullMax;

    public int BaseShieldMin =>
        baseShieldMin;

    public int BaseShieldMax =>
        baseShieldMax;

    public int BaseEnergyMin =>
        baseEnergyMin;

    public int BaseEnergyMax =>
        baseEnergyMax;

    public float BaseSpeedMin =>
        baseSpeedMin;

    public float BaseSpeedMax =>
        baseSpeedMax;

    /*
     * Legacy read-only properties.
     * Оставлены, чтобы старый код/тесты не ломались при компиляции.
     * Реальное создание союзника должно использовать Min/Max.
     */
    public int BaseHull =>
        baseHullMin;

    public int BaseShield =>
        baseShieldMin;

    public int BaseEnergy =>
        baseEnergyMin;

    public float BaseSpeed =>
        baseSpeedMin;

    public int Level =>
        level;

    public AllyRole2A Role =>
        role;

    public IReadOnlyList<NpcBehaviourScenarioEntry>
        BehaviorScenarios =>
        behaviorScenarios;

    public IReadOnlyList<WeaponGroupConfig>
        WeaponGroups =>
        weaponGroups;

    /*
     * Legacy compatibility:
     * раньше AllyConfig отдавал плоский список оружия.
     * Теперь возвращается первая валидная группа, если она есть.
     */
    public IReadOnlyList<WeaponConfig>
        WeaponConfigs =>
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

    public Sprite MapSprite =>
        mapSprite;

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
        NpcBehaviourScenarioConfig behaviorConfig;

        if (!TryGetBehaviorScenario(
                scenario,
                out behaviorConfig))
        {
            return null;
        }

        return behaviorConfig;
    }

    public bool HasBehaviorScenario(
        AllyBehaviourScenario scenario)
    {
        return TryGetBehaviorScenario(
            scenario,
            out _);
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
            if (weaponGroups == null)
                return 0;

            int count = 0;

            for (int i = 0; i < weaponGroups.Length; i++)
            {
                WeaponGroupConfig group =
                    weaponGroups[i];

                if (group == null)
                    continue;

                count += group.WeaponCount;
            }

            return count;
        }
    }

    private IReadOnlyList<WeaponConfig>
        GetFirstValidWeaponGroupWeapons()
    {
        if (weaponGroups == null)
            return EmptyWeaponConfigs;

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

        return EmptyWeaponConfigs;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (behaviorScenarios == null)
        {
            behaviorScenarios =
                new NpcBehaviourScenarioEntry[0];
        }

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            if (behaviorScenarios[i] == null)
            {
                behaviorScenarios[i] =
                    new NpcBehaviourScenarioEntry();
            }
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

        baseSpeedMin = Mathf.Max(0f, baseSpeedMin);
        baseSpeedMax = Mathf.Max(baseSpeedMin, baseSpeedMax);

        level = Mathf.Clamp(level, 1, 10);
    }
#endif
}