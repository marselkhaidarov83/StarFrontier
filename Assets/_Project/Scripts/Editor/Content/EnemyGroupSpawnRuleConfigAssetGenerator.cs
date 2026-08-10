using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel =
            LoadEnemiesByFactionAndLevel();

        SpawnRuleProfile[] profiles =
            BuildProfiles();

        int created = 0;
        int warnings = 0;
        int boundEnemies = 0;
        int errors = 0;

        for (int i = 0; i < profiles.Length; i++)
        {
            SpawnRuleProfile profile =
                profiles[i];

            string id =
                $"enemy_group_spawn_{profile.Key}_01";

            string path =
                $"{OutputRoot}/{id}.asset";

            if (AssetDatabase.LoadAssetAtPath<EnemyGroupSpawnRuleConfig>(path) != null)
                AssetDatabase.DeleteAsset(path);

            EnemyGroupSpawnRuleConfig asset =
                ScriptableObject.CreateInstance<EnemyGroupSpawnRuleConfig>();

            int profileWarnings;
            int profileBoundEnemies;

            WriteSpawnRule(
                asset,
                id,
                profile,
                enemiesByFactionAndLevel,
                out profileWarnings,
                out profileBoundEnemies);

            warnings += profileWarnings;
            boundEnemies += profileBoundEnemies;

            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            if (!ValidateRuleInMemory(asset, path))
                errors++;

            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemyGroupSpawnRuleConfig generator",
            "Done. " +
            $"Spawn rules: {created}. " +
            $"Bound enemy entries: {boundEnemies}. " +
            $"Warnings: {warnings}. " +
            $"Errors: {errors}.",
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

            if (!ValidateRuleInMemory(rule, path))
                errors++;
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
        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel,
        out int warnings,
        out int boundEnemies)
    {
        warnings = 0;
        boundEnemies = 0;

        SetPrivateField(asset, "id", id);
        SetPrivateField(asset, "displayName", profile.DisplayName);
        SetPrivateField(asset, "description", profile.Description);

        EnemyGroupSpawnLevelEntryConfig[] levelEntries =
            new EnemyGroupSpawnLevelEntryConfig[10];

        for (int level = 1; level <= 10; level++)
        {
            EnemyGroupEntryConfig[] enemies =
                new EnemyGroupEntryConfig[3];

            EnemyFaction2A[] factions =
                GetFactions();

            for (int i = 0; i < factions.Length; i++)
            {
                EnemyFaction2A faction =
                    factions[i];

                EnemyConfig enemyConfig =
                    FindEnemyConfig(
                        enemiesByFactionAndLevel,
                        faction,
                        level);

                if (enemyConfig == null)
                {
                    warnings++;
                    Debug.LogWarning(
                        $"EnemyGroupSpawnRule generator: missing EnemyConfig for {faction} L{level:00}.");
                }
                else
                {
                    boundEnemies++;
                }

                EnemyGroupEntryConfig enemyEntry =
                    new EnemyGroupEntryConfig();

                SetPrivateField(enemyEntry, "enemyConfig", enemyConfig);
                SetPrivateField(enemyEntry, "minCount", BuildMinTotal(level));
                SetPrivateField(enemyEntry, "maxCount", BuildMaxTotal(level));
                SetPrivateField(enemyEntry, "weight", BuildEntryWeight(profile, faction));

                enemies[i] = enemyEntry;
            }

            EnemyGroupSpawnLevelEntryConfig levelEntry =
                new EnemyGroupSpawnLevelEntryConfig();

            SetPrivateField(levelEntry, "galaxyLevel", level);
            SetPrivateField(levelEntry, "spawnIntervalSeconds", BuildSpawnIntervalSeconds(level));
            SetPrivateField(levelEntry, "maxAliveGroupsFromThisRule", 1);
            SetPrivateField(levelEntry, "enemies", enemies);

            levelEntries[level - 1] = levelEntry;
        }

        SetPrivateField(asset, "levelEntries", levelEntries);
    }

    private static int BuildEntryWeight(
        SpawnRuleProfile profile,
        EnemyFaction2A faction)
    {
        if (!profile.DominantFaction.HasValue)
            return 1;

        return profile.DominantFaction.Value == faction
            ? 2
            : 1;
    }

    private static bool ValidateRuleInMemory(
        EnemyGroupSpawnRuleConfig rule,
        string path)
    {
        bool isValid = true;

        IReadOnlyList<EnemyGroupSpawnLevelEntryConfig> entries =
            rule.LevelEntries;

        if (entries == null || entries.Count != 10)
        {
            Debug.LogError(
                $"EnemyGroupSpawnRule validation: expected exactly 10 level entries at {path}",
                rule);
            return false;
        }

        for (int level = 1; level <= 10; level++)
        {
            EnemyGroupSpawnLevelEntryConfig levelEntry =
                rule.GetEntryForGalaxyLevel(level);

            if (levelEntry == null)
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: missing level {level} at {path}",
                    rule);
                isValid = false;
                continue;
            }

            IReadOnlyList<EnemyGroupEntryConfig> enemies =
                levelEntry.Enemies;

            if (enemies == null || enemies.Count != 3)
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: level {level} must contain exactly 3 enemy entries, actual {enemies?.Count ?? 0} at {path}",
                    rule);
                isValid = false;
                continue;
            }

            HashSet<EnemyFaction2A> factions =
                new HashSet<EnemyFaction2A>();

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyGroupEntryConfig entry =
                    enemies[i];

                if (entry == null || entry.EnemyConfig == null)
                {
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: empty enemy entry at level {level}, index {i}, path: {path}",
                        rule);
                    isValid = false;
                    continue;
                }

                if (entry.EnemyConfig.Level != level)
                {
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: level mismatch at {path}. Rule L{level:00}, enemy {entry.EnemyConfig.Id}, enemy L{entry.EnemyConfig.Level:00}.",
                        rule);
                    isValid = false;
                }

                if (TryResolveFaction(entry.EnemyConfig, out EnemyFaction2A faction))
                    factions.Add(faction);

                if (entry.MinCount != BuildMinTotal(level) ||
                    entry.MaxCount != BuildMaxTotal(level))
                {
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: wrong count at {path}, L{level:00}. Expected {BuildMinTotal(level)}-{BuildMaxTotal(level)}, actual {entry.MinCount}-{entry.MaxCount}.",
                        rule);
                    isValid = false;
                }
            }

            if (!ContainsAllFactions(factions))
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: level {level} must contain AI, Ancients and Infected at {path}",
                    rule);
                isValid = false;
            }

            int maxCount =
                levelEntry.GetMaxEnemyCount();

            if (level == 1 && (maxCount < 2 || maxCount > 4))
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: L01 max count must be 2-4, actual {maxCount} at {path}",
                    rule);
                isValid = false;
            }

            if (level == 10 && maxCount != 20)
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: L10 max count must be 20, actual {maxCount} at {path}",
                    rule);
                isValid = false;
            }
        }

        return isValid;
    }

    private static Dictionary<EnemyKey, EnemyConfig> LoadEnemiesByFactionAndLevel()
    {
        Dictionary<EnemyKey, EnemyConfig> result =
            new Dictionary<EnemyKey, EnemyConfig>();

        string[] guids =
            AssetDatabase.FindAssets("t:EnemyConfig", new[] { EnemyConfigRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            EnemyConfig config =
                AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);

            if (config == null)
                continue;

            if (!TryParseFactionAndLevelFromPath(path, out EnemyFaction2A faction, out int level))
                continue;

            EnemyKey key =
                new EnemyKey(faction, level);

            if (result.ContainsKey(key))
            {
                Debug.LogWarning(
                    $"EnemyGroupSpawnRule generator: duplicate EnemyConfig for {faction} L{level:00}. Keeping first one. Duplicate path: {path}",
                    config);
                continue;
            }

            result[key] = config;
        }

        return result;
    }

    private static EnemyConfig FindEnemyConfig(
        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel,
        EnemyFaction2A faction,
        int level)
    {
        if (enemiesByFactionAndLevel == null)
            return null;

        EnemyConfig result;

        if (enemiesByFactionAndLevel.TryGetValue(
                new EnemyKey(faction, Mathf.Clamp(level, 1, 10)),
                out result))
        {
            return result;
        }

        return null;
    }

    private static bool TryParseFactionAndLevelFromPath(
        string path,
        out EnemyFaction2A faction,
        out int level)
    {
        faction = EnemyFaction2A.AI;
        level = 1;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string lowerPath =
            path.Replace("\\", "/").ToLowerInvariant();

        bool hasFaction = false;

        if (lowerPath.Contains("/ai/") ||
            lowerPath.Contains("_ai_"))
        {
            faction = EnemyFaction2A.AI;
            hasFaction = true;
        }
        else if (lowerPath.Contains("/ancients/") ||
                 lowerPath.Contains("_ancients_"))
        {
            faction = EnemyFaction2A.Ancients;
            hasFaction = true;
        }
        else if (lowerPath.Contains("/infected/") ||
                 lowerPath.Contains("_infected_"))
        {
            faction = EnemyFaction2A.Infected;
            hasFaction = true;
        }

        if (!hasFaction)
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

    private static bool TryResolveFaction(
        EnemyConfig enemyConfig,
        out EnemyFaction2A faction)
    {
        faction = EnemyFaction2A.AI;

        if (enemyConfig == null)
            return false;

        string id =
            enemyConfig.Id ?? string.Empty;

        string name =
            enemyConfig.name ?? string.Empty;

        string combined =
            (id + " " + name).ToLowerInvariant();

        if (combined.Contains("_ai_") ||
            combined.Contains("enemy_ai"))
        {
            faction = EnemyFaction2A.AI;
            return true;
        }

        if (combined.Contains("_ancients_") ||
            combined.Contains("enemy_ancients"))
        {
            faction = EnemyFaction2A.Ancients;
            return true;
        }

        if (combined.Contains("_infected_") ||
            combined.Contains("enemy_infected"))
        {
            faction = EnemyFaction2A.Infected;
            return true;
        }

        return false;
    }

    private static bool ContainsAllFactions(
        HashSet<EnemyFaction2A> factions)
    {
        if (factions == null)
            return false;

        EnemyFaction2A[] expectedFactions =
            GetFactions();

        for (int i = 0; i < expectedFactions.Length; i++)
        {
            if (!factions.Contains(expectedFactions[i]))
                return false;
        }

        return true;
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
                Mathf.Lerp(5f, 30f, (level - 1) / 9f));

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
                DisplayName = "Враги: сбалансированная группа",
                Description = "Все 3 типа врагов с одинаковым количеством и одинаковым весом выбора.",
                DominantFaction = null
            },
            new SpawnRuleProfile
            {
                Key = "ai",
                DisplayName = "Враги: группа ИИ",
                Description = "Все 3 типа врагов с одинаковым количеством. AI имеет повышенный вес выбора внутри группы.",
                DominantFaction = EnemyFaction2A.AI
            },
            new SpawnRuleProfile
            {
                Key = "ancients",
                DisplayName = "Враги: группа Древних",
                Description = "Все 3 типа врагов с одинаковым количеством. Ancients имеет повышенный вес выбора внутри группы.",
                DominantFaction = EnemyFaction2A.Ancients
            },
            new SpawnRuleProfile
            {
                Key = "infected",
                DisplayName = "Враги: группа Зараженных",
                Description = "Все 3 типа врагов с одинаковым количеством. Infected имеет повышенный вес выбора внутри группы.",
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
            Path.GetFileNameWithoutExtension(path);

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
        public EnemyFaction2A? DominantFaction;
    }

    private struct EnemyKey : IEquatable<EnemyKey>
    {
        private readonly EnemyFaction2A faction;
        private readonly int level;

        public EnemyKey(
            EnemyFaction2A faction,
            int level)
        {
            this.faction = faction;
            this.level = level;
        }

        public bool Equals(
            EnemyKey other)
        {
            return faction == other.faction &&
                   level == other.level;
        }

        public override bool Equals(
            object obj)
        {
            return obj is EnemyKey other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)faction * 397) ^ level;
            }
        }
    }

    private enum EnemyFaction2A
    {
        AI,
        Ancients,
        Infected
    }
}