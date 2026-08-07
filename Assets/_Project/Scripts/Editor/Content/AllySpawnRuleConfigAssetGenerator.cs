using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AllySpawnRuleConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/AllySpawnRules";

    private const string AllyConfigRoot =
        "Assets/_Project/Content/Configs/Ally";

    [MenuItem("STAR FRONTIER/Content/Ally Spawn Rules/Create generated AllySpawnRuleConfig assets")]
    public static void CreateGeneratedAllySpawnRules()
    {
        EnsureFolder(OutputRoot);

        SpawnRuleProfile[] profiles =
            BuildProfiles();

        int createdOrUpdated = 0;
        int warnings = 0;
        int boundAllies = 0;

        for (int i = 0; i < profiles.Length; i++)
        {
            SpawnRuleProfile profile =
                profiles[i];

            string id =
                $"ally_spawn_{profile.Key}_01";

            string path =
                $"{OutputRoot}/{id}.asset";

            AllySpawnRuleConfig asset =
                AssetDatabase.LoadAssetAtPath<AllySpawnRuleConfig>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AllySpawnRuleConfig>();
                AssetDatabase.CreateAsset(asset, path);
            }

            int profileWarnings;
            int profileBoundAllies;

            WriteSpawnRule(
                asset,
                id,
                profile,
                out profileWarnings,
                out profileBoundAllies);

            warnings += profileWarnings;
            boundAllies += profileBoundAllies;

            EditorUtility.SetDirty(asset);
            createdOrUpdated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "AllySpawnRuleConfig generator",
            "Done. " +
            $"Spawn rules: {createdOrUpdated}. " +
            $"Bound ally entries: {boundAllies}. " +
            $"Warnings: {warnings}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/Ally Spawn Rules/Validate generated AllySpawnRuleConfig assets")]
    public static void ValidateGeneratedAllySpawnRules()
    {
        int checkedAssets = 0;
        int errors = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:AllySpawnRuleConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedSpawnRulePath(path))
                continue;

            AllySpawnRuleConfig rule =
                AssetDatabase.LoadAssetAtPath<AllySpawnRuleConfig>(path);

            if (rule == null)
                continue;

            checkedAssets++;

            for (int level = 1; level <= 10; level++)
            {
                AllySpawnLevelEntryConfig levelEntry =
                    rule.GetEntryForGalaxyLevel(level);

                if (levelEntry == null)
                {
                    errors++;
                    Debug.LogError(
                        $"AllySpawnRule validation: missing level {level} at {path}",
                        rule);
                    continue;
                }

                if (!levelEntry.HasValidAllies())
                {
                    errors++;
                    Debug.LogError(
                        $"AllySpawnRule validation: no valid allies for level {level} at {path}",
                        rule);
                }

                int maxCount =
                    levelEntry.GetMaxAllyCount();

                if (level == 1 && (maxCount < 3 || maxCount > 5))
                {
                    errors++;
                    Debug.LogError(
                        $"AllySpawnRule validation: L01 max count must be 3-5, actual {maxCount} at {path}",
                        rule);
                }

                if (level == 10 && maxCount != 30)
                {
                    errors++;
                    Debug.LogError(
                        $"AllySpawnRule validation: L10 max count must be 30, actual {maxCount} at {path}",
                        rule);
                }
            }
        }

        EditorUtility.DisplayDialog(
            "AllySpawnRuleConfig validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/Ally Spawn Rules/Delete generated AllySpawnRuleConfig assets")]
    public static void DeleteGeneratedAllySpawnRules()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated AllySpawnRuleConfig assets",
                $"Delete generated AllySpawnRuleConfig assets inside:\n{OutputRoot}",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:AllySpawnRuleConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedSpawnRulePath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "AllySpawnRuleConfig generator",
            $"Deleted: {deleted}.",
            "OK");
    }

    private static void WriteSpawnRule(
        AllySpawnRuleConfig asset,
        string id,
        SpawnRuleProfile profile,
        out int warnings,
        out int boundAllies)
    {
        warnings = 0;
        boundAllies = 0;

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SetString(serializedObject, "id", id);
        SetString(serializedObject, "displayName", profile.DisplayName);
        SetString(serializedObject, "description", profile.Description);

        SerializedProperty levelEntriesProperty =
            serializedObject.FindProperty("levelEntries");

        if (levelEntriesProperty == null || !levelEntriesProperty.isArray)
            throw new InvalidOperationException("AllySpawnRuleConfig.levelEntries field was not found.");

        levelEntriesProperty.arraySize = 10;

        for (int level = 1; level <= 10; level++)
        {
            SerializedProperty levelEntryProperty =
                levelEntriesProperty.GetArrayElementAtIndex(level - 1);

            SerializedProperty galaxyLevelProperty =
                levelEntryProperty.FindPropertyRelative("galaxyLevel");

            SerializedProperty intervalProperty =
                levelEntryProperty.FindPropertyRelative("spawnIntervalSeconds");

            SerializedProperty offlineIntervalProperty =
                levelEntryProperty.FindPropertyRelative("offlineSpawnIntervalHours");

            SerializedProperty alliesProperty =
                levelEntryProperty.FindPropertyRelative("allies");

            galaxyLevelProperty.intValue = level;
            intervalProperty.floatValue = BuildSpawnIntervalSeconds(level);
            offlineIntervalProperty.floatValue = BuildOfflineSpawnIntervalHours(level);

            List<AllyGroupPlan> plans =
                BuildGroupPlans(profile, level);

            alliesProperty.arraySize = plans.Count;

            for (int i = 0; i < plans.Count; i++)
            {
                AllyGroupPlan plan =
                    plans[i];

                AllyConfig allyConfig =
                    FindAllyConfig(plan.Role, level);

                if (allyConfig == null)
                {
                    warnings++;
                    Debug.LogWarning(
                        $"AllySpawnRule generator: missing AllyConfig for {plan.Role} L{level:00}.");
                }
                else
                {
                    boundAllies++;
                }

                SerializedProperty allyEntryProperty =
                    alliesProperty.GetArrayElementAtIndex(i);

                allyEntryProperty
                    .FindPropertyRelative("allyConfig")
                    .objectReferenceValue = allyConfig;

                allyEntryProperty
                    .FindPropertyRelative("minCount")
                    .intValue = plan.MinCount;

                allyEntryProperty
                    .FindPropertyRelative("maxCount")
                    .intValue = plan.MaxCount;
            }
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<AllyGroupPlan> BuildGroupPlans(
        SpawnRuleProfile profile,
        int level)
    {
        int minTotal =
            BuildMinTotal(level);

        int maxTotal =
            BuildMaxTotal(level);

        Dictionary<AllyRole2A, int> weights =
            BuildRoleWeights(profile.DominantRole);

        Dictionary<AllyRole2A, int> minCounts =
            DistributeCount(minTotal, weights);

        Dictionary<AllyRole2A, int> maxCounts =
            DistributeCount(maxTotal, weights);

        List<AllyGroupPlan> result =
            new List<AllyGroupPlan>();

        foreach (AllyRole2A role in GetRoles())
        {
            int maxCount =
                maxCounts.TryGetValue(role, out int roleMax)
                    ? roleMax
                    : 0;

            if (maxCount <= 0)
                continue;

            int minCount =
                minCounts.TryGetValue(role, out int roleMin)
                    ? roleMin
                    : 0;

            minCount =
                Mathf.Clamp(minCount, 0, maxCount);

            result.Add(
                new AllyGroupPlan
                {
                    Role = role,
                    MinCount = minCount,
                    MaxCount = maxCount
                });
        }

        return result;
    }

    private static Dictionary<AllyRole2A, int> BuildRoleWeights(
        AllyRole2A? dominantRole)
    {
        Dictionary<AllyRole2A, int> result =
            new Dictionary<AllyRole2A, int>();

        foreach (AllyRole2A role in GetRoles())
        {
            result[role] =
                dominantRole.HasValue &&
                dominantRole.Value == role
                    ? 2
                    : 1;
        }

        return result;
    }

    private static Dictionary<AllyRole2A, int> DistributeCount(
        int totalCount,
        Dictionary<AllyRole2A, int> weights)
    {
        Dictionary<AllyRole2A, int> result =
            new Dictionary<AllyRole2A, int>();

        if (weights == null || weights.Count == 0)
            return result;

        int totalWeight = 0;

        foreach (int weight in weights.Values)
            totalWeight += Mathf.Max(0, weight);

        if (totalWeight <= 0)
            return result;

        int assigned = 0;
        List<RemainderEntry> remainders =
            new List<RemainderEntry>();

        foreach (KeyValuePair<AllyRole2A, int> pair in weights)
        {
            float raw =
                totalCount * (pair.Value / (float)totalWeight);

            int count =
                Mathf.FloorToInt(raw);

            result[pair.Key] = count;
            assigned += count;

            remainders.Add(
                new RemainderEntry
                {
                    Role = pair.Key,
                    Remainder = raw - count
                });
        }

        remainders.Sort(
            (left, right) =>
                right.Remainder.CompareTo(left.Remainder));

        int remaining =
            totalCount - assigned;

        int index = 0;

        while (remaining > 0 && remainders.Count > 0)
        {
            AllyRole2A role =
                remainders[index % remainders.Count].Role;

            result[role]++;
            remaining--;
            index++;
        }

        return result;
    }

    private static AllyConfig FindAllyConfig(
        AllyRole2A role,
        int level)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:AllyConfig", new[] { AllyConfigRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            string lowerPath =
                path.Replace("\\", "/").ToLowerInvariant();

            if (!IsMatchingAllyConfigPath(lowerPath, role, level))
                continue;

            AllyConfig config =
                AssetDatabase.LoadAssetAtPath<AllyConfig>(path);

            if (config != null)
                return config;
        }

        return null;
    }

    private static bool IsMatchingAllyConfigPath(
        string lowerPath,
        AllyRole2A role,
        int level)
    {
        if (!lowerPath.Contains("/ally/"))
            return false;

        if (!lowerPath.Contains($"l{level:00}"))
            return false;

        switch (role)
        {
            case AllyRole2A.Military:
                return lowerPath.Contains("/military/") ||
                       lowerPath.Contains("_military_");

            case AllyRole2A.Ranger:
                return lowerPath.Contains("/ranger/") ||
                       lowerPath.Contains("_ranger_");

            case AllyRole2A.Trader:
                return lowerPath.Contains("/trader/") ||
                       lowerPath.Contains("_trader_");

            case AllyRole2A.CivilianTransport:
                return lowerPath.Contains("/civiliantransport/") ||
                       lowerPath.Contains("_civilian_transport_") ||
                       lowerPath.Contains("_civilian_");

            case AllyRole2A.Medic:
                return lowerPath.Contains("/medic/") ||
                       lowerPath.Contains("_medic_") ||
                       lowerPath.Contains("_medical_");

            default:
                return false;
        }
    }

    private static int BuildMinTotal(int level)
    {
        return Mathf.RoundToInt(
            Mathf.Lerp(3f, 18f, (level - 1) / 9f));
    }

    private static int BuildMaxTotal(int level)
    {
        return Mathf.RoundToInt(
            Mathf.Lerp(4f, 30f, (level - 1) / 9f));
    }

    private static float BuildSpawnIntervalSeconds(int level)
    {
        return Mathf.Round(
            Mathf.Lerp(180f, 45f, (level - 1) / 9f));
    }

    private static float BuildOfflineSpawnIntervalHours(int level)
    {
        return Mathf.Round(
            Mathf.Lerp(2f, 12f, (level - 1) / 9f) * 10f) / 10f;
    }

    private static SpawnRuleProfile[] BuildProfiles()
    {
        return new[]
        {
            new SpawnRuleProfile
            {
                Key = "balanced",
                DisplayName = "Союзники: сбалансированная система",
                Description = "Равномерное распределение всех типов союзников.",
                DominantRole = null
            },
            new SpawnRuleProfile
            {
                Key = "trade",
                DisplayName = "Союзники: торговая система",
                Description = "В торговой системе торговцев примерно в 2 раза больше.",
                DominantRole = AllyRole2A.Trader
            },
            new SpawnRuleProfile
            {
                Key = "military",
                DisplayName = "Союзники: военная система",
                Description = "В военной системе военных примерно в 2 раза больше.",
                DominantRole = AllyRole2A.Military
            },
            new SpawnRuleProfile
            {
                Key = "ranger",
                DisplayName = "Союзники: рейнджерская система",
                Description = "В рейнджерской системе рейнджеров примерно в 2 раза больше.",
                DominantRole = AllyRole2A.Ranger
            },
            new SpawnRuleProfile
            {
                Key = "medical",
                DisplayName = "Союзники: медицинская система",
                Description = "В медицинской системе медиков примерно в 2 раза больше.",
                DominantRole = AllyRole2A.Medic
            },
            new SpawnRuleProfile
            {
                Key = "transport",
                DisplayName = "Союзники: транспортная система",
                Description = "В транспортной системе гражданского транспорта примерно в 2 раза больше.",
                DominantRole = AllyRole2A.CivilianTransport
            }
        };
    }

    private static AllyRole2A[] GetRoles()
    {
        return new[]
        {
            AllyRole2A.Ranger,
            AllyRole2A.Military,
            AllyRole2A.Trader,
            AllyRole2A.CivilianTransport,
            AllyRole2A.Medic
        };
    }

    private static bool IsGeneratedSpawnRulePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fileName =
            Path.GetFileNameWithoutExtension(path);

        return fileName.StartsWith(
            "ally_spawn_",
            StringComparison.Ordinal);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent =
            Path.GetDirectoryName(path)?.Replace("\\", "/");

        string folder =
            Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(parent) ||
            string.IsNullOrWhiteSpace(folder))
        {
            throw new InvalidOperationException($"Invalid Unity folder path: {path}");
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"Serialized string field not found: {propertyName}");

        property.stringValue = value ?? string.Empty;
    }

    private sealed class SpawnRuleProfile
    {
        public string Key;
        public string DisplayName;
        public string Description;
        public AllyRole2A? DominantRole;
    }

    private struct AllyGroupPlan
    {
        public AllyRole2A Role;
        public int MinCount;
        public int MaxCount;
    }

    private struct RemainderEntry
    {
        public AllyRole2A Role;
        public float Remainder;
    }
}
