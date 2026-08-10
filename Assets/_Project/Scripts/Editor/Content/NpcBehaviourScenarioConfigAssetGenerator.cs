using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NpcBehaviourScenarioConfigAssetGenerator
{
    private const string MenuRoot =
        "STAR FRONTIER/Content/03. NPC Behaviour Scenarios/";

    private const string DefaultAssetCsvPath =
        "Assets/_Project/Content/Configs/NpcBehaviourScenarios/STAR_FRONTIER_NpcBehaviourScenarioConfig_assets_v0.2.csv";

    private const string DefaultWeightsCsvPath =
        "Assets/_Project/Content/Configs/NpcBehaviourScenarios/STAR_FRONTIER_NpcBehaviourScenarioConfig_weights_v0.2.csv";

    private const string OutputRoot =
        "Assets/_Project/Content/Configs/NpcBehaviourScenarios";

    private static readonly string[] RequiredAssetColumns =
    {
        "assetId",
        "folder",
        "displayName",
        "description",
        "engageEnemiesWeight"
    };

    private static readonly string[] RequiredWeightColumns =
    {
        "assetId",
        "weightOrder",
        "behaviorType",
        "targetStationType",
        "targetUnitSide",
        "targetUnitType",
        "weight"
    };

    [MenuItem(MenuRoot + "Create assets from CSV")]
    public static void CreateAssetsFromCsv()
    {
        GenerateFromCsv(
            DefaultAssetCsvPath,
            DefaultWeightsCsvPath,
            validateOnly: false);
    }

    [MenuItem(MenuRoot + "Validate CSV")]
    public static void ValidateCsv()
    {
        GenerateFromCsv(
            DefaultAssetCsvPath,
            DefaultWeightsCsvPath,
            validateOnly: true);
    }

    [MenuItem(MenuRoot + "Delete generated scenarios")]
    public static void DeleteGeneratedScenarios()
    {
        int deleted =
            DeleteScenarioAssets(confirm: true);

        Debug.Log(
            $"NpcBehaviourScenarioConfig: deleted generated scenario assets/folders: {deleted}");
    }

    private static void GenerateFromCsv(
        string assetCsvPath,
        string weightsCsvPath,
        bool validateOnly)
    {
        DateTime startedAt =
            DateTime.Now;

        List<string> errors =
            new List<string>();

        List<string> warnings =
            new List<string>();

        List<ParsedAssetRow> assetRows =
            ParseAssetRows(assetCsvPath, errors, warnings);

        List<ParsedWeightRow> weightRows =
            ParseWeightRows(weightsCsvPath, errors, warnings);

        Dictionary<string, List<ParsedWeightRow>> weightsByAssetId =
            BuildWeightsByAssetId(weightRows, errors);

        ValidateAssetWeightLinks(
            assetRows,
            weightsByAssetId,
            errors,
            warnings);

        if (errors.Count > 0 || validateOnly)
        {
            Finish(
                errors,
                warnings,
                created: 0,
                updated: 0,
                deleted: 0,
                validatedAssets: assetRows.Count,
                validatedWeights: weightRows.Count,
                validateOnly: validateOnly,
                startedAt: startedAt);

            return;
        }

        PrepareOutputFolders(assetRows);

        int created = 0;
        int updated = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (ParsedAssetRow assetRow in assetRows)
            {
                NpcBehaviourScenarioConfig asset =
                    FindExistingAsset(assetRow);

                if (asset == null)
                {
                    asset =
                        ScriptableObject.CreateInstance<NpcBehaviourScenarioConfig>();

                    AssetDatabase.CreateAsset(
                        asset,
                        assetRow.AssetPath);

                    created++;
                }
                else
                {
                    string currentPath =
                        AssetDatabase.GetAssetPath(asset);

                    if (!string.Equals(
                            currentPath,
                            assetRow.AssetPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        string moveError =
                            AssetDatabase.MoveAsset(
                                currentPath,
                                assetRow.AssetPath);

                        if (!string.IsNullOrWhiteSpace(moveError))
                        {
                            throw new InvalidOperationException(
                                $"Не удалось переместить существующий asset {assetRow.AssetId}: {moveError}");
                        }
                    }

                    updated++;
                }

                WriteSerialized(
                    asset,
                    assetRow,
                    weightsByAssetId[assetRow.AssetId]);

                EditorUtility.SetDirty(asset);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Finish(
            errors,
            warnings,
            created,
            updated,
            deleted: 0,
            validatedAssets: assetRows.Count,
            validatedWeights: weightRows.Count,
            validateOnly: validateOnly,
            startedAt: startedAt);
    }

    private static NpcBehaviourScenarioConfig FindExistingAsset(
        ParsedAssetRow assetRow)
    {
        NpcBehaviourScenarioConfig asset =
            AssetDatabase.LoadAssetAtPath<NpcBehaviourScenarioConfig>(
                assetRow.AssetPath);

        if (asset != null)
            return asset;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:NpcBehaviourScenarioConfig",
                new[] { OutputRoot });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            NpcBehaviourScenarioConfig candidate =
                AssetDatabase.LoadAssetAtPath<NpcBehaviourScenarioConfig>(
                    path);

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
                    assetRow.AssetId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static List<ParsedAssetRow> ParseAssetRows(
        string csvAssetPath,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        if (!File.Exists(csvAssetPath))
        {
            errors.Add($"Asset CSV не найден: {csvAssetPath}");
            return new List<ParsedAssetRow>();
        }

        List<List<string>> rows =
            ReadCsv(csvAssetPath, errors);

        if (rows.Count == 0)
            return new List<ParsedAssetRow>();

        Dictionary<string, int> columnIndex =
            BuildColumnIndex(rows[0]);

        ValidateRequiredColumns(
            columnIndex,
            RequiredAssetColumns,
            errors,
            "Asset CSV");

        if (errors.Count > 0)
            return new List<ParsedAssetRow>();

        List<ParsedAssetRow> result =
            new List<ParsedAssetRow>(rows.Count - 1);

        HashSet<string> knownIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row =
                rows[rowIndex];

            if (IsEmptyRow(row))
                continue;

            int rowNumber =
                rowIndex + 1;

            string assetId =
                Get(row, columnIndex, "assetId");

            if (string.IsNullOrWhiteSpace(assetId))
            {
                errors.Add($"Asset CSV row {rowNumber}: пустой assetId.");
                continue;
            }

            if (!knownIds.Add(assetId))
            {
                errors.Add(
                    $"Asset CSV row {rowNumber} / {assetId}: дублирующий assetId.");

                continue;
            }

            string overallCheck =
                Get(row, columnIndex, "overallCheck");

            if (!string.IsNullOrWhiteSpace(overallCheck) &&
                !string.Equals(
                    overallCheck,
                    "OK",
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Asset CSV row {rowNumber} / {assetId}: overallCheck != OK ({overallCheck}).");

                continue;
            }

            string folder =
                NormalizeAssetPath(Get(row, columnIndex, "folder"));

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder =
                    OutputRoot;

                warnings.Add(
                    $"Asset CSV row {rowNumber} / {assetId}: folder пустой, используется {OutputRoot}.");
            }

            if (!folder.StartsWith(
                    OutputRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Asset CSV row {rowNumber} / {assetId}: folder должен быть внутри {OutputRoot}, получено: {folder}");
            }

            float engageEnemiesWeight =
                ParseFloat(
                    Get(row, columnIndex, "engageEnemiesWeight"),
                    rowNumber,
                    assetId,
                    "engageEnemiesWeight",
                    errors);

            engageEnemiesWeight =
                Mathf.Clamp(engageEnemiesWeight, 0f, 1000f);

            string assetPath =
                $"{folder}/{assetId}.asset";

            result.Add(
                new ParsedAssetRow(
                    assetId,
                    Get(row, columnIndex, "displayName"),
                    Get(row, columnIndex, "description"),
                    engageEnemiesWeight,
                    assetPath,
                    rowNumber));
        }

        return result;
    }

    private static List<ParsedWeightRow> ParseWeightRows(
        string csvAssetPath,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        if (!File.Exists(csvAssetPath))
        {
            errors.Add($"Weights CSV не найден: {csvAssetPath}");
            return new List<ParsedWeightRow>();
        }

        List<List<string>> rows =
            ReadCsv(csvAssetPath, errors);

        if (rows.Count == 0)
            return new List<ParsedWeightRow>();

        Dictionary<string, int> columnIndex =
            BuildColumnIndex(rows[0]);

        ValidateRequiredColumns(
            columnIndex,
            RequiredWeightColumns,
            errors,
            "Weights CSV");

        if (errors.Count > 0)
            return new List<ParsedWeightRow>();

        List<ParsedWeightRow> result =
            new List<ParsedWeightRow>(rows.Count - 1);

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row =
                rows[rowIndex];

            if (IsEmptyRow(row))
                continue;

            int rowNumber =
                rowIndex + 1;

            string assetId =
                Get(row, columnIndex, "assetId");

            if (string.IsNullOrWhiteSpace(assetId))
            {
                errors.Add($"Weights CSV row {rowNumber}: пустой assetId.");
                continue;
            }

            string overallCheck =
                Get(row, columnIndex, "overallCheck");

            if (!string.IsNullOrWhiteSpace(overallCheck) &&
                !string.Equals(
                    overallCheck,
                    "OK",
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Weights CSV row {rowNumber} / {assetId}: overallCheck != OK ({overallCheck}).");

                continue;
            }

            int weightOrder =
                ParseInt(
                    Get(row, columnIndex, "weightOrder"),
                    rowNumber,
                    assetId,
                    "weightOrder",
                    errors);

            SystemNpcBehaviorType behaviorType =
                ParseEnum<SystemNpcBehaviorType>(
                    Get(row, columnIndex, "behaviorType"),
                    rowNumber,
                    assetId,
                    "behaviorType",
                    errors);

            NpcBehaviourTargetStationType targetStationType =
                ParseEnum<NpcBehaviourTargetStationType>(
                    Get(row, columnIndex, "targetStationType"),
                    rowNumber,
                    assetId,
                    "targetStationType",
                    errors);

            NpcBehaviourTargetUnitSide targetUnitSide =
                ParseEnum<NpcBehaviourTargetUnitSide>(
                    Get(row, columnIndex, "targetUnitSide"),
                    rowNumber,
                    assetId,
                    "targetUnitSide",
                    errors);

            NpcBehaviourTargetUnitType targetUnitType =
                ParseEnum<NpcBehaviourTargetUnitType>(
                    Get(row, columnIndex, "targetUnitType"),
                    rowNumber,
                    assetId,
                    "targetUnitType",
                    errors);

            int weight =
                ParseInt(
                    Get(row, columnIndex, "weight"),
                    rowNumber,
                    assetId,
                    "weight",
                    errors);

            if (weight < 0 || weight > 1000)
            {
                errors.Add(
                    $"Weights CSV row {rowNumber} / {assetId}: weight должен быть 0..1000, получено {weight}.");
            }

            ValidateTargetFields(
                rowNumber,
                assetId,
                behaviorType,
                targetStationType,
                targetUnitSide,
                targetUnitType,
                errors,
                warnings);

            result.Add(
                new ParsedWeightRow(
                    assetId,
                    weightOrder,
                    behaviorType,
                    targetStationType,
                    targetUnitSide,
                    targetUnitType,
                    Mathf.Clamp(weight, 0, 1000),
                    rowNumber));
        }

        return result;
    }

    private static Dictionary<string, List<ParsedWeightRow>> BuildWeightsByAssetId(
        IEnumerable<ParsedWeightRow> rows,
        ICollection<string> errors)
    {
        Dictionary<string, List<ParsedWeightRow>> result =
            new Dictionary<string, List<ParsedWeightRow>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (ParsedWeightRow row in rows)
        {
            if (!result.TryGetValue(row.AssetId, out List<ParsedWeightRow> list))
            {
                list =
                    new List<ParsedWeightRow>();

                result.Add(
                    row.AssetId,
                    list);
            }

            list.Add(row);
        }

        foreach (KeyValuePair<string, List<ParsedWeightRow>> pair in result)
        {
            HashSet<int> orders =
                new HashSet<int>();

            foreach (ParsedWeightRow row in pair.Value)
            {
                if (!orders.Add(row.WeightOrder))
                {
                    errors.Add(
                        $"Weights CSV / {pair.Key}: дублирующий weightOrder={row.WeightOrder}.");
                }
            }

            pair.Value.Sort(
                (left, right) =>
                    left.WeightOrder.CompareTo(right.WeightOrder));
        }

        return result;
    }

    private static void ValidateAssetWeightLinks(
        IReadOnlyCollection<ParsedAssetRow> assetRows,
        IReadOnlyDictionary<string, List<ParsedWeightRow>> weightsByAssetId,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        HashSet<string> assetIds =
            new HashSet<string>(
                assetRows.Select(row => row.AssetId),
                StringComparer.OrdinalIgnoreCase);

        foreach (ParsedAssetRow assetRow in assetRows)
        {
            if (!weightsByAssetId.TryGetValue(
                    assetRow.AssetId,
                    out List<ParsedWeightRow> weights) ||
                weights.Count == 0)
            {
                errors.Add(
                    $"Asset CSV row {assetRow.RowNumber} / {assetRow.AssetId}: нет строк BehaviorWeights.");

                continue;
            }

            int totalWeight =
                weights.Sum(row => row.Weight);

            if (totalWeight <= 0)
            {
                errors.Add(
                    $"Asset CSV row {assetRow.RowNumber} / {assetRow.AssetId}: сумма weight должна быть больше 0.");
            }

            if (totalWeight != 1000)
            {
                warnings.Add(
                    $"Asset {assetRow.AssetId}: сумма behavior weight = {totalWeight}, не 1000. Это допустимо для weighted random, но проверь баланс.");
            }
        }

        foreach (string assetId in weightsByAssetId.Keys)
        {
            if (!assetIds.Contains(assetId))
            {
                errors.Add(
                    $"Weights CSV: assetId={assetId} отсутствует в Asset CSV.");
            }
        }
    }

    private static void ValidateTargetFields(
        int rowNumber,
        string assetId,
        SystemNpcBehaviorType behaviorType,
        NpcBehaviourTargetStationType targetStationType,
        NpcBehaviourTargetUnitSide targetUnitSide,
        NpcBehaviourTargetUnitType targetUnitType,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        bool stationAttack =
            behaviorType == SystemNpcBehaviorType.AttackMilitaryStation ||
            behaviorType == SystemNpcBehaviorType.AttackRangerBaseStation ||
            behaviorType == SystemNpcBehaviorType.AttackTradeStation ||
            behaviorType == SystemNpcBehaviorType.AttackScienceStation ||
            behaviorType == SystemNpcBehaviorType.AttackMedicalStation;

        bool unitAttack =
            behaviorType == SystemNpcBehaviorType.AttackMilitaryAlly ||
            behaviorType == SystemNpcBehaviorType.AttackRangerAlly ||
            behaviorType == SystemNpcBehaviorType.AttackTraderAlly ||
            behaviorType == SystemNpcBehaviorType.AttackScienceAlly ||
            behaviorType == SystemNpcBehaviorType.AttackMedicAlly ||
            behaviorType == SystemNpcBehaviorType.EngageEnemies;

        if (stationAttack &&
            targetStationType == NpcBehaviourTargetStationType.None)
        {
            errors.Add(
                $"Weights CSV row {rowNumber} / {assetId}: {behaviorType} требует targetStationType.");
        }

        if (!stationAttack &&
            targetStationType != NpcBehaviourTargetStationType.None)
        {
            warnings.Add(
                $"Weights CSV row {rowNumber} / {assetId}: {behaviorType} обычно не использует targetStationType={targetStationType}.");
        }

        if (unitAttack &&
            targetUnitSide == NpcBehaviourTargetUnitSide.None)
        {
            warnings.Add(
                $"Weights CSV row {rowNumber} / {assetId}: {behaviorType} обычно требует targetUnitSide.");
        }

        if (targetUnitSide == NpcBehaviourTargetUnitSide.None &&
            targetUnitType != NpcBehaviourTargetUnitType.None)
        {
            errors.Add(
                $"Weights CSV row {rowNumber} / {assetId}: targetUnitType заполнен, но targetUnitSide=None.");
        }
    }

    private static int DeleteScenarioAssets(bool confirm)
    {
        if (!AssetDatabase.IsValidFolder(OutputRoot))
            return 0;

        if (confirm)
        {
            bool shouldDelete =
                EditorUtility.DisplayDialog(
                    "Delete NpcBehaviourScenarioConfig assets",
                    $"Будут удалены все NpcBehaviourScenarioConfig asset-ы внутри:\n{OutputRoot}\n\nCSV и скрипты не удаляются.",
                    "Удалить",
                    "Отмена");

            if (!shouldDelete)
                return 0;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                "t:NpcBehaviourScenarioConfig",
                new[] { OutputRoot });

        int deletedAssets =
            0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!path.StartsWith(
                        OutputRoot + "/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path))
                    deletedAssets++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int deletedFolders =
            DeleteEmptyFoldersUnderOutputRoot();

        Debug.Log(
            $"NpcBehaviourScenarioConfig: deleted assets={deletedAssets}, deleted empty folders={deletedFolders}.");

        return deletedAssets;
    }

    private static int DeleteEmptyFoldersUnderOutputRoot()
    {
        string fullRoot =
            ToFullPath(OutputRoot);

        if (!Directory.Exists(fullRoot))
            return 0;

        int deleted =
            0;

        string[] directories =
            Directory
                .GetDirectories(
                    fullRoot,
                    "*",
                    SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToArray();

        foreach (string directory in directories)
        {
            if (!IsDirectoryEmptyIgnoringMeta(directory))
                continue;

            string assetPath =
                ToAssetPath(directory);

            if (string.IsNullOrWhiteSpace(assetPath) ||
                string.Equals(
                    assetPath,
                    OutputRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AssetDatabase.DeleteAsset(assetPath))
            {
                deleted++;
            }
            else
            {
                Directory.Delete(
                    directory,
                    recursive: false);

                deleted++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return deleted;
    }

    private static bool IsDirectoryEmptyIgnoringMeta(string directory)
    {
        foreach (string entry in Directory.GetFileSystemEntries(directory))
        {
            if (!entry.EndsWith(
                    ".meta",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteSerialized(
        NpcBehaviourScenarioConfig asset,
        ParsedAssetRow assetRow,
        IReadOnlyList<ParsedWeightRow> weightRows)
    {
        SerializedObject serialized =
            new SerializedObject(asset);

        SetString(
            serialized,
            "id",
            assetRow.AssetId);

        SetString(
            serialized,
            "displayName",
            assetRow.DisplayName);

        SetString(
            serialized,
            "description",
            assetRow.Description);

        SetFloat(
            serialized,
            "engageEnemiesWeight",
            assetRow.EngageEnemiesWeight);

        SerializedProperty weightsProperty =
            serialized.FindProperty("behaviorWeights");

        if (weightsProperty == null || !weightsProperty.isArray)
        {
            throw new InvalidOperationException(
                "NpcBehaviourScenarioConfig.behaviorWeights не найден или не является массивом.");
        }

        weightsProperty.arraySize =
            weightRows.Count;

        for (int i = 0; i < weightRows.Count; i++)
        {
            SerializedProperty item =
                weightsProperty.GetArrayElementAtIndex(i);

            ParsedWeightRow row =
                weightRows[i];

            SetEnum(
                item,
                "behaviorType",
                row.BehaviorType);

            SetEnum(
                item,
                "targetStationType",
                row.TargetStationType);

            SetEnum(
                item,
                "targetUnitSide",
                row.TargetUnitSide);

            SetEnum(
                item,
                "targetUnitType",
                row.TargetUnitType);

            SetInt(
                item,
                "weight",
                row.Weight);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.name =
            assetRow.AssetId;
    }

    private static void PrepareOutputFolders(
        IReadOnlyCollection<ParsedAssetRow> parsedRows)
    {
        string[] folders =
            parsedRows
                .Select(row => Path.GetDirectoryName(row.AssetPath)?.Replace("\\", "/"))
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        foreach (string folder in folders)
            EnsureExactFolderOnDisk(folder);

        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        foreach (string folder in folders)
        {
            AssetDatabase.ImportAsset(
                folder,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ImportRecursive);
        }

        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureExactFolderOnDisk(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;

        string normalizedFolder =
            folder.Replace("\\", "/");

        string fullPath =
            ToFullPath(normalizedFolder);

        if (File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"Нельзя создать папку '{normalizedFolder}', потому что по этому пути уже есть файл.");
        }

        Directory.CreateDirectory(fullPath);

        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"Не удалось создать папку на диске: {normalizedFolder}");
        }
    }

    private static string NormalizeAssetPath(string path)
    {
        return (path ?? string.Empty)
            .Trim()
            .Replace("\\", "/");
    }

    private static string ToFullPath(string assetPath)
    {
        string normalizedAssetPath =
            assetPath.Replace("\\", "/");

        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new InvalidOperationException(
                "Не удалось определить корень Unity-проекта.");
        }

        return Path.GetFullPath(
            Path.Combine(
                projectRoot,
                normalizedAssetPath));
    }

    private static string ToAssetPath(string fullPath)
    {
        string normalizedFullPath =
            fullPath.Replace("\\", "/");

        string normalizedDataPath =
            Application.dataPath.Replace("\\", "/");

        if (!normalizedFullPath.StartsWith(
                normalizedDataPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "Assets" +
            normalizedFullPath.Substring(normalizedDataPath.Length);
    }

    private static List<List<string>> ReadCsv(
        string assetPath,
        ICollection<string> errors)
    {
        try
        {
            string text =
                File.ReadAllText(assetPath, Encoding.UTF8);

            return CsvParser.Parse(text);
        }
        catch (Exception ex)
        {
            errors.Add(
                $"Не удалось прочитать CSV {assetPath}: {ex.Message}");

            return new List<List<string>>();
        }
    }

    private static Dictionary<string, int> BuildColumnIndex(
        IReadOnlyList<string> header)
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < header.Count; i++)
        {
            string name =
                (header[i] ?? string.Empty)
                .Trim('\uFEFF', ' ', '\t', '\r', '\n');

            if (!string.IsNullOrWhiteSpace(name) &&
                !result.ContainsKey(name))
            {
                result.Add(name, i);
            }
        }

        return result;
    }

    private static void ValidateRequiredColumns(
        IReadOnlyDictionary<string, int> columnIndex,
        IEnumerable<string> requiredColumns,
        ICollection<string> errors,
        string csvName)
    {
        foreach (string column in requiredColumns)
        {
            if (!columnIndex.ContainsKey(column))
                errors.Add($"{csvName}: отсутствует обязательная колонка: {column}");
        }
    }

    private static string Get(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndex,
        string columnName)
    {
        if (!columnIndex.TryGetValue(columnName, out int index) ||
            index < 0 ||
            index >= row.Count)
        {
            return string.Empty;
        }

        return (row[index] ?? string.Empty).Trim();
    }

    private static bool IsEmptyRow(IEnumerable<string> row)
    {
        return row.All(string.IsNullOrWhiteSpace);
    }

    private static T ParseEnum<T>(
        string value,
        int rowNumber,
        string id,
        string columnName,
        ICollection<string> errors)
        where T : struct
    {
        if (Enum.TryParse(value, true, out T result) &&
            Enum.IsDefined(typeof(T), result))
        {
            return result;
        }

        errors.Add(
            $"Row {rowNumber} / {id}: колонка {columnName}, значение '{value}' не распознано как {typeof(T).Name}.");

        return default;
    }

    private static int ParseInt(
        string value,
        int rowNumber,
        string id,
        string columnName,
        ICollection<string> errors)
    {
        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result))
        {
            return result;
        }

        errors.Add(
            $"Row {rowNumber} / {id}: колонка {columnName} должна быть int, значение='{value}'.");

        return 0;
    }

    private static float ParseFloat(
        string value,
        int rowNumber,
        string id,
        string columnName,
        ICollection<string> errors)
    {
        if (float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result))
        {
            return result;
        }

        errors.Add(
            $"Row {rowNumber} / {id}: колонка {columnName} должна быть float, значение='{value}'.");

        return 0f;
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property не найден: {propertyName}");
        }

        property.stringValue =
            value ?? string.Empty;
    }

    private static void SetFloat(
        SerializedObject serialized,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property не найден: {propertyName}");
        }

        property.floatValue =
            value;
    }

    private static void SetInt(
        SerializedProperty parent,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property не найден: {propertyName}");
        }

        property.intValue =
            value;
    }

    private static void SetEnum<T>(
        SerializedProperty parent,
        string propertyName,
        T value)
        where T : Enum
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property не найден: {propertyName}");
        }

        Array values =
            Enum.GetValues(typeof(T));

        int index =
            Array.IndexOf(values, value);

        property.enumValueIndex =
            index >= 0 ? index : 0;
    }

    private static void Finish(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings,
        int created,
        int updated,
        int deleted,
        int validatedAssets,
        int validatedWeights,
        bool validateOnly,
        DateTime startedAt)
    {
        TimeSpan elapsed =
            DateTime.Now - startedAt;

        if (errors.Count > 0)
        {
            string message =
                string.Join("\n", errors.Take(25));

            if (errors.Count > 25)
                message += $"\n...и ещё {errors.Count - 25} ошибок.";

            Debug.LogError(
                $"NpcBehaviourScenarioConfig CSV generation failed. Errors={errors.Count}\n{message}");

            EditorUtility.DisplayDialog(
                "NpcBehaviourScenarioConfig CSV generator - ERROR",
                $"Ошибок: {errors.Count}\n\n{message}",
                "OK");

            return;
        }

        foreach (string warning in warnings.Take(50))
        {
            Debug.LogWarning(
                $"NpcBehaviourScenarioConfig CSV generator: {warning}");
        }

        if (warnings.Count > 50)
        {
            Debug.LogWarning(
                $"NpcBehaviourScenarioConfig CSV generator: and {warnings.Count - 50} more warnings.");
        }

        string mode =
            validateOnly ? "Validation" : "Creation";

        string result =
            $"{mode} complete.\n" +
            $"Asset CSV: {DefaultAssetCsvPath}\n" +
            $"Weights CSV: {DefaultWeightsCsvPath}\n" +
            $"Validated assets: {validatedAssets}\n" +
            $"Validated weights: {validatedWeights}\n" +
            $"Created: {created}\n" +
            $"Updated: {updated}\n" +
            $"Deleted: {deleted}\n" +
            $"Warnings: {warnings.Count}\n" +
            $"Elapsed: {elapsed.TotalSeconds:F1}s";

        Debug.Log(
            $"NpcBehaviourScenarioConfig {result}");

        EditorUtility.DisplayDialog(
            "NpcBehaviourScenarioConfig CSV generator",
            result,
            "OK");
    }

    private sealed class ParsedAssetRow
    {
        public ParsedAssetRow(
            string assetId,
            string displayName,
            string description,
            float engageEnemiesWeight,
            string assetPath,
            int rowNumber)
        {
            AssetId = assetId;
            DisplayName = displayName;
            Description = description;
            EngageEnemiesWeight = engageEnemiesWeight;
            AssetPath = assetPath;
            RowNumber = rowNumber;
        }

        public string AssetId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public float EngageEnemiesWeight { get; }
        public string AssetPath { get; }
        public int RowNumber { get; }
    }

    private sealed class ParsedWeightRow
    {
        public ParsedWeightRow(
            string assetId,
            int weightOrder,
            SystemNpcBehaviorType behaviorType,
            NpcBehaviourTargetStationType targetStationType,
            NpcBehaviourTargetUnitSide targetUnitSide,
            NpcBehaviourTargetUnitType targetUnitType,
            int weight,
            int rowNumber)
        {
            AssetId = assetId;
            WeightOrder = weightOrder;
            BehaviorType = behaviorType;
            TargetStationType = targetStationType;
            TargetUnitSide = targetUnitSide;
            TargetUnitType = targetUnitType;
            Weight = weight;
            RowNumber = rowNumber;
        }

        public string AssetId { get; }
        public int WeightOrder { get; }
        public SystemNpcBehaviorType BehaviorType { get; }
        public NpcBehaviourTargetStationType TargetStationType { get; }
        public NpcBehaviourTargetUnitSide TargetUnitSide { get; }
        public NpcBehaviourTargetUnitType TargetUnitType { get; }
        public int Weight { get; }
        public int RowNumber { get; }
    }

    private static class CsvParser
    {
        public static List<List<string>> Parse(string text)
        {
            List<List<string>> rows =
                new List<List<string>>();

            List<string> row =
                new List<string>();

            StringBuilder cell =
                new StringBuilder();

            bool inQuotes =
                false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch =
                    text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(ch);
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (ch == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }

                if (ch == '\r')
                    continue;

                if (ch == '\n')
                {
                    row.Add(cell.ToString());
                    rows.Add(row);
                    row = new List<string>();
                    cell.Length = 0;
                    continue;
                }

                cell.Append(ch);
            }

            row.Add(cell.ToString());

            if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
                rows.Add(row);

            return rows;
        }
    }
}
