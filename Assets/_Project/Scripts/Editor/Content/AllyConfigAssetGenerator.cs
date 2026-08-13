using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AllyConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/Ally";

    private const string NamePoolRoot =
        "Assets/_Project/Content/Configs/AllyNames";

    private const string WeaponGroupsRoot =
        "Assets/_Project/Content/Configs/WeaponGroups/Ally";

    private static readonly string[] ArtShipRootCandidates =
    {
        "Assets/Art_ships",
        "Assets/Art/Ships"
    };

    private static readonly string[] ScenarioRoots =
    {
        "Assets/_Project/Content/Configs/NpcBehaviourScenarios/Ally",
        "Assets/_Project/Content/Configs/AllyBehaviourScenarios"
    };

    [MenuItem("STAR FRONTIER/Content/04. Ally Configs/Create generated AllyConfig assets")]
    public static void CreateGeneratedAllyConfigs()
    {
        EnsureFolder(OutputRoot);
        EnsureFolder(NamePoolRoot);

        Dictionary<AllyRole2A, AllyNamePoolConfig> namePools =
            CreateOrUpdateNamePools();

        Dictionary<string, NpcBehaviourScenarioConfig> scenarios =
            FindScenarioConfigs();

        Dictionary<string, Sprite> mapSprites =
            FindMapSprites();

        int createdOrUpdated = 0;
        int warnings = 0;
        int totalBoundWeaponGroups = 0;
        int totalBoundSprites = 0;

        foreach (AllyRole2A role in GetRoles())
        {
            string roleFolder =
                $"{OutputRoot}/{GetRoleFolder(role)}";

            EnsureFolder(roleFolder);

            int firstLevel =
                role == AllyRole2A.Ranger
                    ? 0
                    : 1;

            for (int level = firstLevel; level <= 10; level++)
            {
                string id =
                    $"ally_{GetRoleKey(role)}_L{level:00}_01";

                string path =
                    $"{roleFolder}/{id}.asset";

                AllyConfig asset =
                    FindExistingAsset<AllyConfig>(
                        id,
                        path,
                        OutputRoot);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<AllyConfig>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                Sprite mapSprite = null;

                if (mapSprites.TryGetValue(BuildSpriteKey(role, level), out mapSprite))
                {
                    totalBoundSprites++;
                }
                else
                {
                    warnings++;
                    Debug.LogWarning(
                        $"AllyConfig generator: no map sprite found in ally* art folders for {role} L{level:00}.",
                        asset);
                }

                List<WeaponGroupConfig> weaponGroups =
                    FindWeaponGroups(role, level);

                if (weaponGroups.Count == 0 &&
                    !IsRangerLevelZero(role, level))
                {
                    warnings++;
                    Debug.LogWarning(
                        $"AllyConfig generator: no weapon groups for {role} L{level:00}.");
                }
                else
                {
                    totalBoundWeaponGroups += weaponGroups.Count;
                }

                WriteAllyConfig(
                    asset,
                    id,
                    role,
                    level,
                    scenarios,
                    weaponGroups,
                    mapSprite,
                    namePools.TryGetValue(role, out AllyNamePoolConfig namePool)
                        ? namePool
                        : null);

                EditorUtility.SetDirty(asset);
                createdOrUpdated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "AllyConfig generator",
            "Done. " +
            $"AllyConfig assets: {createdOrUpdated}. " +
            $"Ally scenario refs found: {scenarios.Count}. " +
            $"Weapon groups bound: {totalBoundWeaponGroups}. " +
            $"Map sprites bound: {totalBoundSprites}. " +
            $"Warnings: {warnings}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/04. Ally Configs/Validate generated AllyConfig assets")]
    public static void ValidateGeneratedAllyConfigs()
    {
        int errors = 0;
        int checkedAssets = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:AllyConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedAllyConfigPath(path))
                continue;

            AllyConfig config =
                AssetDatabase.LoadAssetAtPath<AllyConfig>(path);

            if (config == null)
                continue;

            checkedAssets++;

            if (string.IsNullOrWhiteSpace(config.Id))
            {
                errors++;
                Debug.LogError($"AllyConfig validation: empty id at {path}", config);
            }

            if (!IsAllowedGeneratedLevel(config.Role, config.Level))
            {
                errors++;
                Debug.LogError($"AllyConfig validation: invalid level at {path}", config);
            }

            if (config.BaseHullMin <= 0 ||
                config.BaseHullMax < config.BaseHullMin ||
                config.BaseShieldMin < 0 ||
                config.BaseShieldMax < config.BaseShieldMin ||
                config.BaseEnergyMin < 0 ||
                config.BaseEnergyMax < config.BaseEnergyMin ||
                config.BaseSpeedMin <= 0f ||
                config.BaseSpeedMax < config.BaseSpeedMin ||
                config.BaseEnergyRegen < 0f ||
                config.BaseAcceleration <= 0f ||
                config.BaseTurnRate <= 0f ||
                config.BaseCargoCapacity < 0 ||
                config.WeaponSlotCount < 0 ||
                config.ModuleSlotCount < 0)
            {
                errors++;
                Debug.LogError($"AllyConfig validation: invalid stat range at {path}", config);
            }

            if (config.HasDuplicateBehaviorScenarios())
            {
                errors++;
                Debug.LogError($"AllyConfig validation: duplicate scenarios at {path}", config);
            }

            List<AllyBehaviourScenario> expectedScenarios =
                BuildScenarioList(config.Role);

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
                        $"AllyConfig validation: missing behavior scenario {expectedScenario} at {path}",
                        config);
                }
            }

            if (config.WeaponGroupCount == 0 &&
                !IsRangerLevelZero(config.Role, config.Level))
            {
                errors++;
                Debug.LogError($"AllyConfig validation: no valid weapon groups at {path}", config);
            }

            if (config.NamePool == null || config.NamePool.NameCount < 100)
            {
                errors++;
                Debug.LogError($"AllyConfig validation: name pool has less than 100 names at {path}", config);
            }

            SerializedObject serializedConfig =
                new SerializedObject(config);

            SerializedProperty mapSpriteProperty =
                serializedConfig.FindProperty("mapSprite");

            if (mapSpriteProperty == null ||
                mapSpriteProperty.objectReferenceValue == null)
            {
                errors++;
                Debug.LogError($"AllyConfig validation: mapSprite is not assigned at {path}", config);
            }

            SerializedProperty combatSpriteProperty =
                serializedConfig.FindProperty("combatSprite");

            if (combatSpriteProperty == null ||
                combatSpriteProperty.objectReferenceValue == null)
            {
                errors++;
                Debug.LogError($"AllyConfig validation: combatSprite is not assigned at {path}", config);
            }
        }

        EditorUtility.DisplayDialog(
            "AllyConfig validation",
            $"Checked: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/04. Ally Configs/Delete generated AllyConfig assets")]
    public static void DeleteGeneratedAllyConfigs()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated AllyConfig assets",
                $"Delete all AllyConfig assets inside:\n{OutputRoot}\n\nName pools are not deleted.",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:AllyConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedAllyConfigPath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "AllyConfig generator",
            $"Deleted AllyConfig assets: {deleted}.",
            "OK");
    }

    private static void WriteAllyConfig(
        AllyConfig asset,
        string id,
        AllyRole2A role,
        int level,
        Dictionary<string, NpcBehaviourScenarioConfig> scenarios,
        List<WeaponGroupConfig> weaponGroups,
        Sprite mapSprite,
        AllyNamePoolConfig namePool)
    {
        AllyStats stats =
            BuildStats(role, level);

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SetString(serializedObject, "id", id);
        SetString(serializedObject, "displayName", BuildDisplayName(role, level));
        SetString(serializedObject, "description", BuildDescription(role, level));

        SetInt(serializedObject, "baseHullMin", stats.HullMin);
        SetInt(serializedObject, "baseHullMax", stats.HullMax);
        SetInt(serializedObject, "baseShieldMin", stats.ShieldMin);
        SetInt(serializedObject, "baseShieldMax", stats.ShieldMax);
        SetInt(serializedObject, "baseEnergyMin", stats.EnergyMin);
        SetInt(serializedObject, "baseEnergyMax", stats.EnergyMax);
        SetFloat(serializedObject, "baseEnergyRegen", stats.EnergyRegen);
        SetFloat(serializedObject, "baseSpeedMin", stats.SpeedMin);
        SetFloat(serializedObject, "baseSpeedMax", stats.SpeedMax);
        SetFloat(serializedObject, "baseAcceleration", stats.Acceleration);
        SetFloat(serializedObject, "baseTurnRate", stats.TurnRate);
        SetInt(serializedObject, "baseCargoCapacity", stats.CargoCapacity);
        SetInt(serializedObject, "weaponSlotCount", stats.WeaponSlotCount);
        SetInt(serializedObject, "moduleSlotCount", stats.ModuleSlotCount);
        SetInt(serializedObject, "level", level);
        SetEnum(serializedObject, "role", (int)role);
        SetObject(serializedObject, "mapSprite", mapSprite);
        SetObject(serializedObject, "combatSprite", mapSprite);
        SetObject(serializedObject, "namePool", namePool);

        WriteScenarioEntries(
            serializedObject,
            role,
            level,
            scenarios);

        WriteWeaponGroups(
            serializedObject,
            weaponGroups);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        asset.name = id;
    }

    private static void WriteScenarioEntries(
        SerializedObject serializedObject,
        AllyRole2A role,
        int level,
        Dictionary<string, NpcBehaviourScenarioConfig> scenarios)
    {
        List<AllyBehaviourScenario> roleScenarios =
            BuildScenarioList(role);

        SerializedProperty array =
            serializedObject.FindProperty("behaviorScenarios");

        if (array == null || !array.isArray)
            throw new InvalidOperationException("AllyConfig.behaviorScenarios field was not found.");

        array.arraySize = roleScenarios.Count;

        for (int i = 0; i < roleScenarios.Count; i++)
        {
            AllyBehaviourScenario scenario =
                roleScenarios[i];

            SerializedProperty item =
                array.GetArrayElementAtIndex(i);

            SerializedProperty scenarioProperty =
                item.FindPropertyRelative("scenario");

            SerializedProperty behaviorConfigProperty =
                item.FindPropertyRelative("behaviorConfig");

            scenarioProperty.enumValueIndex =
                Array.IndexOf(
                    Enum.GetValues(typeof(AllyBehaviourScenario)),
                    scenario);

            string exactKey =
                BuildScenarioKey(role, level, scenario);

            string genericKey =
                BuildGenericScenarioKey(scenario);

            if (!scenarios.TryGetValue(
                    exactKey,
                    out NpcBehaviourScenarioConfig behaviorConfig))
            {
                scenarios.TryGetValue(
                    genericKey,
                    out behaviorConfig);
            }

            behaviorConfigProperty.objectReferenceValue =
                behaviorConfig;
        }
    }

    private static void WriteWeaponGroups(
        SerializedObject serializedObject,
        List<WeaponGroupConfig> weaponGroups)
    {
        SerializedProperty array =
            serializedObject.FindProperty("weaponGroups");

        if (array == null || !array.isArray)
            throw new InvalidOperationException("AllyConfig.weaponGroups field was not found.");

        array.arraySize = weaponGroups.Count;

        for (int i = 0; i < weaponGroups.Count; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue =
                weaponGroups[i];
        }
    }

    private static Dictionary<AllyRole2A, AllyNamePoolConfig> CreateOrUpdateNamePools()
    {
        Dictionary<AllyRole2A, AllyNamePoolConfig> result =
            new Dictionary<AllyRole2A, AllyNamePoolConfig>();

        foreach (AllyRole2A role in GetRoles())
        {
            string id =
                $"name_pool_ally_{GetRoleKey(role)}_100";

            string path =
                $"{NamePoolRoot}/{id}.asset";

            AllyNamePoolConfig asset =
                FindExistingAsset<AllyNamePoolConfig>(
                    id,
                    path,
                    NamePoolRoot);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AllyNamePoolConfig>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serializedObject =
                new SerializedObject(asset);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", $"{BuildRoleName(role)} name pool");
            SetString(serializedObject, "description", $"100 runtime names for {BuildRoleName(role)} allies.");
            SetEnum(serializedObject, "role", (int)role);

            SerializedProperty namesProperty =
                serializedObject.FindProperty("names");

            namesProperty.arraySize = 100;

            string[] generatedNames =
                BuildNames(role);

            for (int i = 0; i < generatedNames.Length; i++)
            {
                namesProperty
                    .GetArrayElementAtIndex(i)
                    .stringValue = generatedNames[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            result[role] = asset;
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
                path.ToLowerInvariant();

            if (!IsAllyScenarioPath(lowerPath))
                continue;

            if (!TryParseScenarioFromPath(
                    lowerPath,
                    out AllyBehaviourScenario scenario))
            {
                continue;
            }

            if (TryParseRoleFromPath(
                    lowerPath,
                    out AllyRole2A role) &&
                TryParseLevelFromPath(
                    lowerPath,
                    out int level))
            {
                result[BuildScenarioKey(role, level, scenario)] = config;
                continue;
            }

            result[BuildGenericScenarioKey(scenario)] = config;
        }

        return result;
    }

    private static Dictionary<string, Sprite> FindMapSprites()
    {
        Dictionary<string, Sprite> result =
            new Dictionary<string, Sprite>();

        foreach (AllyRole2A role in GetRoles())
        {
            if (role == AllyRole2A.Ranger)
            {
                Sprite levelZeroSprite =
                    FindRangerLevelZeroMapSprite();

                if (levelZeroSprite != null)
                    result[BuildSpriteKey(role, 0)] = levelZeroSprite;
            }

            for (int level = 1; level <= 10; level++)
            {
                Sprite sprite =
                    FindMapSpriteByTokens(
                        BuildMapSpriteTokens(role),
                        level);

                if (sprite == null)
                    continue;

                result[BuildSpriteKey(role, level)] = sprite;
            }
        }

        return result;
    }

    private static Sprite FindRangerLevelZeroMapSprite()
    {
        List<string> paths =
            FindArtShipAssetPaths("t:Texture2D");

        paths.Sort(StringComparer.Ordinal);

        for (int i = 0; i < paths.Count; i++)
        {
            string fileName =
                Path.GetFileNameWithoutExtension(paths[i]);

            if (!string.Equals(
                    fileName,
                    "ally_ranger_level_00_01",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Sprite sprite =
                FindFirstSpriteAtPath(paths[i]);

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static string BuildSpriteKey(
        AllyRole2A role,
        int level)
    {
        return $"{GetRoleKey(role)}|L{level:00}";
    }

    private static string[] BuildMapSpriteTokens(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Ranger:
                return new[] { "ranger" };

            case AllyRole2A.Military:
                return new[] { "military" };

            case AllyRole2A.Trader:
                return new[] { "trader" };

            case AllyRole2A.Science:
                return new[] { "science" };

            case AllyRole2A.Medic:
                return new[] { "medic" };

            default:
                return new[] { GetRoleKey(role) };
        }
    }

    private static Sprite FindFirstSpriteAtPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(path);

        for (int i = 0; i < assets.Length; i++)
        {
            sprite = assets[i] as Sprite;

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static Sprite FindMapSpriteByTokens(
        string[] tokens,
        int level)
    {
        List<string> paths =
            FindArtShipAssetPaths("t:Texture2D");

        paths.Sort(StringComparer.Ordinal);

        for (int i = 0; i < paths.Count; i++)
        {
            string searchable =
                paths[i].Replace("\\", "/").ToLowerInvariant();

            if (!TryParseFirstNumberFromFileName(paths[i], out int spriteLevel) ||
                spriteLevel != level)
            {
                continue;
            }

            if (!ContainsAllTokens(searchable, tokens))
                continue;

            Sprite sprite =
                FindFirstSpriteAtPath(paths[i]);

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static bool TryParseFirstNumberFromFileName(
        string path,
        out int number)
    {
        number = 0;

        string fileName =
            Path.GetFileNameWithoutExtension(path);

        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        for (int i = 0; i < fileName.Length; i++)
        {
            if (!char.IsDigit(fileName[i]))
                continue;

            int startIndex = i;

            while (i < fileName.Length &&
                   char.IsDigit(fileName[i]))
            {
                i++;
            }

            string token =
                fileName.Substring(startIndex, i - startIndex);

            return int.TryParse(token, out number);
        }

        return false;
    }

    private static bool ContainsAllTokens(
        string value,
        string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            tokens == null)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!value.Contains(tokens[i].ToLowerInvariant()))
                return false;
        }

        return true;
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

            if (!IsAllyArtShipPath(lowerPath))
            {
                continue;
            }

            if (!result.Contains(path))
                result.Add(path);
        }

        return result;
    }

    private static bool IsAllyArtShipPath(string lowerPath)
    {
        if (string.IsNullOrWhiteSpace(lowerPath))
            return false;

        string[] parts =
            lowerPath.Replace("\\", "/").Split('/');

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("ally", StringComparison.Ordinal))
                return true;
        }

        return false;
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
        AllyRole2A role,
        int level)
    {
        List<WeaponGroupConfig> result =
            new List<WeaponGroupConfig>();

        string[] guids =
            AssetDatabase.FindAssets("t:WeaponGroupConfig", new[] { WeaponGroupsRoot });

        WeaponGroupAllyType expectedType =
            ToWeaponGroupAllyType(role);

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            WeaponGroupConfig config =
                AssetDatabase.LoadAssetAtPath<WeaponGroupConfig>(path);

            if (config == null)
                continue;

            if (!IsMatchingWeaponGroup(
                    config,
                    path,
                    expectedType,
                    role,
                    level))
            {
                continue;
            }

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

    private static List<AllyBehaviourScenario> BuildScenarioList(AllyRole2A role)
    {
        List<AllyBehaviourScenario> result =
            new List<AllyBehaviourScenario>
            {
                AllyBehaviourScenario.Normal,
                AllyBehaviourScenario.EnemyInvasion
            };

        if (role == AllyRole2A.Ranger ||
            role == AllyRole2A.Military)
        {
            result.Add(AllyBehaviourScenario.EnemySystemInvasion);
        }

        return result;
    }

    private static AllyStats BuildStats(AllyRole2A role, int level)
    {
        if (IsRangerLevelZero(role, level))
            return BuildRangerLevelZeroStats();

        AllyStats baseStats =
            GetBaseStats(role);

        float growth =
            1f + 0.16f * (level - 1);

        int hullMin =
            Mathf.RoundToInt(baseStats.HullMin * growth);

        int shieldMin =
            Mathf.RoundToInt(baseStats.ShieldMin * growth);

        int energyMin =
            Mathf.RoundToInt(baseStats.EnergyMin * growth);

        float speedMin =
            baseStats.SpeedMin + 0.75f * (level - 1);

        float acceleration =
            baseStats.Acceleration + 0.15f * (level - 1);

        float turnRate =
            baseStats.TurnRate + 1.5f * (level - 1);

        int cargoCapacity =
            baseStats.CargoCapacity + Mathf.Max(0, level - 1) * baseStats.CargoPerLevel;

        return new AllyStats
        {
            HullMin = hullMin,
            HullMax = hullMin + Mathf.RoundToInt(12 + level * 3),
            ShieldMin = shieldMin,
            ShieldMax = shieldMin + Mathf.RoundToInt(8 + level * 2),
            EnergyMin = energyMin,
            EnergyMax = energyMin + Mathf.RoundToInt(10 + level * 2),
            EnergyRegen = Round(baseStats.EnergyRegen + 0.2f * (level - 1)),
            SpeedMin = Round(speedMin),
            SpeedMax = Round(speedMin + 2.5f + level * 0.25f),
            Acceleration = Round(acceleration),
            TurnRate = Round(turnRate),
            CargoCapacity = cargoCapacity,
            WeaponSlotCount = baseStats.WeaponSlotCount,
            ModuleSlotCount = baseStats.ModuleSlotCount
        };
    }

    private static AllyStats BuildRangerLevelZeroStats()
    {
        return new AllyStats
        {
            HullMin = 75,
            HullMax = 90,
            ShieldMin = 35,
            ShieldMax = 45,
            EnergyMin = 70,
            EnergyMax = 85,
            EnergyRegen = 6f,
            SpeedMin = 42f,
            SpeedMax = 45f,
            Acceleration = 4f,
            TurnRate = 95f,
            CargoCapacity = 18,
            CargoPerLevel = 0,
            WeaponSlotCount = 0,
            ModuleSlotCount = 0
        };
    }

    private static AllyStats GetBaseStats(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Military:
                return new AllyStats
                {
                    HullMin = 140,
                    ShieldMin = 100,
                    EnergyMin = 120,
                    EnergyRegen = 8f,
                    SpeedMin = 45f,
                    Acceleration = 5f,
                    TurnRate = 90f,
                    CargoCapacity = 24,
                    CargoPerLevel = 2,
                    WeaponSlotCount = 2,
                    ModuleSlotCount = 2
                };

            case AllyRole2A.Ranger:
                return new AllyStats
                {
                    HullMin = 110,
                    ShieldMin = 80,
                    EnergyMin = 110,
                    EnergyRegen = 10f,
                    SpeedMin = 48f,
                    Acceleration = 6f,
                    TurnRate = 120f,
                    CargoCapacity = 30,
                    CargoPerLevel = 2,
                    WeaponSlotCount = 1,
                    ModuleSlotCount = 2
                };

            case AllyRole2A.Trader:
                return new AllyStats
                {
                    HullMin = 90,
                    ShieldMin = 50,
                    EnergyMin = 100,
                    EnergyRegen = 7f,
                    SpeedMin = 40f,
                    Acceleration = 3.5f,
                    TurnRate = 65f,
                    CargoCapacity = 90,
                    CargoPerLevel = 6,
                    WeaponSlotCount = 1,
                    ModuleSlotCount = 3
                };

            case AllyRole2A.Science:
                return new AllyStats
                {
                    HullMin = 85,
                    ShieldMin = 55,
                    EnergyMin = 130,
                    EnergyRegen = 9f,
                    SpeedMin = 41f,
                    Acceleration = 4f,
                    TurnRate = 75f,
                    CargoCapacity = 45,
                    CargoPerLevel = 3,
                    WeaponSlotCount = 1,
                    ModuleSlotCount = 3
                };


            case AllyRole2A.Medic:
                return new AllyStats
                {
                    HullMin = 75,
                    ShieldMin = 45,
                    EnergyMin = 90,
                    EnergyRegen = 8f,
                    SpeedMin = 42f,
                    Acceleration = 4.5f,
                    TurnRate = 85f,
                    CargoCapacity = 35,
                    CargoPerLevel = 3,
                    WeaponSlotCount = 1,
                    ModuleSlotCount = 2
                };

            default:
                return new AllyStats
                {
                    HullMin = 80,
                    ShieldMin = 40,
                    EnergyMin = 80,
                    EnergyRegen = 6f,
                    SpeedMin = 40f,
                    Acceleration = 4f,
                    TurnRate = 80f,
                    CargoCapacity = 20,
                    CargoPerLevel = 2,
                    WeaponSlotCount = 1,
                    ModuleSlotCount = 1
                };
        }
    }

    private static string[] BuildNames(AllyRole2A role)
    {
        string prefix;

        switch (role)
        {
            case AllyRole2A.Military:
                prefix = "Щит";
                break;

            case AllyRole2A.Ranger:
                prefix = "Следопыт";
                break;

            case AllyRole2A.Trader:
                prefix = "Караван";
                break;

            case AllyRole2A.Science:
                prefix = "Исследователь";
                break;


            case AllyRole2A.Medic:
                prefix = "Милосердие";
                break;

            default:
                prefix = "Союзник";
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
            string suffix =
                suffixes[i % suffixes.Length];

            int number =
                i + 1;

            result[i] =
                $"{prefix} {suffix}-{number:00}";
        }

        return result;
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

    private static bool IsGeneratedAllyConfigPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalizedPath =
            path.Replace("\\", "/");

        foreach (AllyRole2A role in GetRoles())
        {
            string roleFolder =
                $"{OutputRoot}/{GetRoleFolder(role)}/";

            if (normalizedPath.StartsWith(
                    roleFolder,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllyScenarioPath(string lowerPath)
    {
        if (string.IsNullOrWhiteSpace(lowerPath))
            return false;

        string normalizedPath =
            lowerPath.Replace("\\", "/");

        if (normalizedPath.Contains("/npcbehaviourscenarios/ally/"))
            return true;

        if (normalizedPath.Contains("/allybehaviourscenarios/"))
            return true;

        return false;
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

    private static bool TryParseRoleFromPath(
        string lowerPath,
        out AllyRole2A role)
    {
        role = AllyRole2A.Ranger;

        if (lowerPath.Contains("/military/") ||
            lowerPath.Contains("_military_"))
        {
            role = AllyRole2A.Military;
            return true;
        }

        if (lowerPath.Contains("/ranger/") ||
            lowerPath.Contains("_ranger_"))
        {
            role = AllyRole2A.Ranger;
            return true;
        }

        if (lowerPath.Contains("/trader/") ||
            lowerPath.Contains("_trader_"))
        {
            role = AllyRole2A.Trader;
            return true;
        }

        if (lowerPath.Contains("/science/") ||
            lowerPath.Contains("_science_"))
        {
            role = AllyRole2A.Science;
            return true;
        }


        if (lowerPath.Contains("/medic/") ||
            lowerPath.Contains("_medic_") ||
            lowerPath.Contains("_medical_"))
        {
            role = AllyRole2A.Medic;
            return true;
        }

        return false;
    }

    private static bool TryParseLevelFromPath(
        string lowerPath,
        out int level)
    {
        level = 0;

        for (int i = 0; i <= 10; i++)
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

    private static bool IsAllowedGeneratedLevel(
        AllyRole2A role,
        int level)
    {
        if (IsRangerLevelZero(role, level))
            return true;

        return level >= 1 && level <= 10;
    }

    private static bool IsRangerLevelZero(
        AllyRole2A role,
        int level)
    {
        return role == AllyRole2A.Ranger &&
               level == 0;
    }

    private static bool IsMatchingWeaponGroup(
        WeaponGroupConfig config,
        string path,
        WeaponGroupAllyType expectedType,
        AllyRole2A role,
        int level)
    {
        if (config == null)
            return false;

        if (config.GroupKind == WeaponGroupKind.Ally &&
            config.AllyType == expectedType &&
            config.Level == level)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string lowerPath =
            path.Replace("\\", "/").ToLowerInvariant();

        if (!lowerPath.Contains("/weapongroups/ally/"))
            return false;

        if (!TryParseRoleFromPath(lowerPath, out AllyRole2A parsedRole))
            return false;

        if (parsedRole != role)
            return false;

        if (!TryParseLevelFromPath(lowerPath, out int parsedLevel))
            return false;

        return parsedLevel == level;
    }

    private static string BuildScenarioKey(
        AllyRole2A role,
        int level,
        AllyBehaviourScenario scenario)
    {
        return $"{GetRoleKey(role)}|L{level:00}|{scenario}";
    }

    private static string BuildGenericScenarioKey(
        AllyBehaviourScenario scenario)
    {
        return $"generic|{scenario}";
    }

    private static WeaponGroupAllyType ToWeaponGroupAllyType(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Military:
                return WeaponGroupAllyType.Military;

            case AllyRole2A.Ranger:
                return WeaponGroupAllyType.Ranger;

            case AllyRole2A.Trader:
                return WeaponGroupAllyType.Trader;

            case AllyRole2A.Science:
                return WeaponGroupAllyType.Science;


            case AllyRole2A.Medic:
                return WeaponGroupAllyType.Medic;

            default:
                return WeaponGroupAllyType.None;
        }
    }

    private static string GetRoleKey(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Military:
                return "military";

            case AllyRole2A.Ranger:
                return "ranger";

            case AllyRole2A.Trader:
                return "trader";

            case AllyRole2A.Science:
                return "science";


            case AllyRole2A.Medic:
                return "medic";

            default:
                return role.ToString().ToLowerInvariant();
        }
    }

    private static string GetRoleFolder(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Military:
                return "Military";

            case AllyRole2A.Ranger:
                return "Ranger";

            case AllyRole2A.Trader:
                return "Trader";

            case AllyRole2A.Science:
                return "Science";


            case AllyRole2A.Medic:
                return "Medic";

            default:
                return role.ToString();
        }
    }

    private static string BuildRoleName(AllyRole2A role)
    {
        switch (role)
        {
            case AllyRole2A.Military:
                return "Военный";

            case AllyRole2A.Ranger:
                return "Рейнджер";

            case AllyRole2A.Trader:
                return "Торговец";

            case AllyRole2A.Science:
                return "Научный союзник";


            case AllyRole2A.Medic:
                return "Медик";

            default:
                return role.ToString();
        }
    }

    private static string BuildDisplayName(AllyRole2A role, int level)
    {
        return $"{BuildRoleName(role)} ур. {level}";
    }

    private static string BuildDescription(AllyRole2A role, int level)
    {
        return $"{BuildRoleName(role)}, уровень {level}. Сгенерированный конфиг союзника: роль, уровень, сценарии поведения, группы оружия и пул имён.";
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
        int value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"Serialized enum field not found: {propertyName}");

        property.intValue = value;
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

    private struct AllyStats
    {
        public int HullMin;
        public int HullMax;
        public int ShieldMin;
        public int ShieldMax;
        public int EnergyMin;
        public int EnergyMax;
        public float EnergyRegen;
        public float SpeedMin;
        public float SpeedMax;
        public float Acceleration;
        public float TurnRate;
        public int CargoCapacity;
        public int CargoPerLevel;
        public int WeaponSlotCount;
        public int ModuleSlotCount;
    }
}
