using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AllyConfig",
    menuName = "StarFrontier/Configs/Npc/Ally")]
public sealed class AllyConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private int baseHull = 50;
    [SerializeField] private int baseShield = 20;
    [SerializeField] private int baseEnergy = 50;
    [SerializeField] private float baseSpeed = 2f;
    [SerializeField]
    [Range(1, 10)]
    private int level = 1;

    [Header("Ally Role")]
    [SerializeField] private AllyRole2A role =
        AllyRole2A.Ranger;

    [Header("Behavior Scenarios")]
    [Tooltip(
        "Профили поведения союзника по сценариям. " +
        "ID сценария хранится строкой и может быть расширен.")]
    [SerializeField]
    private AllyBehaviourScenarioConfig[] behaviorScenarios =
        new AllyBehaviourScenarioConfig[0];

    [Header("Weapons")]
    [SerializeField]
    private WeaponConfig[] weaponConfigs =
        new WeaponConfig[0];

    [Header("Visuals")]
    [SerializeField] private Sprite mapSprite;

    public int BaseHull => baseHull;
    public int BaseShield => baseShield;
    public int BaseEnergy => baseEnergy;
    public float BaseSpeed => baseSpeed;
    public int Level => level;

    public AllyRole2A Role => role;

    public IReadOnlyList<AllyBehaviourScenarioConfig>
        BehaviorScenarios =>
        behaviorScenarios;

    public IReadOnlyList<WeaponConfig> WeaponConfigs =>
        weaponConfigs;

    /*
     * Старое свойство оставляем для совместимости.
     * Старый код, который ожидает AllyConfig.WeaponConfig,
     * получает первое непустое оружие из массива.
     */
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

    public Sprite MapSprite => mapSprite;

    public bool TryGetBehaviorScenario(
        string scenarioId,
        out AllyBehaviourScenarioConfig scenario)
    {
        scenario = null;

        if (string.IsNullOrWhiteSpace(scenarioId))
            return false;

        if (behaviorScenarios == null)
            return false;

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            AllyBehaviourScenarioConfig candidate =
                behaviorScenarios[i];

            if (candidate == null)
                continue;

            if (!candidate.IsValid())
                continue;

            if (!candidate.Matches(scenarioId))
                continue;

            scenario = candidate;
            return true;
        }

        return false;
    }

    public AllyBehaviourScenarioConfig GetBehaviorScenario(
        string scenarioId)
    {
        AllyBehaviourScenarioConfig scenario;

        if (!TryGetBehaviorScenario(
                scenarioId,
                out scenario))
        {
            return null;
        }

        return scenario;
    }

    public bool HasBehaviorScenario(string scenarioId)
    {
        return TryGetBehaviorScenario(
            scenarioId,
            out _);
    }

    public bool HasDuplicateBehaviorScenarioIds()
    {
        if (behaviorScenarios == null)
            return false;

        HashSet<string> scenarioIds =
            new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            AllyBehaviourScenarioConfig scenario =
                behaviorScenarios[i];

            if (scenario == null)
                continue;

            if (!scenario.IsValid())
                continue;

            string normalizedId =
                scenario.Id.Trim();

            if (!scenarioIds.Add(normalizedId))
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
        if (weaponConfigs == null)
            weaponConfigs = new WeaponConfig[0];

        if (behaviorScenarios == null)
        {
            behaviorScenarios =
                new AllyBehaviourScenarioConfig[0];
        }

        for (int i = 0; i < behaviorScenarios.Length; i++)
        {
            if (behaviorScenarios[i] == null)
            {
                behaviorScenarios[i] =
                    new AllyBehaviourScenarioConfig();
            }

            behaviorScenarios[i].Validate();
        }

        baseHull = Mathf.Max(1, baseHull);
        baseShield = Mathf.Max(0, baseShield);
        baseEnergy = Mathf.Max(0, baseEnergy);
        baseSpeed = Mathf.Max(0f, baseSpeed);
        level = Mathf.Clamp(level, 1, 10);
    }
#endif
}