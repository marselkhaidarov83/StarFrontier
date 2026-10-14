using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EnemySpawnRuleAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/Population/EnemySpawnRules";

    private const string EnemyConfigRoot =
        "Assets/_Project/Content/Configs/Population/Enemies";

    private const string RuleSetId =
        "enemy_spawn_rule_factions_01";

    [MenuItem("STAR FRONTIER/Content/06b. Enemy Spawn Rules/Create generated EnemySpawnRule assets")]
    public static void CreateGeneratedEnemySpawnRules()
    {
        EnsureFolder(OutputRoot);

        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel =
            LoadEnemiesByFactionAndLevel();

        EnemySpawnProfile[] profiles =
            BuildProfiles();

        int created = 0;
        int updated = 0;
        int warnings = 0;
        int errors = 0;

        EnemySpawnRuleConfig[] generatedRules =
            new EnemySpawnRuleConfig[profiles.Length];

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                EnemySpawnProfile profile =
                    profiles[i];

                string id =
                    BuildRuleId(profile.Faction);

                string path =
                    $"{OutputRoot}/{id}.asset";

                EnemySpawnRuleConfig asset =
                    FindExistingEnemySpawnRule(
                        id,
                        path);

                if (asset == null)
                {
                    asset =
                        ScriptableObject.CreateInstance<EnemySpawnRuleConfig>();

                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    updated++;
                }

                warnings += WriteEnemySpawnRule(
                    asset,
                    id,
                    profile,
                    enemiesByFactionAndLevel);

                EditorUtility.SetDirty(asset);

                if (!ValidateEnemySpawnRuleInMemory(asset, path))
                    errors++;

                generatedRules[i] = asset;
            }

            string ruleSetPath =
                $"{OutputRoot}/{RuleSetId}.asset";

            EnemySpawnRuleSetConfig ruleSet =
                FindExistingEnemySpawnRuleSet(
                    RuleSetId,
                    ruleSetPath);

            if (ruleSet == null)
            {
                ruleSet =
                    ScriptableObject.CreateInstance<EnemySpawnRuleSetConfig>();

                AssetDatabase.CreateAsset(ruleSet, ruleSetPath);
                created++;
            }
            else
            {
                updated++;
            }

            WriteEnemySpawnRuleSet(
                ruleSet,
                generatedRules);

            EditorUtility.SetDirty(ruleSet);

            if (!ValidateEnemySpawnRuleSetInMemory(ruleSet, ruleSetPath))
                errors++;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemySpawnRule generator",
            $"Done. Created: {created}. Updated: {updated}. Warnings: {warnings}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/06b. Enemy Spawn Rules/Validate generated EnemySpawnRule assets")]
    public static void ValidateGeneratedEnemySpawnRules()
    {
        int checkedAssets = 0;
        int errors = 0;

        string[] ruleGuids =
            AssetDatabase.FindAssets(
                "t:EnemySpawnRuleConfig",
                new[] { OutputRoot });

        for (int i = 0; i < ruleGuids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(ruleGuids[i]);

            EnemySpawnRuleConfig rule =
                AssetDatabase.LoadAssetAtPath<EnemySpawnRuleConfig>(path);

            if (rule == null)
                continue;

            checkedAssets++;

            if (!ValidateEnemySpawnRuleInMemory(rule, path))
                errors++;
        }

        string ruleSetPath =
            $"{OutputRoot}/{RuleSetId}.asset";

        EnemySpawnRuleSetConfig ruleSet =
            AssetDatabase.LoadAssetAtPath<EnemySpawnRuleSetConfig>(ruleSetPath);

        checkedAssets++;

        if (!ValidateEnemySpawnRuleSetInMemory(ruleSet, ruleSetPath))
            errors++;

        EditorUtility.DisplayDialog(
            "EnemySpawnRule validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/06b. Enemy Spawn Rules/Delete generated EnemySpawnRule assets")]
    public static void DeleteGeneratedEnemySpawnRules()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated EnemySpawnRule assets",
                $"Delete generated EnemySpawnRule assets inside:\n{OutputRoot}",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] ruleGuids =
            AssetDatabase.FindAssets(
                "t:EnemySpawnRuleConfig",
                new[] { OutputRoot });

        for (int i = 0; i < ruleGuids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(ruleGuids[i]);

            if (!IsGeneratedEnemySpawnRulePath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        string ruleSetPath =
            $"{OutputRoot}/{RuleSetId}.asset";

        if (AssetDatabase.LoadAssetAtPath<EnemySpawnRuleSetConfig>(ruleSetPath) != null &&
            AssetDatabase.DeleteAsset(ruleSetPath))
        {
            deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemySpawnRule generator",
            $"Deleted: {deleted}.",
            "OK");
    }

    private static int WriteEnemySpawnRule(
        EnemySpawnRuleConfig asset,
        string id,
        EnemySpawnProfile profile,
        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel)
    {
        int warnings = 0;

        SetPrivateField(asset, "id", id);
        SetPrivateField(asset, "displayName", profile.DisplayName);
        SetPrivateField(asset, "description", profile.Description);
        SetPrivateField(asset, "faction", profile.Faction);

        EnemySpawnLevelEntryConfig[] levelEntries =
            new EnemySpawnLevelEntryConfig[10];

        for (int galaxyLevel = 1; galaxyLevel <= 10; galaxyLevel++)
        {
            EnemySpawnLevelEntryConfig levelEntry =
                new EnemySpawnLevelEntryConfig();

            SetPrivateField(levelEntry, "galaxyLevel", galaxyLevel);
            SetPrivateField(levelEntry, "spawnIntervalSeconds", BuildSpawnIntervalSeconds(galaxyLevel));
            SetPrivateField(levelEntry, "maxAliveGroupsFromThisRule", BuildMaxAliveGroups(galaxyLevel));
            SetPrivateField(
                levelEntry,
                "enemies",
                BuildEnemyEntries(
                    profile.Faction,
                    galaxyLevel,
                    enemiesByFactionAndLevel,
                    ref warnings));

            levelEntries[galaxyLevel - 1] = levelEntry;
        }

        SetPrivateField(asset, "levelEntries", levelEntries);

        return warnings;
    }

    private static void WriteEnemySpawnRuleSet(
        EnemySpawnRuleSetConfig ruleSet,
        EnemySpawnRuleConfig[] rules)
    {
        SetPrivateField(ruleSet, "id", RuleSetId);
        SetPrivateField(ruleSet, "displayName", "Правила спавна врагов по фракциям");
        SetPrivateField(
            ruleSet,
            "description",
            "Агрегирующий ассет для вражеских систем. Содержит отдельные правила спавна AI, Ancients и Infected.");

        EnemySpawnRuleSetEntry[] entries =
            new EnemySpawnRuleSetEntry[3];

        EnemyFaction2A[] factions =
            GetFactions();

        for (int i = 0; i < factions.Length; i++)
        {
            EnemySpawnRuleSetEntry entry =
                new EnemySpawnRuleSetEntry();

            SetPrivateField(entry, "faction", factions[i]);
            SetPrivateField(entry, "spawnRule", FindRuleForFaction(rules, factions[i]));

            entries[i] = entry;
        }

        SetPrivateField(ruleSet, "factionRules", entries);
    }

    private static EnemyGroupEntryConfig[] BuildEnemyEntries(
        EnemyFaction2A faction,
        int galaxyLevel,
        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel,
        ref int warnings)
    {
        int[] enemyLevels =
            GetEnemyLevelsForGalaxyLevel(galaxyLevel);

        EnemyGroupEntryConfig[] entries =
            new EnemyGroupEntryConfig[enemyLevels.Length];

        int totalMinCount =
            BuildMinTotal(galaxyLevel);

        int totalMaxCount =
            BuildMaxTotal(galaxyLevel);

        for (int i = 0; i < enemyLevels.Length; i++)
        {
            int enemyLevel =
                enemyLevels[i];

            EnemyConfig enemyConfig =
                FindEnemyConfig(
                    enemiesByFactionAndLevel,
                    faction,
                    enemyLevel);

            if (enemyConfig == null)
            {
                warnings++;

                AppLog.Warning(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRule generator: missing EnemyConfig for " +
                    faction +
                    " L" +
                    enemyLevel.ToString("00") +
                    " while building galaxy level " +
                    galaxyLevel.ToString("00") +
                    ".");
            }

            int minCount =
                BuildDistributedCount(
                    totalMinCount,
                    i,
                    enemyLevels.Length);

            int maxCount =
                BuildDistributedCount(
                    totalMaxCount,
                    i,
                    enemyLevels.Length);

            if (maxCount <= minCount)
                maxCount = minCount + 1;

            EnemyGroupEntryConfig entry =
                new EnemyGroupEntryConfig();

            SetPrivateField(entry, "enemyConfig", enemyConfig);
            SetPrivateField(entry, "minCount", minCount);
            SetPrivateField(entry, "maxCount", maxCount);
            SetPrivateField(entry, "weight", BuildEnemyEntryWeight(i));

            entries[i] = entry;
        }

        return entries;
    }

    private static int BuildEnemyEntryWeight(
        int index)
    {
        if (index <= 0)
            return 3;

        if (index == 1)
            return 2;

        return 1;
    }

    private static Dictionary<EnemyKey, EnemyConfig> LoadEnemiesByFactionAndLevel()
    {
        Dictionary<EnemyKey, EnemyConfig> result =
            new Dictionary<EnemyKey, EnemyConfig>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:EnemyConfig",
                new[] { EnemyConfigRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            EnemyConfig config =
                AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);

            if (config == null)
                continue;

            if (!TryParseFactionAndLevelFromPath(
                    path,
                    out EnemyFaction2A faction,
                    out int level))
            {
                AppLog.Warning(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRule generator: cannot resolve faction/level for EnemyConfig at " +
                    path);
                continue;
            }

            EnemyKey key =
                new EnemyKey(
                    faction,
                    level);

            if (result.ContainsKey(key))
            {
                AppLog.Warning(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRule generator: duplicate EnemyConfig for " +
                    faction +
                    " L" +
                    level.ToString("00") +
                    ". Keeping first one. Duplicate path: " +
                    path);
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
                new EnemyKey(
                    faction,
                    Mathf.Clamp(level, 1, 10)),
                out result))
        {
            return result;
        }

        return null;
    }

    private static bool ValidateEnemySpawnRuleInMemory(
        EnemySpawnRuleConfig rule,
        string path)
    {
        bool isValid = true;

        if (rule == null)
        {
            AppLog.Error(DebugLogChannel.EditorGenerators,
                "EnemySpawnRule validation: missing rule at " + path);
            return false;
        }

        if (rule.LevelEntries == null ||
            rule.LevelEntries.Count != 10)
        {
            AppLog.Error(DebugLogChannel.EditorGenerators,
                "EnemySpawnRule validation: expected exactly 10 level entries at " + path);
            return false;
        }

        for (int galaxyLevel = 1; galaxyLevel <= 10; galaxyLevel++)
        {
            EnemySpawnLevelEntryConfig entry =
                rule.GetEntryForGalaxyLevel(galaxyLevel);

            if (entry == null)
            {
                AppLog.Error(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRule validation: missing level " +
                    galaxyLevel +
                    " at " +
                    path);
                isValid = false;
                continue;
            }

            if (!entry.HasValidEnemies())
            {
                AppLog.Error(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRule validation: level " +
                    galaxyLevel +
                    " has no valid enemies at " +
                    path);
                isValid = false;
                continue;
            }

            int[] expectedLevels =
                GetEnemyLevelsForGalaxyLevel(galaxyLevel);

            if (entry.Enemies == null ||
                entry.Enemies.Count != expectedLevels.Length)
            {
                AppLog.Error(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRule validation: level " +
                    galaxyLevel +
                    " must contain enemy levels " +
                    DescribeLevels(expectedLevels) +
                    " at " +
                    path);
                isValid = false;
                continue;
            }

            for (int enemyIndex = 0; enemyIndex < entry.Enemies.Count; enemyIndex++)
            {
                EnemyGroupEntryConfig enemyEntry =
                    entry.Enemies[enemyIndex];

                if (enemyEntry == null ||
                    enemyEntry.EnemyConfig == null ||
                    !enemyEntry.IsValid())
                {
                    AppLog.Error(DebugLogChannel.EditorGenerators,
                        "EnemySpawnRule validation: invalid enemy entry at " +
                        path +
                        ", galaxy level " +
                        galaxyLevel +
                        ", index " +
                        enemyIndex +
                        ".");
                    isValid = false;
                    continue;
                }

                if (enemyEntry.EnemyConfig.Level != expectedLevels[enemyIndex])
                {
                    AppLog.Error(DebugLogChannel.EditorGenerators,
                        "EnemySpawnRule validation: wrong enemy level at " +
                        path +
                        ". Expected L" +
                        expectedLevels[enemyIndex].ToString("00") +
                        ", got L" +
                        enemyEntry.EnemyConfig.Level.ToString("00") +
                        ".");
                    isValid = false;
                }

                if (!TryResolveFactionFromEnemyConfig(
                        enemyEntry.EnemyConfig,
                        out EnemyFaction2A enemyFaction) ||
                    enemyFaction != rule.Faction)
                {
                    AppLog.Error(DebugLogChannel.EditorGenerators,
                        "EnemySpawnRule validation: enemy faction mismatch at " +
                        path +
                        ". Rule faction: " +
                        rule.Faction +
                        ", enemy: " +
                        enemyEntry.EnemyConfig.Id +
                        ".");
                    isValid = false;
                }
            }
        }

        return isValid;
    }

    private static bool ValidateEnemySpawnRuleSetInMemory(
        EnemySpawnRuleSetConfig ruleSet,
        string path)
    {
        if (ruleSet == null)
        {
            AppLog.Error(DebugLogChannel.EditorGenerators,
                "EnemySpawnRuleSet validation: missing rule set at " + path);
            return false;
        }

        bool isValid = true;

        EnemyFaction2A[] factions =
            GetFactions();

        for (int i = 0; i < factions.Length; i++)
        {
            EnemySpawnRuleConfig rule =
                ruleSet.GetRuleForFaction(factions[i]);

            if (rule == null)
            {
                AppLog.Error(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRuleSet validation: missing rule for faction " +
                    factions[i] +
                    " at " +
                    path);
                isValid = false;
                continue;
            }

            if (rule.Faction != factions[i])
            {
                AppLog.Error(DebugLogChannel.EditorGenerators,
                    "EnemySpawnRuleSet validation: rule faction mismatch for " +
                    factions[i] +
                    " at " +
                    path);
                isValid = false;
            }
        }

        return isValid;
    }

    private static EnemySpawnRuleConfig FindExistingEnemySpawnRule(
        string id,
        string path)
    {
        EnemySpawnRuleConfig asset =
            AssetDatabase.LoadAssetAtPath<EnemySpawnRuleConfig>(path);

        if (asset != null)
            return asset;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:EnemySpawnRuleConfig",
                new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            EnemySpawnRuleConfig candidate =
                AssetDatabase.LoadAssetAtPath<EnemySpawnRuleConfig>(candidatePath);

            if (candidate == null)
                continue;

            if (string.Equals(
                    candidate.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static EnemySpawnRuleSetConfig FindExistingEnemySpawnRuleSet(
        string id,
        string path)
    {
        EnemySpawnRuleSetConfig asset =
            AssetDatabase.LoadAssetAtPath<EnemySpawnRuleSetConfig>(path);

        if (asset != null)
            return asset;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:EnemySpawnRuleSetConfig",
                new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            EnemySpawnRuleSetConfig candidate =
                AssetDatabase.LoadAssetAtPath<EnemySpawnRuleSetConfig>(candidatePath);

            if (candidate == null)
                continue;

            if (string.Equals(
                    candidate.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static EnemySpawnRuleConfig FindRuleForFaction(
        EnemySpawnRuleConfig[] rules,
        EnemyFaction2A faction)
    {
        if (rules == null)
            return null;

        for (int i = 0; i < rules.Length; i++)
        {
            EnemySpawnRuleConfig rule =
                rules[i];

            if (rule == null)
                continue;

            if (rule.Faction == faction)
                return rule;
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
            lowerPath.Contains("_ai_") ||
            lowerPath.Contains("enemy_ai"))
        {
            faction = EnemyFaction2A.AI;
            hasFaction = true;
        }
        else if (lowerPath.Contains("/ancients/") ||
                 lowerPath.Contains("_ancients_") ||
                 lowerPath.Contains("enemy_ancients"))
        {
            faction = EnemyFaction2A.Ancients;
            hasFaction = true;
        }
        else if (lowerPath.Contains("/infected/") ||
                 lowerPath.Contains("_infected_") ||
                 lowerPath.Contains("enemy_infected"))
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

    private static bool TryResolveFactionFromEnemyConfig(
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

        string path =
            AssetDatabase.GetAssetPath(enemyConfig);

        string combined =
            (id + " " + name + " " + path).Replace("\\", "/").ToLowerInvariant();

        if (combined.Contains("/ai/") ||
            combined.Contains("_ai_") ||
            combined.Contains("enemy_ai"))
        {
            faction = EnemyFaction2A.AI;
            return true;
        }

        if (combined.Contains("/ancients/") ||
            combined.Contains("_ancients_") ||
            combined.Contains("enemy_ancients"))
        {
            faction = EnemyFaction2A.Ancients;
            return true;
        }

        if (combined.Contains("/infected/") ||
            combined.Contains("_infected_") ||
            combined.Contains("enemy_infected"))
        {
            faction = EnemyFaction2A.Infected;
            return true;
        }

        return false;
    }

    private static EnemySpawnProfile[] BuildProfiles()
    {
        return new[]
        {
            new EnemySpawnProfile
            {
                Faction = EnemyFaction2A.AI,
                DisplayName = "Спавн врагов: ИИ",
                Description = "Правило population-спавна врагов фракции AI по уровням галактики."
            },
            new EnemySpawnProfile
            {
                Faction = EnemyFaction2A.Ancients,
                DisplayName = "Спавн врагов: Древние",
                Description = "Правило population-спавна врагов фракции Ancients по уровням галактики."
            },
            new EnemySpawnProfile
            {
                Faction = EnemyFaction2A.Infected,
                DisplayName = "Спавн врагов: Заражённые",
                Description = "Правило population-спавна врагов фракции Infected по уровням галактики."
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

    private static string BuildRuleId(
        EnemyFaction2A faction)
    {
        return "enemy_spawn_rule_" + GetFactionKey(faction) + "_01";
    }

    private static string GetFactionKey(
        EnemyFaction2A faction)
    {
        switch (faction)
        {
            case EnemyFaction2A.Ancients:
                return "ancients";
            case EnemyFaction2A.Infected:
                return "infected";
            default:
                return "ai";
        }
    }

    private static int[] GetEnemyLevelsForGalaxyLevel(
        int galaxyLevel)
    {
        int clampedGalaxyLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

        List<int> levels =
            new List<int>();

        for (int offset = 0; offset <= 2; offset++)
        {
            int enemyLevel =
                clampedGalaxyLevel - offset;

            if (enemyLevel < 1)
                continue;

            levels.Add(enemyLevel);
        }

        return levels.ToArray();
    }

    private static float BuildSpawnIntervalSeconds(
        int galaxyLevel)
    {
        return Mathf.Round(
            Mathf.Lerp(
                180f,
                45f,
                (galaxyLevel - 1) / 9f));
    }

    private static int BuildMaxAliveGroups(
        int galaxyLevel)
    {
        if (galaxyLevel >= 8)
            return 3;

        if (galaxyLevel >= 4)
            return 2;

        return 1;
    }

    private static int BuildMinTotal(
        int galaxyLevel)
    {
        if (galaxyLevel >= 8)
            return 4;

        if (galaxyLevel >= 4)
            return 3;

        return 2;
    }

    private static int BuildMaxTotal(
     int galaxyLevel)
    {
        int minTotal =
            BuildMinTotal(galaxyLevel);

        if (galaxyLevel >= 8)
            return Mathf.Max(minTotal + 2, 6);

        if (galaxyLevel >= 4)
            return Mathf.Max(minTotal + 2, 5);

        return Mathf.Max(minTotal + 1, 3);
    }

    private static int BuildDistributedCount(
        int total,
        int index,
        int length)
    {
        if (length <= 0)
            return 0;

        int baseCount =
            total / length;

        int remainder =
            total % length;

        return baseCount + (index < remainder ? 1 : 0);
    }

    private static string DescribeLevels(
        int[] levels)
    {
        if (levels == null || levels.Length == 0)
            return "none";

        string result = string.Empty;

        for (int i = 0; i < levels.Length; i++)
        {
            if (i > 0)
                result += ", ";

            result += "L" + levels[i].ToString("00");
        }

        return result;
    }

    private static bool IsGeneratedEnemySpawnRulePath(
        string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string fileName =
            System.IO.Path.GetFileNameWithoutExtension(path);

        return fileName.StartsWith(
            "enemy_spawn_rule_",
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
            "Serialized field not found: " + fieldName + " on " + target.GetType().Name);
    }

    private sealed class EnemySpawnProfile
    {
        public EnemyFaction2A Faction;
        public string DisplayName;
        public string Description;
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
}