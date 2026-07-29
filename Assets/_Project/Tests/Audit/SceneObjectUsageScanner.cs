using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarFrontier.EditorTools
{
    /// <summary>
    /// Report-only scene audit tool. It never deletes or modifies scene objects.
    ///
    /// Reports:
    /// - GameObjects with missing MonoBehaviour scripts;
    /// - empty leaf GameObjects with no incoming serialized references;
    /// - empty container GameObjects for manual review;
    /// - inactive leaf objects with no incoming serialized references.
    ///
    /// An object can still be used through code, tags, names, reflection, animation,
    /// Timeline, Addressables or runtime activation. Treat every result as a candidate.
    /// </summary>
    public static class SceneObjectUsageScanner
    {
        private const string ReportDirectoryName = "UnusedAuditReports";

        [MenuItem("STAR FRONTIER/Audit/Scene objects/Scan current open scenes")]
        public static void ScanCurrentOpenScenes()
        {
            if (!CanRun())
            {
                return;
            }

            try
            {
                List<SceneObjectAuditRow> rows = new List<SceneObjectAuditRow>();
                int loadedSceneCount = 0;

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    loadedSceneCount++;
                    EditorUtility.DisplayProgressBar(
                        "Scene Object Audit",
                        "Scanning " + GetSceneDisplayName(scene),
                        i / (float)Math.Max(1, SceneManager.sceneCount));

                    AuditScene(scene, rows);
                }

                string reportPath = WriteReport(rows, "current_scenes");
                ShowFinishedDialog(rows, loadedSceneCount, reportPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Scene Object Audit failed", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("STAR FRONTIER/Audit/Scene objects/Scan all project scenes")]
        public static void ScanAllProjectScenes()
        {
            if (!CanRun())
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            string[] scenePaths = AssetDatabase
                .FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            List<SceneObjectAuditRow> rows = new List<SceneObjectAuditRow>();

            try
            {
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    string scenePath = scenePaths[i];
                    EditorUtility.DisplayProgressBar(
                        "Scene Object Audit",
                        $"Opening and scanning {scenePath} ({i + 1}/{scenePaths.Length})",
                        i / (float)Math.Max(1, scenePaths.Length));

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    AuditScene(scene, rows);
                }

                string reportPath = WriteReport(rows, "all_project_scenes");
                ShowFinishedDialog(rows, scenePaths.Length, reportPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Scene Object Audit failed", exception.Message, "OK");
            }
            finally
            {
                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                catch (Exception restoreException)
                {
                    Debug.LogError("Could not restore the original scene setup: " + restoreException);
                }

                EditorUtility.ClearProgressBar();
            }
        }

        private static bool CanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Scene Object Audit",
                    "Exit Play Mode before running the audit.",
                    "OK");
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(
                    "Scene Object Audit",
                    "Wait for script compilation to finish.",
                    "OK");
                return false;
            }

            return true;
        }

        private static void AuditScene(Scene scene, ICollection<SceneObjectAuditRow> rows)
        {
            GameObject[] allObjects = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToArray();

            Dictionary<GameObject, int> incomingSerializedReferences =
                BuildIncomingSerializedReferenceCounts(scene, allObjects);

            foreach (GameObject gameObject in allObjects)
            {
                Component[] components = gameObject.GetComponents<Component>();
                int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                int nonTransformComponentCount = components.Count(
                    component => component != null && !(component is Transform));
                int childCount = gameObject.transform.childCount;
                int incomingReferences = incomingSerializedReferences.TryGetValue(gameObject, out int count)
                    ? count
                    : 0;
                bool prefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObject);

                List<string> categories = new List<string>();

                if (missingScriptCount > 0)
                {
                    categories.Add("MISSING_SCRIPT");
                }

                if (nonTransformComponentCount == 0 && childCount == 0 && incomingReferences == 0)
                {
                    categories.Add(prefabInstance
                        ? "REVIEW_PREFAB_EMPTY_LEAF"
                        : "CANDIDATE_EMPTY_LEAF");
                }

                if (nonTransformComponentCount == 0 && childCount > 0 && incomingReferences == 0)
                {
                    categories.Add("REVIEW_EMPTY_CONTAINER");
                }

                if (!gameObject.activeInHierarchy &&
                    childCount == 0 &&
                    incomingReferences == 0 &&
                    nonTransformComponentCount > 0)
                {
                    categories.Add("REVIEW_INACTIVE_UNREFERENCED_LEAF");
                }

                if (categories.Count == 0)
                {
                    continue;
                }

                rows.Add(new SceneObjectAuditRow
                {
                    ScenePath = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path,
                    HierarchyPath = GetHierarchyPath(gameObject),
                    Categories = string.Join(" | ", categories),
                    ActiveSelf = gameObject.activeSelf,
                    ActiveInHierarchy = gameObject.activeInHierarchy,
                    ChildCount = childCount,
                    IncomingSerializedReferenceCount = incomingReferences,
                    MissingScriptCount = missingScriptCount,
                    ComponentTypes = string.Join(" | ", components.Select(
                        component => component == null
                            ? "<Missing Script>"
                            : component.GetType().FullName)),
                    PrefabAssetPath = prefabInstance
                        ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject)
                        : string.Empty,
                    Notes = BuildNotes(categories)
                });
            }
        }

        private static Dictionary<GameObject, int> BuildIncomingSerializedReferenceCounts(
            Scene scene,
            IEnumerable<GameObject> allObjects)
        {
            Dictionary<GameObject, int> counts = allObjects.ToDictionary(
                gameObject => gameObject,
                _ => 0);

            foreach (GameObject owner in allObjects)
            {
                Component[] components = owner.GetComponents<Component>();

                foreach (Component component in components)
                {
                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    try
                    {
                        SerializedObject serializedObject = new SerializedObject(component);
                        SerializedProperty property = serializedObject.GetIterator();

                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference)
                            {
                                continue;
                            }

                            UnityEngine.Object referencedObject = property.objectReferenceValue;
                            GameObject target = GetReferencedGameObject(referencedObject);

                            if (target == null ||
                                target == owner ||
                                target.scene != scene ||
                                !counts.ContainsKey(target))
                            {
                                continue;
                            }

                            counts[target]++;
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            $"Could not inspect serialized fields on {GetHierarchyPath(owner)} / " +
                            $"{component.GetType().Name}: {exception.Message}");
                    }
                }
            }

            return counts;
        }

        private static GameObject GetReferencedGameObject(UnityEngine.Object referencedObject)
        {
            if (referencedObject is GameObject gameObject)
            {
                return gameObject;
            }

            if (referencedObject is Component component)
            {
                return component.gameObject;
            }

            return null;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            List<string> names = new List<string>();
            Transform current = gameObject.transform;

            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string GetSceneDisplayName(Scene scene)
        {
            return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
        }

        private static string BuildNotes(IEnumerable<string> categories)
        {
            HashSet<string> set = new HashSet<string>(categories);
            List<string> notes = new List<string>();

            if (set.Contains("MISSING_SCRIPT"))
            {
                notes.Add("Broken MonoBehaviour reference. Fix or remove the missing component after identifying its former purpose.");
            }

            if (set.Contains("CANDIDATE_EMPTY_LEAF"))
            {
                notes.Add("Transform-only leaf object with no incoming serialized references.");
            }

            if (set.Contains("REVIEW_PREFAB_EMPTY_LEAF"))
            {
                notes.Add("Empty leaf belongs to a prefab instance. Review the prefab asset instead of deleting only the scene child.");
            }

            if (set.Contains("REVIEW_EMPTY_CONTAINER"))
            {
                notes.Add("May be an intentional hierarchy organizer, spawn root, camera target or runtime anchor.");
            }

            if (set.Contains("REVIEW_INACTIVE_UNREFERENCED_LEAF"))
            {
                notes.Add("May be enabled through code, animation, name, tag, Timeline or another runtime mechanism.");
            }

            return string.Join(" ", notes);
        }

        private static string WriteReport(
            IReadOnlyCollection<SceneObjectAuditRow> rows,
            string scopeName)
        {
            string reportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ReportDirectoryName);
            Directory.CreateDirectory(reportDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportPath = Path.Combine(
                reportDirectory,
                $"scene_objects_{scopeName}_{timestamp}.csv");

            StringBuilder csv = new StringBuilder();
            csv.AppendLine(
                "ScenePath,HierarchyPath,Categories,ActiveSelf,ActiveInHierarchy," +
                "ChildCount,IncomingSerializedReferenceCount,MissingScriptCount," +
                "ComponentTypes,PrefabAssetPath,Notes");

            foreach (SceneObjectAuditRow row in rows
                         .OrderBy(row => row.ScenePath, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(row => row.HierarchyPath, StringComparer.OrdinalIgnoreCase))
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(row.ScenePath),
                    Csv(row.HierarchyPath),
                    Csv(row.Categories),
                    Csv(row.ActiveSelf.ToString()),
                    Csv(row.ActiveInHierarchy.ToString()),
                    Csv(row.ChildCount.ToString()),
                    Csv(row.IncomingSerializedReferenceCount.ToString()),
                    Csv(row.MissingScriptCount.ToString()),
                    Csv(row.ComponentTypes),
                    Csv(row.PrefabAssetPath),
                    Csv(row.Notes)
                }));
            }

            File.WriteAllText(reportPath, csv.ToString(), new UTF8Encoding(true));
            return reportPath;
        }

        private static void ShowFinishedDialog(
            IReadOnlyCollection<SceneObjectAuditRow> rows,
            int sceneCount,
            string reportPath)
        {
            int missingScripts = rows.Count(row => row.MissingScriptCount > 0);
            int emptyLeaves = rows.Count(
                row => row.Categories.Contains("CANDIDATE_EMPTY_LEAF"));

            Debug.Log(
                $"Scene object audit complete. Scenes: {sceneCount}; report rows: {rows.Count}; " +
                $"objects with missing scripts: {missingScripts}; empty-leaf candidates: {emptyLeaves}; " +
                $"report: {reportPath}");

            EditorUtility.DisplayDialog(
                "Scene Object Audit",
                $"Audit complete.\n\n" +
                $"Scenes scanned: {sceneCount}\n" +
                $"Report rows: {rows.Count}\n" +
                $"Objects with missing scripts: {missingScripts}\n" +
                $"Empty-leaf candidates: {emptyLeaves}\n\n" +
                $"Nothing was deleted or modified.",
                "Open report folder");

            EditorUtility.RevealInFinder(reportPath);
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class SceneObjectAuditRow
        {
            public string ScenePath = string.Empty;
            public string HierarchyPath = string.Empty;
            public string Categories = string.Empty;
            public bool ActiveSelf;
            public bool ActiveInHierarchy;
            public int ChildCount;
            public int IncomingSerializedReferenceCount;
            public int MissingScriptCount;
            public string ComponentTypes = string.Empty;
            public string PrefabAssetPath = string.Empty;
            public string Notes = string.Empty;
        }
    }
}
