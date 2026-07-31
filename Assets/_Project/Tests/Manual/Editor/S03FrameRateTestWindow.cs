using UnityEditor;
using UnityEngine;

public sealed class S03FrameRateTestWindow :
    EditorWindow
{
    private int _previousVSyncCount;
    private int _previousTargetFrameRate;
    private bool _settingsCaptured;

    [MenuItem(
        "STAR FRONTIER/QA/S03-01 Frame Rate Test")]
    public static void OpenWindow()
    {
        GetWindow<S03FrameRateTestWindow>(
            "S03 FPS Test");
    }

    private void OnEnable()
    {
        CaptureSettings();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2A-S03-01 FPS test",
            EditorStyles.boldLabel);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Current target FPS:",
            Application.targetFrameRate.ToString());

        EditorGUILayout.Space();

        if (GUILayout.Button("Set 30 FPS"))
        {
            SetFrameRate(30);
        }

        if (GUILayout.Button("Set 60 FPS"))
        {
            SetFrameRate(60);
        }

        if (GUILayout.Button("Set 120 FPS"))
        {
            SetFrameRate(120);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Restore previous settings"))
        {
            RestoreSettings();
        }

        EditorGUILayout.HelpBox(
            "Open Play Mode, select 30/60/120 FPS " +
            "and check keyboard movement, bounds, " +
            "camera and cancellation.",
            MessageType.Info);
    }

    private void CaptureSettings()
    {
        if (_settingsCaptured)
            return;

        _previousVSyncCount =
            QualitySettings.vSyncCount;

        _previousTargetFrameRate =
            Application.targetFrameRate;

        _settingsCaptured = true;
    }

    private static void SetFrameRate(
        int targetFrameRate)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate =
            targetFrameRate;

        Debug.Log(
            "[S03FrameRateTest] Target FPS = " +
            targetFrameRate);
    }

    private void RestoreSettings()
    {
        if (!_settingsCaptured)
            return;

        QualitySettings.vSyncCount =
            _previousVSyncCount;

        Application.targetFrameRate =
            _previousTargetFrameRate;

        Debug.Log(
            "[S03FrameRateTest] Settings restored.");
    }

    private void OnDisable()
    {
        RestoreSettings();
    }
}