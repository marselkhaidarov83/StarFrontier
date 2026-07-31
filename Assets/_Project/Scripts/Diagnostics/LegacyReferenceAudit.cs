#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LegacyReferenceAudit
{
    private const string ProjectAssetsRoot = "Assets/_Project";

    private static readonly string[] CandidateNames =
    {
        "SystemMapController",
        "SystemMapController2",
        "ShipMarkerView",
        "ShipMarkerView2",
        "SystemShipMarkerController",
        "SystemShipMarkerController2",
        "SystemMapHUDController",

        "ArrivalFxView",
        "ArrivalFxView2",
        "ShipEngineGlowView",
        "ShipEngineGlowView2",
        "SystemDestinationMarkerController",
        "SystemDestinationMarkerController2",
        "SystemMapClickArea",
        "SystemMapClickArea2",
        "SystemMapDestinationController",
        "SystemMapDestinationController2",
        "SystemTransitionController",
        "SystemTransitionController2",
        "SystemTravelArrivalHandler",
        "SystemTravelArrivalHandler2",
        "SystemTravelHudController",
        "SystemTravelVisualController",
        "SystemTravelVisualController2",
        "TravelLineView",
        "TravelLineView2",

        "CombatStartedEvent"
    };

    private static readonly HashSet<string> CandidateSet =
        new HashSet<string>(CandidateNames, StringComparer.Ordinal);

    private sealed class ScriptInfo
    {
        public string ClassName;
        public string AssetPath;
        public string Guid;
    }

    private sealed class UsageInfo
    {
        public string ClassName;
        public string AssetPath;
        public string HierarchyPath;
        public bool GameObjectActive;
        public bool ComponentEnabled;
        public List<string> VisualAssets = new List<string>();
    }

    private sealed class MissingScriptInfo
    {
        public string AssetPath;
        public string HierarchyPath;
        public int Count;
    }

    [MenuItem("STAR FRONTIER/Diagnostics/Run Legacy Reference Audit")]
    private static void RunAudit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Legacy Reference Audit",
                "Остановите Play Mode перед запуском аудита.",
                "OK");

            return;
        }

        try
        {
            List<ScriptInfo> scripts = FindCandidateScripts();
            List<UsageInfo> usages = new List<UsageInfo>();
            List<MissingScriptInfo> missingScripts =
                new List<MissingScriptInfo>();

            ScanPrefabs(usages, missingScripts);
            ScanScenes(usages, missingScripts);

            Dictionary<string, List<string>> codeReferences =
                FindCodeReferences();

            List<string> suspiciousFiles = FindSuspiciousScriptFiles();
            string outputDirectory = CreateOutputDirectory();

            WriteReferenceReport(
                Path.Combine(
                    outputDirectory,
                    "S3-02_Automated_Reference_Report.md"),
                scripts,
                usages,
                codeReferences,
                suspiciousFiles);

            WriteMissingScriptsReport(
                Path.Combine(
                    outputDirectory,
                    "S3-02_Missing_Scripts_Report.md"),
                missingScripts);

            WriteArtReport(
                Path.Combine(
                    outputDirectory,
                    "S3-02_Legacy_Art_Report.md"),
                usages);

            string reportPath = Path.Combine(
                outputDirectory,
                "S3-02_Automated_Reference_Report.md");

            Debug.Log(
                $"[LegacyReferenceAudit] Аудит завершён: {reportPath}");

            EditorUtility.RevealInFinder(reportPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Legacy Reference Audit",
                "Аудит завершился с ошибкой. Проверьте Console.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static List<ScriptInfo> FindCandidateScripts()
    {
        List<ScriptInfo> result = new List<ScriptInfo>();

        string[] guids = AssetDatabase.FindAssets(
            "t:MonoScript",
            new[] { ProjectAssetsRoot });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string className = Path.GetFileNameWithoutExtension(path);

            if (!CandidateSet.Contains(className))
                continue;

            result.Add(new ScriptInfo
            {
                ClassName = className,
                AssetPath = path,
                Guid = AssetDatabase.AssetPathToGUID(path)
            });
        }

        return result
            .OrderBy(item => item.ClassName)
            .ThenBy(item => item.AssetPath)
            .ToList();
    }

    private static void ScanPrefabs(
        List<UsageInfo> usages,
        List<MissingScriptInfo> missingScripts)
    {
        string[] paths = FindAssetPaths("t:Prefab")
            .Where(path => path.EndsWith(
                ".prefab",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        for (int index = 0; index < paths.Length; index++)
        {
            string path = paths[index];

            EditorUtility.DisplayProgressBar(
                "Legacy Reference Audit",
                $"Проверка prefab: {path}",
                paths.Length == 0
                    ? 0f
                    : (float)index / paths.Length);

            GameObject root = null;

            try
            {
                root = PrefabUtility.LoadPrefabContents(path);

                if (root != null)
                {
                    ScanHierarchy(
                        root,
                        path,
                        usages,
                        missingScripts);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Не удалось проверить prefab {path}: " +
                    exception.Message);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ScanScenes(
        List<UsageInfo> usages,
        List<MissingScriptInfo> missingScripts)
    {
        string[] paths = FindAssetPaths("t:Scene")
            .Where(path => path.EndsWith(
                ".unity",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        for (int index = 0; index < paths.Length; index++)
        {
            string path = paths[index];

            EditorUtility.DisplayProgressBar(
                "Legacy Reference Audit",
                $"Проверка сцены: {path}",
                paths.Length == 0
                    ? 0f
                    : (float)index / paths.Length);

            Scene previewScene = default;

            try
            {
                previewScene =
                    EditorSceneManager.OpenPreviewScene(path);

                foreach (GameObject root
                         in previewScene.GetRootGameObjects())
                {
                    ScanHierarchy(
                        root,
                        path,
                        usages,
                        missingScripts);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Не удалось проверить сцену {path}: " +
                    exception.Message);
            }
            finally
            {
                if (previewScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }
    }

    private static void ScanHierarchy(
        GameObject root,
        string assetPath,
        List<UsageInfo> usages,
        List<MissingScriptInfo> missingScripts)
    {
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);

        foreach (Transform currentTransform in transforms)
        {
            GameObject gameObject = currentTransform.gameObject;

            int missingCount =
                GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(gameObject);

            if (missingCount > 0)
            {
                missingScripts.Add(new MissingScriptInfo
                {
                    AssetPath = assetPath,
                    HierarchyPath =
                        GetHierarchyPath(currentTransform),
                    Count = missingCount
                });
            }

            foreach (Component component
                     in gameObject.GetComponents<Component>())
            {
                if (component == null)
                    continue;

                string className = component.GetType().Name;

                if (!CandidateSet.Contains(className))
                    continue;

                bool enabled = true;

                if (component is Behaviour behaviour)
                    enabled = behaviour.enabled;

                usages.Add(new UsageInfo
                {
                    ClassName = className,
                    AssetPath = assetPath,
                    HierarchyPath =
                        GetHierarchyPath(currentTransform),
                    GameObjectActive = gameObject.activeSelf,
                    ComponentEnabled = enabled,
                    VisualAssets = CollectVisualAssets(gameObject)
                });
            }
        }
    }

    private static List<string> CollectVisualAssets(GameObject root)
    {
        HashSet<string> result = new HashSet<string>();

        foreach (Component component
                 in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            try
            {
                SerializedObject serializedObject =
                    new SerializedObject(component);

                SerializedProperty property =
                    serializedObject.GetIterator();

                bool enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (property.propertyType !=
                        SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    UnityEngine.Object referencedObject =
                        property.objectReferenceValue;

                    if (referencedObject == null)
                        continue;

                    string path =
                        AssetDatabase.GetAssetPath(referencedObject);

                    if (IsVisualAsset(path))
                        result.Add(path);
                }
            }
            catch
            {
                // Диагностика должна продолжаться.
            }
        }

        return result.OrderBy(path => path).ToList();
    }

    private static bool IsVisualAsset(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string extension =
            Path.GetExtension(path).ToLowerInvariant();

        return extension == ".png"
            || extension == ".jpg"
            || extension == ".jpeg"
            || extension == ".psd"
            || extension == ".tga"
            || extension == ".tif"
            || extension == ".tiff"
            || extension == ".mat"
            || extension == ".spriteatlas"
            || extension == ".rendertexture"
            || extension == ".ttf"
            || extension == ".otf"
            || extension == ".shader";
    }

    private static Dictionary<string, List<string>>
        FindCodeReferences()
    {
        Dictionary<string, List<string>> result =
            CandidateNames.ToDictionary(
                name => name,
                name => new List<string>());

        string[] scriptPaths = FindAssetPaths("t:MonoScript")
            .Where(path => path.EndsWith(
                ".cs",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string scriptPath in scriptPaths)
        {
            string ownClassName =
                Path.GetFileNameWithoutExtension(scriptPath);

            string[] lines;

            try
            {
                lines = File.ReadAllLines(
                    ToAbsoluteProjectPath(scriptPath));
            }
            catch
            {
                continue;
            }

            foreach (string candidate in CandidateNames)
            {
                if (ownClassName == candidate)
                    continue;

                Regex tokenRegex = new Regex(
                    $@"\b{Regex.Escape(candidate)}\b");

                for (int lineIndex = 0;
                     lineIndex < lines.Length;
                     lineIndex++)
                {
                    if (!tokenRegex.IsMatch(lines[lineIndex]))
                        continue;

                    result[candidate].Add(
                        $"{scriptPath}:{lineIndex + 1}");

                    break;
                }
            }
        }

        return result;
    }

    private static List<string> FindSuspiciousScriptFiles()
    {
        string[] scriptPaths = FindAssetPaths("t:MonoScript")
            .Where(path => path.EndsWith(
                ".cs",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        HashSet<string> result = new HashSet<string>();

        Dictionary<string, List<string>> pathsByName =
            scriptPaths
                .GroupBy(path =>
                    Path.GetFileNameWithoutExtension(path))
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

        foreach (string path in scriptPaths)
        {
            string fileName =
                Path.GetFileNameWithoutExtension(path);

            string lowerName = fileName.ToLowerInvariant();

            if (lowerName.Contains("copy")
                || lowerName.Contains("backup")
                || lowerName.Contains("old")
                || lowerName.Contains("legacy")
                || lowerName.Contains("temp"))
            {
                result.Add(path);
            }

            if (!fileName.EndsWith("2"))
                continue;

            string baseName =
                fileName.Substring(0, fileName.Length - 1);

            if (!pathsByName.ContainsKey(baseName))
                continue;

            result.Add(path);

            foreach (string basePath in pathsByName[baseName])
                result.Add(basePath);
        }

        return result.OrderBy(path => path).ToList();
    }

    private static string[] FindAssetPaths(string filter)
    {
        return AssetDatabase.FindAssets(
                filter,
                new[] { ProjectAssetsRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .OrderBy(path => path)
            .ToArray();
    }

    private static string CreateOutputDirectory()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new InvalidOperationException(
                "Не удалось определить корень проекта.");
        }

        string outputDirectory = Path.Combine(
            projectRoot,
            "Documentation",
            "Sprint3",
            "LegacyAudit",
            "Generated");

        Directory.CreateDirectory(outputDirectory);

        return outputDirectory;
    }

    private static void WriteReferenceReport(
        string reportPath,
        List<ScriptInfo> scripts,
        List<UsageInfo> usages,
        Dictionary<string, List<string>> codeReferences,
        List<string> suspiciousFiles)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "# S3-02 — Автоматический отчёт legacy-ссылок");
        builder.AppendLine();
        builder.AppendLine(
            $"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine(
            "| Класс | Файл | GUID | Сцены/prefab | Ссылки из кода | Статус |");
        builder.AppendLine(
            "|---|---|---|---:|---:|---|");

        foreach (string candidate in CandidateNames)
        {
            List<ScriptInfo> matchingScripts = scripts
                .Where(item => item.ClassName == candidate)
                .ToList();

            int usageCount = usages.Count(
                item => item.ClassName == candidate);

            int codeCount = codeReferences.TryGetValue(
                candidate,
                out List<string> references)
                    ? references.Count
                    : 0;

            string scriptPaths = matchingScripts.Count == 0
                ? "не найден"
                : string.Join(
                    "<br>",
                    matchingScripts.Select(
                        item => Escape(item.AssetPath)));

            string guids = matchingScripts.Count == 0
                ? "—"
                : string.Join(
                    "<br>",
                    matchingScripts.Select(item => item.Guid));

            string s = DetermineStatus(
                    matchingScripts.Count,
                    usageCount,
                    codeCount);
            builder.AppendLine(
                $"| `{candidate}` | {scriptPaths} | {guids} | " +
                $"{usageCount} | {codeCount} | " +
                $"{s} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            "## Подробные ссылки в сценах и prefab");
        builder.AppendLine();
        builder.AppendLine(
            "| Класс | Ресурс | Путь объекта | Активен | Компонент включён |");
        builder.AppendLine("|---|---|---|---|---|");

        foreach (UsageInfo usage in usages
                     .OrderBy(item => item.ClassName)
                     .ThenBy(item => item.AssetPath)
                     .ThenBy(item => item.HierarchyPath))
        {
            builder.AppendLine(
                $"| `{usage.ClassName}` | " +
                $"{Escape(usage.AssetPath)} | " +
                $"{Escape(usage.HierarchyPath)} | " +
                $"{ToYesNo(usage.GameObjectActive)} | " +
                $"{ToYesNo(usage.ComponentEnabled)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Ссылки из C#-кода");

        foreach (string candidate in CandidateNames)
        {
            builder.AppendLine();
            builder.AppendLine($"### {candidate}");

            if (!codeReferences.TryGetValue(
                    candidate,
                    out List<string> references)
                || references.Count == 0)
            {
                builder.AppendLine("Не обнаружены.");
                continue;
            }

            foreach (string reference in references)
                builder.AppendLine($"- `{reference}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Подозрительные имена файлов");

        foreach (string path in suspiciousFiles)
            builder.AppendLine($"- `{path}`");

        WriteUtf8(reportPath, builder.ToString());
    }

    private static void WriteMissingScriptsReport(
        string reportPath,
        List<MissingScriptInfo> missingScripts)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("# S3-02 — Missing Script");
        builder.AppendLine();

        if (missingScripts.Count == 0)
        {
            builder.AppendLine(
                "Missing Script в сценах и prefab не обнаружены.");
        }
        else
        {
            builder.AppendLine(
                "| Ресурс | Путь объекта | Количество |");
            builder.AppendLine("|---|---|---:|");

            foreach (MissingScriptInfo item in missingScripts)
            {
                builder.AppendLine(
                    $"| {Escape(item.AssetPath)} | " +
                    $"{Escape(item.HierarchyPath)} | " +
                    $"{item.Count} |");
            }
        }

        WriteUtf8(reportPath, builder.ToString());
    }

    private static void WriteArtReport(
        string reportPath,
        List<UsageInfo> usages)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "# S3-02 — Графические legacy-ссылки");
        builder.AppendLine();
        builder.AppendLine(
            "| Компонент | Сцена/prefab | Объект | Ресурс |");
        builder.AppendLine("|---|---|---|---|");

        bool hasRows = false;

        foreach (UsageInfo usage in usages)
        {
            foreach (string visualAsset in usage.VisualAssets)
            {
                hasRows = true;

                builder.AppendLine(
                    $"| `{usage.ClassName}` | " +
                    $"{Escape(usage.AssetPath)} | " +
                    $"{Escape(usage.HierarchyPath)} | " +
                    $"{Escape(visualAsset)} |");
            }
        }

        if (!hasRows)
        {
            builder.AppendLine(
                "| — | — | — | Не обнаружено |");
        }

        WriteUtf8(reportPath, builder.ToString());
    }

    private static string DetermineStatus(
        int scriptCount,
        int usageCount,
        int codeReferenceCount)
    {
        if (scriptCount == 0)
            return "файл не найден";

        if (usageCount > 0)
            return "**используется — не удалять**";

        if (codeReferenceCount > 0)
            return "**есть ссылки из кода — не удалять**";

        return "требуется ручная проверка";
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static string ToAbsoluteProjectPath(string assetPath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        return Path.Combine(
            projectRoot ?? string.Empty,
            assetPath.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }

    private static string ToYesNo(bool value)
    {
        return value ? "да" : "нет";
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        return value
            .Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private static void WriteUtf8(
        string path,
        string content)
    {
        File.WriteAllText(
            path,
            content,
            new UTF8Encoding(false));
    }
}

#endif