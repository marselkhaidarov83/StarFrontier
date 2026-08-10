using System;
using UnityEditor;
using UnityEngine;

public static class SystemPopulationRuleAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/SystemPopulationRules";

    private const string AllySpawnRuleRoot =
        "Assets/_Project/Content/Configs/AllySpawnRules";

    private const string EnemyGroupSpawnRuleRoot =
        "Assets/_Project/Content/Configs/EnemyGroupSpawnRules";

    [MenuItem("STAR FRONTIER/Content/08. System Population Rules/Create generated SystemPopulationRule assets")]
    public static void CreateGeneratedSystemPopulationRules()
    {
        EnsureFolder(OutputRoot);

        PopulationRuleProfile[] profiles =
            BuildProfiles();

        int createdOrUpdated = 0;
        int warnings = 0;

        for (int i = 0; i < profiles.Length; i++)
        {
            PopulationRuleProfile profile =
                profiles[i];

            string id =
                $"system_population_rule_{profile.Key}_01";

            string path =
                $"{OutputRoot}/{id}.asset";

            SystemPopulationRule asset =
                AssetDatabase.LoadAssetAtPath<SystemPopulationRule>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SystemPopulationRule>();
                AssetDatabase.CreateAsset(asset, path);
            }

            warnings += WritePopulationRule(
                asset,
                id,
                profile);

            EditorUtility.SetDirty(asset);
            createdOrUpdated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemPopulationRule generator",
            $"Done. Rules: {createdOrUpdated}. Warnings: {warnings}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/08. System Population Rules/Validate generated SystemPopulationRule assets")]
    public static void ValidateGeneratedSystemPopulationRules()
    {
        int checkedAssets = 0;
        int errors = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:SystemPopulationRule", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedPopulationRulePath(path))
                continue;

            SystemPopulationRule rule =
                AssetDatabase.LoadAssetAtPath<SystemPopulationRule>(path);

            if (rule == null)
                continue;

            checkedAssets++;

            if (rule.AllySpawnRules == null ||
                rule.AllySpawnRules.Length != 1 ||
                rule.AllySpawnRules[0] == null)
            {
                errors++;
                Debug.LogError(
                    $"SystemPopulationRule validation: exactly one ally spawn rule is required at {path}",
                    rule);
            }

            if (rule.EnemyGroupSpawnRuleEntries == null ||
                rule.EnemyGroupSpawnRuleEntries.Length != 3)
            {
                errors++;
                Debug.LogError(
                    $"SystemPopulationRule validation: exactly three weighted enemy group spawn rule entries are required at {path}",
                    rule);
                continue;
            }

            for (int e = 0; e < rule.EnemyGroupSpawnRuleEntries.Length; e++)
            {
                SystemPopulationEnemyGroupRuleEntry entry =
                    rule.EnemyGroupSpawnRuleEntries[e];

                if (entry != null && entry.IsValid())
                    continue;

                errors++;
                Debug.LogError(
                    $"SystemPopulationRule validation: missing or invalid weighted enemy group spawn rule entry {e} at {path}",
                    rule);
            }
        }

        EditorUtility.DisplayDialog(
            "SystemPopulationRule validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/08. System Population Rules/Delete generated SystemPopulationRule assets")]
    public static void DeleteGeneratedSystemPopulationRules()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated SystemPopulationRule assets",
                $"Delete generated SystemPopulationRule assets inside:\n{OutputRoot}",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:SystemPopulationRule", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedPopulationRulePath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemPopulationRule generator",
            $"Deleted: {deleted}.",
            "OK");
    }

    private static int WritePopulationRule(
        SystemPopulationRule asset,
        string id,
        PopulationRuleProfile profile)
    {
        int warnings = 0;

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SetString(serializedObject, "id", id);
        SetString(serializedObject, "displayName", profile.DisplayName);
        SetString(serializedObject, "description", profile.Description);

        AllySpawnRuleConfig allyRule =
            FindAllySpawnRule(profile.AllyRuleKey);

        if (allyRule == null)
        {
            warnings++;
            Debug.LogWarning(
                $"SystemPopulationRule generator: missing AllySpawnRuleConfig '{profile.AllyRuleKey}'.");
        }

        SerializedProperty alliesProperty =
            serializedObject.FindProperty("allySpawnRules");

        if (alliesProperty == null || !alliesProperty.isArray)
            throw new InvalidOperationException("SystemPopulationRule.allySpawnRules field was not found.");

        alliesProperty.arraySize = 1;
        alliesProperty
            .GetArrayElementAtIndex(0)
            .objectReferenceValue = allyRule;

        SerializedProperty enemiesProperty =
            serializedObject.FindProperty("enemyGroupSpawnRuleEntries");

        if (enemiesProperty == null || !enemiesProperty.isArray)
            throw new InvalidOperationException("SystemPopulationRule.enemyGroupSpawnRuleEntries field was not found.");

        EnemyGroupPlan[] enemyPlans =
        {
            new EnemyGroupPlan
            {
                RuleId = "enemy_group_spawn_ai_01",
                Weight = profile.AiWeight
            },
            new EnemyGroupPlan
            {
                RuleId = "enemy_group_spawn_ancients_01",
                Weight = profile.AncientsWeight
            },
            new EnemyGroupPlan
            {
                RuleId = "enemy_group_spawn_infected_01",
                Weight = profile.InfectedWeight
            }
        };

        enemiesProperty.arraySize = enemyPlans.Length;

        for (int i = 0; i < enemyPlans.Length; i++)
        {
            EnemyGroupPlan plan =
                enemyPlans[i];

            EnemyGroupSpawnRuleConfig enemyRule =
                FindEnemyGroupSpawnRule(plan.RuleId);

            if (enemyRule == null)
            {
                warnings++;
                Debug.LogWarning(
                    $"SystemPopulationRule generator: missing EnemyGroupSpawnRuleConfig '{plan.RuleId}' for {id}.");
            }

            SerializedProperty entryProperty =
                enemiesProperty.GetArrayElementAtIndex(i);

            entryProperty
                .FindPropertyRelative("enemyGroupSpawnRule")
                .objectReferenceValue = enemyRule;

            entryProperty
                .FindPropertyRelative("weight")
                .intValue = Mathf.Max(1, plan.Weight);
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        return warnings;
    }

    private static AllySpawnRuleConfig FindAllySpawnRule(
        string id)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:AllySpawnRuleConfig", new[] { AllySpawnRuleRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!PathOrFileNameContainsId(path, id))
                continue;

            AllySpawnRuleConfig rule =
                AssetDatabase.LoadAssetAtPath<AllySpawnRuleConfig>(path);

            if (rule != null)
                return rule;
        }

        return null;
    }

    private static EnemyGroupSpawnRuleConfig FindEnemyGroupSpawnRule(
        string id)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:EnemyGroupSpawnRuleConfig", new[] { EnemyGroupSpawnRuleRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!PathOrFileNameContainsId(path, id))
                continue;

            EnemyGroupSpawnRuleConfig rule =
                AssetDatabase.LoadAssetAtPath<EnemyGroupSpawnRuleConfig>(path);

            if (rule != null)
                return rule;
        }

        return null;
    }

    private static bool PathOrFileNameContainsId(
        string path,
        string id)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string lowerPath =
            path.Replace("\\", "/").ToLowerInvariant();

        return lowerPath.Contains(id.ToLowerInvariant());
    }

    private static PopulationRuleProfile[] BuildProfiles()
    {
        return new[]
        {
            new PopulationRuleProfile
            {
                Key = "balanced",
                DisplayName = "Популяция системы: сбалансированные союзники",
                Description = "Один balanced ally rule и три вражеских group spawn rules.",
                AllyRuleKey = "ally_spawn_balanced_01",
                AiWeight = 1,
                AncientsWeight = 1,
                InfectedWeight = 1
            },
            new PopulationRuleProfile
            {
                Key = "trade",
                DisplayName = "Популяция системы: торговцы",
                Description = "В системе превалируют торговцы.",
                AllyRuleKey = "ally_spawn_trade_01",
                AiWeight = 1,
                AncientsWeight = 1,
                InfectedWeight = 1
            },
            new PopulationRuleProfile
            {
                Key = "military",
                DisplayName = "Популяция системы: военные",
                Description = "В системе превалируют военные союзники, а враги Древние появляются чаще.",
                AllyRuleKey = "ally_spawn_military_01",
                AiWeight = 1,
                AncientsWeight = 2,
                InfectedWeight = 1
            },
            new PopulationRuleProfile
            {
                Key = "ranger",
                DisplayName = "Популяция системы: рейнджеры",
                Description = "В системе превалируют рейнджеры.",
                AllyRuleKey = "ally_spawn_ranger_01",
                AiWeight = 1,
                AncientsWeight = 1,
                InfectedWeight = 1
            },
            new PopulationRuleProfile
            {
                Key = "medical",
                DisplayName = "Популяция системы: медики",
                Description = "В системе превалируют медики, а зараженные враги появляются чаще.",
                AllyRuleKey = "ally_spawn_medical_01",
                AiWeight = 1,
                AncientsWeight = 1,
                InfectedWeight = 2
            },
            new PopulationRuleProfile
            {
                Key = "science",
                DisplayName = "Популяция системы: научная база",
                Description = "В научной системе чаще встречаются научные союзники, а враги ИИ появляются чаще.",
                AllyRuleKey = "ally_spawn_science_01",
                AiWeight = 2,
                AncientsWeight = 1,
                InfectedWeight = 1
            }
        };
    }

    private static bool IsGeneratedPopulationRulePath(
        string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string fileName =
            System.IO.Path.GetFileNameWithoutExtension(path);

        return fileName.StartsWith(
            "system_population_rule_",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureFolder(
        string folder)
    {
        string[] parts =
            folder.Split('/');

        string current =
            parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next =
                current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    private sealed class PopulationRuleProfile
    {
        public string Key;
        public string DisplayName;
        public string Description;
        public string AllyRuleKey;
        public int AiWeight;
        public int AncientsWeight;
        public int InfectedWeight;
    }

    private sealed class EnemyGroupPlan
    {
        public string RuleId;
        public int Weight;
    }
}
