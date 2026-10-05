using System;
using UnityEditor;
using UnityEngine;

public static class StarSystemPopulationRuleBinder
{
    private const string StarSystemRoot =
        "Assets/_Project/Content/Configs/StarSystems";

    private const string PopulationRuleRoot =
        "Assets/_Project/Content/Configs/SystemPopulationRules";

    private const string SectorRoot =
        "Assets/_Project/Content/Configs/Sectors";

    [MenuItem("STAR FRONTIER/Content/09. System Population Rules/Bind rules to StarSystemConfig by station")]
    public static void BindRulesToStarSystemsByStation()
    {
        BindRulesToStarSystemsByStationInternal(
            onlyMissing: false,
            validateAfterBind: false);
    }

    [MenuItem("STAR FRONTIER/Content/09. System Population Rules/Repair missing StarSystemConfig rule bindings")]
    public static void RepairMissingStarSystemRuleBindings()
    {
        BindRulesToStarSystemsByStationInternal(
            onlyMissing: true,
            validateAfterBind: true);
    }

    private static void BindRulesToStarSystemsByStationInternal(
        bool onlyMissing,
        bool validateAfterBind)
    {
        RuleSet rules =
            LoadRules();

        if (!rules.HasRequiredRules())
        {
            EditorUtility.DisplayDialog(
                "SystemPopulationRule binder",
                "Required SystemPopulationRule assets were not found. " +
                "Run Create generated SystemPopulationRule assets first.",
                "OK");
            return;
        }

        int updated = 0;
        int unchanged = 0;
        int warnings = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:StarSystemConfig", new[] { StarSystemRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            StarSystemConfig starSystem =
                AssetDatabase.LoadAssetAtPath<StarSystemConfig>(path);

            if (starSystem == null)
                continue;

            SystemPopulationRule rule =
                PickRuleForSystem(
                    starSystem,
                    rules,
                    ref warnings);

            if (rule == null)
                continue;

            SerializedObject serializedObject =
                new SerializedObject(starSystem);

            SerializedProperty ruleProperty =
                serializedObject.FindProperty("systemPopulationRule");

            if (ruleProperty == null)
            {
                warnings++;
                Debug.LogWarning(
                    "StarSystemPopulationRuleBinder: StarSystemConfig.systemPopulationRule field was not found. " +
                    "Use the updated StarSystemConfig.cs first.",
                    starSystem);
                continue;
            }

            if (onlyMissing && ruleProperty.objectReferenceValue != null)
            {
                unchanged++;
                continue;
            }

            if (ruleProperty.objectReferenceValue == rule)
            {
                unchanged++;
                continue;
            }

            ruleProperty.objectReferenceValue = rule;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(starSystem);
            updated++;

            Debug.Log(
                "StarSystemPopulationRuleBinder: bound " +
                rule.Id + " to " + starSystem.Id +
                " by station " + DescribeStation(starSystem));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemPopulationRule binder",
            $"Done. Updated: {updated}. Unchanged: {unchanged}. Warnings: {warnings}.",
            "OK");

        if (validateAfterBind)
            ValidateStarSystemPopulationRuleBindings();
    }

    [MenuItem("STAR FRONTIER/Content/09. System Population Rules/Validate StarSystemConfig population rule bindings")]
    public static void ValidateStarSystemPopulationRuleBindings()
    {
        int checkedAssets = 0;
        int errors = 0;
        int warnings = 0;
        int repaired = 0;

        RuleSet rules =
            LoadRules();

        string[] guids =
            AssetDatabase.FindAssets("t:StarSystemConfig", new[] { StarSystemRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            StarSystemConfig starSystem =
                AssetDatabase.LoadAssetAtPath<StarSystemConfig>(path);

            if (starSystem == null)
                continue;

            checkedAssets++;

            if (starSystem.SystemPopulationRule != null)
                continue;

            SystemPopulationRule expectedRule =
                PickRuleForSystem(
                    starSystem,
                    rules,
                    ref warnings);

            string assignError = string.Empty;

            if (expectedRule != null &&
                TryAssignPopulationRule(
                    starSystem,
                    expectedRule,
                    out assignError))
            {
                repaired++;
                Debug.Log(
                    "StarSystemPopulationRule validation: repaired missing SystemPopulationRule at " +
                    path + " (" + starSystem.Id + "). Assigned: " +
                    expectedRule.Id + ". Station: " + DescribeStation(starSystem),
                    starSystem);
                continue;
            }

            errors++;
            Debug.LogError(
                "StarSystemPopulationRule validation: missing SystemPopulationRule at " +
                path + " (" + starSystem.Id + "). Expected: " +
                (expectedRule != null ? expectedRule.Id : "none") +
                ". Station: " + DescribeStation(starSystem) +
                ". AssignError: " + assignError,
                starSystem);
        }

        if (repaired > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "StarSystemConfig population rule validation",
            $"Checked: {checkedAssets}. Repaired: {repaired}. Errors: {errors}. Warnings: {warnings}.",
            "OK");
    }

    private static bool TryAssignPopulationRule(
        StarSystemConfig starSystem,
        SystemPopulationRule rule,
        out string error)
    {
        error = string.Empty;

        if (starSystem == null)
        {
            error = "StarSystemConfig is null.";
            return false;
        }

        if (rule == null)
        {
            error = "SystemPopulationRule is null.";
            return false;
        }

        SerializedObject serializedObject =
            new SerializedObject(starSystem);

        SerializedProperty ruleProperty =
            serializedObject.FindProperty("systemPopulationRule");

        if (ruleProperty == null)
        {
            error = "StarSystemConfig.systemPopulationRule field was not found.";
            return false;
        }

        ruleProperty.objectReferenceValue = rule;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(starSystem);
        return true;
    }

    private static SystemPopulationRule PickRuleForSystem(
        StarSystemConfig starSystem,
        RuleSet rules,
        ref int warnings)
    {
        bool useNoEnemyRule =
            IsFirstSectorSystem(starSystem);

        if (starSystem == null)
            return PickEnemyModeRule(
                rules.Balanced,
                rules.BalancedNoEnemies,
                useNoEnemyRule);

        StationConfig station =
            starSystem.Station;

        if (station == null)
        {
            warnings++;
            Debug.LogWarning(
                "StarSystemPopulationRuleBinder: system has no StationConfig, using balanced rule: " +
                starSystem.Id,
                starSystem);

            return PickEnemyModeRule(
                rules.Balanced,
                rules.BalancedNoEnemies,
                useNoEnemyRule);
        }

        switch (station.StationType)
        {
            case StationType.Trade:
                return PickEnemyModeRule(
                    rules.Trade,
                    rules.TradeNoEnemies,
                    useNoEnemyRule);

            case StationType.Military:
                return PickEnemyModeRule(
                    rules.Military,
                    rules.MilitaryNoEnemies,
                    useNoEnemyRule);

            case StationType.RangerBase:
                return PickEnemyModeRule(
                    rules.Ranger,
                    rules.RangerNoEnemies,
                    useNoEnemyRule);

            case StationType.Medical:
                return PickEnemyModeRule(
                    rules.Medical,
                    rules.MedicalNoEnemies,
                    useNoEnemyRule);

            case StationType.Science:
                return PickEnemyModeRule(
                    rules.Science,
                    rules.ScienceNoEnemies,
                    useNoEnemyRule);

            default:
                warnings++;
                Debug.LogWarning(
                    "StarSystemPopulationRuleBinder: unsupported station type " +
                    station.StationType + ", using balanced rule: " + starSystem.Id,
                    starSystem);
                return PickEnemyModeRule(
                    rules.Balanced,
                    rules.BalancedNoEnemies,
                    useNoEnemyRule);
        }
    }

    private static RuleSet LoadRules()
    {
        return new RuleSet
        {
            Balanced = FindRule("system_population_rule_balanced_01"),
            Trade = FindRule("system_population_rule_trade_01"),
            Military = FindRule("system_population_rule_military_01"),
            Ranger = FindRule("system_population_rule_ranger_01"),
            Medical = FindRule("system_population_rule_medical_01"),
            Science = FindRule("system_population_rule_science_01"),
            BalancedNoEnemies = FindRule("system_population_rule_balanced_no_enemies_01"),
            TradeNoEnemies = FindRule("system_population_rule_trade_no_enemies_01"),
            MilitaryNoEnemies = FindRule("system_population_rule_military_no_enemies_01"),
            RangerNoEnemies = FindRule("system_population_rule_ranger_no_enemies_01"),
            MedicalNoEnemies = FindRule("system_population_rule_medical_no_enemies_01"),
            ScienceNoEnemies = FindRule("system_population_rule_science_no_enemies_01")
        };
    }

    private static SystemPopulationRule PickEnemyModeRule(
        SystemPopulationRule regularRule,
        SystemPopulationRule noEnemyRule,
        bool useNoEnemyRule)
    {
        if (useNoEnemyRule && noEnemyRule != null)
            return noEnemyRule;

        return regularRule;
    }

    private static bool IsFirstSectorSystem(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return false;

        string[] guids =
            AssetDatabase.FindAssets("t:SectorConfig", new[] { SectorRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            SectorConfig sector =
                AssetDatabase.LoadAssetAtPath<SectorConfig>(path);

            if (sector == null ||
                sector.Systems == null ||
                sector.Order != 1)
            {
                continue;
            }

            for (int systemIndex = 0; systemIndex < sector.Systems.Length; systemIndex++)
            {
                if (sector.Systems[systemIndex] == starSystem)
                    return true;
            }
        }

        return false;
    }

    private static SystemPopulationRule FindRule(
        string id)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:SystemPopulationRule", new[] { PopulationRuleRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            string lowerPath =
                path.Replace("\\", "/").ToLowerInvariant();

            if (!lowerPath.Contains(id.ToLowerInvariant()))
                continue;

            SystemPopulationRule rule =
                AssetDatabase.LoadAssetAtPath<SystemPopulationRule>(path);

            if (rule != null)
                return rule;
        }

        return null;
    }

    private static string DescribeStation(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return "none";

        if (starSystem.Station == null)
            return "none";

        return starSystem.Station.Id + " / " + starSystem.Station.StationType;
    }

    private sealed class RuleSet
    {
        public SystemPopulationRule Balanced;
        public SystemPopulationRule Trade;
        public SystemPopulationRule Military;
        public SystemPopulationRule Ranger;
        public SystemPopulationRule Medical;
        public SystemPopulationRule Science;
        public SystemPopulationRule BalancedNoEnemies;
        public SystemPopulationRule TradeNoEnemies;
        public SystemPopulationRule MilitaryNoEnemies;
        public SystemPopulationRule RangerNoEnemies;
        public SystemPopulationRule MedicalNoEnemies;
        public SystemPopulationRule ScienceNoEnemies;

        public bool HasRequiredRules()
        {
            return Balanced != null &&
                   Trade != null &&
                   Military != null &&
                   Ranger != null &&
                   Medical != null &&
                   Science != null &&
                   BalancedNoEnemies != null &&
                   TradeNoEnemies != null &&
                   MilitaryNoEnemies != null &&
                   RangerNoEnemies != null &&
                   MedicalNoEnemies != null &&
                   ScienceNoEnemies != null;
        }
    }
}
