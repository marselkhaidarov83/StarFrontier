using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AllyConfig",
    menuName = "StarFrontier/Configs/Npc/Ally")]
public sealed class AllyConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField]
    private int baseHull = 50;

    [SerializeField]
    private int baseShield = 20;

    [SerializeField]
    private int baseEnergy = 50;

    [SerializeField]
    private float baseSpeed = 2f;

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
    private AllyBehaviourScenarioEntry[] behaviorScenarios =
        new AllyBehaviourScenarioEntry[0];

    [Header("Weapons")]
    [SerializeField]
    private WeaponConfig[] weaponConfigs =
        new WeaponConfig[0];

    [Header("Visuals")]
    [SerializeField]
    private Sprite mapSprite;

    public int BaseHull =>
        baseHull;

    public int BaseShield =>
        baseShield;

    public int BaseEnergy =>
        baseEnergy;

    public float BaseSpeed =>
        baseSpeed;

    public int Level =>
        level;

    public AllyRole2A Role =>
        role;

    public IReadOnlyList<AllyBehaviourScenarioEntry>
        BehaviorScenarios =>
        behaviorScenarios;

    public IReadOnlyList<WeaponConfig>
        WeaponConfigs =>
        weaponConfigs;

    public WeaponConfig WeaponConfig
    {
        get
        {
            if (weaponConfigs == null)
                return null;

            for (int i = 0; i < weaponConfigs.Length; i++)
            {
                if (weaponConfigs[i] != null)
                    return weaponConfigs[i];
            }

            return null;
        }
    }

    public Sprite MapSprite =>
        mapSprite;

    public bool TryGetBehaviorScenario(
        AllyBehaviourScenario scenario,
        out AllyBehaviourScenarioConfig behaviorConfig)
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

    public AllyBehaviourScenarioConfig GetBehaviorScenario(
        AllyBehaviourScenario scenario)
    {
        AllyBehaviourScenarioConfig behaviorConfig;

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
        if (behaviorScenarios == null)
        {
            behaviorScenarios =
                new AllyBehaviourScenarioEntry[0];
        }

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            if (behaviorScenarios[i] == null)
            {
                behaviorScenarios[i] =
                    new AllyBehaviourScenarioEntry();
            }
        }

        if (weaponConfigs == null)
            weaponConfigs = new WeaponConfig[0];

        baseHull = Mathf.Max(1, baseHull);
        baseShield = Mathf.Max(0, baseShield);
        baseEnergy = Mathf.Max(0, baseEnergy);
        baseSpeed = Mathf.Max(0f, baseSpeed);
        level = Mathf.Clamp(level, 1, 10);
    }
#endif
}