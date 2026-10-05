using System;
using System.Reflection;
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

        int created = 0;
        int updated = 0;
        int warnings = 0;
        int errors = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                PopulationRuleProfile profile =
                    profiles[i];

                string id =
                    $"system_population_rule_{profile.Key}_01";

                string path =
                    $"{OutputRoot}/{id}.asset";

                SystemPopulationRule asset =
                    FindExistingPopulationRule(id, path);

                if (asset == null)
                {
                    asset =
                        ScriptableObject.CreateInstance<SystemPopulationRule>();

                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    string currentPath =
                        AssetDatabase.GetAssetPath(asset);

                    if (!string.Equals(
                            currentPath,
                            path,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        string moveError =
                            AssetDatabase.MoveAsset(currentPath, path);

                        if (!string.IsNullOrWhiteSpace(moveError))
                        {
                            throw new InvalidOperationException(
                                $"Не удалось переместить существующий SystemPopulationRule {id}: {moveError}");
                        }
                    }

                    updated++;
                }

                warnings += WritePopulationRule(
                    asset,
                    id,
                    profile);

                EditorUtility.SetDirty(asset);

                if (!ValidateRuleInMemory(asset, path))
                    errors++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemPopulationRule generator",
            $"Done. Created: {created}. Updated: {updated}. Warnings: {warnings}. Errors: {errors}.",
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

            if (!ValidateRuleInMemory(rule, path))
                errors++;
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

        int deleted =
            DeleteGeneratedPopulationRulesWithoutDialog();

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

        SetPrivateField(asset, "id", id);
        SetPrivateField(asset, "displayName", profile.DisplayName);
        SetPrivateField(asset, "description", profile.Description);

        AllySpawnRuleConfig allyRule =
            FindAllySpawnRule(profile.AllyRuleKey);

        if (allyRule == null)
        {
            warnings++;
            Debug.LogWarning(
                $"SystemPopulationRule generator: missing AllySpawnRuleConfig '{profile.AllyRuleKey}'.");
        }

        SetPrivateField(
            asset,
            "allySpawnRules",
            new[] { allyRule });

        if (!profile.HasEnemies)
        {
            SetPrivateField(
                asset,
                "enemyGroupSpawnRuleEntries",
                Array.Empty<SystemPopulationEnemyGroupRuleEntry>());

            return warnings;
        }

        EnemyGroupSpawnRuleConfig enemyRule =
            FindEnemyGroupSpawnRule(profile.EnemyGroupRuleKey);

        if (enemyRule == null)
        {
            warnings++;
            Debug.LogWarning(
                $"SystemPopulationRule generator: missing EnemyGroupSpawnRuleConfig '{profile.EnemyGroupRuleKey}' for {id}.");
        }

        SystemPopulationEnemyGroupRuleEntry enemyEntry =
            new SystemPopulationEnemyGroupRuleEntry();

        SetPrivateField(enemyEntry, "enemyGroupSpawnRule", enemyRule);
        SetPrivateField(enemyEntry, "weight", 1);

        SetPrivateField(
            asset,
            "enemyGroupSpawnRuleEntries",
            new[] { enemyEntry });

        return warnings;
    }

    private static bool ValidateRuleInMemory(
        SystemPopulationRule rule,
        string path)
    {
        bool isValid = true;

        if (rule.AllySpawnRules == null ||
            rule.AllySpawnRules.Length != 1 ||
            rule.AllySpawnRules[0] == null)
        {
            Debug.LogError(
                $"SystemPopulationRule validation: exactly one ally spawn rule is required at {path}",
                rule);
            isValid = false;
        }

        if (rule.EnemyGroupSpawnRuleEntries == null ||
            rule.EnemyGroupSpawnRuleEntries.Length == 0)
        {
            return isValid;
        }

        if (rule.EnemyGroupSpawnRuleEntries.Length != 1)
        {
            Debug.LogError(
                $"SystemPopulationRule validation: zero or one enemy group spawn rule entry is required at {path}",
                rule);
            return false;
        }

        SystemPopulationEnemyGroupRuleEntry enemyEntry =
            rule.EnemyGroupSpawnRuleEntries[0];

        if (enemyEntry == null || !enemyEntry.IsValid())
        {
            Debug.LogError(
                $"SystemPopulationRule validation: missing or invalid enemy group spawn rule entry at {path}",
                rule);
            isValid = false;
        }
        else if (enemyEntry.Weight != 1)
        {
            Debug.LogError(
                $"SystemPopulationRule validation: enemy group spawn rule entry weight must be 1 at {path}",
                rule);
            isValid = false;
        }

        return isValid;
    }

    private static SystemPopulationRule FindExistingPopulationRule(
        string id,
        string path)
    {
        SystemPopulationRule asset =
            AssetDatabase.LoadAssetAtPath<SystemPopulationRule>(path);

        if (asset != null)
            return asset;

        string[] guids =
            AssetDatabase.FindAssets("t:SystemPopulationRule", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (string.IsNullOrWhiteSpace(candidatePath))
                continue;

            SystemPopulationRule candidate =
                AssetDatabase.LoadAssetAtPath<SystemPopulationRule>(candidatePath);

            if (candidate == null)
                continue;

            SerializedObject serialized =
                new SerializedObject(candidate);

            SerializedProperty idProperty =
                serialized.FindProperty("id");

            if (idProperty == null)
                continue;

            if (string.Equals(
                    idProperty.stringValue,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
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
        if (string.IsNullOrEmpty(path) ||
            string.IsNullOrEmpty(id))
        {
            return false;
        }

        string lowerPath =
            path.Replace("\\", "/").ToLowerInvariant();

        return lowerPath.Contains(id.ToLowerInvariant());
    }

    private static PopulationRuleProfile[] BuildProfiles()
    {
        PopulationRuleProfile[] enemyProfiles =
        {
            new PopulationRuleProfile
            {
                Key = "balanced",
                DisplayName = "Популяция системы: сбалансированная",
                Description = "Сбалансированные союзники и сбалансированная вражеская группа.",
                AllyRuleKey = "ally_spawn_balanced_01",
                EnemyGroupRuleKey = "enemy_group_spawn_balanced_01",
                HasEnemies = true
            },
            new PopulationRuleProfile
            {
                Key = "trade",
                DisplayName = "Популяция системы: торговцы",
                Description = "В системе превалируют торговцы. Враги используют сбалансированную группу.",
                AllyRuleKey = "ally_spawn_trade_01",
                EnemyGroupRuleKey = "enemy_group_spawn_balanced_01",
                HasEnemies = true
            },
            new PopulationRuleProfile
            {
                Key = "military",
                DisplayName = "Популяция системы: военные",
                Description = "В системе превалируют военные союзники. Основная вражеская группа: Древние.",
                AllyRuleKey = "ally_spawn_military_01",
                EnemyGroupRuleKey = "enemy_group_spawn_ancients_01",
                HasEnemies = true
            },
            new PopulationRuleProfile
            {
                Key = "ranger",
                DisplayName = "Популяция системы: рейнджеры",
                Description = "В системе превалируют рейнджеры. Враги используют сбалансированную группу.",
                AllyRuleKey = "ally_spawn_ranger_01",
                EnemyGroupRuleKey = "enemy_group_spawn_balanced_01",
                HasEnemies = true
            },
            new PopulationRuleProfile
            {
                Key = "medical",
                DisplayName = "Популяция системы: медики",
                Description = "В системе превалируют медики. Основная вражеская группа: Зараженные.",
                AllyRuleKey = "ally_spawn_medical_01",
                EnemyGroupRuleKey = "enemy_group_spawn_infected_01",
                HasEnemies = true
            },
            new PopulationRuleProfile
            {
                Key = "science",
                DisplayName = "Популяция системы: научная база",
                Description = "В системе превалируют научные союзники. Основная вражеская группа: ИИ.",
                AllyRuleKey = "ally_spawn_science_01",
                EnemyGroupRuleKey = "enemy_group_spawn_ai_01",
                HasEnemies = true
            }
        };

        PopulationRuleProfile[] noEnemyProfiles =
            new PopulationRuleProfile[enemyProfiles.Length];

        for (int i = 0; i < enemyProfiles.Length; i++)
        {
            PopulationRuleProfile source =
                enemyProfiles[i];

            noEnemyProfiles[i] =
                new PopulationRuleProfile
                {
                    Key = source.Key + "_no_enemies",
                    DisplayName = source.DisplayName + " без врагов",
                    Description = source.Description + " Вражеские группы не создаются.",
                    AllyRuleKey = source.AllyRuleKey,
                    EnemyGroupRuleKey = string.Empty,
                    HasEnemies = false
                };
        }

        PopulationRuleProfile[] result =
            new PopulationRuleProfile[enemyProfiles.Length + noEnemyProfiles.Length];

        Array.Copy(
            enemyProfiles,
            0,
            result,
            0,
            enemyProfiles.Length);

        Array.Copy(
            noEnemyProfiles,
            0,
            result,
            enemyProfiles.Length,
            noEnemyProfiles.Length);

        return result;
    }

    private static int DeleteGeneratedPopulationRulesWithoutDialog()
    {
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

        return deleted;
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

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        Type type =
            target.GetType();

        while (type != null)
        {
            FieldInfo field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            type = type.BaseType;
        }

        throw new InvalidOperationException(
            $"Serialized field not found: {fieldName} on {target.GetType().Name}");
    }

    private sealed class PopulationRuleProfile
    {
        public string Key;
        public string DisplayName;
        public string Description;
        public string AllyRuleKey;
        public string EnemyGroupRuleKey;
        public bool HasEnemies;
    }
}
