using System;
using UnityEditor;
using UnityEngine;

public static class StarSystemPopulationRuleBinder
{
    private const string StarSystemRoot =
        "Assets/_Project/Content/Configs/Galaxy/StarSystems";

    private const string PopulationRuleRoot =
        "Assets/_Project/Content/Configs/Population/SystemPopulationRules";

    private const string EnemySpawnRuleRoot =
        "Assets/_Project/Content/Configs/Population/EnemySpawnRules";

    private const string SectorRoot =
        "Assets/_Project/Content/Configs/Galaxy/Sectors";

    private const string EnemySpawnRuleSetId =
        "enemy_spawn_rule_factions_01";

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

        EnemySpawnRuleSetConfig enemyRuleSet =
            FindEnemySpawnRuleSet(EnemySpawnRuleSetId);

        if (enemyRuleSet == null)
        {
            EditorUtility.DisplayDialog(
                "SystemPopulationRule binder",
                "Required EnemySpawnRuleSetConfig was not found: " +
                EnemySpawnRuleSetId +
                ". Run Create generated EnemySpawnRule assets first.",
                "OK");
            return;
        }

        int populationUpdated = 0;
        int enemyUpdated = 0;
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

            SerializedObject serializedObject =
                new SerializedObject(starSystem);

            bool changed = false;

            SystemPopulationRule populationRule =
                PickRuleForSystem(
                    starSystem,
                    rules,
                    ref warnings);

            string populationError = string.Empty;
            bool populationChanged = false;

            if (populationRule != null)
            {
                if (TryBindObjectReference(
                        serializedObject,
                        "systemPopulationRule",
                        populationRule,
                        onlyMissing,
                        out populationChanged,
                        out populationError))
                {
                    if (populationChanged)
                    {
                        populationUpdated++;
                        changed = true;

                        Debug.Log(
                            "StarSystemPopulationRuleBinder: bound " +
                            populationRule.Id + " to " + starSystem.Id +
                            " by station " + DescribeStation(starSystem),
                            starSystem);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(populationError))
            {
                warnings++;
                Debug.LogWarning(
                    "StarSystemPopulationRuleBinder: " +
                    populationError +
                    " System: " +
                    starSystem.Id,
                    starSystem);
            }

            if (ShouldBindEnemySpawnRuleSet(starSystem))
            {
                string enemyError = string.Empty;
                bool enemyChanged = false;

                if (TryBindObjectReference(
                        serializedObject,
                        "enemySpawnRuleSet",
                        enemyRuleSet,
                        onlyMissing,
                        out enemyChanged,
                        out enemyError))
                {
                    if (enemyChanged)
                    {
                        enemyUpdated++;
                        changed = true;

                        Debug.Log(
                            "StarSystemPopulationRuleBinder: bound " +
                            enemyRuleSet.Id +
                            " to enemySpawnRuleSet of " +
                            starSystem.Id,
                            starSystem);
                    }
                }

                if (!string.IsNullOrWhiteSpace(enemyError))
                {
                    warnings++;
                    Debug.LogWarning(
                        "StarSystemPopulationRuleBinder: " +
                        enemyError +
                        " System: " +
                        starSystem.Id,
                        starSystem);
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(starSystem);
            }
            else
            {
                unchanged++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemPopulationRule binder",
            "Done. " +
            $"Population updated: {populationUpdated}. " +
            $"Enemy updated: {enemyUpdated}. " +
            $"Unchanged: {unchanged}. " +
            $"Warnings: {warnings}.",
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

        EnemySpawnRuleSetConfig enemyRuleSet =
            FindEnemySpawnRuleSet(EnemySpawnRuleSetId);

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

            SystemPopulationRule expectedPopulationRule =
                PickRuleForSystem(
                    starSystem,
                    rules,
                    ref warnings);

            if (starSystem.SystemPopulationRule == null)
            {
                string assignError = string.Empty;

                if (expectedPopulationRule != null &&
                    TryAssignPopulationRule(
                        starSystem,
                        expectedPopulationRule,
                        out assignError))
                {
                    repaired++;

                    Debug.Log(
                        "StarSystemPopulationRule validation: repaired missing SystemPopulationRule at " +
                        path + " (" + starSystem.Id + "). Assigned: " +
                        expectedPopulationRule.Id + ". Station: " + DescribeStation(starSystem),
                        starSystem);
                }
                else
                {
                    errors++;

                    Debug.LogError(
                        "StarSystemPopulationRule validation: missing SystemPopulationRule at " +
                        path + " (" + starSystem.Id + "). Expected: " +
                        (expectedPopulationRule != null ? expectedPopulationRule.Id : "none") +
                        ". Station: " + DescribeStation(starSystem) +
                        ". AssignError: " + assignError,
                        starSystem);
                }
            }

            if (!ShouldBindEnemySpawnRuleSet(starSystem))
                continue;

            if (enemyRuleSet == null)
            {
                errors++;

                Debug.LogError(
                    "StarSystemPopulationRule validation: missing EnemySpawnRuleSetConfig asset " +
                    EnemySpawnRuleSetId +
                    ". Run EnemySpawnRuleAssetGenerator first.",
                    starSystem);
                continue;
            }

            if (starSystem.EnemySpawnRuleSet != null)
                continue;

            string enemyAssignError = string.Empty;

            if (TryAssignEnemySpawnRuleSet(
                    starSystem,
                    enemyRuleSet,
                    out enemyAssignError))
            {
                repaired++;

                Debug.Log(
                    "StarSystemPopulationRule validation: repaired missing EnemySpawnRuleSet at " +
                    path + " (" + starSystem.Id + "). Assigned: " +
                    enemyRuleSet.Id + ".",
                    starSystem);
            }
            else
            {
                errors++;

                Debug.LogError(
                    "StarSystemPopulationRule validation: missing EnemySpawnRuleSet at " +
                    path + " (" + starSystem.Id + "). Expected: " +
                    enemyRuleSet.Id +
                    ". AssignError: " + enemyAssignError,
                    starSystem);
            }
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

    private static bool TryAssignEnemySpawnRuleSet(
        StarSystemConfig starSystem,
        EnemySpawnRuleSetConfig ruleSet,
        out string error)
    {
        error = string.Empty;

        if (starSystem == null)
        {
            error = "StarSystemConfig is null.";
            return false;
        }

        if (ruleSet == null)
        {
            error = "EnemySpawnRuleSetConfig is null.";
            return false;
        }

        SerializedObject serializedObject =
            new SerializedObject(starSystem);

        SerializedProperty ruleProperty =
            serializedObject.FindProperty("enemySpawnRuleSet");

        if (ruleProperty == null)
        {
            error = "StarSystemConfig.enemySpawnRuleSet field was not found.";
            return false;
        }

        ruleProperty.objectReferenceValue = ruleSet;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(starSystem);
        return true;
    }

    private static bool TryBindObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value,
        bool onlyMissing,
        out bool changed,
        out string error)
    {
        changed = false;
        error = string.Empty;

        if (serializedObject == null)
        {
            error = "SerializedObject is null.";
            return false;
        }

        if (value == null)
        {
            error = "Value for " + propertyName + " is null.";
            return false;
        }

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            error = "StarSystemConfig." + propertyName + " field was not found.";
            return false;
        }

        if (onlyMissing && property.objectReferenceValue != null)
            return true;

        if (property.objectReferenceValue == value)
            return true;

        property.objectReferenceValue = value;
        changed = true;
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

    private static bool ShouldBindEnemySpawnRuleSet(
        StarSystemConfig starSystem)
    {
        return GetSectorOrderForSystem(starSystem) >= 2;
    }

    private static bool IsFirstSectorSystem(
        StarSystemConfig starSystem)
    {
        return GetSectorOrderForSystem(starSystem) == 1;
    }

    private static int GetSectorOrderForSystem(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return 0;

        string[] guids =
            AssetDatabase.FindAssets("t:SectorConfig", new[] { SectorRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            SectorConfig sector =
                AssetDatabase.LoadAssetAtPath<SectorConfig>(path);

            if (sector == null ||
                sector.Systems == null)
            {
                continue;
            }

            for (int systemIndex = 0; systemIndex < sector.Systems.Length; systemIndex++)
            {
                if (sector.Systems[systemIndex] == starSystem)
                    return sector.Order;
            }
        }

        return 0;
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

    private static EnemySpawnRuleSetConfig FindEnemySpawnRuleSet(
        string id)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:EnemySpawnRuleSetConfig", new[] { EnemySpawnRuleRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            EnemySpawnRuleSetConfig ruleSet =
                AssetDatabase.LoadAssetAtPath<EnemySpawnRuleSetConfig>(path);

            if (ruleSet == null)
                continue;

            if (string.Equals(
                    ruleSet.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ruleSet;
            }
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