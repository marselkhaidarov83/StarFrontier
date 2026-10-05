using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AllySpawnRuleConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/AllySpawnRules";

    private const string AllyConfigRoot =
        "Assets/_Project/Content/Configs/Ally";

    [MenuItem("STAR FRONTIER/Content/06. Ally Spawn Rules/Create generated AllySpawnRuleConfig assets")]
    public static void CreateGeneratedAllySpawnRules()
    {
        EnsureFolder(OutputRoot);

        Dictionary<AllyKey, AllyConfig> alliesByRoleAndLevel =
            LoadAlliesByRoleAndLevel();

        SpawnRuleProfile[] profiles =
            BuildProfiles();

        int createdOrUpdated = 0;
        int warnings = 0;
        int boundAllies = 0;
        int errors = 0;

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
                alliesByRoleAndLevel,
                out profileWarnings,
                out profileBoundAllies);

            warnings += profileWarnings;
            boundAllies += profileBoundAllies;

            if (!ValidateRuleInMemory(asset, path))
                errors++;

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
            $"Warnings: {warnings}. " +
            $"Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/06. Ally Spawn Rules/Validate generated AllySpawnRuleConfig assets")]
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

            if (!ValidateRuleInMemory(rule, path))
                errors++;
        }

        EditorUtility.DisplayDialog(
            "AllySpawnRuleConfig validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/06. Ally Spawn Rules/Delete generated AllySpawnRuleConfig assets")]
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
        Dictionary<AllyKey, AllyConfig> alliesByRoleAndLevel,
        out int warnings,
        out int boundAllies)
    {
        warnings = 0;
        boundAllies = 0;

        SetPrivateField(asset, "id", id);
        SetPrivateField(asset, "displayName", profile.DisplayName);
        SetPrivateField(asset, "description", profile.Description);

        AllySpawnLevelEntryConfig[] levelEntries =
            new AllySpawnLevelEntryConfig[10];

        for (int level = 1; level <= 10; level++)
        {
            List<AllyGroupPlan> plans =
                BuildGroupPlans(profile, level);

            AllyGroupEntryConfig[] allies =
                new AllyGroupEntryConfig[plans.Count];

            for (int i = 0; i < plans.Count; i++)
            {
                AllyGroupPlan plan =
                    plans[i];

                AllyConfig allyConfig =
                    FindAllyConfig(
                        alliesByRoleAndLevel,
                        plan.Role,
                        level);

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

                AllyGroupEntryConfig allyEntry =
                    new AllyGroupEntryConfig();

                SetPrivateField(allyEntry, "allyConfig", allyConfig);
                SetPrivateField(allyEntry, "minCount", plan.MinCount);
                SetPrivateField(allyEntry, "maxCount", plan.MaxCount);

                allies[i] = allyEntry;
            }

            AllySpawnLevelEntryConfig levelEntry =
                new AllySpawnLevelEntryConfig();

            SetPrivateField(levelEntry, "galaxyLevel", level);
            SetPrivateField(levelEntry, "spawnIntervalSeconds", BuildSpawnIntervalSeconds(level));
            SetPrivateField(levelEntry, "offlineSpawnIntervalHours", BuildOfflineSpawnIntervalHours(level));
            SetPrivateField(levelEntry, "allies", allies);

            levelEntries[level - 1] = levelEntry;
        }

        SetPrivateField(asset, "levelEntries", levelEntries);
    }

    private static bool ValidateRuleInMemory(
        AllySpawnRuleConfig rule,
        string path)
    {
        bool isValid = true;

        IReadOnlyList<AllySpawnLevelEntryConfig> entries =
            rule.LevelEntries;

        if (entries == null || entries.Count != 10)
        {
            Debug.LogError(
                $"AllySpawnRule validation: expected exactly 10 level entries at {path}",
                rule);
            return false;
        }

        for (int level = 1; level <= 10; level++)
        {
            AllySpawnLevelEntryConfig levelEntry =
                rule.GetEntryForGalaxyLevel(level);

            if (levelEntry == null)
            {
                Debug.LogError(
                    $"AllySpawnRule validation: missing level {level} at {path}",
                    rule);
                isValid = false;
                continue;
            }

            if (!levelEntry.HasValidAllies())
            {
                Debug.LogError(
                    $"AllySpawnRule validation: no valid allies for level {level} at {path}",
                    rule);
                isValid = false;
            }

            if (!HasEveryRole(levelEntry))
            {
                Debug.LogError(
                    $"AllySpawnRule validation: level {level} must include Ranger, Military, Trader, Science and Medic at {path}",
                    rule);
                isValid = false;
            }

            int maxCount =
                levelEntry.GetMaxAllyCount();

            if (level == 1 && maxCount != 5)
            {
                Debug.LogError(
                    $"AllySpawnRule validation: L01 max count must be 5 so every ally type is present, actual {maxCount} at {path}",
                    rule);
                isValid = false;
            }

            if (level == 10 && maxCount != 30)
            {
                Debug.LogError(
                    $"AllySpawnRule validation: L10 max count must be 30, actual {maxCount} at {path}",
                    rule);
                isValid = false;
            }
        }

        return isValid;
    }

    private static bool HasEveryRole(
        AllySpawnLevelEntryConfig levelEntry)
    {
        if (levelEntry == null || levelEntry.Allies == null)
            return false;

        HashSet<AllyRole2A> roles =
            new HashSet<AllyRole2A>();

        IReadOnlyList<AllyGroupEntryConfig> allies =
            levelEntry.Allies;

        for (int i = 0; i < allies.Count; i++)
        {
            AllyGroupEntryConfig entry =
                allies[i];

            if (entry == null || entry.AllyConfig == null)
                continue;

            roles.Add(entry.AllyConfig.Role);
        }

        AllyRole2A[] expectedRoles =
            GetRoles();

        for (int i = 0; i < expectedRoles.Length; i++)
        {
            if (!roles.Contains(expectedRoles[i]))
                return false;
        }

        return true;
    }

    private static Dictionary<AllyKey, AllyConfig> LoadAlliesByRoleAndLevel()
    {
        Dictionary<AllyKey, AllyConfig> result =
            new Dictionary<AllyKey, AllyConfig>();

        string[] guids =
            AssetDatabase.FindAssets("t:AllyConfig", new[] { AllyConfigRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            AllyConfig config =
                AssetDatabase.LoadAssetAtPath<AllyConfig>(path);

            if (config == null)
                continue;

            AllyRole2A role;
            int level;

            if (!TryParseRoleAndLevelFromPath(path, out role, out level))
            {
                if (!IsGeneratedAllyRole(config.Role))
                    continue;

                role = config.Role;
                level = Mathf.Clamp(config.Level, 1, 10);
            }

            AddAllyConfig(
                result,
                role,
                level,
                config,
                path);
        }

        return result;
    }

    private static void AddAllyConfig(
        Dictionary<AllyKey, AllyConfig> alliesByRoleAndLevel,
        AllyRole2A role,
        int level,
        AllyConfig config,
        string path)
    {
        AllyKey key =
            new AllyKey(role, Mathf.Clamp(level, 1, 10));

        if (alliesByRoleAndLevel.ContainsKey(key))
        {
            Debug.LogWarning(
                $"AllySpawnRule generator: duplicate AllyConfig for {role} L{level:00}. Keeping first one. Duplicate path: {path}",
                config);
            return;
        }

        alliesByRoleAndLevel[key] = config;
    }

    private static bool TryParseRoleAndLevelFromPath(
        string path,
        out AllyRole2A role,
        out int level)
    {
        role = AllyRole2A.Ranger;
        level = 1;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string lowerPath =
            path.Replace("\\", "/").ToLowerInvariant();

        bool hasRole = false;

        if (lowerPath.Contains("/ranger/") || lowerPath.Contains("_ranger_"))
        {
            role = AllyRole2A.Ranger;
            hasRole = true;
        }
        else if (lowerPath.Contains("/military/") || lowerPath.Contains("_military_"))
        {
            role = AllyRole2A.Military;
            hasRole = true;
        }
        else if (lowerPath.Contains("/trader/") || lowerPath.Contains("_trader_"))
        {
            role = AllyRole2A.Trader;
            hasRole = true;
        }
        else if (lowerPath.Contains("/science/") || lowerPath.Contains("_science_"))
        {
            role = AllyRole2A.Science;
            hasRole = true;
        }
        else if (lowerPath.Contains("/medic/") || lowerPath.Contains("_medic_") || lowerPath.Contains("_medical_"))
        {
            role = AllyRole2A.Medic;
            hasRole = true;
        }

        if (!hasRole)
            return false;

        for (int candidate = 1; candidate <= 10; candidate++)
        {
            if (!lowerPath.Contains($"l{candidate:00}"))
                continue;

            level = candidate;
            return true;
        }

        return false;
    }

    private static AllyConfig FindAllyConfig(
        Dictionary<AllyKey, AllyConfig> alliesByRoleAndLevel,
        AllyRole2A role,
        int level)
    {
        if (alliesByRoleAndLevel == null)
            return null;

        AllyConfig result;

        if (alliesByRoleAndLevel.TryGetValue(
                new AllyKey(role, Mathf.Clamp(level, 1, 10)),
                out result))
        {
            return result;
        }

        return null;
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

        EnsureEveryRoleHasMaxCount(maxCounts, maxTotal);

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

    private static void EnsureEveryRoleHasMaxCount(
        Dictionary<AllyRole2A, int> maxCounts,
        int maxTotal)
    {
        AllyRole2A[] roles =
            GetRoles();

        if (maxCounts == null || maxTotal < roles.Length)
            return;

        for (int i = 0; i < roles.Length; i++)
        {
            AllyRole2A role =
                roles[i];

            if (!maxCounts.ContainsKey(role))
                maxCounts[role] = 0;
        }

        for (int i = 0; i < roles.Length; i++)
        {
            AllyRole2A role =
                roles[i];

            if (maxCounts[role] > 0)
                continue;

            AllyRole2A donor =
                FindLargestDonorRole(maxCounts, roles);

            if (maxCounts[donor] <= 1)
                break;

            maxCounts[donor]--;
            maxCounts[role] = 1;
        }
    }

    private static AllyRole2A FindLargestDonorRole(
        Dictionary<AllyRole2A, int> counts,
        AllyRole2A[] roles)
    {
        AllyRole2A result =
            roles[0];

        int bestCount =
            counts.TryGetValue(result, out int initialCount)
                ? initialCount
                : 0;

        for (int i = 1; i < roles.Length; i++)
        {
            AllyRole2A role =
                roles[i];

            int count =
                counts.TryGetValue(role, out int roleCount)
                    ? roleCount
                    : 0;

            if (count <= bestCount)
                continue;

            bestCount = count;
            result = role;
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

    private static int BuildMinTotal(int level)
    {
        return Mathf.RoundToInt(
            Mathf.Lerp(3f, 18f, (level - 1) / 9f));
    }

    private static int BuildMaxTotal(int level)
    {
        return Mathf.RoundToInt(
            Mathf.Lerp(5f, 30f, (level - 1) / 9f));
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
                Key = "science",
                DisplayName = "Союзники: научная система",
                Description = "В научной системе научных союзников примерно в 2 раза больше.",
                DominantRole = AllyRole2A.Science
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
            AllyRole2A.Science,
            AllyRole2A.Medic
        };
    }

    private static bool IsGeneratedAllyRole(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Ranger:
            case AllyRole2A.Military:
            case AllyRole2A.Trader:
            case AllyRole2A.Science:
            case AllyRole2A.Medic:
                return true;

            default:
                return false;
        }
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

    private struct AllyKey : IEquatable<AllyKey>
    {
        private readonly AllyRole2A role;
        private readonly int level;

        public AllyKey(AllyRole2A role, int level)
        {
            this.role = role;
            this.level = level;
        }

        public bool Equals(AllyKey other)
        {
            return role == other.role &&
                   level == other.level;
        }

        public override bool Equals(object obj)
        {
            return obj is AllyKey other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)role * 397) ^ level;
            }
        }
    }
}
