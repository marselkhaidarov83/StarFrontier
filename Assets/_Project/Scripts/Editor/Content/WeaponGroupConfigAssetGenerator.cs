using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WeaponGroupConfigAssetGenerator
{
    private const string MenuRoot = "STAR FRONTIER/Content/Weapon Groups/";

    private const string DefaultCsvPath =
        "Assets/_Project/Content/Configs/WeaponGroups/STAR_FRONTIER_WeaponGroupConfig_400_assets_v1.2_mixed_ally_ranks.csv";

    private const string WeaponSearchFolder = "Assets/_Project/Content/Configs/Weapons";
    private const string OutputRoot = "Assets/_Project/Content/Configs/WeaponGroups";

    private static readonly string[] RequiredColumns =
    {
        "id",
        "displayName",
        "description",
        "level",
        "groupKind",
        "allyType",
        "enemyFaction",
        "strengthRank",
        "strengthOrder",
        "weaponOwner",
        "weaponOwnerKey",
        "variant",
        "weaponCount",
        "powerScore",
        "tierPattern",
        "familyPattern",
        "weaponConfig01",
        "weaponConfig02",
        "weaponConfig03",
        "weaponConfig04",
        "weaponConfig05",
        "overallCheck"
    };

    [MenuItem(MenuRoot + "Create assets from CSV")]
    public static void CreateAssetsFromCsv()
    {
        GenerateFromCsv(DefaultCsvPath, validateOnly: false);
    }

    [MenuItem(MenuRoot + "Validate CSV")]
    public static void ValidateCsv()
    {
        GenerateFromCsv(DefaultCsvPath, validateOnly: true);
    }

    [MenuItem(MenuRoot + "Delete generated groups")]
    public static void DeleteGeneratedGroups()
    {
        var deleted = DeleteWeaponGroupAssets(confirm: true);
        Debug.Log($"WeaponGroupConfig: deleted generated group assets/folders: {deleted}");
    }

    private static void GenerateFromCsv(string csvAssetPath, bool validateOnly)
    {
        var startedAt = DateTime.Now;
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!File.Exists(csvAssetPath))
        {
            errors.Add($"CSV не найден: {csvAssetPath}");
            Finish(errors, warnings, 0, 0, 0, 0, validateOnly, startedAt);
            return;
        }

        var weaponIndex = BuildWeaponIndex(errors);
        var rows = ReadCsv(csvAssetPath, errors);
        if (rows.Count == 0)
        {
            Finish(errors, warnings, 0, 0, 0, 0, validateOnly, startedAt);
            return;
        }

        var header = rows[0];
        var columnIndex = BuildColumnIndex(header);
        ValidateRequiredColumns(columnIndex, errors);

        if (errors.Count > 0)
        {
            Finish(errors, warnings, 0, 0, 0, 0, validateOnly, startedAt);
            return;
        }

        var parsedRows = ValidateRows(rows, columnIndex, weaponIndex, errors, warnings);

        if (errors.Count > 0 || validateOnly)
        {
            Finish(errors, warnings, 0, 0, 0, parsedRows.Count, validateOnly, startedAt);
            return;
        }

        PrepareOutputFolders(parsedRows);

        var created = 0;
        var updated = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (var parsedRow in parsedRows)
            {
                var asset = AssetDatabase.LoadAssetAtPath<WeaponGroupConfig>(parsedRow.AssetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<WeaponGroupConfig>();
                    AssetDatabase.CreateAsset(asset, parsedRow.AssetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                WriteSerialized(asset, parsedRow);
                EditorUtility.SetDirty(asset);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Finish(errors, warnings, created, updated, 0, parsedRows.Count, validateOnly, startedAt);
    }

    private static int DeleteWeaponGroupAssets(bool confirm)
    {
        if (!AssetDatabase.IsValidFolder(OutputRoot))
            return 0;

        if (confirm)
        {
            var shouldDelete = EditorUtility.DisplayDialog(
                "Delete WeaponGroupConfig assets",
                $"Будут удалены все WeaponGroupConfig asset-ы внутри:\n{OutputRoot}\n\nCSV, скрипты и WeaponConfig asset-ы не удаляются. Все подпапки внутри WeaponGroups будут очищены, включая случайные Ally 1 / Enemy 1.",
                "Удалить",
                "Отмена");

            if (!shouldDelete)
                return 0;
        }

        var guids = AssetDatabase.FindAssets("t:WeaponGroupConfig", new[] { OutputRoot });
        var deletedAssets = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!path.StartsWith(OutputRoot + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

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

        var deletedEmptyFolders = DeleteEmptyFoldersUnderOutputRoot();
        Debug.Log($"WeaponGroupConfig: deleted assets={deletedAssets}, deleted empty folders={deletedEmptyFolders}.");

        return deletedAssets;
    }

    private static int DeleteEmptyFoldersUnderOutputRoot()
    {
        var fullRoot = ToFullPath(OutputRoot);
        if (!Directory.Exists(fullRoot))
            return 0;

        var deleted = 0;
        var directories = Directory
            .GetDirectories(fullRoot, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length)
            .ToArray();

        foreach (var directory in directories)
        {
            if (!IsDirectoryEmptyIgnoringMeta(directory))
                continue;

            var assetPath = ToAssetPath(directory);
            if (string.IsNullOrWhiteSpace(assetPath) || string.Equals(assetPath, OutputRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (AssetDatabase.DeleteAsset(assetPath))
            {
                deleted++;
            }
            else
            {
                // Резервная очистка для случайных папок вида Ally 1 / Enemy 1,
                // если Unity не удаляет их через AssetDatabase.
                Directory.Delete(directory, recursive: false);
                deleted++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return deleted;
    }

    private static bool IsDirectoryEmptyIgnoringMeta(string directory)
    {
        foreach (var entry in Directory.GetFileSystemEntries(directory))
        {
            if (!entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string ToAssetPath(string fullPath)
    {
        var normalizedFullPath = fullPath.Replace("\\", "/");
        var normalizedDataPath = Application.dataPath.Replace("\\", "/");

        if (!normalizedFullPath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return "Assets" + normalizedFullPath.Substring(normalizedDataPath.Length);
    }

    private static List<ParsedWeaponGroupRow> ValidateRows(
        IReadOnlyList<List<string>> rows,
        IReadOnlyDictionary<string, int> columnIndex,
        IReadOnlyDictionary<string, WeaponConfig> weaponIndex,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        var result = new List<ParsedWeaponGroupRow>(rows.Count - 1);
        var knownGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (IsEmptyRow(row))
                continue;

            var rowNumber = rowIndex + 1;
            var id = Get(row, columnIndex, "id");

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"Row {rowNumber}: пустой id.");
                continue;
            }

            if (!knownGroupIds.Add(id))
            {
                errors.Add($"Row {rowNumber} / {id}: дублирующий id группы в CSV.");
                continue;
            }

            var overallCheck = Get(row, columnIndex, "overallCheck");
            if (!string.Equals(overallCheck, "OK", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Row {rowNumber} / {id}: overallCheck != OK ({overallCheck}). Asset не будет создан.");
                continue;
            }

            var groupKind = ParseEnum<WeaponGroupKind>(Get(row, columnIndex, "groupKind"), rowNumber, id, errors);
            var allyType = ParseEnumOrNone<WeaponGroupAllyType>(Get(row, columnIndex, "allyType"));
            var enemyFaction = ParseEnumOrNone<WeaponGroupEnemyFaction>(Get(row, columnIndex, "enemyFaction"));

            ValidateKindSpecificFields(groupKind, allyType, enemyFaction, rowNumber, id, errors);

            var weaponRefs = ResolveWeapons(row, columnIndex, weaponIndex, rowNumber, id, errors, warnings);
            var declaredWeaponCount = ParseInt(Get(row, columnIndex, "weaponCount"), rowNumber, id, "weaponCount", errors);
            if (weaponRefs.Count != declaredWeaponCount)
            {
                errors.Add($"Row {rowNumber} / {id}: weaponCount={declaredWeaponCount}, но найдено ссылок weaponConfigXX={weaponRefs.Count}.");
            }

            result.Add(new ParsedWeaponGroupRow(
                row,
                columnIndex,
                weaponRefs,
                groupKind,
                allyType,
                enemyFaction,
                BuildAssetPath(id, groupKind, allyType, enemyFaction, row, columnIndex)));
        }

        return result;
    }

    private static void ValidateKindSpecificFields(
        WeaponGroupKind groupKind,
        WeaponGroupAllyType allyType,
        WeaponGroupEnemyFaction enemyFaction,
        int rowNumber,
        string id,
        ICollection<string> errors)
    {
        if (groupKind == WeaponGroupKind.Ally)
        {
            if (allyType == WeaponGroupAllyType.None)
                errors.Add($"Row {rowNumber} / {id}: groupKind=Ally, но allyType пустой.");
            if (enemyFaction != WeaponGroupEnemyFaction.None)
                errors.Add($"Row {rowNumber} / {id}: groupKind=Ally, но enemyFaction заполнен.");
        }

        if (groupKind == WeaponGroupKind.Enemy)
        {
            if (enemyFaction == WeaponGroupEnemyFaction.None)
                errors.Add($"Row {rowNumber} / {id}: groupKind=Enemy, но enemyFaction пустой.");
            if (allyType != WeaponGroupAllyType.None)
                errors.Add($"Row {rowNumber} / {id}: groupKind=Enemy, но allyType заполнен.");
        }
    }

    private static Dictionary<string, WeaponConfig> BuildWeaponIndex(ICollection<string> errors)
    {
        var result = new Dictionary<string, WeaponConfig>(StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets("t:WeaponConfig", new[] { WeaponSearchFolder });

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
            if (weapon == null)
                continue;

            var serialized = new SerializedObject(weapon);
            var idProperty = serialized.FindProperty("id");
            var id = idProperty?.stringValue;

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"WeaponConfig без id: {path}");
                continue;
            }

            if (result.ContainsKey(id))
            {
                errors.Add($"Дублирующий WeaponConfig id: {id}. Второй путь: {path}");
                continue;
            }

            result.Add(id, weapon);
        }

        if (result.Count == 0)
            errors.Add($"Не найдены WeaponConfig asset-ы в {WeaponSearchFolder}.");

        return result;
    }

    private static List<WeaponConfig> ResolveWeapons(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndex,
        IReadOnlyDictionary<string, WeaponConfig> weaponIndex,
        int rowNumber,
        string groupId,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        var result = new List<WeaponConfig>(5);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i <= 5; i++)
        {
            var column = $"weaponConfig{i:00}";
            var weaponId = Get(row, columnIndex, column);
            if (string.IsNullOrWhiteSpace(weaponId))
                continue;

            if (!weaponIndex.TryGetValue(weaponId, out var weaponConfig) || weaponConfig == null)
            {
                errors.Add($"Row {rowNumber} / {groupId}: WeaponConfig не найден: {weaponId}");
                continue;
            }

            if (!seen.Add(weaponId))
            {
                errors.Add($"Row {rowNumber} / {groupId}: повтор WeaponConfig {weaponId} внутри группы.");
                continue;
            }

            result.Add(weaponConfig);
        }

        return result;
    }

    private static void WriteSerialized(WeaponGroupConfig asset, ParsedWeaponGroupRow parsedRow)
    {
        var row = parsedRow.Row;
        var columnIndex = parsedRow.ColumnIndex;
        var serialized = new SerializedObject(asset);

        SetString(serialized, "id", Get(row, columnIndex, "id"));
        SetString(serialized, "displayName", Get(row, columnIndex, "displayName"));
        SetString(serialized, "description", Get(row, columnIndex, "description"));

        SetInt(serialized, "level", ParseIntStrict(Get(row, columnIndex, "level")));
        SetEnum(serialized, "groupKind", parsedRow.GroupKind);
        SetEnum(serialized, "allyType", parsedRow.AllyType);
        SetEnum(serialized, "enemyFaction", parsedRow.EnemyFaction);
        SetInt(serialized, "strengthRank", ParseIntStrict(Get(row, columnIndex, "strengthRank")));
        SetString(serialized, "strengthOrder", Get(row, columnIndex, "strengthOrder"));

        SetString(serialized, "weaponOwner", Get(row, columnIndex, "weaponOwner"));
        SetString(serialized, "weaponOwnerKey", Get(row, columnIndex, "weaponOwnerKey"));

        SetInt(serialized, "variant", ParseIntStrict(Get(row, columnIndex, "variant")));
        SetInt(serialized, "weaponCount", ParseIntStrict(Get(row, columnIndex, "weaponCount")));
        SetInt(serialized, "powerScore", ParseIntStrict(Get(row, columnIndex, "powerScore")));
        SetString(serialized, "tierPattern", Get(row, columnIndex, "tierPattern"));
        SetString(serialized, "familyPattern", Get(row, columnIndex, "familyPattern"));

        var weaponsProperty = serialized.FindProperty("weaponConfigs");
        if (weaponsProperty == null || !weaponsProperty.isArray)
            throw new InvalidOperationException("WeaponGroupConfig.weaponConfigs не найден или не является массивом/List.");

        weaponsProperty.arraySize = parsedRow.WeaponRefs.Count;
        for (var i = 0; i < parsedRow.WeaponRefs.Count; i++)
        {
            weaponsProperty.GetArrayElementAtIndex(i).objectReferenceValue = parsedRow.WeaponRefs[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.name = Get(row, columnIndex, "id");
    }

    private static string BuildAssetPath(
        string id,
        WeaponGroupKind groupKind,
        WeaponGroupAllyType allyType,
        WeaponGroupEnemyFaction enemyFaction,
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndex)
    {
        var ownerFolder = groupKind == WeaponGroupKind.Ally
            ? $"Ally/{allyType}"
            : $"Enemy/{enemyFaction}";

        return $"{OutputRoot}/{ownerFolder}/{id}.asset";
    }

    private static void PrepareOutputFolders(IReadOnlyCollection<ParsedWeaponGroupRow> parsedRows)
    {
        var folders = parsedRows
            .Select(row => Path.GetDirectoryName(row.AssetPath)?.Replace("\\", "/"))
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var folder in folders)
        {
            EnsureExactFolderOnDisk(folder);
        }

        // Важно: папки создаются ДО AssetDatabase.StartAssetEditing.
        // Если создавать и сразу проверять папку внутри StartAssetEditing,
        // Unity может ещё не видеть её в AssetDatabase.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        foreach (var folder in folders)
        {
            AssetDatabase.ImportAsset(folder, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureExactFolderOnDisk(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var normalizedFolder = folder.Replace("\\", "/");
        var fullPath = ToFullPath(normalizedFolder);

        if (File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"Нельзя создать папку '{normalizedFolder}', потому что по этому пути уже есть файл. Удали или переименуй этот файл вручную.");
        }

        // Не используем AssetDatabase.CreateFolder.
        // Он может создать 'Ally 1', 'Enemy 1' при конфликте имени.
        // Directory.CreateDirectory создаёт только точный путь и никогда не переименовывает папку.
        Directory.CreateDirectory(fullPath);

        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Не удалось создать папку на диске: {normalizedFolder}");
        }
    }

    private static string ToFullPath(string assetPath)
    {
        var normalizedAssetPath = assetPath.Replace("\\", "/");
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Не удалось определить корень Unity-проекта.");

        return Path.GetFullPath(Path.Combine(projectRoot, normalizedAssetPath));
    }

    private static List<List<string>> ReadCsv(string assetPath, ICollection<string> errors)
    {
        try
        {
            var text = File.ReadAllText(assetPath, Encoding.UTF8);
            return CsvParser.Parse(text);
        }
        catch (Exception ex)
        {
            errors.Add($"Не удалось прочитать CSV {assetPath}: {ex.Message}");
            return new List<List<string>>();
        }
    }

    private static Dictionary<string, int> BuildColumnIndex(IReadOnlyList<string> header)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var name = (header[i] ?? string.Empty).Trim('\uFEFF', ' ', '\t', '\r', '\n');
            if (!string.IsNullOrWhiteSpace(name) && !result.ContainsKey(name))
                result.Add(name, i);
        }

        return result;
    }

    private static void ValidateRequiredColumns(
        IReadOnlyDictionary<string, int> columnIndex,
        ICollection<string> errors)
    {
        foreach (var column in RequiredColumns)
        {
            if (!columnIndex.ContainsKey(column))
                errors.Add($"В CSV отсутствует обязательная колонка: {column}");
        }
    }

    private static string Get(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndex,
        string columnName)
    {
        if (!columnIndex.TryGetValue(columnName, out var index) || index < 0 || index >= row.Count)
            return string.Empty;

        return (row[index] ?? string.Empty).Trim();
    }

    private static bool IsEmptyRow(IEnumerable<string> row)
    {
        return row.All(string.IsNullOrWhiteSpace);
    }

    private static T ParseEnum<T>(string value, int rowNumber, string id, ICollection<string> errors)
        where T : struct
    {
        if (Enum.TryParse<T>(value, true, out var result))
            return result;

        errors.Add($"Row {rowNumber} / {id}: значение '{value}' не распознано как {typeof(T).Name}.");
        return default;
    }

    private static T ParseEnumOrNone<T>(string value) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return default;

        return Enum.TryParse<T>(value, true, out var result) ? result : default;
    }

    private static int ParseInt(string value, int rowNumber, string id, string columnName, ICollection<string> errors)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        errors.Add($"Row {rowNumber} / {id}: колонка {columnName} должна быть int, значение='{value}'.");
        return 0;
    }

    private static int ParseIntStrict(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Serialized property не найден: {propertyName}");

        property.stringValue = value ?? string.Empty;
    }

    private static void SetInt(SerializedObject serialized, string propertyName, int value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Serialized property не найден: {propertyName}");

        property.intValue = value;
    }

    private static void SetEnum<T>(SerializedObject serialized, string propertyName, T value)
        where T : Enum
    {
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Serialized property не найден: {propertyName}");

        var values = Enum.GetValues(typeof(T));
        var index = Array.IndexOf(values, value);
        property.enumValueIndex = index >= 0 ? index : 0;
    }

    private static void Finish(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings,
        int created,
        int updated,
        int deleted,
        int validated,
        bool validateOnly,
        DateTime startedAt)
    {
        var elapsed = DateTime.Now - startedAt;

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Take(25));
            if (errors.Count > 25)
                message += $"\n...и ещё {errors.Count - 25} ошибок.";

            Debug.LogError($"WeaponGroupConfig CSV generation failed. Errors={errors.Count}\n{message}");
            EditorUtility.DisplayDialog(
                "WeaponGroupConfig CSV generator — ERROR",
                $"Ошибок: {errors.Count}\n\n{message}",
                "OK");
            return;
        }

        foreach (var warning in warnings.Take(50))
            Debug.LogWarning($"WeaponGroupConfig CSV generator: {warning}");

        if (warnings.Count > 50)
            Debug.LogWarning($"WeaponGroupConfig CSV generator: and {warnings.Count - 50} more warnings.");

        var mode = validateOnly ? "Validation" : "Creation";
        var result =
            $"{mode} complete.\n" +
            $"CSV: {DefaultCsvPath}\n" +
            $"Validated rows: {validated}\n" +
            $"Created: {created}\n" +
            $"Updated: {updated}\n" +
            $"Deleted: {deleted}\n" +
            $"Warnings: {warnings.Count}\n" +
            $"Elapsed: {elapsed.TotalSeconds:F1}s";

        Debug.Log($"WeaponGroupConfig {result}");
        EditorUtility.DisplayDialog("WeaponGroupConfig CSV generator", result, "OK");
    }

    private sealed class ParsedWeaponGroupRow
    {
        public ParsedWeaponGroupRow(
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> columnIndex,
            IReadOnlyList<WeaponConfig> weaponRefs,
            WeaponGroupKind groupKind,
            WeaponGroupAllyType allyType,
            WeaponGroupEnemyFaction enemyFaction,
            string assetPath)
        {
            Row = row;
            ColumnIndex = columnIndex;
            WeaponRefs = weaponRefs;
            GroupKind = groupKind;
            AllyType = allyType;
            EnemyFaction = enemyFaction;
            AssetPath = assetPath;
        }

        public IReadOnlyList<string> Row { get; }
        public IReadOnlyDictionary<string, int> ColumnIndex { get; }
        public IReadOnlyList<WeaponConfig> WeaponRefs { get; }
        public WeaponGroupKind GroupKind { get; }
        public WeaponGroupAllyType AllyType { get; }
        public WeaponGroupEnemyFaction EnemyFaction { get; }
        public string AssetPath { get; }
    }

    private static class CsvParser
    {
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

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

                switch (ch)
                {
                    case '"':
                        inQuotes = true;
                        break;

                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;

                    case '\r':
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                            i++;
                        row.Add(cell.ToString());
                        rows.Add(row);
                        row = new List<string>();
                        cell.Clear();
                        break;

                    case '\n':
                        row.Add(cell.ToString());
                        rows.Add(row);
                        row = new List<string>();
                        cell.Clear();
                        break;

                    default:
                        cell.Append(ch);
                        break;
                }
            }

            row.Add(cell.ToString());
            if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
                rows.Add(row);

            return rows;
        }
    }
}