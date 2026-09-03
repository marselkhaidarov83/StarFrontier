#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NpcBehaviourTransitionMatrixAssetGenerator
{
    private const string OutputFolder =
        "Assets/_Project/Content/Configs/Population/NpcBehaviorScenarios";

    private const string AssetPath =
        OutputFolder + "/npc_behaviour_transition_matrix_2a.asset";

    [MenuItem("STAR FRONTIER/Content/03a. NPC/Create NPC Behaviour Transition Matrix")]
    public static void CreateNpcBehaviourTransitionMatrix()
    {
        EnsureFolder(OutputFolder);

        NpcBehaviourTransitionMatrixConfig asset =
            AssetDatabase.LoadAssetAtPath<NpcBehaviourTransitionMatrixConfig>(
                AssetPath);

        if (asset == null)
        {
            asset =
                ScriptableObject.CreateInstance<NpcBehaviourTransitionMatrixConfig>();

            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        SerializedObject serializedObject = new SerializedObject(asset);

        WriteBaseConfigFields(serializedObject);
        WriteTransitionRules(serializedObject);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;

        Debug.Log(
            "NPC Behaviour Transition Matrix generated: " +
            AssetPath);
    }

    private static void WriteBaseConfigFields(
        SerializedObject serializedObject)
    {
        SerializedProperty idProperty =
            serializedObject.FindProperty("id");

        if (idProperty != null)
            idProperty.stringValue = "npc_behaviour_transition_matrix_2a";

        SerializedProperty displayNameProperty =
            serializedObject.FindProperty("displayName");

        if (displayNameProperty != null)
            displayNameProperty.stringValue = "NPC Behaviour Transition Matrix 2A";

        SerializedProperty descriptionProperty =
            serializedObject.FindProperty("description");

        if (descriptionProperty != null)
        {
            descriptionProperty.stringValue =
                "Allowed next NPC behavior matrix for Stage 2A.";
        }
    }

    private static void WriteTransitionRules(
    SerializedObject serializedObject)
    {
        SerializedProperty rulesProperty =
            serializedObject.FindProperty("rules");

        if (rulesProperty == null)
        {
            Debug.LogError(
                "NpcBehaviourTransitionMatrixConfig.rules field was not found.");

            return;
        }

        List<RuleData> rules = BuildRules();

        rulesProperty.arraySize = rules.Count;

        for (int i = 0; i < rules.Count; i++)
        {
            SerializedProperty ruleProperty =
                rulesProperty.GetArrayElementAtIndex(i);

            SerializedProperty previousBehaviorProperty =
                ruleProperty.FindPropertyRelative("previousBehavior");

            SerializedProperty allowedNextBehaviorsProperty =
                ruleProperty.FindPropertyRelative("allowedNextBehaviors");

            SetEnumValue(
                previousBehaviorProperty,
                rules[i].PreviousBehavior);

            allowedNextBehaviorsProperty.arraySize =
                rules[i].AllowedNextBehaviors.Length;

            for (int j = 0; j < rules[i].AllowedNextBehaviors.Length; j++)
            {
                SerializedProperty itemProperty =
                    allowedNextBehaviorsProperty.GetArrayElementAtIndex(j);

                SetEnumValue(
                    itemProperty,
                    rules[i].AllowedNextBehaviors[j]);
            }
        }
    }

    private static void SetEnumValue(
        SerializedProperty property,
        SystemNpcBehaviorType value)
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
            "SystemNpcBehaviorType value was not found in SerializedProperty enum names: " +
            enumName);
    }

    private static List<RuleData> BuildRules()
    {
        SystemNpcBehaviorType[] engageEnemiesNextBehaviors =
        {
            SystemNpcBehaviorType.EngageEnemies,
            SystemNpcBehaviorType.PatrolSystem,
            SystemNpcBehaviorType.PlanetToPlanetTravel,
            SystemNpcBehaviorType.TravelToAnotherSystem,
            SystemNpcBehaviorType.CollectDebris,
            SystemNpcBehaviorType.AttackMeteorite,
            SystemNpcBehaviorType.AttackMilitaryStation,
            SystemNpcBehaviorType.AttackRangerBaseStation,
            SystemNpcBehaviorType.AttackTradeStation,
            SystemNpcBehaviorType.AttackScienceStation,
            SystemNpcBehaviorType.AttackMedicalStation,
            SystemNpcBehaviorType.AttackMilitaryAlly,
            SystemNpcBehaviorType.AttackRangerAlly,
            SystemNpcBehaviorType.AttackTraderAlly,
            SystemNpcBehaviorType.AttackScienceAlly,
            SystemNpcBehaviorType.AttackMedicAlly
        };

        SystemNpcBehaviorType[] mobileNextBehaviors =
        {
            SystemNpcBehaviorType.EngageEnemies,
            SystemNpcBehaviorType.PatrolSystem,
            SystemNpcBehaviorType.PlanetToPlanetTravel,
            SystemNpcBehaviorType.TravelToAnotherSystem,
            SystemNpcBehaviorType.CollectDebris,
            SystemNpcBehaviorType.AttackMeteorite
        };

        SystemNpcBehaviorType[] attackTargetNextBehaviors =
        {
            SystemNpcBehaviorType.EngageEnemies,
            SystemNpcBehaviorType.AttackMilitaryStation,
            SystemNpcBehaviorType.AttackRangerBaseStation,
            SystemNpcBehaviorType.AttackTradeStation,
            SystemNpcBehaviorType.AttackScienceStation,
            SystemNpcBehaviorType.AttackMedicalStation,
            SystemNpcBehaviorType.AttackMilitaryAlly,
            SystemNpcBehaviorType.AttackRangerAlly,
            SystemNpcBehaviorType.AttackTraderAlly,
            SystemNpcBehaviorType.AttackScienceAlly,
            SystemNpcBehaviorType.AttackMedicAlly,
            SystemNpcBehaviorType.PatrolSystem,
            SystemNpcBehaviorType.PlanetToPlanetTravel
        };

        return new List<RuleData>
        {
            new RuleData(
                SystemNpcBehaviorType.EngageEnemies,
                engageEnemiesNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.PatrolSystem,
                mobileNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.PlanetToPlanetTravel,
                new[]
                {
                    SystemNpcBehaviorType.StayOnPlanetForDays
                }),

            new RuleData(
                SystemNpcBehaviorType.TravelToAnotherSystem,
                mobileNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.StayOnPlanetForDays,
                new[]
                {
                    SystemNpcBehaviorType.EngageEnemies,
                    SystemNpcBehaviorType.PatrolSystem,
                    SystemNpcBehaviorType.PlanetToPlanetTravel,
                    SystemNpcBehaviorType.TravelToAnotherSystem,
                    SystemNpcBehaviorType.CollectDebris,
                    SystemNpcBehaviorType.AttackMeteorite,
                    SystemNpcBehaviorType.AnnihilateOnPlanet,
                    SystemNpcBehaviorType.StayOnPlanetForDays
                }),

            new RuleData(
                SystemNpcBehaviorType.AnnihilateOnPlanet,
                new SystemNpcBehaviorType[0]),

            new RuleData(
                SystemNpcBehaviorType.CollectDebris,
                mobileNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackMeteorite,
                mobileNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackMilitaryStation,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackRangerBaseStation,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackTradeStation,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackScienceStation,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackMedicalStation,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackMilitaryAlly,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackRangerAlly,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackTraderAlly,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackScienceAlly,
                attackTargetNextBehaviors),

            new RuleData(
                SystemNpcBehaviorType.AttackMedicAlly,
                attackTargetNextBehaviors)
        };
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private readonly struct RuleData
    {
        public readonly SystemNpcBehaviorType PreviousBehavior;
        public readonly SystemNpcBehaviorType[] AllowedNextBehaviors;

        public RuleData(
            SystemNpcBehaviorType previousBehavior,
            SystemNpcBehaviorType[] allowedNextBehaviors)
        {
            PreviousBehavior = previousBehavior;
            AllowedNextBehaviors = allowedNextBehaviors;
        }
    }
}
#endif