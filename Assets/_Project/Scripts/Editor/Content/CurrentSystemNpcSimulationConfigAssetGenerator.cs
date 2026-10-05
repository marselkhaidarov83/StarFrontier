#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CurrentSystemNpcSimulationConfigAssetGenerator
{
    private const string OutputFolder =
        "Assets/_Project/Content/Configs/Population/CurrentSystemNpcSimulation";

    private const string AssetPath =
        OutputFolder + "/current_system_npc_simulation_2a.asset";

    [MenuItem("STAR FRONTIER/Content/08b. Current System NPC Simulation/Create Current System NPC Simulation Config")]
    public static void CreateCurrentSystemNpcSimulationConfig()
    {
        EnsureFolder(OutputFolder);

        CurrentSystemNpcSimulationConfig asset =
            AssetDatabase.LoadAssetAtPath<CurrentSystemNpcSimulationConfig>(
                AssetPath);

        if (asset == null)
        {
            asset =
                ScriptableObject.CreateInstance<CurrentSystemNpcSimulationConfig>();

            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SerializedProperty maxInitialRouteBuildsPerTickProperty =
            serializedObject.FindProperty("maxInitialRouteBuildsPerTick");

        if (maxInitialRouteBuildsPerTickProperty != null)
            maxInitialRouteBuildsPerTickProperty.intValue = 2;

        SerializedProperty routeBuildTimeBudgetMsPerTickProperty =
            serializedObject.FindProperty("routeBuildTimeBudgetMsPerTick");

        if (routeBuildTimeBudgetMsPerTickProperty != null)
            routeBuildTimeBudgetMsPerTickProperty.floatValue = 2f;

        SerializedProperty routeRefreshTimeBudgetMsPerTickProperty =
            serializedObject.FindProperty("routeRefreshTimeBudgetMsPerTick");

        if (routeRefreshTimeBudgetMsPerTickProperty != null)
            routeRefreshTimeBudgetMsPerTickProperty.floatValue = 0.75f;

        SerializedProperty ensureDirectionRouteBuildBudgetSafetyMsProperty =
            serializedObject.FindProperty("ensureDirectionRouteBuildBudgetSafetyMs");

        if (ensureDirectionRouteBuildBudgetSafetyMsProperty != null)
            ensureDirectionRouteBuildBudgetSafetyMsProperty.floatValue = 0.05f;

        SerializedProperty routeTargetRerollAttemptsProperty =
            serializedObject.FindProperty("routeTargetRerollAttempts");

        if (routeTargetRerollAttemptsProperty != null)
            routeTargetRerollAttemptsProperty.intValue = 6;

        SerializedProperty routeTargetRerollMinDistanceProperty =
            serializedObject.FindProperty("routeTargetRerollMinDistance");

        if (routeTargetRerollMinDistanceProperty != null)
            routeTargetRerollMinDistanceProperty.floatValue = 1f;

        SerializedProperty maxSystemExitCompletionsPerTickProperty =
            serializedObject.FindProperty("maxSystemExitCompletionsPerTick");

        if (maxSystemExitCompletionsPerTickProperty != null)
            maxSystemExitCompletionsPerTickProperty.intValue = 2;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;

        Debug.Log(
            "Current System NPC Simulation Config generated: " +
            AssetPath);
    }

    [MenuItem("STAR FRONTIER/Content/08b. Current System NPC Simulation/Validate Current System NPC Simulation Config")]
    public static void ValidateCurrentSystemNpcSimulationConfig()
    {
        CurrentSystemNpcSimulationConfig asset =
            AssetDatabase.LoadAssetAtPath<CurrentSystemNpcSimulationConfig>(
                AssetPath);

        if (asset == null)
        {
            EditorUtility.DisplayDialog(
                "Current System NPC Simulation Config validation",
                "Asset was not found:\n" + AssetPath,
                "OK");

            return;
        }

        EditorUtility.DisplayDialog(
            "Current System NPC Simulation Config validation",
            "Checked: 1. Errors: 0.",
            "OK");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts =
            folder.Split('/');

        string current =
            parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next =
                current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
#endif