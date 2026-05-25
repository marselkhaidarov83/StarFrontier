#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

    public class ConfigValidatorWindow : EditorWindow
    {
        private Vector2 scrollPosition;

        private List<ValidationIssue> issues =
            new List<ValidationIssue>();

        private int errorCount;
        private int warningCount;

        [MenuItem("SpaceWorld/Tools/Config Validator")]
        public static void ShowWindow()
        {
            GetWindow<ConfigValidatorWindow>(
                "Config Validator");
        }

        private void OnGUI()
        {
            DrawHeader();

            DrawValidateButton();

            DrawSummary();

            DrawIssueList();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);

            GUILayout.Label(
                "CONFIG VALIDATOR",
                EditorStyles.boldLabel);

            GUILayout.Space(10);
        }

        private void DrawValidateButton()
        {
            if (GUILayout.Button(
                "Validate All",
                GUILayout.Height(40)))
            {
                RunValidation();
            }
        }

        private void DrawSummary()
        {
            GUILayout.Space(10);

            EditorGUILayout.LabelField(
                $"Errors: {errorCount}");

            EditorGUILayout.LabelField(
                $"Warnings: {warningCount}");

            GUILayout.Space(10);
        }

        private void DrawIssueList()
        {
            scrollPosition =
                EditorGUILayout.BeginScrollView(
                    scrollPosition);

            foreach (var issue in issues)
            {
                DrawIssue(issue);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIssue(ValidationIssue issue)
        {
            GUIStyle style =
                issue.Severity ==
                ValidationSeverity.Error
                ? GetErrorStyle()
                : GetWarningStyle();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                issue.SourceObject != null
                    ? issue.SourceObject.name
                    : "Unknown",
                style,
                GUILayout.Width(200)))
            {
                if (issue.SourceObject != null)
                {
                    Selection.activeObject =
                        issue.SourceObject;

                    EditorGUIUtility.PingObject(
                        issue.SourceObject);
                }
            }

            EditorGUILayout.LabelField(
                issue.Message);

            EditorGUILayout.EndHorizontal();
        }

        private void RunValidation()
        {
            issues.Clear();

            errorCount = 0;
            warningCount = 0;

            var allIssues = ValidationMenuRunner.RunAll();

            issues.AddRange(allIssues);

            foreach (var issue in issues)
            {
                if (issue.Severity ==
                    ValidationSeverity.Error)
                    errorCount++;

                if (issue.Severity ==
                    ValidationSeverity.Warning)
                    warningCount++;
            }
        }

        private GUIStyle GetErrorStyle()
        {
            var style = new GUIStyle(
                GUI.skin.button);

            style.normal.textColor =
                Color.red;

            return style;
        }

        private GUIStyle GetWarningStyle()
        {
            var style = new GUIStyle(
                GUI.skin.button);

            style.normal.textColor =
                new Color(1f, 0.6f, 0f);

            return style;
        }
    }
#endif