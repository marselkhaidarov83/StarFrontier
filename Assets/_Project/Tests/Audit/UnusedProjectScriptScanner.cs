using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace StarFrontier.EditorTools
{
    /// <summary>
    /// Report-only audit tool. It never deletes or modifies project assets.
    ///
    /// Finds scripts that have:
    /// 1) no serialized references from project assets; and
    /// 2) no textual type-name references from other C# files.
    ///
    /// Results are candidates for manual review, not proof that deletion is safe.
    /// Reflection, string-based loading, generated code and external packages can hide usage.
    /// </summary>
    public static class UnusedProjectScriptScanner
    {
        private const string ReportDirectoryName = "UnusedAuditReports";
        private const int DependencyBatchSize = 250;

        private static readonly string[] SpecialEntryPointMarkers =
        {
            "RuntimeInitializeOnLoadMethod",
            "InitializeOnLoad",
            "InitializeOnLoadMethod",
            "MenuItem(",
            "CustomEditor(",
            "CustomPropertyDrawer(",
            "CreateAssetMenu(",
            "AssetPostprocessor",
            "IPreprocessBuildWithReport",
            "IPostprocessBuildWithReport",
            "DidReloadScripts"
        };

        [MenuItem("STAR FRONTIER/Audit/Scripts/Find unused script candidates")]
        public static void Run()
        {
            if (!CanRun())
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Unused Script Audit",
                    "Collecting C# scripts...",
                    0f);

                string[] scriptPaths = AssetDatabase
                    .FindAssets("t:MonoScript", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Dictionary<string, string> sourceByPath = LoadScriptSources(scriptPaths);
                HashSet<string> serializedDependencies = CollectSerializedDependencies();
                List<ScriptAuditRow> rows = new List<ScriptAuditRow>(scriptPaths.Length);

                for (int i = 0; i < scriptPaths.Length; i++)
                {
                    string scriptPath = scriptPaths[i];
                    EditorUtility.DisplayProgressBar(
                        "Unused Script Audit",
                        "Checking " + scriptPath,
                        0.35f + 0.6f * (i / (float)Math.Max(1, scriptPaths.Length)));

                    MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    Type type = monoScript != null ? monoScript.GetClass() : null;
                    bool serializedReference = serializedDependencies.Contains(scriptPath);
                    string source = sourceByPath.TryGetValue(scriptPath, out string text) ? text : string.Empty;

                    if (type == null)
                    {
                        rows.Add(new ScriptAuditRow
                        {
                            Status = "TYPE_NOT_RESOLVED",
                            ScriptPath = scriptPath,
                            Notes = "MonoScript.GetClass returned null. Check filename/class-name mismatch, compile errors, multiple classes or unsupported script type."
                        });
                        continue;
                    }

                    List<string> externalCodeReferences = FindExternalCodeReferences(
                        type.Name,
                        scriptPath,
                        sourceByPath);

                    bool editorOrTest = IsEditorOrTestScript(scriptPath, type);
                    bool specialEntryPoint = ContainsSpecialEntryPoint(source);

                    string status;
                    string notes;

                    if (editorOrTest)
                    {
                        status = "EXCLUDED_EDITOR_OR_TEST";
                        notes = "Editor/test scripts are not classified as unused by this audit.";
                    }
                    else if (!serializedReference && externalCodeReferences.Count == 0 && specialEntryPoint)
                    {
                        status = "REVIEW_SPECIAL_ENTRY_POINT";
                        notes = "No normal references found, but the source contains a Unity/editor entry-point attribute or interface.";
                    }
                    else if (!serializedReference && externalCodeReferences.Count == 0)
                    {
                        status = "CANDIDATE_HIGH";
                        notes = "No serialized asset reference and no type-name reference from another C# file. Manual review is mandatory.";
                    }
                    else if (serializedReference && externalCodeReferences.Count > 0)
                    {
                        status = "USED_SERIALIZED_AND_CODE";
                        notes = string.Empty;
                    }
                    else if (serializedReference)
                    {
                        status = "USED_SERIALIZED";
                        notes = string.Empty;
                    }
                    else
                    {
                        status = "USED_CODE_ONLY";
                        notes = "Not serialized in project assets, but referenced by C# source.";
                    }

                    rows.Add(new ScriptAuditRow
                    {
                        Status = status,
                        ScriptPath = scriptPath,
                        TypeName = type.FullName ?? type.Name,
                        BaseTypeName = type.BaseType != null ? type.BaseType.FullName : string.Empty,
                        SerializedReference = serializedReference,
                        ExternalCodeReferenceCount = externalCodeReferences.Count,
                        ExternalCodeReferenceFiles = string.Join(" | ", externalCodeReferences.Take(20)),
                        Notes = notes
                    });
                }

                string reportPath = WriteReport(rows);
                int highCandidates = rows.Count(row => row.Status == "CANDIDATE_HIGH");
                int unresolved = rows.Count(row => row.Status == "TYPE_NOT_RESOLVED");

                Debug.Log(
                    $"Unused script audit complete. High candidates: {highCandidates}; " +
                    $"unresolved types: {unresolved}; report: {reportPath}");

                EditorUtility.DisplayDialog(
                    "Unused Script Audit",
                    $"Audit complete.\n\n" +
                    $"High candidates: {highCandidates}\n" +
                    $"Unresolved script types: {unresolved}\n\n" +
                    $"Nothing was deleted. Open the CSV report and review every candidate manually.",
                    "Open report folder");

                EditorUtility.RevealInFinder(reportPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Unused Script Audit failed",
                    exception.Message,
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool CanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Unused Script Audit",
                    "Exit Play Mode before running the audit.",
                    "OK");
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(
                    "Unused Script Audit",
                    "Wait for script compilation to finish.",
                    "OK");
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> LoadScriptSources(IEnumerable<string> scriptPaths)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string scriptPath in scriptPaths)
            {
                try
                {
                    result[scriptPath] = File.ReadAllText(Path.GetFullPath(scriptPath));
                }
                catch (Exception exception)
                {
                    result[scriptPath] = string.Empty;
                    Debug.LogWarning($"Could not read {scriptPath}: {exception.Message}");
                }
            }

            return result;
        }

        private static HashSet<string> CollectSerializedDependencies()
        {
            string[] assetPaths = AssetDatabase
                .GetAllAssetPaths()
                .Where(IsDependencyRootAsset)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int start = 0; start < assetPaths.Length; start += DependencyBatchSize)
            {
                string[] batch = assetPaths
                    .Skip(start)
                    .Take(DependencyBatchSize)
                    .ToArray();

                EditorUtility.DisplayProgressBar(
                    "Unused Script Audit",
                    $"Scanning asset dependencies {Math.Min(start + batch.Length, assetPaths.Length)}/{assetPaths.Length}",
                    0.05f + 0.25f * (start / (float)Math.Max(1, assetPaths.Length)));

                string[] dependencies = AssetDatabase.GetDependencies(batch, true);
                foreach (string dependency in dependencies)
                {
                    result.Add(dependency);
                }
            }

            return result;
        }

        private static bool IsDependencyRootAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.IsValidFolder(path))
            {
                return false;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".cs":
                case ".asmdef":
                case ".asmref":
                case ".dll":
                case ".pdb":
                case ".mdb":
                case ".rsp":
                    return false;
                default:
                    return true;
            }
        }

        private static List<string> FindExternalCodeReferences(
            string typeName,
            string ownPath,
            IReadOnlyDictionary<string, string> sourceByPath)
        {
            string pattern = @"(?<![A-Za-z0-9_])" + Regex.Escape(typeName) + @"(?![A-Za-z0-9_])";
            Regex regex = new Regex(pattern, RegexOptions.CultureInvariant);
            List<string> references = new List<string>();

            foreach (KeyValuePair<string, string> pair in sourceByPath)
            {
                if (string.Equals(pair.Key, ownPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(pair.Value) && regex.IsMatch(pair.Value))
                {
                    references.Add(pair.Key);
                }
            }

            references.Sort(StringComparer.OrdinalIgnoreCase);
            return references;
        }

        private static bool IsEditorOrTestScript(string path, Type type)
        {
            string normalizedPath = path.Replace('\\', '/');

            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedPath.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedPath.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return typeof(UnityEditor.Editor).IsAssignableFrom(type) ||
                   typeof(EditorWindow).IsAssignableFrom(type) ||
                   typeof(AssetPostprocessor).IsAssignableFrom(type);
        }

        private static bool ContainsSpecialEntryPoint(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return SpecialEntryPointMarkers.Any(
                marker => source.IndexOf(marker, StringComparison.Ordinal) >= 0);
        }

        private static string WriteReport(IReadOnlyCollection<ScriptAuditRow> rows)
        {
            string reportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ReportDirectoryName);
            Directory.CreateDirectory(reportDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportPath = Path.Combine(reportDirectory, $"unused_scripts_{timestamp}.csv");

            StringBuilder csv = new StringBuilder();
            csv.AppendLine(
                "Status,ScriptPath,TypeName,BaseTypeName,SerializedReference," +
                "ExternalCodeReferenceCount,ExternalCodeReferenceFiles,Notes");

            foreach (ScriptAuditRow row in rows
                         .OrderBy(row => row.Status, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(row => row.ScriptPath, StringComparer.OrdinalIgnoreCase))
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(row.Status),
                    Csv(row.ScriptPath),
                    Csv(row.TypeName),
                    Csv(row.BaseTypeName),
                    Csv(row.SerializedReference.ToString()),
                    Csv(row.ExternalCodeReferenceCount.ToString()),
                    Csv(row.ExternalCodeReferenceFiles),
                    Csv(row.Notes)
                }));
            }

            File.WriteAllText(reportPath, csv.ToString(), new UTF8Encoding(true));
            return reportPath;
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class ScriptAuditRow
        {
            public string Status = string.Empty;
            public string ScriptPath = string.Empty;
            public string TypeName = string.Empty;
            public string BaseTypeName = string.Empty;
            public bool SerializedReference;
            public int ExternalCodeReferenceCount;
            public string ExternalCodeReferenceFiles = string.Empty;
            public string Notes = string.Empty;
        }
    }
}
