using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EnemyConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/Enemies";

    private const string NamePoolRoot =
        "Assets/_Project/Content/Configs/EnemyNames";

    private const string WeaponGroupsRoot =
        "Assets/_Project/Content/Configs/WeaponGroups/Enemy";

    private static readonly string[] ArtShipRootCandidates =
    {
        "Assets/_Project/Art_ships",
        "Assets/Art_ships",
        "Assets/_Project/Art/Art_ships",
        "Assets/Art/Art_ships"
    };

    private static readonly string[] ScenarioRoots =
    {
        "Assets/_Project/Content/Configs/NpcBehaviourScenarios/Enemy"
    };

    [MenuItem("STAR FRONTIER/Content/05. Enemy Configs/Create generated EnemyConfig assets")]
    public static void CreateGeneratedEnemyConfigs()
    {
        EnsureFolder(OutputRoot);
        EnsureFolder(NamePoolRoot);

        Dictionary<WeaponGroupEnemyFaction, EnemyNamePoolConfig> namePools =
            CreateOrUpdateNamePools();

        Dictionary<string, NpcBehaviourScenarioConfig> scenarios =
            FindScenarioConfigs();

        Dictionary<WeaponGroupEnemyFaction, Sprite> mapSprites =
            FindMapSprites();

        int created = 0;
        int updated = 0;
        int warnings = 0;
        int totalBoundWeaponGroups = 0;
        int totalBoundScenarios = 0;
        int totalBoundSprites = 0;

        foreach (WeaponGroupEnemyFaction faction in GetFactions())
        {
            string factionFolder =
                $"{OutputRoot}/{GetFactionFolder(faction)}";

            EnsureFolder(factionFolder);

            for (int level = 1; level <= 10; level++)
            {
                string id =
                    $"enemy_{GetFactionKey(faction)}_L{level:00}_01";

                string path =
                    $"{factionFolder}/{id}.asset";

                EnemyConfig asset =
                    FindExistingAsset<EnemyConfig>(
                        id,
                        path,
                        OutputRoot);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<EnemyConfig>();
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    updated++;
                }

                Sprite mapSprite = null;

                if (mapSprites.TryGetValue(faction, out mapSprite))
                {
                    totalBoundSprites++;
                }
                else
                {
                    warnings++;
                    Debug.LogWarning(
                        $"EnemyConfig generator: no map sprite found in Art_ships for {faction}.",
                        asset);
                }

                List<WeaponGroupConfig> weaponGroups =
                    FindWeaponGroups(faction, level);

                if (weaponGroups.Count == 0)
                {
                    warnings++;
                    Debug.LogWarning(
                        $"EnemyConfig generator: no weapon groups for {faction} L{level:00}.",
                        asset);
                }

                EnemyNamePoolConfig namePool = null;
                namePools.TryGetValue(faction, out namePool);

                int boundScenarios =
                    WriteEnemyConfig(
                        asset,
                        id,
                        faction,
                        level,
                        scenarios,
                        weaponGroups,
                        mapSprite,
                        namePool);

                totalBoundScenarios += boundScenarios;
                totalBoundWeaponGroups += weaponGroups.Count;

                EditorUtility.SetDirty(asset);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemyConfig generator",
            "Done. " +
            $"Created: {created}. " +
            $"Updated: {updated}. " +
            $"Enemy scenario refs found: {scenarios.Count}. " +
            $"Scenario refs bound: {totalBoundScenarios}. " +
            $"Weapon groups bound: {totalBoundWeaponGroups}. " +
            $"Map sprites bound: {totalBoundSprites}. " +
            $"Warnings: {warnings}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/05. Enemy Configs/Validate generated EnemyConfig assets")]
    public static void ValidateGeneratedEnemyConfigs()
    {
        int errors = 0;
        int checkedAssets = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:EnemyConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedEnemyConfigPath(path))
                continue;

            EnemyConfig config =
                AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);

            if (config == null)
                continue;

            checkedAssets++;

            if (string.IsNullOrWhiteSpace(config.Id))
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: empty id at {path}", config);
            }

            if (string.IsNullOrWhiteSpace(config.DisplayName))
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: empty displayName at {path}", config);
            }

            if (config.Level < 1 || config.Level > 10)
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: invalid level at {path}", config);
            }

            if (config.BaseHullMin <= 0 ||
                config.BaseHullMax < config.BaseHullMin ||
                config.BaseShieldMin < 0 ||
                config.BaseShieldMax < config.BaseShieldMin ||
                config.BaseEnergyMin < 0 ||
                config.BaseEnergyMax < config.BaseEnergyMin ||
                config.BaseSpeedMin <= 0f ||
                config.BaseSpeedMax < config.BaseSpeedMin)
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: invalid stat range at {path}", config);
            }

            if (config.CreditRewardMin < 0 ||
                config.CreditRewardMax < config.CreditRewardMin ||
                config.XpRewardMin < 0 ||
                config.XpRewardMax < config.XpRewardMin ||
                config.DangerTier < 1 ||
                config.DangerTier > 5)
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: invalid reward range at {path}", config);
            }

            if (config.HasDuplicateBehaviorScenarios())
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: duplicate scenarios at {path}", config);
            }

            List<AllyBehaviourScenario> expectedScenarios =
                BuildScenarioList();

            for (int scenarioIndex = 0;
                 scenarioIndex < expectedScenarios.Count;
                 scenarioIndex++)
            {
                AllyBehaviourScenario expectedScenario =
                    expectedScenarios[scenarioIndex];

                if (!config.HasBehaviorScenario(expectedScenario))
                {
                    errors++;
                    Debug.LogError(
                        $"EnemyConfig validation: missing behavior scenario {expectedScenario} at {path}",
                        config);
                }
            }

            if (config.WeaponGroupCount == 0)
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: no valid weapon groups at {path}", config);
            }

            if (config.NamePool == null ||
                config.NamePool.NameCount < 100)
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: name pool has less than 100 names at {path}", config);
            }

            SerializedObject serializedConfig =
                new SerializedObject(config);

            SerializedProperty mapSpriteProperty =
                serializedConfig.FindProperty("mapSprite");

            if (mapSpriteProperty == null ||
                mapSpriteProperty.objectReferenceValue == null)
            {
                errors++;
                Debug.LogError($"EnemyConfig validation: mapSprite is not assigned at {path}", config);
            }
        }

        EditorUtility.DisplayDialog(
            "EnemyConfig validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/05. Enemy Configs/Delete generated EnemyConfig assets")]
    public static void DeleteGeneratedEnemyConfigs()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated EnemyConfig assets",
                $"Delete all generated EnemyConfig assets inside:\n{OutputRoot}\n\nName pools are not deleted.",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:EnemyConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedEnemyConfigPath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EnemyConfig generator",
            $"Deleted EnemyConfig assets: {deleted}.",
            "OK");
    }

    private static int WriteEnemyConfig(
        EnemyConfig asset,
        string id,
        WeaponGroupEnemyFaction faction,
        int level,
        Dictionary<string, NpcBehaviourScenarioConfig> scenarios,
        List<WeaponGroupConfig> weaponGroups,
        Sprite mapSprite,
        EnemyNamePoolConfig namePool)
    {
        EnemyStats stats =
            BuildStats(faction, level);

        SerializedObject serializedObject =
            new SerializedObject(asset);

        serializedObject.Update();

        SetString(serializedObject, "id", id);
        SetString(serializedObject, "displayName", BuildDisplayName(faction, level));
        SetString(serializedObject, "description", BuildDescription(faction, level));

        SetInt(serializedObject, "baseHullMin", stats.HullMin);
        SetInt(serializedObject, "baseHullMax", stats.HullMax);
        SetInt(serializedObject, "baseShieldMin", stats.ShieldMin);
        SetInt(serializedObject, "baseShieldMax", stats.ShieldMax);
        SetInt(serializedObject, "baseEnergyMin", stats.EnergyMin);
        SetInt(serializedObject, "baseEnergyMax", stats.EnergyMax);
        SetFloat(serializedObject, "baseSpeedMin", stats.SpeedMin);
        SetFloat(serializedObject, "baseSpeedMax", stats.SpeedMax);
        SetInt(serializedObject, "level", level);
        SetEnum(serializedObject, "archetype", BuildArchetype(faction));
        SetObject(serializedObject, "mapSprite", mapSprite);
        SetObject(serializedObject, "namePool", namePool);

        int boundScenarios =
            WriteScenarioEntries(
                serializedObject,
                faction,
                level,
                scenarios);

        WriteWeaponGroups(
            serializedObject,
            weaponGroups);

        SetInt(serializedObject, "creditRewardMin", stats.CreditRewardMin);
        SetInt(serializedObject, "creditRewardMax", stats.CreditRewardMax);
        SetInt(serializedObject, "xpRewardMin", stats.XpRewardMin);
        SetInt(serializedObject, "xpRewardMax", stats.XpRewardMax);
        SetInt(serializedObject, "dangerTier", stats.DangerTier);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        asset.name = id;

        return boundScenarios;
    }

    private static int WriteScenarioEntries(
        SerializedObject serializedObject,
        WeaponGroupEnemyFaction faction,
        int level,
        Dictionary<string, NpcBehaviourScenarioConfig> scenarios)
    {
        List<AllyBehaviourScenario> scenarioList =
            BuildScenarioList();

        SerializedProperty array =
            serializedObject.FindProperty("behaviorScenarios");

        if (array == null || !array.isArray)
            throw new InvalidOperationException("EnemyConfig.behaviorScenarios field was not found.");

        array.arraySize = scenarioList.Count;

        int bound = 0;

        for (int i = 0; i < scenarioList.Count; i++)
        {
            AllyBehaviourScenario scenario =
                scenarioList[i];

            SerializedProperty item =
                array.GetArrayElementAtIndex(i);

            SerializedProperty scenarioProperty =
                item.FindPropertyRelative("scenario");

            SerializedProperty behaviorConfigProperty =
                item.FindPropertyRelative("behaviorConfig");

            if (scenarioProperty == null || behaviorConfigProperty == null)
            {
                throw new InvalidOperationException(
                    "NpcBehaviourScenarioEntry fields scenario/behaviorConfig were not found.");
            }

            scenarioProperty.enumValueIndex =
                Array.IndexOf(
                    Enum.GetValues(typeof(AllyBehaviourScenario)),
                    scenario);

            string exactKey =
                BuildScenarioKey(faction, level, scenario);

            string genericKey =
                BuildGenericScenarioKey(scenario);

            NpcBehaviourScenarioConfig behaviorConfig = null;

            if (!scenarios.TryGetValue(exactKey, out behaviorConfig))
                scenarios.TryGetValue(genericKey, out behaviorConfig);

            behaviorConfigProperty.objectReferenceValue = behaviorConfig;

            if (behaviorConfig != null)
                bound++;
        }

        return bound;
    }

    private static void WriteWeaponGroups(
        SerializedObject serializedObject,
        List<WeaponGroupConfig> weaponGroups)
    {
        SerializedProperty array =
            serializedObject.FindProperty("weaponGroups");

        if (array == null || !array.isArray)
            throw new InvalidOperationException("EnemyConfig.weaponGroups field was not found.");

        array.arraySize = weaponGroups.Count;

        for (int i = 0; i < weaponGroups.Count; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue =
                weaponGroups[i];
        }
    }

    private static Dictionary<WeaponGroupEnemyFaction, EnemyNamePoolConfig>
        CreateOrUpdateNamePools()
    {
        Dictionary<WeaponGroupEnemyFaction, EnemyNamePoolConfig> result =
            new Dictionary<WeaponGroupEnemyFaction, EnemyNamePoolConfig>();

        foreach (WeaponGroupEnemyFaction faction in GetFactions())
        {
            string id =
                $"name_pool_enemy_{GetFactionKey(faction)}_100";

            string path =
                $"{NamePoolRoot}/{id}.asset";

            EnemyNamePoolConfig asset =
                FindExistingAsset<EnemyNamePoolConfig>(
                    id,
                    path,
                    NamePoolRoot);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyNamePoolConfig>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serializedObject =
                new SerializedObject(asset);

            serializedObject.Update();

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", $"{BuildFactionName(faction)} - пул имён");
            SetString(serializedObject, "description", $"100 имён для врагов фракции {BuildFactionName(faction)}.");
            SetEnum(serializedObject, "faction", faction);

            SerializedProperty namesProperty =
                serializedObject.FindProperty("names");

            if (namesProperty == null || !namesProperty.isArray)
                throw new InvalidOperationException("EnemyNamePoolConfig.names field was not found.");

            namesProperty.arraySize = 100;

            string[] generatedNames =
                BuildNames(faction);

            for (int i = 0; i < generatedNames.Length; i++)
            {
                namesProperty
                    .GetArrayElementAtIndex(i)
                    .stringValue = generatedNames[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            asset.name = id;
            EditorUtility.SetDirty(asset);
            result[faction] = asset;
        }

        return result;
    }

    private static Dictionary<string, NpcBehaviourScenarioConfig> FindScenarioConfigs()
    {
        Dictionary<string, NpcBehaviourScenarioConfig> result =
            new Dictionary<string, NpcBehaviourScenarioConfig>();

        string[] guids =
            AssetDatabase.FindAssets("t:NpcBehaviourScenarioConfig", ScenarioRoots);

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            NpcBehaviourScenarioConfig config =
                AssetDatabase.LoadAssetAtPath<NpcBehaviourScenarioConfig>(path);

            if (config == null)
                continue;

            string lowerPath =
                path.Replace("\\", "/").ToLowerInvariant();

            if (!IsEnemyScenarioPath(lowerPath))
                continue;

            if (!TryParseScenarioFromPath(lowerPath, out AllyBehaviourScenario scenario))
                continue;

            if (TryParseFactionFromPath(lowerPath, out WeaponGroupEnemyFaction faction) &&
                TryParseLevelFromPath(lowerPath, out int level))
            {
                result[BuildScenarioKey(faction, level, scenario)] = config;
                continue;
            }

            result[BuildGenericScenarioKey(scenario)] = config;
        }

        return result;
    }

    private static Dictionary<WeaponGroupEnemyFaction, Sprite> FindMapSprites()
    {
        Dictionary<WeaponGroupEnemyFaction, Sprite> result =
            new Dictionary<WeaponGroupEnemyFaction, Sprite>();

        List<string> paths =
            FindArtShipAssetPaths("t:Sprite");

        paths.Sort(StringComparer.Ordinal);

        for (int i = 0; i < paths.Count; i++)
        {
            string path =
                paths[i];

            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(path);

            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                Sprite sprite =
                    assets[assetIndex] as Sprite;

                if (sprite == null)
                    continue;

                string searchable =
                    (path + "_" + sprite.name + "_")
                    .Replace("\\", "/")
                    .ToLowerInvariant();

                if (!TryParseFactionFromPath(searchable, out WeaponGroupEnemyFaction faction))
                    continue;

                if (!result.ContainsKey(faction))
                    result[faction] = sprite;
            }
        }

        return result;
    }

    private static List<string> FindArtShipAssetPaths(string filter)
    {
        List<string> validRoots =
            new List<string>();

        for (int i = 0; i < ArtShipRootCandidates.Length; i++)
        {
            string root =
                ArtShipRootCandidates[i];

            if (AssetDatabase.IsValidFolder(root))
                validRoots.Add(root);
        }

        List<string> result =
            new List<string>();

        string[] guids =
            validRoots.Count > 0
                ? AssetDatabase.FindAssets(filter, validRoots.ToArray())
                : AssetDatabase.FindAssets(filter);

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            string lowerPath =
                path.Replace("\\", "/").ToLowerInvariant();

            if (validRoots.Count == 0 &&
                !lowerPath.Contains("/art_ships/"))
            {
                continue;
            }

            if (!result.Contains(path))
                result.Add(path);
        }

        return result;
    }

    private static T FindExistingAsset<T>(
        string id,
        string preferredPath,
        string searchRoot)
        where T : UnityEngine.Object
    {
        T preferredAsset =
            AssetDatabase.LoadAssetAtPath<T>(preferredPath);

        if (preferredAsset != null)
            return preferredAsset;

        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[] { searchRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
                continue;

            if (IsAssetIdMatch(asset, path, id))
                return asset;
        }

        return null;
    }

    private static bool IsAssetIdMatch(
        UnityEngine.Object asset,
        string path,
        string expectedId)
    {
        if (asset == null ||
            string.IsNullOrWhiteSpace(expectedId))
        {
            return false;
        }

        if (string.Equals(
                asset.name,
                expectedId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string fileName =
            Path.GetFileNameWithoutExtension(path);

        if (string.Equals(
                fileName,
                expectedId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SerializedProperty idProperty =
            serializedObject.FindProperty("id");

        return idProperty != null &&
               string.Equals(
                   idProperty.stringValue,
                   expectedId,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static List<WeaponGroupConfig> FindWeaponGroups(
        WeaponGroupEnemyFaction faction,
        int level)
    {
        List<WeaponGroupConfig> result =
            new List<WeaponGroupConfig>();

        string[] guids =
            AssetDatabase.FindAssets("t:WeaponGroupConfig", new[] { WeaponGroupsRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            WeaponGroupConfig config =
                AssetDatabase.LoadAssetAtPath<WeaponGroupConfig>(path);

            if (config == null)
                continue;

            if (!IsMatchingWeaponGroup(config, path, faction, level))
                continue;

            result.Add(config);
        }

        result.Sort(
            (left, right) =>
            {
                int rankCompare =
                    left.StrengthRank.CompareTo(right.StrengthRank);

                if (rankCompare != 0)
                    return rankCompare;

                return string.CompareOrdinal(left.Id, right.Id);
            });

        return result;
    }

    private static List<AllyBehaviourScenario> BuildScenarioList()
    {
        return new List<AllyBehaviourScenario>
        {
            AllyBehaviourScenario.Normal,
            AllyBehaviourScenario.EnemyInvasion,
            AllyBehaviourScenario.EnemySystemInvasion
        };
    }

    private static EnemyStats BuildStats(
        WeaponGroupEnemyFaction faction,
        int level)
    {
        EnemyStats baseStats =
            GetBaseStats(faction);

        float growth =
            1f + 0.18f * (level - 1);

        int hullMin =
            Mathf.RoundToInt(baseStats.HullMin * growth);

        int shieldMin =
            Mathf.RoundToInt(baseStats.ShieldMin * growth);

        int energyMin =
            Mathf.RoundToInt(baseStats.EnergyMin * growth);

        float speedMin =
            baseStats.SpeedMin + 0.8f * (level - 1);

        int creditRewardMin =
            Mathf.RoundToInt(baseStats.CreditRewardMin * growth);

        int xpRewardMin =
            Mathf.RoundToInt(baseStats.XpRewardMin * growth);

        int dangerTier =
            Mathf.Clamp(1 + ((level - 1) / 2), 1, 5);

        return new EnemyStats
        {
            HullMin = hullMin,
            HullMax = hullMin + Mathf.RoundToInt(14 + level * 4),
            ShieldMin = shieldMin,
            ShieldMax = shieldMin + Mathf.RoundToInt(10 + level * 3),
            EnergyMin = energyMin,
            EnergyMax = energyMin + Mathf.RoundToInt(12 + level * 3),
            SpeedMin = Round(speedMin),
            SpeedMax = Round(speedMin + 2.75f + level * 0.3f),
            CreditRewardMin = creditRewardMin,
            CreditRewardMax = creditRewardMin + 40 + level * 25,
            XpRewardMin = xpRewardMin,
            XpRewardMax = xpRewardMin + 6 + level * 3,
            DangerTier = dangerTier
        };
    }

    private static EnemyStats GetBaseStats(WeaponGroupEnemyFaction faction)
    {
        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                return new EnemyStats
                {
                    HullMin = 90,
                    ShieldMin = 110,
                    EnergyMin = 120,
                    SpeedMin = 45f,
                    CreditRewardMin = 150,
                    XpRewardMin = 20
                };

            case WeaponGroupEnemyFaction.Ancients:
                return new EnemyStats
                {
                    HullMin = 130,
                    ShieldMin = 140,
                    EnergyMin = 150,
                    SpeedMin = 38f,
                    CreditRewardMin = 220,
                    XpRewardMin = 30
                };

            case WeaponGroupEnemyFaction.Infected:
                return new EnemyStats
                {
                    HullMin = 120,
                    ShieldMin = 70,
                    EnergyMin = 100,
                    SpeedMin = 50f,
                    CreditRewardMin = 180,
                    XpRewardMin = 24
                };

            default:
                return new EnemyStats
                {
                    HullMin = 90,
                    ShieldMin = 80,
                    EnergyMin = 100,
                    SpeedMin = 42f,
                    CreditRewardMin = 150,
                    XpRewardMin = 20
                };
        }
    }

    private static EnemyArchetype BuildArchetype(
        WeaponGroupEnemyFaction faction)
    {
        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                return EnemyArchetype.Kiter;

            case WeaponGroupEnemyFaction.Ancients:
                return EnemyArchetype.Tank;

            case WeaponGroupEnemyFaction.Infected:
                return EnemyArchetype.Chaser;

            default:
                return EnemyArchetype.Chaser;
        }
    }

    private static string[] BuildNames(WeaponGroupEnemyFaction faction)
    {
        string prefix;

        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                prefix = "Протокол";
                break;

            case WeaponGroupEnemyFaction.Ancients:
                prefix = "Реликт";
                break;

            case WeaponGroupEnemyFaction.Infected:
                prefix = "Заражённый";
                break;

            default:
                prefix = "Враг";
                break;
        }

        string[] suffixes =
        {
            "Нова", "Орион", "Вега", "Атлас", "Гелиос",
            "Кеплер", "Саган", "Зенит", "Астра", "Пионер"
        };

        string[] result =
            new string[100];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] =
                $"{prefix} {suffixes[i % suffixes.Length]}-{i + 1:00}";
        }

        return result;
    }

    private static WeaponGroupEnemyFaction[] GetFactions()
    {
        return new[]
        {
            WeaponGroupEnemyFaction.AI,
            WeaponGroupEnemyFaction.Ancients,
            WeaponGroupEnemyFaction.Infected
        };
    }

    private static bool IsGeneratedEnemyConfigPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalizedPath =
            path.Replace("\\", "/");

        foreach (WeaponGroupEnemyFaction faction in GetFactions())
        {
            string factionFolder =
                $"{OutputRoot}/{GetFactionFolder(faction)}/";

            if (normalizedPath.StartsWith(factionFolder, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsEnemyScenarioPath(string lowerPath)
    {
        if (string.IsNullOrWhiteSpace(lowerPath))
            return false;

        string normalizedPath =
            lowerPath.Replace("\\", "/");

        return normalizedPath.Contains("/npcbehaviourscenarios/enemy/");
    }

    private static bool TryParseScenarioFromPath(
        string lowerPath,
        out AllyBehaviourScenario scenario)
    {
        scenario = AllyBehaviourScenario.Normal;

        if (lowerPath.Contains("enemy_system_invasion"))
        {
            scenario = AllyBehaviourScenario.EnemySystemInvasion;
            return true;
        }

        if (lowerPath.Contains("enemy_invasion"))
        {
            scenario = AllyBehaviourScenario.EnemyInvasion;
            return true;
        }

        if (lowerPath.Contains("normal"))
        {
            scenario = AllyBehaviourScenario.Normal;
            return true;
        }

        return false;
    }

    private static bool TryParseFactionFromPath(
        string lowerPath,
        out WeaponGroupEnemyFaction faction)
    {
        faction = WeaponGroupEnemyFaction.AI;

        if (lowerPath.Contains("/ai/") || lowerPath.Contains("_ai_"))
        {
            faction = WeaponGroupEnemyFaction.AI;
            return true;
        }

        if (lowerPath.Contains("/ancients/") ||
            lowerPath.Contains("/ancient/") ||
            lowerPath.Contains("_ancients_") ||
            lowerPath.Contains("_ancient_"))
        {
            faction = WeaponGroupEnemyFaction.Ancients;
            return true;
        }

        if (lowerPath.Contains("/infected/") || lowerPath.Contains("_infected_"))
        {
            faction = WeaponGroupEnemyFaction.Infected;
            return true;
        }

        return false;
    }

    private static bool TryParseLevelFromPath(
        string lowerPath,
        out int level)
    {
        level = 0;

        for (int i = 1; i <= 10; i++)
        {
            string token =
                $"l{i:00}";

            if (!lowerPath.Contains(token))
                continue;

            level = i;
            return true;
        }

        return false;
    }

    private static bool IsMatchingWeaponGroup(
        WeaponGroupConfig config,
        string path,
        WeaponGroupEnemyFaction faction,
        int level)
    {
        if (config == null)
            return false;

        if (config.GroupKind == WeaponGroupKind.Enemy &&
            config.EnemyFaction == faction &&
            config.Level == level)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string lowerPath =
            path.Replace("\\", "/").ToLowerInvariant();

        if (!lowerPath.Contains("/weapongroups/enemy/"))
            return false;

        if (!TryParseFactionFromPath(lowerPath, out WeaponGroupEnemyFaction parsedFaction))
            return false;

        if (parsedFaction != faction)
            return false;

        if (!TryParseLevelFromPath(lowerPath, out int parsedLevel))
            return false;

        return parsedLevel == level;
    }

    private static string BuildScenarioKey(
        WeaponGroupEnemyFaction faction,
        int level,
        AllyBehaviourScenario scenario)
    {
        return $"{GetFactionKey(faction)}|L{level:00}|{scenario}";
    }

    private static string BuildGenericScenarioKey(AllyBehaviourScenario scenario)
    {
        return $"generic|{scenario}";
    }

    private static string GetFactionKey(WeaponGroupEnemyFaction faction)
    {
        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                return "ai";

            case WeaponGroupEnemyFaction.Ancients:
                return "ancients";

            case WeaponGroupEnemyFaction.Infected:
                return "infected";

            default:
                return faction.ToString().ToLowerInvariant();
        }
    }

    private static string GetFactionFolder(WeaponGroupEnemyFaction faction)
    {
        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                return "AI";

            case WeaponGroupEnemyFaction.Ancients:
                return "Ancients";

            case WeaponGroupEnemyFaction.Infected:
                return "Infected";

            default:
                return faction.ToString();
        }
    }

    private static string BuildFactionName(WeaponGroupEnemyFaction faction)
    {
        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                return "ИИ";

            case WeaponGroupEnemyFaction.Ancients:
                return "Древние";

            case WeaponGroupEnemyFaction.Infected:
                return "Заражённые";

            default:
                return faction.ToString();
        }
    }

    private static string BuildDisplayName(
        WeaponGroupEnemyFaction faction,
        int level)
    {
        return $"{BuildFactionName(faction)} ур. {level}";
    }

    private static string BuildDescription(
        WeaponGroupEnemyFaction faction,
        int level)
    {
        return $"{BuildFactionName(faction)}, уровень {level}. Сгенерированный конфиг врага: фракция, уровень, сценарии поведения, группы оружия и пул имён.";
    }

    private static float Round(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent =
            Path.GetDirectoryName(path)?.Replace("\\", "/");

        string folder =
            Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException($"Invalid Unity folder path: {path}");

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

    private static void SetInt(
        SerializedObject serializedObject,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"Serialized int field not found: {propertyName}");

        property.intValue = value;
    }

    private static void SetFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"Serialized float field not found: {propertyName}");

        property.floatValue = value;
    }

    private static void SetEnum(
        SerializedObject serializedObject,
        string propertyName,
        Enum value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"Serialized enum field not found: {propertyName}");

        Array enumValues =
            Enum.GetValues(value.GetType());

        int index =
            Array.IndexOf(enumValues, value);

        if (index < 0)
            throw new InvalidOperationException($"Enum value {value} not found for {propertyName}");

        property.enumValueIndex = index;
    }

    private static void SetObject(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"Serialized object field not found: {propertyName}");

        property.objectReferenceValue = value;
    }

    private struct EnemyStats
    {
        public int HullMin;
        public int HullMax;
        public int ShieldMin;
        public int ShieldMax;
        public int EnergyMin;
        public int EnergyMax;
        public float SpeedMin;
        public float SpeedMax;
        public int CreditRewardMin;
        public int CreditRewardMax;
        public int XpRewardMin;
        public int XpRewardMax;
        public int DangerTier;
    }
}
