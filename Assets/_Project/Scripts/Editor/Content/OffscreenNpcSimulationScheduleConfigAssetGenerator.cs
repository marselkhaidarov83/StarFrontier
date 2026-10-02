#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class OffscreenNpcSimulationScheduleConfigAssetGenerator
{
    private const string OutputFolder =
        "Assets/_Project/Content/Configs/Population/OffscreenNpcSimulation";

    private const string AssetPath =
        OutputFolder + "/offscreen_npc_simulation_schedule_2a.asset";

    [MenuItem("STAR FRONTIER/Content/08a. Offscreen NPC Simulation/Create Offscreen NPC Simulation Schedule Config")]
    public static void CreateOffscreenNpcSimulationScheduleConfig()
    {
        EnsureFolder(OutputFolder);

        OffscreenNpcSimulationScheduleConfig asset =
            AssetDatabase.LoadAssetAtPath<OffscreenNpcSimulationScheduleConfig>(
                AssetPath);

        if (asset == null)
        {
            asset =
                ScriptableObject.CreateInstance<
                    OffscreenNpcSimulationScheduleConfig>();

            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        SerializedObject serializedObject =
            new SerializedObject(asset);

        WriteSchedule(serializedObject);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;

        Debug.Log(
            "Offscreen NPC Simulation Schedule generated: " +
            AssetPath);
    }

    [MenuItem("STAR FRONTIER/Content/08a. Offscreen NPC Simulation/Validate Offscreen NPC Simulation Schedule Config")]
    public static void ValidateOffscreenNpcSimulationScheduleConfig()
    {
        OffscreenNpcSimulationScheduleConfig asset =
            AssetDatabase.LoadAssetAtPath<OffscreenNpcSimulationScheduleConfig>(
                AssetPath);

        if (asset == null)
        {
            EditorUtility.DisplayDialog(
                "Offscreen NPC Simulation Schedule validation",
                "Asset was not found:\n" + AssetPath,
                "OK");

            return;
        }

        int errors = ValidateAsset(asset);

        EditorUtility.DisplayDialog(
            "Offscreen NPC Simulation Schedule validation",
            "Checked: 1. Errors: " + errors + ".",
            "OK");
    }

    private static void WriteSchedule(
        SerializedObject serializedObject)
    {
        SerializedProperty unreachableSystemPriorityProperty =
            serializedObject.FindProperty("unreachableSystemPriority");

        if (unreachableSystemPriorityProperty != null)
            unreachableSystemPriorityProperty.intValue = 99;

        SerializedProperty maxOffscreenAssignNewPerSystemTickProperty =
            serializedObject.FindProperty("maxOffscreenAssignNewPerSystemTick");

        if (maxOffscreenAssignNewPerSystemTickProperty != null)
            maxOffscreenAssignNewPerSystemTickProperty.intValue = 15;

        SerializedProperty maxOffscreenReassignPerSystemTickProperty =
            serializedObject.FindProperty("maxOffscreenReassignPerSystemTick");

        if (maxOffscreenReassignPerSystemTickProperty != null)
            maxOffscreenReassignPerSystemTickProperty.intValue = 15;

        SerializedProperty maxOffscreenActiveBehaviorPerSystemTickProperty =
            serializedObject.FindProperty("maxOffscreenActiveBehaviorPerSystemTick");

        if (maxOffscreenActiveBehaviorPerSystemTickProperty != null)
            maxOffscreenActiveBehaviorPerSystemTickProperty.intValue = 30;

        SerializedProperty maxOffscreenCompletedTravelsPerSystemTickProperty =
            serializedObject.FindProperty("maxOffscreenCompletedTravelsPerSystemTick");

        if (maxOffscreenCompletedTravelsPerSystemTickProperty != null)
            maxOffscreenCompletedTravelsPerSystemTickProperty.intValue = 25;

        SerializedProperty ticksProperty =
            serializedObject.FindProperty("ticks");

        if (ticksProperty == null)
        {
            Debug.LogError(
                "OffscreenNpcSimulationScheduleConfig.ticks field was not found.");

            return;
        }

        ScheduleTickData[] ticks =
        {
        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            2,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            3,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            2,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            4,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            2,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            3,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            5,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            2,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            99,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),

        new ScheduleTickData(
            1,
            OffscreenNpcSimulationSystemLimitMode.MaxCount,
            1),
    };

        ticksProperty.arraySize = ticks.Length;

        for (int i = 0; i < ticks.Length; i++)
        {
            SerializedProperty tickProperty =
                ticksProperty.GetArrayElementAtIndex(i);

            SerializedProperty priorityProperty =
                tickProperty.FindPropertyRelative("priority");

            SerializedProperty systemLimitModeProperty =
                tickProperty.FindPropertyRelative("systemLimitMode");

            SerializedProperty maxSystemsProperty =
                tickProperty.FindPropertyRelative("maxSystems");

            if (priorityProperty != null)
                priorityProperty.intValue = ticks[i].Priority;

            SetEnumValue(
                systemLimitModeProperty,
                ticks[i].SystemLimitMode);

            if (maxSystemsProperty != null)
                maxSystemsProperty.intValue = ticks[i].MaxSystems;
        }
    }

    private static void SetEnumValue(
        SerializedProperty property,
        OffscreenNpcSimulationSystemLimitMode value)
    {
        if (property == null)
            return;

        string enumName = value.ToString();

        for (int i = 0; i < property.enumNames.Length; i++)
        {
            if (property.enumNames[i] == enumName)
            {
                property.enumValueIndex = i;
                return;
            }
        }

        Debug.LogError(
            "OffscreenNpcSimulationSystemLimitMode value was not found: " +
            enumName);
    }

    private static int ValidateAsset(
        OffscreenNpcSimulationScheduleConfig asset)
    {
        int errors = 0;

        if (asset.UnreachableSystemPriority < 1)
        {
            Debug.LogError(
                "Offscreen NPC schedule has invalid unreachable priority.",
                asset);

            errors++;
        }

        if (asset.MaxOffscreenAssignNewPerSystemTick < 1)
        {
            Debug.LogError(
                "Offscreen NPC schedule has invalid AssignNew limit.",
                asset);

            errors++;
        }

        if (asset.MaxOffscreenReassignPerSystemTick < 1)
        {
            Debug.LogError(
                "Offscreen NPC schedule has invalid Reassign limit.",
                asset);

            errors++;
        }

        if (asset.MaxOffscreenActiveBehaviorPerSystemTick < 1)
        {
            Debug.LogError(
                "Offscreen NPC schedule has invalid ActiveBehavior limit.",
                asset);

            errors++;
        }

        if (asset.MaxOffscreenCompletedTravelsPerSystemTick < 1)
        {
            Debug.LogError(
                "Offscreen NPC schedule has invalid CompletedTravels limit.",
                asset);

            errors++;
        }

        OffscreenNpcSimulationScheduleTick[] ticks =
            asset.Ticks;

        if (ticks == null || ticks.Length == 0)
        {
            Debug.LogError(
                "Offscreen NPC schedule has no ticks.",
                asset);

            errors++;
            return errors;
        }

        for (int i = 0; i < ticks.Length; i++)
        {
            OffscreenNpcSimulationScheduleTick tick =
                ticks[i];

            if (tick == null)
            {
                Debug.LogError(
                    "Offscreen NPC schedule tick is null. Index: " + i,
                    asset);

                errors++;
                continue;
            }

            if (tick.Priority < 1)
            {
                Debug.LogError(
                    "Offscreen NPC schedule tick has invalid priority. Index: " +
                    i,
                    asset);

                errors++;
            }

            if (!tick.ProcessAll &&
                (tick.MaxSystems < 1 || tick.MaxSystems > 10))
            {
                Debug.LogError(
                    "Offscreen NPC schedule tick has invalid max systems. Index: " +
                    i,
                    asset);

                errors++;
            }
        }

        return errors;
    }

    private static void EnsureFolder(
        string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts =
            folderPath.Split('/');

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

    private readonly struct ScheduleTickData
    {
        public readonly int Priority;
        public readonly OffscreenNpcSimulationSystemLimitMode SystemLimitMode;
        public readonly int MaxSystems;

        public ScheduleTickData(
            int priority,
            OffscreenNpcSimulationSystemLimitMode systemLimitMode,
            int maxSystems)
        {
            Priority = Mathf.Max(1, priority);
            SystemLimitMode = systemLimitMode;
            MaxSystems = Mathf.Clamp(maxSystems, 1, 10);
        }
    }
}
#endif