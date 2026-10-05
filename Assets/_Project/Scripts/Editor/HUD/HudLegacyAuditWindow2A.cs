#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor-only аудит для production-задачи 2A-S03-03-T01.
///
/// Инструмент:
/// - сканирует включённые Build Settings scenes;
/// - перечисляет Canvas roots и потенциальные HUD-панели;
/// - ищет дубли имён и Missing Script;
/// - ищет прямые ссылки UI на scene objects за пределами Canvas;
/// - создаёт generated audit report и отдельную mapping-таблицу.
///
/// Инструмент ничего не удаляет и не изменяет в сценах.
/// </summary>
public sealed class HudLegacyAuditWindow2A : EditorWindow
{
    private const string ReportAssetPath =
        "Assets/_Project/QA/Reports/" +
        "2A-S03-03-T01_HUD_Legacy_Audit.generated.md";

    private const string MappingAssetPath =
        "Assets/_Project/QA/Reports/" +
        "2A-S03-03-T01_HUD_Mapping.md";

    private static readonly string[] HudNameTokens =
    {
        "hud",
        "canvas",
        "panel",
        "ship",
        "status",
        "fuel",
        "target",
        "interaction",
        "interact",
        "recenter",
        "returntoship",
        "return_to_ship",
        "marker",
        "control",
        "joystick",
        "move"
    };

    private static readonly string[] GameplayTypeTokens =
    {
        "service",
        "state",
        "gameplay",
        "movement",
        "combat",
        "travel",
        "fuel",
        "target",
        "interaction",
        "simulation"
    };

    [SerializeField]
    private string branchName =
        "task/2A-S03-03-T01-hud-audit";

    [SerializeField]
    private string commitSha = "";

    private sealed class MappingEntry
    {
        public string ScenePath;
        public string ObjectPath;
        public string Kind;
    }

    [MenuItem("STAR FRONTIER/QA/HUD Legacy Audit")]
    public static void OpenWindow()
    {
        HudLegacyAuditWindow2A window =
            GetWindow<HudLegacyAuditWindow2A>();

        window.titleContent =
            new GUIContent("HUD Legacy Audit");

        window.minSize =
            new Vector2(560f, 240f);

        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2A-S03-03-T01 — HUD Legacy Audit",
            EditorStyles.boldLabel);

        EditorGUILayout.Space(6f);

        EditorGUILayout.HelpBox(
            "Инструмент только формирует отчёт. " +
            "Он не удаляет, не выключает и не перемещает объекты.",
            MessageType.Info);

        branchName = EditorGUILayout.TextField(
            "Branch",
            branchName);

        commitSha = EditorGUILayout.TextField(
            "Exact SHA",
            commitSha);

        EditorGUILayout.Space(10f);

        using (new EditorGUI.DisabledScope(
                   EditorApplication.isPlaying))
        {
            if (GUILayout.Button(
                    "Scan Enabled Build Scenes",
                    GUILayout.Height(36f)))
            {
                RunAudit();
            }
        }

        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField(
            "Generated report:",
            ReportAssetPath);

        EditorGUILayout.LabelField(
            "Manual mapping:",
            MappingAssetPath);
    }

    private void RunAudit()
    {
        if (string.IsNullOrWhiteSpace(commitSha))
        {
            bool continueWithoutSha =
                EditorUtility.DisplayDialog(
                    "Exact SHA не заполнен",
                    "Без точного SHA результат нельзя считать " +
                    "формальным evidence.\n\n" +
                    "Продолжить и пометить SHA как NOT_RECORDED?",
                    "Продолжить",
                    "Отмена");

            if (!continueWithoutSha)
            {
                return;
            }
        }

        bool canContinue =
            EditorSceneManager
                .SaveCurrentModifiedScenesIfUserWantsTo();

        if (!canContinue)
        {
            return;
        }

        SceneSetup[] previousSetup =
            EditorSceneManager.GetSceneManagerSetup();

        StringBuilder report =
            new StringBuilder(32_768);

        List<MappingEntry> mappingEntries =
            new List<MappingEntry>();

        try
        {
            WriteReportHeader(report);

            EditorBuildSettingsScene[] buildScenes =
                EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .ToArray();

            report.AppendLine("## Enabled Build Settings scenes");
            report.AppendLine();

            foreach (EditorBuildSettingsScene buildScene
                     in buildScenes)
            {
                report.AppendLine(
                    "- `" + buildScene.path + "`");
            }

            report.AppendLine();

            foreach (EditorBuildSettingsScene buildScene
                     in buildScenes)
            {
                try
                {
                    Scene scene =
                        EditorSceneManager.OpenScene(
                            buildScene.path,
                            OpenSceneMode.Single);

                    AuditScene(
                        scene,
                        report,
                        mappingEntries);
                }
                catch (Exception exception)
                {
                    report.AppendLine(
                        "## Scene open failure");

                    report.AppendLine();
                    report.AppendLine(
                        "- Scene: `" + buildScene.path + "`");

                    report.AppendLine(
                        "- Error: `" +
                        EscapeMarkdown(exception.Message) +
                        "`");

                    report.AppendLine();

                    Debug.LogException(exception);
                }
            }

            WriteGeneratedReport(report);
            WriteMappingTemplateIfMissing(mappingEntries);

            AssetDatabase.Refresh();

            TextAsset reportAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    ReportAssetPath);

            if (reportAsset != null)
            {
                Selection.activeObject = reportAsset;
                EditorGUIUtility.PingObject(reportAsset);
            }

            EditorUtility.DisplayDialog(
                "HUD audit completed",
                "Generated report:\n" +
                ReportAssetPath +
                "\n\nManual mapping:\n" +
                MappingAssetPath,
                "OK");

            Debug.Log(
                "[2A-S03-03-T01] HUD audit completed. " +
                "Report: " + ReportAssetPath);
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(
                previousSetup);
        }
    }

    private void WriteReportHeader(StringBuilder report)
    {
        report.AppendLine(
            "# 2A-S03-03-T01 — HUD Legacy Audit");

        report.AppendLine();
        report.AppendLine(
            "- Generated UTC: `" +
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") +
            "`");

        report.AppendLine(
            "- Unity: `" +
            Application.unityVersion +
            "`");

        report.AppendLine(
            "- Branch: `" +
            EscapeMarkdown(
                string.IsNullOrWhiteSpace(branchName)
                    ? "NOT_RECORDED"
                    : branchName) +
            "`");

        report.AppendLine(
            "- Exact SHA: `" +
            EscapeMarkdown(
                string.IsNullOrWhiteSpace(commitSha)
                    ? "NOT_RECORDED"
                    : commitSha) +
            "`");

        report.AppendLine();
        report.AppendLine(
            "> Generated file. Do not store final manual " +
            "decisions here. Fill HUD_Mapping.md instead.");

        report.AppendLine();
    }

    private static void AuditScene(
        Scene scene,
        StringBuilder report,
        List<MappingEntry> mappingEntries)
    {
        report.AppendLine(
            "# Scene: `" + scene.path + "`");

        report.AppendLine();

        GameObject[] sceneRoots =
            scene.GetRootGameObjects();

        Canvas[] allCanvases =
            sceneRoots
                .SelectMany(root =>
                    root.GetComponentsInChildren<Canvas>(true))
                .Distinct()
                .OrderBy(canvas =>
                    GetHierarchyPath(canvas.transform))
                .ToArray();

        Canvas[] rootCanvases =
            allCanvases
                .Where(IsRootCanvas)
                .ToArray();

        report.AppendLine(
            "- All Canvas components: **" +
            allCanvases.Length +
            "**");

        report.AppendLine(
            "- Root Canvas components: **" +
            rootCanvases.Length +
            "**");

        report.AppendLine();

        if (rootCanvases.Length == 0)
        {
            report.AppendLine(
                "_No Canvas components found._");

            report.AppendLine();
        }

        foreach (Canvas canvas in rootCanvases)
        {
            AuditCanvas(
                scene,
                canvas,
                report,
                mappingEntries);
        }

        WriteSceneDuplicateNames(
            allCanvases,
            report);

        WriteMissingScripts(
            sceneRoots,
            report);

        report.AppendLine();
    }

    private static void AuditCanvas(
        Scene scene,
        Canvas canvas,
        StringBuilder report,
        List<MappingEntry> mappingEntries)
    {
        string canvasPath =
            GetHierarchyPath(canvas.transform);

        CanvasScaler scaler =
            canvas.GetComponent<CanvasScaler>();

        GraphicRaycaster raycaster =
            canvas.GetComponent<GraphicRaycaster>();

        report.AppendLine(
            "## Root Canvas: `" + canvasPath + "`");

        report.AppendLine();
        report.AppendLine(
            "- Active in hierarchy: `" +
            canvas.gameObject.activeInHierarchy +
            "`");

        report.AppendLine(
            "- Active self: `" +
            canvas.gameObject.activeSelf +
            "`");

        report.AppendLine(
            "- Render Mode: `" +
            canvas.renderMode +
            "`");

        report.AppendLine(
            "- Sorting Order: `" +
            canvas.sortingOrder +
            "`");

        report.AppendLine(
            "- Canvas Scaler: `" +
            GetCanvasScalerSummary(scaler) +
            "`");

        report.AppendLine(
            "- Graphic Raycaster: `" +
            (raycaster != null ? "PRESENT" : "MISSING") +
            "`");

        report.AppendLine(
            "- Initial classification: `" +
            GetInitialCanvasClassification(canvas) +
            "`");

        report.AppendLine();

        mappingEntries.Add(
            new MappingEntry
            {
                ScenePath = scene.path,
                ObjectPath = canvasPath,
                Kind = "ROOT_CANVAS_" + canvas.renderMode
            });

        RectTransform[] candidates =
            canvas
                .GetComponentsInChildren<RectTransform>(true)
                .Where(rectTransform =>
                    rectTransform == canvas.transform ||
                    rectTransform.parent == canvas.transform ||
                    LooksLikeHudName(rectTransform.name))
                .Distinct()
                .OrderBy(rectTransform =>
                    GetHierarchyPath(rectTransform))
                .ToArray();

        report.AppendLine("### HUD/panel candidates");
        report.AppendLine();
        report.AppendLine(
            "| Active | Object path | Reason |");
        report.AppendLine(
            "|---|---|---|");

        foreach (RectTransform candidate in candidates)
        {
            string candidatePath =
                GetHierarchyPath(candidate);

            string reason =
                candidate == canvas.transform
                    ? "Canvas root"
                    : candidate.parent == canvas.transform
                        ? "Direct Canvas child"
                        : "HUD token in name";

            report.AppendLine(
                "| " +
                candidate.gameObject.activeInHierarchy +
                " | `" +
                EscapeMarkdown(candidatePath) +
                "` | " +
                reason +
                " |");

            mappingEntries.Add(
                new MappingEntry
                {
                    ScenePath = scene.path,
                    ObjectPath = candidatePath,
                    Kind = reason
                });
        }

        report.AppendLine();

        List<string> referenceRows =
            FindExternalSceneReferences(
                scene,
                canvas);

        report.AppendLine(
            "### External scene-object references from UI");

        report.AppendLine();

        if (referenceRows.Count == 0)
        {
            report.AppendLine(
                "_No external scene-object references found._");
        }
        else
        {
            report.AppendLine(
                "| Classification | UI component | " +
                "Serialized field | Referenced object | Type |");

            report.AppendLine(
                "|---|---|---|---|---|");

            foreach (string row in referenceRows)
            {
                report.AppendLine(row);
            }
        }

        report.AppendLine();
    }

    private static List<string> FindExternalSceneReferences(
        Scene scene,
        Canvas canvas)
    {
        HashSet<string> rows =
            new HashSet<string>();

        MonoBehaviour[] components =
            canvas.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour component in components)
        {
            if (component == null)
            {
                continue;
            }

            SerializedObject serializedObject;

            try
            {
                serializedObject =
                    new SerializedObject(component);
            }
            catch
            {
                continue;
            }

            SerializedProperty property =
                serializedObject.GetIterator();

            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.name == "m_Script" ||
                    property.propertyType !=
                    SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                UnityEngine.Object referencedObject =
                    property.objectReferenceValue;

                if (!TryGetReferencedTransform(
                        referencedObject,
                        out Transform targetTransform,
                        out string targetType))
                {
                    continue;
                }

                if (targetTransform.gameObject.scene != scene)
                {
                    continue;
                }

                if (targetTransform.IsChildOf(canvas.transform))
                {
                    continue;
                }

                bool suspicious =
                    referencedObject is MonoBehaviour &&
                    LooksLikeGameplayType(targetType);

                string classification =
                    suspicious
                        ? "SUSPECT_GAMEPLAY_REFERENCE"
                        : "REVIEW_EXTERNAL_REFERENCE";

                string row =
                    "| " + classification +
                    " | `" +
                    EscapeMarkdown(
                        component.GetType().Name +
                        " @ " +
                        GetHierarchyPath(component.transform)) +
                    "` | `" +
                    EscapeMarkdown(property.propertyPath) +
                    "` | `" +
                    EscapeMarkdown(
                        GetHierarchyPath(targetTransform)) +
                    "` | `" +
                    EscapeMarkdown(targetType) +
                    "` |";

                rows.Add(row);
            }
        }

        return rows
            .OrderBy(row => row)
            .ToList();
    }

    private static bool TryGetReferencedTransform(
        UnityEngine.Object referencedObject,
        out Transform targetTransform,
        out string targetType)
    {
        targetTransform = null;
        targetType = "";

        if (referencedObject is Component component)
        {
            targetTransform = component.transform;
            targetType = component.GetType().FullName;
            return true;
        }

        if (referencedObject is GameObject gameObject)
        {
            targetTransform = gameObject.transform;
            targetType = nameof(GameObject);
            return true;
        }

        return false;
    }

    private static void WriteSceneDuplicateNames(
        Canvas[] allCanvases,
        StringBuilder report)
    {
        RectTransform[] candidates =
            allCanvases
                .SelectMany(canvas =>
                    canvas.GetComponentsInChildren
                        <RectTransform>(true))
                .Where(rectTransform =>
                    LooksLikeHudName(rectTransform.name))
                .Distinct()
                .ToArray();

        IGrouping<string, RectTransform>[] duplicateGroups =
            candidates
                .GroupBy(rectTransform =>
                    NormalizeObjectName(rectTransform.name))
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key)
                .ToArray();

        report.AppendLine(
            "## Duplicate HUD-like names");

        report.AppendLine();

        if (duplicateGroups.Length == 0)
        {
            report.AppendLine(
                "_No duplicate HUD-like names found._");

            report.AppendLine();
            return;
        }

        foreach (IGrouping<string, RectTransform> group
                 in duplicateGroups)
        {
            report.AppendLine(
                "### `" +
                EscapeMarkdown(group.Key) +
                "`");

            foreach (RectTransform item in group)
            {
                report.AppendLine(
                    "- Active `" +
                    item.gameObject.activeInHierarchy +
                    "` — `" +
                    EscapeMarkdown(
                        GetHierarchyPath(item)) +
                    "`");
            }

            report.AppendLine();
        }
    }

    private static void WriteMissingScripts(
        GameObject[] sceneRoots,
        StringBuilder report)
    {
        List<string> findings =
            new List<string>();

        foreach (GameObject root in sceneRoots)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform transform in transforms)
            {
                int missingCount =
                    GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            transform.gameObject);

                if (missingCount <= 0)
                {
                    continue;
                }

                findings.Add(
                    "- `" +
                    EscapeMarkdown(
                        GetHierarchyPath(transform)) +
                    "` — missing scripts: **" +
                    missingCount +
                    "**");
            }
        }

        report.AppendLine("## Missing Script findings");
        report.AppendLine();

        if (findings.Count == 0)
        {
            report.AppendLine(
                "_No Missing Script components found._");
        }
        else
        {
            foreach (string finding in findings)
            {
                report.AppendLine(finding);
            }
        }

        report.AppendLine();
    }

    private static void WriteGeneratedReport(
        StringBuilder report)
    {
        string fullPath =
            ToAbsoluteProjectPath(ReportAssetPath);

        string directory =
            Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            fullPath,
            report.ToString(),
            new UTF8Encoding(false));
    }

    private void WriteMappingTemplateIfMissing(
        List<MappingEntry> entries)
    {
        string fullPath =
            ToAbsoluteProjectPath(MappingAssetPath);

        if (File.Exists(fullPath))
        {
            Debug.Log(
                "[2A-S03-03-T01] Mapping file already exists " +
                "and was not overwritten: " +
                MappingAssetPath);

            return;
        }

        string directory =
            Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder mapping =
            new StringBuilder(16_384);

        mapping.AppendLine(
            "# 2A-S03-03-T01 — HUD Mapping");

        mapping.AppendLine();
        mapping.AppendLine(
            "- Branch: `" +
            EscapeMarkdown(branchName) +
            "`");

        mapping.AppendLine(
            "- Start SHA: `" +
            EscapeMarkdown(
                string.IsNullOrWhiteSpace(commitSha)
                    ? "NOT_RECORDED"
                    : commitSha) +
            "`");

        mapping.AppendLine();
        mapping.AppendLine(
            "Allowed decisions: `PRODUCTION`, " +
            "`WORLD_SPACE_ALLOWED`, `LEGACY_ARCHIVE`, " +
            "`MAPPED_KEEP`, `DELETE_CANDIDATE`, " +
            "`MIGRATE_T02`, `BLOCKED`.");

        mapping.AppendLine();
        mapping.AppendLine(
            "| Scene | Object path | Kind | Final decision | " +
            "Reason/evidence | Replacement or owner |");

        mapping.AppendLine(
            "|---|---|---|---|---|---|");

        IEnumerable<MappingEntry> uniqueEntries =
            entries
                .GroupBy(entry =>
                    entry.ScenePath +
                    "\n" +
                    entry.ObjectPath)
                .Select(group => group.First())
                .OrderBy(entry => entry.ScenePath)
                .ThenBy(entry => entry.ObjectPath);

        foreach (MappingEntry entry in uniqueEntries)
        {
            mapping.AppendLine(
                "| `" +
                EscapeMarkdown(entry.ScenePath) +
                "` | `" +
                EscapeMarkdown(entry.ObjectPath) +
                "` | " +
                EscapeMarkdown(entry.Kind) +
                " | UNCLASSIFIED |  |  |");
        }

        mapping.AppendLine();
        mapping.AppendLine(
            "## Direct gameplay references");

        mapping.AppendLine();
        mapping.AppendLine(
            "| UI object/component | Referenced gameplay object | " +
            "Why it exists | Decision | Target task |");

        mapping.AppendLine(
            "|---|---|---|---|---|");

        File.WriteAllText(
            fullPath,
            mapping.ToString(),
            new UTF8Encoding(false));
    }

    private static bool IsRootCanvas(Canvas canvas)
    {
        Transform parent =
            canvas.transform.parent;

        while (parent != null)
        {
            if (parent.GetComponent<Canvas>() != null)
            {
                return false;
            }

            parent = parent.parent;
        }

        return true;
    }

    private static string GetInitialCanvasClassification(
        Canvas canvas)
    {
        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            return "WORLD_SPACE_ALLOWED_REVIEW";
        }

        if (!canvas.gameObject.activeInHierarchy)
        {
            return "INACTIVE_OR_LEGACY_REVIEW";
        }

        return "SCREEN_SPACE_PRODUCTION_REVIEW";
    }

    private static bool LooksLikeHudName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        return HudNameTokens.Any(
            token =>
                objectName.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool LooksLikeGameplayType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        return GameplayTypeTokens.Any(
            token =>
                typeName.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string GetCanvasScalerSummary(
        CanvasScaler scaler)
    {
        if (scaler == null)
        {
            return "MISSING";
        }

        return
            "Mode=" + scaler.uiScaleMode +
            "; Ref=" + scaler.referenceResolution +
            "; Match=" + scaler.matchWidthOrHeight;
    }

    private static string GetHierarchyPath(
        Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        Stack<string> names =
            new Stack<string>();

        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static string NormalizeObjectName(
        string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return "<empty>";
        }

        return objectName
            .Replace("(Clone)", "")
            .Replace("_", "")
            .Replace(" ", "")
            .Trim()
            .ToLowerInvariant();
    }

    private static string EscapeMarkdown(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value
                .Replace("|", "\\|")
                .Replace("`", "'");
    }

    private static string ToAbsoluteProjectPath(
        string assetPath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)
                .FullName;

        return Path.Combine(
            projectRoot,
            assetPath.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }
}

#endif