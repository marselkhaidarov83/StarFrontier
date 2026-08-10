using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EnemyGroupSpawnRuleConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/EnemyGroupSpawnRules";

    private const string EnemyConfigRoot =
        "Assets/_Project/Content/Configs/Enemies";

    [MenuItem("STAR FRONTIER/Content/07. Enemy Group Spawn Rules/Create generated EnemyGroupSpawnRuleConfig assets")]
    public static void CreateGeneratedEnemyGroupSpawnRules()
    {
        EnsureFolder(OutputRoot);

        SpawnRuleProfile[] profiles =
            BuildProfiles();

        int createdOrUpdated = 0;
        int warnings = 0;
        int boundEnemies = 0;

        for (int i = 0; i < profiles.Length; i++)
        {
            SpawnRuleProfile profile =
                profiles[i];

            string id =
                $"enemy_group_spawn_{profile.Key}_01";

            string path =
                $"{OutputRoot}/{id}.asset";

            EnemyGroupSpawnRuleConfig asset =
                AssetDatabase.LoadAssetAtPath<EnemyGroupSpawnRuleConfig>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyGroupSpawnRuleConfig>();
                AssetDatabase.CreateAsset(asset, path);
            }

            int profileWarnings;
            int profileBoundEnemies;

            WriteSpawnRule(
                asset,
                id,
                profile,
                out profileWarnings,
                out profileBoundEnemies);

            warnings += profileWarnings;
            boundEnemies += profileBoundEnemies;

            EditorUtility.SetDirty(asset);
            createdOrUpdated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemyGroupSpawnRuleConfig generator",
            "Done. " +
            $"Spawn rules: {createdOrUpdated}. " +
            $"Bound enemy entries: {boundEnemies}. " +
            $"Warnings: {warnings}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/07. Enemy Group Spawn Rules/Validate generated EnemyGroupSpawnRuleConfig assets")]
    public static void ValidateGeneratedEnemyGroupSpawnRules()
    {
        int checkedAssets = 0;
        int errors = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:EnemyGroupSpawnRuleConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedSpawnRulePath(path))
                continue;

            EnemyGroupSpawnRuleConfig rule =
                AssetDatabase.LoadAssetAtPath<EnemyGroupSpawnRuleConfig>(path);

            if (rule == null)
                continue;

            checkedAssets++;

            for (int level = 1; level <= 10; level++)
            {
                EnemyGroupSpawnLevelEntryConfig levelEntry =
                    rule.GetEntryForGalaxyLevel(level);

                if (levelEntry == null)
                {
                    errors++;
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: missing level {level} at {path}",
                        rule);
                    continue;
                }

                if (!levelEntry.HasValidEnemies())
                {
                    errors++;
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: no valid enemies for level {level} at {path}",
                        rule);
                }

                int maxCount =
                    levelEntry.GetMaxEnemyCount();

                if (level == 1 && (maxCount < 2 || maxCount > 4))
                {
                    errors++;
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: L01 max count must be 2-4, actual {maxCount} at {path}",
                        rule);
                }

                if (level == 10 && maxCount != 20)
                {
                    errors++;
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: L10 max count must be 20, actual {maxCount} at {path}",
                        rule);
                }

                IReadOnlyList<EnemyGroupEntryConfig> enemies =
                    levelEntry.Enemies;

                for (int e = 0; e < enemies.Count; e++)
                {
                    EnemyGroupEntryConfig enemyEntry =
                        enemies[e];

                    if (enemyEntry == null || enemyEntry.EnemyConfig == null)
                        continue;

                    if (enemyEntry.EnemyConfig.Level == level)
                        continue;

                    errors++;
                    Debug.LogError(
                        "EnemyGroupSpawnRule validation: enemy config level mismatch. " +
                        $"Rule level: {level}, enemy: {enemyEntry.EnemyConfig.Id}, " +
                        $"enemy level: {enemyEntry.EnemyConfig.Level}, path: {path}",
                        rule);
                }
            }
        }

        EditorUtility.DisplayDialog(
            "EnemyGroupSpawnRuleConfig validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/07. Enemy Group Spawn Rules/Delete generated EnemyGroupSpawnRuleConfig assets")]
    public static void DeleteGeneratedEnemyGroupSpawnRules()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated EnemyGroupSpawnRuleConfig assets",
                $"Delete generated EnemyGroupSpawnRuleConfig assets inside:\n{OutputRoot}",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:EnemyGroupSpawnRuleConfig", new[] { OutputRoot });

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
            "EnemyGroupSpawnRuleConfig generator",
            $"Deleted: {deleted}.",
            "OK");
    }

    private static void WriteSpawnRule(
        EnemyGroupSpawnRuleConfig asset,
        string id,
        SpawnRuleProfile profile,
        out int warnings,
        out int boundEnemies)
    {
        warnings = 0;
        boundEnemies = 0;

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SetString(serializedObject, "id", id);
        SetString(serializedObject, "displayName", profile.DisplayName);
        SetString(serializedObject, "description", profile.Description);

        SerializedProperty levelEntriesProperty =
            serializedObject.FindProperty("levelEntries");

        if (levelEntriesProperty == null || !levelEntriesProperty.isArray)
            throw new InvalidOperationException("EnemyGroupSpawnRuleConfig.levelEntries field was not found.");

        levelEntriesProperty.arraySize = 10;

        for (int level = 1; level <= 10; level++)
        {
            SerializedProperty levelEntryProperty =
                levelEntriesProperty.GetArrayElementAtIndex(level - 1);

            SerializedProperty galaxyLevelProperty =
                levelEntryProperty.FindPropertyRelative("galaxyLevel");

            SerializedProperty intervalProperty =
                levelEntryProperty.FindPropertyRelative("spawnIntervalSeconds");

            SerializedProperty maxGroupsProperty =
                levelEntryProperty.FindPropertyRelative("maxAliveGroupsFromThisRule");

            SerializedProperty enemiesProperty =
                levelEntryProperty.FindPropertyRelative("enemies");

            galaxyLevelProperty.intValue = level;
            intervalProperty.floatValue = BuildSpawnIntervalSeconds(level);
            maxGroupsProperty.intValue = 1;

            List<EnemyGroupPlan> plans =
                BuildGroupPlans(profile, level);

            enemiesProperty.arraySize = plans.Count;

            for (int i = 0; i < plans.Count; i++)
            {
                EnemyGroupPlan plan =
                    plans[i];

                EnemyConfig enemyConfig =
                    FindEnemyConfig(plan.Faction, level);

                if (enemyConfig == null)
                {
                    warnings++;
                    Debug.LogWarning(
                        $"EnemyGroupSpawnRule generator: missing EnemyConfig for {plan.Faction} L{level:00}.");
                }
                else
                {
                    boundEnemies++;
                }

                SerializedProperty enemyEntryProperty =
                    enemiesProperty.GetArrayElementAtIndex(i);

                enemyEntryProperty
                    .FindPropertyRelative("enemyConfig")
                    .objectReferenceValue = enemyConfig;

                enemyEntryProperty
                    .FindPropertyRelative("minCount")
                    .intValue = plan.MinCount;

                enemyEntryProperty
                    .FindPropertyRelative("maxCount")
                    .intValue = plan.MaxCount;

                enemyEntryProperty
                    .FindPropertyRelative("weight")
                    .intValue = plan.Weight;
            }
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<EnemyGroupPlan> BuildGroupPlans(
        SpawnRuleProfile profile,
        int level)
    {
        int minTotal =
            BuildMinTotal(level);

        int maxTotal =
            BuildMaxTotal(level);

        Dictionary<EnemyFaction2A, int> weights =
            BuildFactionWeights(profile.DominantFaction);

        Dictionary<EnemyFaction2A, int> minCounts =
            DistributeCount(minTotal, weights);

        Dictionary<EnemyFaction2A, int> maxCounts =
            DistributeCount(maxTotal, weights);

        List<EnemyGroupPlan> result =
            new List<EnemyGroupPlan>();

        foreach (EnemyFaction2A faction in GetFactions())
        {
            int maxCount =
                maxCounts.TryGetValue(faction, out int factionMax)
                    ? factionMax
                    : 0;

            if (maxCount <= 0)
                continue;

            int minCount =
                minCounts.TryGetValue(faction, out int factionMin)
                    ? factionMin
                    : 0;

            minCount =
                Mathf.Clamp(minCount, 0, maxCount);

            int weight =
                weights.TryGetValue(faction, out int factionWeight)
                    ? factionWeight
                    : 1;

            result.Add(
                new EnemyGroupPlan
                {
                    Faction = faction,
                    MinCount = minCount,
                    MaxCount = maxCount,
                    Weight = Mathf.Max(1, weight)
                });
        }

        return result;
    }

    private static Dictionary<EnemyFaction2A, int> BuildFactionWeights(
        EnemyFaction2A? dominantFaction)
    {
        Dictionary<EnemyFaction2A, int> result =
            new Dictionary<EnemyFaction2A, int>();

        foreach (EnemyFaction2A faction in GetFactions())
        {
            result[faction] =
                dominantFaction.HasValue &&
                dominantFaction.Value == faction
                    ? 2
                    : 1;
        }

        return result;
    }

    private static Dictionary<EnemyFaction2A, int> DistributeCount(
        int totalCount,
        Dictionary<EnemyFaction2A, int> weights)
    {
        Dictionary<EnemyFaction2A, int> result =
            new Dictionary<EnemyFaction2A, int>();

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

        foreach (KeyValuePair<EnemyFaction2A, int> pair in weights)
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
                    Faction = pair.Key,
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
            EnemyFaction2A faction =
                remainders[index % remainders.Count].Faction;

            result[faction]++;
            remaining--;
            index++;
        }

        return result;
    }

    private static EnemyConfig FindEnemyConfig(
        EnemyFaction2A faction,
        int level)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:EnemyConfig", new[] { EnemyConfigRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            string lowerPath =
                path.Replace("\\", "/").ToLowerInvariant();

            if (!IsMatchingEnemyConfigPath(lowerPath, faction, level))
                continue;

            EnemyConfig config =
                AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);

            if (config != null)
                return config;
        }

        return null;
    }

    private static bool IsMatchingEnemyConfigPath(
        string lowerPath,
        EnemyFaction2A faction,
        int level)
    {
        if (!lowerPath.Contains("/enemies/"))
            return false;

        if (!lowerPath.Contains($"l{level:00}"))
            return false;

        switch (faction)
        {
            case EnemyFaction2A.AI:
                return lowerPath.Contains("/ai/") ||
                       lowerPath.Contains("_ai_");

            case EnemyFaction2A.Ancients:
                return lowerPath.Contains("/ancients/") ||
                       lowerPath.Contains("_ancients_");

            case EnemyFaction2A.Infected:
                return lowerPath.Contains("/infected/") ||
                       lowerPath.Contains("_infected_");

            default:
                return false;
        }
    }

    private static int BuildMinTotal(int level)
    {
        int allyMin =
            Mathf.RoundToInt(
                Mathf.Lerp(3f, 18f, (level - 1) / 9f));

        return Mathf.Max(
            2,
            Mathf.RoundToInt(allyMin * 2f / 3f));
    }

    private static int BuildMaxTotal(int level)
    {
        int allyMax =
            Mathf.RoundToInt(
                Mathf.Lerp(4f, 30f, (level - 1) / 9f));

        return Mathf.Max(
            2,
            Mathf.RoundToInt(allyMax * 2f / 3f));
    }

    private static float BuildSpawnIntervalSeconds(int level)
    {
        return Mathf.Round(
            Mathf.Lerp(180f, 45f, (level - 1) / 9f));
    }

    private static SpawnRuleProfile[] BuildProfiles()
    {
        return new[]
        {
            new SpawnRuleProfile
            {
                Key = "balanced",
                DisplayName = "Враги: сбалансированная система",
                Description = "Равномерное распределение всех типов врагов.",
                DominantFaction = null
            },
            new SpawnRuleProfile
            {
                Key = "ai",
                DisplayName = "Враги: система ИИ",
                Description = "В системе ИИ врагов типа AI примерно в 2 раза больше.",
                DominantFaction = EnemyFaction2A.AI
            },
            new SpawnRuleProfile
            {
                Key = "ancients",
                DisplayName = "Враги: система Древних",
                Description = "В системе Древних врагов Ancients примерно в 2 раза больше.",
                DominantFaction = EnemyFaction2A.Ancients
            },
            new SpawnRuleProfile
            {
                Key = "infected",
                DisplayName = "Враги: зараженная система",
                Description = "В зараженной системе врагов Infected примерно в 2 раза больше.",
                DominantFaction = EnemyFaction2A.Infected
            }
        };
    }

    private static EnemyFaction2A[] GetFactions()
    {
        return new[]
        {
            EnemyFaction2A.AI,
            EnemyFaction2A.Ancients,
            EnemyFaction2A.Infected
        };
    }

    private static bool IsGeneratedSpawnRulePath(
        string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string fileName =
            System.IO.Path.GetFileNameWithoutExtension(path);

        return fileName.StartsWith(
            "enemy_group_spawn_",
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

    private sealed class SpawnRuleProfile
    {
        public string Key;
        public string DisplayName;
        public string Description;
        public EnemyFaction2A? DominantFaction;
    }

    private sealed class EnemyGroupPlan
    {
        public EnemyFaction2A Faction;
        public int MinCount;
        public int MaxCount;
        public int Weight;
    }

    private sealed class RemainderEntry
    {
        public EnemyFaction2A Faction;
        public float Remainder;
    }

    private enum EnemyFaction2A
    {
        AI,
        Ancients,
        Infected
    }
}