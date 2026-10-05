using System;
using System.Collections;
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
        int updated = 0;
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

            EnemyGroupSpawnRuleConfig asset =
                FindExistingSpawnRule(
                    id,
                    path);

            if (asset == null)
            {
                asset =
                    ScriptableObject.CreateInstance<EnemyGroupSpawnRuleConfig>();

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
                        AssetDatabase.MoveAsset(
                            currentPath,
                            path);

                    if (!string.IsNullOrWhiteSpace(moveError))
                    {
                        throw new InvalidOperationException(
                            $"EnemyGroupSpawnRule generator: failed to move existing asset {id}: {moveError}");
                    }
                }

                updated++;
            }

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

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            if (!ValidateRuleInMemory(asset, path))
                errors++;

        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemyGroupSpawnRuleConfig generator",
            "Done. " +
            $"Created: {created}. " +
            $"Updated: {updated}. " +
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
            int[] enemyLevels =
                GetEnemyLevelsForGalaxyLevel(level);

            EnemyGroupSpawnLevelEntryConfig levelEntry =
                new EnemyGroupSpawnLevelEntryConfig();

            SetPrivateField(levelEntry, "galaxyLevel", level);
            SetPrivateField(levelEntry, "spawnIntervalSeconds", BuildSpawnIntervalSeconds(level));
            SetPrivateField(levelEntry, "maxAliveGroupsFromThisRule", 1);
            SetPrivateField(
                levelEntry,
                "enemyGroups",
                BuildEnemyGroups(
                    profile,
                    level,
                    enemyLevels,
                    enemiesByFactionAndLevel,
                    ref warnings,
                    ref boundEnemies));

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

            if (!ValidateEnemyGroups(rule, levelEntry, level, path))
                isValid = false;

        }

        return isValid;
    }

    private static EnemyGroupSpawnRuleConfig FindExistingSpawnRule(
        string id,
        string path)
    {
        EnemyGroupSpawnRuleConfig asset =
            AssetDatabase.LoadAssetAtPath<EnemyGroupSpawnRuleConfig>(path);

        if (asset != null)
            return asset;

        string[] guids =
            AssetDatabase.FindAssets("t:EnemyGroupSpawnRuleConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (string.IsNullOrWhiteSpace(candidatePath))
                continue;

            EnemyGroupSpawnRuleConfig candidate =
                AssetDatabase.LoadAssetAtPath<EnemyGroupSpawnRuleConfig>(candidatePath);

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

    private static Array BuildEnemyGroups(
        SpawnRuleProfile profile,
        int galaxyLevel,
        int[] enemyLevels,
        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel,
        ref int warnings,
        ref int boundEnemies)
    {
        Type groupType =
            FindType("EnemyGroupSpawnOptionConfig") ??
            FindType("EnemyGroupSpawnGroupConfig");

        if (groupType == null)
            return null;

        EnemyFaction2A[] factions =
            GetFactions();

        Array groups =
            Array.CreateInstance(groupType, factions.Length);

        for (int factionIndex = 0; factionIndex < factions.Length; factionIndex++)
        {
            EnemyFaction2A faction =
                factions[factionIndex];

            object group =
                Activator.CreateInstance(groupType);

            TrySetPrivateField(group, "faction", faction);
            TrySetPrivateField(group, "weight", BuildEntryWeight(profile, faction));
            TrySetPrivateField(
                group,
                "enemies",
                BuildEnemyGroupMembers(
                    faction,
                    galaxyLevel,
                    enemyLevels,
                    enemiesByFactionAndLevel,
                    ref warnings,
                    ref boundEnemies));

            groups.SetValue(group, factionIndex);
        }

        return groups;
    }

    private static EnemyGroupEntryConfig[] BuildEnemyGroupMembers(
        EnemyFaction2A faction,
        int galaxyLevel,
        int[] enemyLevels,
        Dictionary<EnemyKey, EnemyConfig> enemiesByFactionAndLevel,
        ref int warnings,
        ref int boundEnemies)
    {
        EnemyGroupEntryConfig[] enemies =
            new EnemyGroupEntryConfig[enemyLevels.Length];

        int totalMinCount =
            BuildMinTotal(galaxyLevel);

        int totalMaxCount =
            BuildMaxTotal(galaxyLevel);

        for (int enemyLevelIndex = 0; enemyLevelIndex < enemyLevels.Length; enemyLevelIndex++)
        {
            int enemyLevel =
                enemyLevels[enemyLevelIndex];

            EnemyConfig enemyConfig =
                FindEnemyConfig(
                    enemiesByFactionAndLevel,
                    faction,
                    enemyLevel);

            if (enemyConfig == null)
            {
                warnings++;
                Debug.LogWarning(
                    $"EnemyGroupSpawnRule generator: missing grouped EnemyConfig for {faction} L{enemyLevel:00} in galaxy L{galaxyLevel:00}.");
            }
            else
            {
                boundEnemies++;
            }

            EnemyGroupEntryConfig enemyEntry =
                new EnemyGroupEntryConfig();

            SetPrivateField(enemyEntry, "enemyConfig", enemyConfig);
            SetPrivateField(
                enemyEntry,
                "minCount",
                BuildDistributedCount(
                    totalMinCount,
                    enemyLevelIndex,
                    enemyLevels.Length));
            SetPrivateField(
                enemyEntry,
                "maxCount",
                BuildDistributedCount(
                    totalMaxCount,
                    enemyLevelIndex,
                    enemyLevels.Length));
            TrySetPrivateField(enemyEntry, "weight", 1);

            enemies[enemyLevelIndex] = enemyEntry;
        }

        return enemies;
    }

    private static bool ValidateEnemyGroups(
        EnemyGroupSpawnRuleConfig rule,
        EnemyGroupSpawnLevelEntryConfig levelEntry,
        int galaxyLevel,
        string path)
    {
        object groupsValue =
            GetMemberValue(levelEntry, "EnemyGroups") ??
            GetMemberValue(levelEntry, "enemyGroups");

        IReadOnlyList<object> groups =
            ConvertToObjectList(groupsValue);

        if (groups == null || groups.Count == 0)
        {
            Debug.LogError(
                $"EnemyGroupSpawnRule validation: level {galaxyLevel} must contain enemy groups at {path}",
                rule);
            return false;
        }

        if (groups.Count != GetFactions().Length)
        {
            Debug.LogError(
                $"EnemyGroupSpawnRule validation: level {galaxyLevel} must contain exactly {GetFactions().Length} enemy groups at {path}",
                rule);
            return false;
        }

        bool isValid =
            true;

        HashSet<EnemyFaction2A> factions =
            new HashSet<EnemyFaction2A>();

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            object group =
                groups[groupIndex];

            if (group == null)
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: empty enemy group at level {galaxyLevel}, index {groupIndex}, path: {path}",
                    rule);
                isValid = false;
                continue;
            }

            EnemyFaction2A faction =
                ResolveEnemyGroupFaction(group);

            factions.Add(faction);

            object enemiesValue =
                GetMemberValue(group, "Enemies") ??
                GetMemberValue(group, "enemies");

            IReadOnlyList<EnemyGroupEntryConfig> enemies =
                ConvertToEnemyEntryList(enemiesValue);

            int[] expectedLevels =
                GetEnemyLevelsForGalaxyLevel(galaxyLevel);

            if (enemies == null || enemies.Count != expectedLevels.Length)
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: group {groupIndex} at level {galaxyLevel} must contain enemy levels {DescribeEnemyLevelsForGalaxyLevel(galaxyLevel)} at {path}",
                    rule);
                isValid = false;
                continue;
            }

            int totalMinCount =
                0;

            int totalMaxCount =
                0;

            HashSet<int> enemyLevels =
                new HashSet<int>();

            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                EnemyGroupEntryConfig entry =
                    enemies[enemyIndex];

                if (entry == null || entry.EnemyConfig == null)
                {
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: empty grouped enemy entry at level {galaxyLevel}, group {groupIndex}, index {enemyIndex}, path: {path}",
                        rule);
                    isValid = false;
                    continue;
                }

                enemyLevels.Add(entry.EnemyConfig.Level);

                if (!TryResolveFaction(entry.EnemyConfig, out EnemyFaction2A entryFaction) ||
                    entryFaction != faction)
                {
                    Debug.LogError(
                        $"EnemyGroupSpawnRule validation: grouped enemy faction mismatch at {path}. Group faction {faction}, enemy {entry.EnemyConfig.Id}.",
                        rule);
                    isValid = false;
                }

                totalMinCount += entry.MinCount;
                totalMaxCount += entry.MaxCount;
            }

            if (!ContainsExpectedEnemyLevels(enemyLevels, galaxyLevel))
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: grouped level {galaxyLevel}, group {groupIndex} must contain enemy levels {DescribeEnemyLevelsForGalaxyLevel(galaxyLevel)} at {path}",
                    rule);
                isValid = false;
            }

            if (totalMinCount != BuildMinTotal(galaxyLevel) ||
                totalMaxCount != BuildMaxTotal(galaxyLevel))
            {
                Debug.LogError(
                    $"EnemyGroupSpawnRule validation: grouped total count mismatch at {path}, L{galaxyLevel:00}, group {groupIndex}. Expected total {BuildMinTotal(galaxyLevel)}-{BuildMaxTotal(galaxyLevel)}, actual total {totalMinCount}-{totalMaxCount}.",
                    rule);
                isValid = false;
            }
        }

        if (!ContainsAllFactions(factions))
        {
            Debug.LogError(
                $"EnemyGroupSpawnRule validation: grouped level {galaxyLevel} must contain AI, Ancients and Infected groups at {path}",
                rule);
            isValid = false;
        }

        return isValid;
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

    private static bool IsAllowedEnemyLevelForGalaxyLevel(
        int enemyLevel,
        int galaxyLevel)
    {
        int[] expectedLevels =
            GetEnemyLevelsForGalaxyLevel(galaxyLevel);

        for (int i = 0; i < expectedLevels.Length; i++)
        {
            if (expectedLevels[i] == enemyLevel)
                return true;
        }

        return false;
    }

    private static bool ContainsExpectedEnemyLevels(
        HashSet<int> actualLevels,
        int galaxyLevel)
    {
        if (actualLevels == null)
            return false;

        int[] expectedLevels =
            GetEnemyLevelsForGalaxyLevel(galaxyLevel);

        for (int i = 0; i < expectedLevels.Length; i++)
        {
            if (!actualLevels.Contains(expectedLevels[i]))
                return false;
        }

        return actualLevels.Count == expectedLevels.Length;
    }

    private static string DescribeEnemyLevelsForGalaxyLevel(
        int galaxyLevel)
    {
        int[] expectedLevels =
            GetEnemyLevelsForGalaxyLevel(galaxyLevel);

        List<string> labels =
            new List<string>();

        for (int i = 0; i < expectedLevels.Length; i++)
            labels.Add($"L{expectedLevels[i]:00}");

        return string.Join(", ", labels);
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

    private static int BuildDistributedCount(
        int totalCount,
        int entryIndex,
        int entryCount)
    {
        if (entryCount <= 0)
            return 0;

        int baseCount =
            totalCount / entryCount;

        int remainder =
            totalCount % entryCount;

        return baseCount + (entryIndex < remainder ? 1 : 0);
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

    private static bool TrySetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        if (target == null)
            return false;

        FieldInfo field =
            FindField(
                target.GetType(),
                fieldName);

        if (field == null)
            return false;

        field.SetValue(target, value);
        return true;
    }

    private static FieldInfo FindField(
        Type type,
        string fieldName)
    {
        while (type != null)
        {
            FieldInfo field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }

    private static Type FindType(
        string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type =
                assembly.GetType(typeName);

            if (type != null)
                return type;

            Type[] types;

            try
            {
                types =
                    assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types =
                    ex.Types;
            }

            for (int i = 0; i < types.Length; i++)
            {
                type =
                    types[i];

                if (type != null &&
                    type.Name == typeName)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static object GetMemberValue(
        object target,
        string memberName)
    {
        if (target == null)
            return null;

        Type type =
            target.GetType();

        FieldInfo field =
            FindField(type, memberName);

        if (field != null)
            return field.GetValue(target);

        while (type != null)
        {
            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

            if (property != null)
                return property.GetValue(target);

            type = type.BaseType;
        }

        return null;
    }

    private static IReadOnlyList<object> ConvertToObjectList(
        object value)
    {
        if (value == null)
            return null;

        List<object> result =
            new List<object>();

        if (value is IEnumerable enumerable)
        {
            foreach (object item in enumerable)
                result.Add(item);

            return result;
        }

        return null;
    }

    private static IReadOnlyList<EnemyGroupEntryConfig> ConvertToEnemyEntryList(
        object value)
    {
        if (value == null)
            return null;

        if (value is IReadOnlyList<EnemyGroupEntryConfig> readOnlyList)
            return readOnlyList;

        if (value is IEnumerable enumerable)
        {
            List<EnemyGroupEntryConfig> result =
                new List<EnemyGroupEntryConfig>();

            foreach (object item in enumerable)
            {
                if (item is EnemyGroupEntryConfig entry)
                    result.Add(entry);
            }

            return result;
        }

        return null;
    }

    private static EnemyFaction2A ResolveEnemyGroupFaction(
        object group)
    {
        object value =
            GetMemberValue(group, "Faction") ??
            GetMemberValue(group, "faction");

        if (value is EnemyFaction2A faction)
            return faction;

        object enemiesValue =
            GetMemberValue(group, "Enemies") ??
            GetMemberValue(group, "enemies");

        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            ConvertToEnemyEntryList(enemiesValue);

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyGroupEntryConfig entry =
                    enemies[i];

                if (entry != null &&
                    TryResolveFaction(entry.EnemyConfig, out faction))
                {
                    return faction;
                }
            }
        }

        return EnemyFaction2A.AI;
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
