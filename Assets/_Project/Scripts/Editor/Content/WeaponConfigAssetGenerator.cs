using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WeaponConfigAssetGenerator
{
    private const string InputCsvPath =
        "Assets/_Project/Content/Configs/Weapons/STAR_FRONTIER_WeaponConfig_300_config_fields_RU_no_enemy_types_v1.4_wave_range.csv";

    private const string OutputRootFolder =
        "Assets/_Project/Content/Configs/Weapons";

    private const string OutputFolderCommon =
        OutputRootFolder + "/Common";

    private const string OutputFolderAI =
        OutputRootFolder + "/AI";

    private const string OutputFolderInfected =
        OutputRootFolder + "/Infected";

    private const string OutputFolderAncients =
        OutputRootFolder + "/Ancients";

    private static readonly string[] OwnerOutputFolders =
    {
        OutputFolderCommon,
        OutputFolderAI,
        OutputFolderInfected,
        OutputFolderAncients
    };

    private static readonly Dictionary<string, string> ProjectilePrefabAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "prefab_weapon_common_pulse_projectile", "PF_weapon_pulse_common_projectile" },
            { "prefab_weapon_ai_pulse_projectile", "PF_weapon_pulse_ai_projectile" },
            { "prefab_weapon_ancients_shard_volley_projectile", "PF_weapon_pulse_ancients_projectile" },
            { "prefab_weapon_infected_burst_projectile", "PF_weapon_pulse_infected_projectile" },

            { "prefab_weapon_common_plasma_projectile", "PF_weapon_heavy_common_projectile" },
            { "prefab_weapon_ai_core_bolt_projectile", "PF_weapon_heavy_ai_projectile" },
            { "prefab_weapon_ancient_plasma_projectile", "PF_weapon_heavy_ancients_projectile" },
            { "prefab_weapon_infected_acid_projectile", "PF_weapon_heavy_infected_projectile" },

            { "prefab_weapon_common_missile_projectile", "PF_weapon_missile_common_projectile" },
            { "prefab_weapon_ai_swarm_missile_projectile", "PF_weapon_missile_ai_projectile" },
            { "prefab_weapon_ancient_orb_projectile", "PF_weapon_missile_ancients_projectile" },
            { "prefab_weapon_ancients_orb_projectile", "PF_weapon_missile_ancients_projectile" },
            { "prefab_weapon_infected_swarm_projectile", "PF_weapon_missile_infected_projectile" },

            { "prefab_weapon_common_wave_cannon_projectile", "PF_weapon_wave_common_projectile" },
            { "prefab_weapon_ai_disruptor_projectile", "PF_weapon_wave_ai_projectile" },
            { "prefab_weapon_ancient_singularity_projectile", "PF_weapon_wave_ancients_projectile" },
            { "prefab_weapon_infected_spore_projectile", "PF_weapon_wave_infected_projectile" },
        };

    private const string MenuRoot =
        "STAR FRONTIER/Content/01. Weapons/";

    [MenuItem(MenuRoot + "Generate WeaponConfig Assets From CSV")]
    public static void GenerateWeaponConfigAssetsFromCsv()
    {
        TextAsset csvAsset =
            AssetDatabase.LoadAssetAtPath<TextAsset>(InputCsvPath);

        if (csvAsset == null)
        {
            EditorUtility.DisplayDialog(
                "WeaponConfig generator",
                "CSV file not found:\n" + InputCsvPath,
                "OK");

            Debug.LogError(
                "[WeaponConfigAssetGenerator] CSV file not found: " +
                InputCsvPath);

            return;
        }

        GenerateFromCsvText(csvAsset.text);
    }

    [MenuItem(MenuRoot + "Validate WeaponConfig CSV Only")]
    public static void ValidateWeaponConfigCsvOnly()
    {
        TextAsset csvAsset =
            AssetDatabase.LoadAssetAtPath<TextAsset>(InputCsvPath);

        if (csvAsset == null)
        {
            EditorUtility.DisplayDialog(
                "WeaponConfig CSV validation",
                "CSV file not found:\n" + InputCsvPath,
                "OK");

            Debug.LogError(
                "[WeaponConfigAssetGenerator] CSV file not found: " +
                InputCsvPath);

            return;
        }

        CsvValidationResult validationResult =
            ValidateCsv(csvAsset.text);

        string message =
            "Rows: " + validationResult.RowCount +
            "\nErrors: " + validationResult.Errors.Count +
            "\nWarnings: " + validationResult.Warnings.Count;

        EditorUtility.DisplayDialog(
            "WeaponConfig CSV validation",
            message,
            "OK");

        LogValidationResult(validationResult);
    }

    [MenuItem(MenuRoot + "Delete WeaponConfig Assets In Owner Folders")]
    public static void DeleteWeaponConfigAssetsInOwnerFoldersMenu()
    {
        var warnings =
            new List<string>();

        EnsureOutputFolders();

        int deletedCount =
            DeleteAssetFilesInOwnerFolders(warnings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning(
                "[WeaponConfigAssetGenerator] " +
                warnings[i]);
        }

        EditorUtility.DisplayDialog(
            "WeaponConfig asset cleanup",
            "Deleted .asset files: " + deletedCount +
            "\nWarnings: " + warnings.Count +
            "\n\nFolders:\n" +
            OutputFolderCommon + "\n" +
            OutputFolderAI + "\n" +
            OutputFolderInfected + "\n" +
            OutputFolderAncients,
            "OK");
    }

    private static void GenerateFromCsvText(string csvText)
    {
        CsvValidationResult validationResult =
            ValidateCsv(csvText);

        if (validationResult.Errors.Count > 0)
        {
            LogValidationResult(validationResult);

            EditorUtility.DisplayDialog(
                "WeaponConfig generator",
                "CSV contains errors. Assets were not generated.\n\nErrors: " +
                validationResult.Errors.Count +
                "\nSee Unity Console for details.",
                "OK");

            return;
        }

        EnsureOutputFolders();

        int createdCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;

        var warnings = new List<string>(validationResult.Warnings);

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0; i < validationResult.Rows.Count; i++)
            {
                Dictionary<string, string> row =
                    validationResult.Rows[i];

                string id =
                    Get(row, "id");

                string targetFolder =
                    GetOutputFolderForWeaponOwner(
                        Get(row, "weaponOwner"),
                        id,
                        warnings);

                string targetAssetPath =
                    targetFolder + "/" + id + ".asset";

                string assetPath;

                WeaponConfig config =
                    FindExistingWeaponConfigAsset(
                        id,
                        targetAssetPath,
                        targetFolder,
                        warnings,
                        out assetPath);

                bool created = false;

                if (config == null)
                {
                    config =
                        ScriptableObject.CreateInstance<WeaponConfig>();

                    AssetDatabase.CreateAsset(config, assetPath);
                    created = true;
                }
                else if (!string.Equals(assetPath, targetAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    string moveError =
                        AssetDatabase.MoveAsset(assetPath, targetAssetPath);

                    if (string.IsNullOrEmpty(moveError))
                    {
                        assetPath =
                            targetAssetPath;
                    }
                    else
                    {
                        warnings.Add(
                            RowPrefix(id) +
                            "Existing asset was updated in place because it could not be moved to target folder. " +
                            "From: " + assetPath +
                            ". To: " + targetAssetPath +
                            ". Error: " + moveError);
                    }
                }

                SerializedObject serializedObject =
                    new SerializedObject(config);

                List<string> rowWarnings =
                    ApplyRowToWeaponConfig(serializedObject, row, id);

                warnings.AddRange(rowWarnings);

                bool hasRequiredMissingField =
                    ContainsRequiredMissingField(rowWarnings);

                if (hasRequiredMissingField)
                {
                    skippedCount++;

                    if (created)
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }

                    Debug.LogError(
                        "[WeaponConfigAssetGenerator] Asset skipped because required serialized fields are missing: " +
                        id);

                    continue;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(config);

                if (created)
                    createdCount++;
                else
                    updatedCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning(
                "[WeaponConfigAssetGenerator] " +
                warnings[i]);
        }

        string resultMessage =
            "WeaponConfig generation complete." +
            "\nCreated: " + createdCount +
            "\nUpdated: " + updatedCount +
            "\nSkipped: " + skippedCount +
            "\nWarnings: " + warnings.Count +
            "\n\nOutput folders:" +
            "\n" + OutputFolderCommon +
            "\n" + OutputFolderAI +
            "\n" + OutputFolderInfected +
            "\n" + OutputFolderAncients;

        EditorUtility.DisplayDialog(
            "WeaponConfig generator",
            resultMessage,
            "OK");

        Debug.Log(
            "[WeaponConfigAssetGenerator] " +
            resultMessage);
    }

    private static WeaponConfig FindExistingWeaponConfigAsset(
        string id,
        string targetAssetPath,
        string targetFolder,
        List<string> warnings,
        out string assetPath)
    {
        assetPath =
            targetAssetPath;

        WeaponConfig config =
            AssetDatabase.LoadAssetAtPath<WeaponConfig>(targetAssetPath);

        if (config != null)
            return config;

        string fileName =
            id + ".asset";

        for (int i = 0; i < OwnerOutputFolders.Length; i++)
        {
            string candidatePath =
                OwnerOutputFolders[i] + "/" + fileName;

            if (string.Equals(candidatePath, targetAssetPath, StringComparison.OrdinalIgnoreCase))
                continue;

            config =
                AssetDatabase.LoadAssetAtPath<WeaponConfig>(candidatePath);

            if (config == null)
                continue;

            assetPath =
                candidatePath;

            return config;
        }

        string[] guids =
            AssetDatabase.FindAssets(id + " t:WeaponConfig", OwnerOutputFolders);

        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsAssetInOwnerFolder(candidatePath))
                continue;

            string candidateName =
                Path.GetFileNameWithoutExtension(candidatePath);

            if (!string.Equals(candidateName, id, StringComparison.OrdinalIgnoreCase))
                continue;

            config =
                AssetDatabase.LoadAssetAtPath<WeaponConfig>(candidatePath);

            if (config == null)
                continue;

            assetPath =
                candidatePath;

            return config;
        }

        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            warnings.Add(
                RowPrefix(id) +
                "Target folder is missing. Asset will be created at requested path after folder creation: " +
                targetAssetPath);
        }

        return null;
    }

    private static List<string> ApplyRowToWeaponConfig(
    SerializedObject serializedObject,
    Dictionary<string, string> row,
    string rowId)
    {
        var warnings =
            new List<string>();

        SetRequiredString(serializedObject, "id", Get(row, "id"), rowId, warnings);
        SetRequiredString(serializedObject, "displayName", Get(row, "displayName"), rowId, warnings);
        SetRequiredString(serializedObject, "description", Get(row, "description"), rowId, warnings);

        SetSpriteReference(serializedObject, "icon", Get(row, "icon"), rowId, warnings);

        SetRequiredInt(serializedObject, "level", Get(row, "level"), rowId, warnings);
        SetRequiredEnum(serializedObject, "equipmentTier", Get(row, "equipmentTier"), rowId, warnings);

        ApplyWeaponOwner(serializedObject, Get(row, "weaponOwner"), rowId, warnings);

        SetRequiredInt(serializedObject, "cargoSize", Get(row, "cargoSize"), rowId, warnings);

        SetRequiredInt(serializedObject, "baseDamageMin", Get(row, "baseDamageMin"), rowId, warnings);
        SetRequiredInt(serializedObject, "baseDamageMax", Get(row, "baseDamageMax"), rowId, warnings);

        SetRequiredFloat(serializedObject, "rangeMin", Get(row, "rangeMin"), rowId, warnings);
        SetRequiredFloat(serializedObject, "rangeMax", Get(row, "rangeMax"), rowId, warnings);

        SetRequiredInt(serializedObject, "energyCostMin", Get(row, "energyCostMin"), rowId, warnings);
        SetRequiredInt(serializedObject, "energyCostMax", Get(row, "energyCostMax"), rowId, warnings);

        SetRequiredInt(serializedObject, "projectileLifetimeMin", Get(row, "projectileLifetimeMin"), rowId, warnings);
        SetRequiredInt(serializedObject, "projectileLifetimeMax", Get(row, "projectileLifetimeMax"), rowId, warnings);

        SetRequiredBool(serializedObject, "isHitscan", Get(row, "isHitscan"), rowId, warnings);

        SetRequiredEnum(serializedObject, "weaponType", Get(row, "weaponType"), rowId, warnings);
        SetRequiredBool(serializedObject, "autoDetectShotType", Get(row, "autoDetectShotType"), rowId, warnings);
        SetRequiredEnum(serializedObject, "shotType", Get(row, "shotType"), rowId, warnings);
        SetRequiredInt(serializedObject, "shotCount", Get(row, "shotCount"), rowId, warnings);
        SetRequiredEnum(serializedObject, "damageType", Get(row, "damageType"), rowId, warnings);
        SetRequiredEnum(serializedObject, "targetingMode", Get(row, "targetingMode"), rowId, warnings);

        SetGameObjectReference(serializedObject, "projectilePrefabRef", Get(row, "projectilePrefabRef"), rowId, warnings);

        SetRequiredBool(serializedObject, "usesAmmo", Get(row, "usesAmmo"), rowId, warnings);
        SetRequiredInt(serializedObject, "maxAmmoChargesMin", Get(row, "maxAmmoChargesMin"), rowId, warnings);
        SetRequiredInt(serializedObject, "maxAmmoChargesMax", Get(row, "maxAmmoChargesMax"), rowId, warnings);

        return warnings;
    }

    private static void ApplyWeaponOwner(
        SerializedObject serializedObject,
        string value,
        string rowId,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        bool applied =
            SetOptionalEnum(serializedObject, "weaponOwner", value, rowId, warnings, false);

        if (applied)
            return;

        applied =
            SetOptionalEnum(serializedObject, "ownerProfile2A", value, rowId, warnings, false);

        if (!applied)
        {
            warnings.Add(
                RowPrefix(rowId) +
                "weaponOwner was not written. Add serialized field 'weaponOwner' or use existing 'ownerProfile2A' in WeaponConfig.");
        }
    }

    private static CsvValidationResult ValidateCsv(string csvText)
    {
        var result =
            new CsvValidationResult();

        List<string[]> records =
            ParseCsv(csvText);

        if (records.Count == 0)
        {
            result.Errors.Add("CSV is empty.");
            return result;
        }

        string[] headers =
            records[0];

        var headerIndexes =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            string header =
                headers[i].Trim();

            if (!headerIndexes.ContainsKey(header))
                headerIndexes.Add(header, i);
        }

        string[] requiredHeaders =
        {
        "id",
        "displayName",
        "description",
        "level",
        "equipmentTier",
        "weaponOwner",
        "cargoSize",
        "baseDamageMin",
        "baseDamageMax",
        "rangeMin",
        "rangeMax",
        "energyCostMin",
        "energyCostMax",
        "projectileLifetimeMin",
        "projectileLifetimeMax",
        "isHitscan",
        "weaponType",
        "autoDetectShotType",
        "shotType",
        "shotCount",
        "damageType",
        "targetingMode",
        "projectilePrefabRef",
        "usesAmmo",
        "maxAmmoChargesMin",
        "maxAmmoChargesMax"
    };

        for (int i = 0; i < requiredHeaders.Length; i++)
        {
            if (!headerIndexes.ContainsKey(requiredHeaders[i]))
            {
                result.Errors.Add(
                    "Missing required CSV column: " +
                    requiredHeaders[i]);
            }
        }

        if (result.Errors.Count > 0)
            return result;

        var ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            string[] record =
                records[recordIndex];

            if (record.Length == 1 && string.IsNullOrWhiteSpace(record[0]))
                continue;

            Dictionary<string, string> row =
                BuildRow(headers, record);

            string id =
                Get(row, "id");

            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(
                    "Row " + (recordIndex + 1) + ": id is empty.");

                continue;
            }

            if (!ids.Add(id))
            {
                result.Errors.Add(
                    "Row " + (recordIndex + 1) + ": duplicate id: " + id);
            }

            ValidateInt(row, "level", id, result.Errors);
            ValidateInt(row, "cargoSize", id, result.Errors);

            ValidateInt(row, "baseDamageMin", id, result.Errors);
            ValidateInt(row, "baseDamageMax", id, result.Errors);

            ValidateFloat(row, "rangeMin", id, result.Errors);
            ValidateFloat(row, "rangeMax", id, result.Errors);

            ValidateInt(row, "energyCostMin", id, result.Errors);
            ValidateInt(row, "energyCostMax", id, result.Errors);

            ValidateInt(row, "projectileLifetimeMin", id, result.Errors);
            ValidateInt(row, "projectileLifetimeMax", id, result.Errors);

            ValidateBool(row, "isHitscan", id, result.Errors);
            ValidateBool(row, "usesAmmo", id, result.Errors);

            ValidateBool(row, "autoDetectShotType", id, result.Errors);
            ValidateInt(row, "shotCount", id, result.Errors);

            ValidateInt(row, "maxAmmoChargesMin", id, result.Errors);
            ValidateInt(row, "maxAmmoChargesMax", id, result.Errors);

            result.Rows.Add(row);
        }

        result.RowCount =
            result.Rows.Count;

        return result;
    }

    private static Dictionary<string, string> BuildRow(
        string[] headers,
        string[] record)
    {
        var row =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            string key =
                headers[i].Trim();

            string value =
                i < record.Length ? record[i] : string.Empty;

            row[key] =
                value == null ? string.Empty : value.Trim();
        }

        return row;
    }

    private static string Get(
        Dictionary<string, string> row,
        string key)
    {
        if (row == null || !row.TryGetValue(key, out string value))
            return string.Empty;

        return value ?? string.Empty;
    }

    private static void SetRequiredString(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(
                RequiredMissing(rowId, propertyName));

            return;
        }

        if (property.propertyType != SerializedPropertyType.String)
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " is not a string field.");

            return;
        }

        property.stringValue =
            value ?? string.Empty;
    }

    private static void SetRequiredInt(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(
                RequiredMissing(rowId, propertyName));

            return;
        }

        if (!TryParseInt(value, out int parsedValue))
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " has invalid int value: " +
                value);

            return;
        }

        if (property.propertyType == SerializedPropertyType.Integer)
        {
            property.intValue =
                parsedValue;

            return;
        }

        warnings.Add(
            RowPrefix(rowId) +
            propertyName +
            " is not an int field.");
    }

    private static void SetRequiredFloat(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(
                RequiredMissing(rowId, propertyName));

            return;
        }

        if (!TryParseFloat(value, out float parsedValue))
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " has invalid float value: " +
                value);

            return;
        }

        if (property.propertyType == SerializedPropertyType.Float)
        {
            property.floatValue =
                parsedValue;

            return;
        }

        warnings.Add(
            RowPrefix(rowId) +
            propertyName +
            " is not a float field.");
    }

    private static void SetRequiredBool(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(
                RequiredMissing(rowId, propertyName));

            return;
        }

        if (!TryParseBool(value, out bool parsedValue))
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " has invalid bool value: " +
                value);

            return;
        }

        if (property.propertyType == SerializedPropertyType.Boolean)
        {
            property.boolValue =
                parsedValue;

            return;
        }

        warnings.Add(
            RowPrefix(rowId) +
            propertyName +
            " is not a bool field.");
    }

    private static void SetRequiredEnum(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        bool applied =
            SetOptionalEnum(
                serializedObject,
                propertyName,
                value,
                rowId,
                warnings,
                true);

        if (!applied)
            return;
    }

    private static bool SetOptionalEnum(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings,
        bool required)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            if (required)
            {
                warnings.Add(
                    RequiredMissing(rowId, propertyName));
            }

            return false;
        }

        if (property.propertyType != SerializedPropertyType.Enum)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue =
                    value ?? string.Empty;

                return true;
            }

            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " is not an enum or string field.");

            return false;
        }

        int enumIndex =
            FindEnumIndex(property, value);

        if (enumIndex < 0)
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " cannot parse enum value '" +
                value +
                "'. Available values: " +
                string.Join(", ", property.enumNames));

            return false;
        }

        property.enumValueIndex =
            enumIndex;

        return true;
    }

    private static int FindEnumIndex(
        SerializedProperty property,
        string value)
    {
        string normalizedValue =
            NormalizeEnumToken(value);

        for (int i = 0; i < property.enumNames.Length; i++)
        {
            if (NormalizeEnumToken(property.enumNames[i]) == normalizedValue)
                return i;
        }

        for (int i = 0; i < property.enumDisplayNames.Length; i++)
        {
            if (NormalizeEnumToken(property.enumDisplayNames[i]) == normalizedValue)
                return i;
        }

        return -1;
    }

    private static string NormalizeEnumToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void SetSpriteReference(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " field not found. Icon was not assigned.");

            return;
        }

        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " is not an object reference field.");

            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            property.objectReferenceValue =
                null;

            return;
        }

        Sprite sprite =
            FindAssetByPathOrName<Sprite>(value, "t:Sprite");

        if (sprite == null)
        {
            warnings.Add(
                RowPrefix(rowId) +
                "Sprite not found for icon value: " +
                value +
                ". Existing icon reference was preserved.");

            return;
        }

        property.objectReferenceValue =
            sprite;
    }

    private static void SetGameObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        string rowId,
        List<string> warnings)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " field not found. Projectile prefab was not assigned.");

            return;
        }

        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            warnings.Add(
                RowPrefix(rowId) +
                propertyName +
                " is not an object reference field.");

            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            property.objectReferenceValue =
                null;

            return;
        }

        GameObject prefab =
            FindProjectilePrefabByPathOrName(value);

        if (prefab == null)
        {
            warnings.Add(
                RowPrefix(rowId) +
                "Prefab not found for projectilePrefabRef value: " +
                value +
                ". Existing prefab reference was preserved.");

            return;
        }

        property.objectReferenceValue =
            prefab;
    }

    private static GameObject FindProjectilePrefabByPathOrName(string value)
    {
        GameObject prefab =
            FindAssetByPathOrName<GameObject>(value, "t:Prefab");

        if (prefab != null)
            return prefab;

        if (!ProjectilePrefabAliases.TryGetValue(
                value,
                out string aliasName))
        {
            return null;
        }

        return FindAssetByPathOrName<GameObject>(aliasName, "t:Prefab");
    }

    private static T FindAssetByPathOrName<T>(
        string value,
        string filterType)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmedValue =
            value.Trim();

        if (trimmedValue.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return AssetDatabase.LoadAssetAtPath<T>(trimmedValue);
        }

        string[] guids =
            AssetDatabase.FindAssets(trimmedValue + " " + filterType);

        string normalizedTargetName =
            NormalizeAssetName(trimmedValue);

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            string nameWithoutExtension =
                Path.GetFileNameWithoutExtension(path);

            if (NormalizeAssetName(nameWithoutExtension) != normalizedTargetName)
                continue;

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                return asset;
        }

        if (guids.Length > 0)
        {
            string firstPath =
                AssetDatabase.GUIDToAssetPath(guids[0]);

            return AssetDatabase.LoadAssetAtPath<T>(firstPath);
        }

        return null;
    }

    private static string NormalizeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant();
    }

    private static bool ContainsRequiredMissingField(List<string> warnings)
    {
        for (int i = 0; i < warnings.Count; i++)
        {
            if (warnings[i].Contains("[REQUIRED_FIELD_MISSING]"))
                return true;
        }

        return false;
    }

    private static string RequiredMissing(
        string rowId,
        string propertyName)
    {
        return RowPrefix(rowId) +
               "[REQUIRED_FIELD_MISSING] Serialized field not found in WeaponConfig: " +
               propertyName;
    }

    private static string RowPrefix(string rowId)
    {
        return "Row id '" + rowId + "': ";
    }

    private static bool TryParseInt(
        string value,
        out int result)
    {
        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool TryParseFloat(
        string value,
        out float result)
    {
        if (float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result))
        {
            return true;
        }

        string normalized =
            value == null ? string.Empty : value.Replace(',', '.');

        return float.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool TryParseBool(
        string value,
        out bool result)
    {
        result = false;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized =
            value.Trim().ToLowerInvariant();

        if (normalized == "true" || normalized == "1" || normalized == "yes")
        {
            result = true;
            return true;
        }

        if (normalized == "false" || normalized == "0" || normalized == "no")
        {
            result = false;
            return true;
        }

        return false;
    }

    private static void ValidateInt(
        Dictionary<string, string> row,
        string key,
        string id,
        List<string> errors)
    {
        if (!TryParseInt(Get(row, key), out _))
        {
            errors.Add(
                RowPrefix(id) +
                key +
                " must be an integer.");
        }
    }

    private static void ValidateFloat(
        Dictionary<string, string> row,
        string key,
        string id,
        List<string> errors)
    {
        if (!TryParseFloat(Get(row, key), out _))
        {
            errors.Add(
                RowPrefix(id) +
                key +
                " must be a float.");
        }
    }

    private static void ValidateBool(
        Dictionary<string, string> row,
        string key,
        string id,
        List<string> errors)
    {
        if (!TryParseBool(Get(row, key), out _))
        {
            errors.Add(
                RowPrefix(id) +
                key +
                " must be true or false.");
        }
    }

    private static void EnsureOutputFolders()
    {
        EnsureFolder(OutputRootFolder);

        for (int i = 0; i < OwnerOutputFolders.Length; i++)
        {
            EnsureFolder(OwnerOutputFolders[i]);
        }
    }

    private static int DeleteAssetFilesInOwnerFolders(List<string> warnings)
    {
        int deletedCount = 0;

        for (int i = 0; i < OwnerOutputFolders.Length; i++)
        {
            string folder =
                OwnerOutputFolders[i];

            string absoluteFolderPath =
                ToAbsoluteProjectPath(folder);

            if (!Directory.Exists(absoluteFolderPath))
                continue;

            string[] assetFilePaths =
                Directory.GetFiles(
                    absoluteFolderPath,
                    "*.asset",
                    SearchOption.TopDirectoryOnly);

            for (int assetIndex = 0; assetIndex < assetFilePaths.Length; assetIndex++)
            {
                string assetPath =
                    ToUnityAssetPath(assetFilePaths[assetIndex]);

                bool deleted =
                    AssetDatabase.DeleteAsset(assetPath);

                if (deleted)
                {
                    deletedCount++;
                    continue;
                }

                warnings.Add(
                    "Failed to delete old asset before generation: " +
                    assetPath);
            }
        }

        return deletedCount;
    }

    private static bool IsAssetInOwnerFolder(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        for (int i = 0; i < OwnerOutputFolders.Length; i++)
        {
            string folderPrefix =
                OwnerOutputFolders[i] + "/";

            if (assetPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string GetOutputFolderForWeaponOwner(
        string weaponOwner,
        string id,
        List<string> warnings)
    {
        string normalizedOwner =
            NormalizeToken(weaponOwner);

        switch (normalizedOwner)
        {
            case "playerandrangers":
            case "player":
            case "rangers":
            case "ranger":
            case "common":
                return OutputFolderCommon;

            case "ai":
                return OutputFolderAI;

            case "infected":
                return OutputFolderInfected;

            case "ancients":
            case "ancient":
                return OutputFolderAncients;
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (id.StartsWith("weapon_common_", StringComparison.OrdinalIgnoreCase))
                return OutputFolderCommon;

            if (id.StartsWith("weapon_ai_", StringComparison.OrdinalIgnoreCase))
                return OutputFolderAI;

            if (id.StartsWith("weapon_infected_", StringComparison.OrdinalIgnoreCase))
                return OutputFolderInfected;

            if (id.StartsWith("weapon_ancient_", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("weapon_ancients_", StringComparison.OrdinalIgnoreCase))
            {
                return OutputFolderAncients;
            }
        }

        warnings.Add(
            RowPrefix(id) +
            "Unknown weaponOwner '" + weaponOwner +
            "'. Asset will be written to Common folder as safe fallback.");

        return OutputFolderCommon;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }

    private static string ToAbsoluteProjectPath(string unityAssetPath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string relativePath =
            unityAssetPath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        return Path.Combine(
            projectRoot,
            relativePath);
    }

    private static string ToUnityAssetPath(string absolutePath)
    {
        string normalizedAbsolutePath =
            absolutePath.Replace('\\', '/');

        string normalizedDataPath =
            Application.dataPath.Replace('\\', '/');

        if (normalizedAbsolutePath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" +
                   normalizedAbsolutePath.Substring(normalizedDataPath.Length);
        }

        return normalizedAbsolutePath;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts =
            folderPath.Split('/');

        if (parts.Length == 0 || parts[0] != "Assets")
        {
            throw new InvalidOperationException(
                "Unity asset folder must start with Assets/: " +
                folderPath);
        }

        string current =
            "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            string next =
                current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current =
                next;
        }
    }

    private static void LogValidationResult(
        CsvValidationResult validationResult)
    {
        for (int i = 0; i < validationResult.Errors.Count; i++)
        {
            Debug.LogError(
                "[WeaponConfigAssetGenerator] " +
                validationResult.Errors[i]);
        }

        for (int i = 0; i < validationResult.Warnings.Count; i++)
        {
            Debug.LogWarning(
                "[WeaponConfigAssetGenerator] " +
                validationResult.Warnings[i]);
        }

        Debug.Log(
            "[WeaponConfigAssetGenerator] CSV rows: " +
            validationResult.RowCount +
            ", errors: " +
            validationResult.Errors.Count +
            ", warnings: " +
            validationResult.Warnings.Count);
    }

    private static List<string[]> ParseCsv(string csvText)
    {
        var records =
            new List<string[]>();

        if (string.IsNullOrEmpty(csvText))
            return records;

        var currentRecord =
            new List<string>();

        var currentField =
            new StringBuilder();

        bool insideQuotes =
            false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char current =
                csvText[i];

            if (current == '"')
            {
                bool nextIsQuote =
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '"';

                if (insideQuotes && nextIsQuote)
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (current == ',' && !insideQuotes)
            {
                currentRecord.Add(currentField.ToString());
                currentField.Length = 0;
                continue;
            }

            if ((current == '\n' || current == '\r') && !insideQuotes)
            {
                if (current == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    i++;

                currentRecord.Add(currentField.ToString());
                currentField.Length = 0;

                if (currentRecord.Count > 1 || !string.IsNullOrWhiteSpace(currentRecord[0]))
                {
                    records.Add(currentRecord.ToArray());
                }

                currentRecord.Clear();
                continue;
            }

            currentField.Append(current);
        }

        currentRecord.Add(currentField.ToString());

        if (currentRecord.Count > 1 || !string.IsNullOrWhiteSpace(currentRecord[0]))
        {
            records.Add(currentRecord.ToArray());
        }

        return records;
    }

    private sealed class CsvValidationResult
    {
        public int RowCount;
        public readonly List<Dictionary<string, string>> Rows =
            new List<Dictionary<string, string>>();

        public readonly List<string> Errors =
            new List<string>();

        public readonly List<string> Warnings =
            new List<string>();
    }
}
