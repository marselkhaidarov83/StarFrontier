using System;
using UnityEditor;
using UnityEngine;

public static class InhabitedPlanetMissionConfigBinder
{
    private const string PlanetRoot = "Assets/_Project/Content/Configs/Galaxy/Planets";
    private const string SharedConfigPath =
        "Assets/_Project/Content/Configs/PlanetMission/planetMissionConfig_shared_01.asset";
    private const string DebugConfigPath =
        "Assets/_Project/Content/Configs/Game/debugLogConfig01.asset";

    [MenuItem("STAR FRONTIER/Content/12. Missions/Bind shared config to inhabited planets")]
    public static void BindSharedConfig()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ReportFailure("Stop Play Mode before binding planet assets.");
            return;
        }

        var shared = AssetDatabase.LoadAssetAtPath<PlanetMissionConfig>(SharedConfigPath);
        if (shared == null)
        {
            ReportFailure("Shared PlanetMissionConfig asset was not found: " + SharedConfigPath);
            return;
        }
        if (shared.PossibleMissionOffers == null)
        {
            ReportFailure("Shared PlanetMissionConfig has no mission entries: " + SharedConfigPath);
            return;
        }

        bool hasUsableEntry = false;
        foreach (var entry in shared.PossibleMissionOffers)
        {
            var template = entry != null ? entry.MissionTemplateConfig : null;
            if (entry != null && entry.IsEnabled && entry.Weight > 0 && template != null &&
                template.SourcePlanet == null && MissionLifecycleRules.IsPlayableType(template.MissionType))
            {
                hasUsableEntry = true;
                break;
            }
        }
        if (!hasUsableEntry)
        {
            ReportFailure("Shared mission config has no enabled planet-independent playable template.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:PlanetConfig", new[] { PlanetRoot });
        Array.Sort(guids, StringComparer.Ordinal);
        int inhabited = 0;
        int updated = 0;
        int unchanged = 0;
        int skipped = 0;
        int invalid = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var planet = AssetDatabase.LoadAssetAtPath<PlanetConfig>(path);
            if (planet == null)
            {
                invalid++;
                Log("Skipped missing PlanetConfig | Path=" + path);
                continue;
            }
            if (!planet.IsInhabited)
            {
                skipped++;
                continue;
            }

            inhabited++;
            var serialized = new SerializedObject(planet);
            SerializedProperty property = serialized.FindProperty("planetMissionConfig");
            if (property == null)
            {
                invalid++;
                Log("Skipped missing planetMissionConfig field | Planet=" + planet.Id);
                continue;
            }
            if (property.objectReferenceValue == shared)
            {
                unchanged++;
                continue;
            }

            property.objectReferenceValue = shared;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(planet);
            updated++;
            Log("Assigned | Planet=" + planet.Id + " | Config=" + shared.Id);
        }

        if (updated > 0)
            AssetDatabase.SaveAssets();

        Log("Finished | Inhabited=" + inhabited + " | Updated=" + updated +
            " | AlreadyAssigned=" + unchanged + " | UninhabitedSkipped=" + skipped +
            " | Invalid=" + invalid + " | Config=" + shared.Id);
        EditorUtility.DisplayDialog("Mission config binder",
            "Inhabited: " + inhabited + "\nUpdated: " + updated +
            "\nAlready assigned: " + unchanged + "\nInvalid: " + invalid,
            "OK");
    }

    private static void ReportFailure(string message)
    {
        Log("Failed | " + message);
        EditorUtility.DisplayDialog("Mission config binder", message, "OK");
    }

    private static void Log(string message)
    {
        var debugConfig = AssetDatabase.LoadAssetAtPath<DebugLogConfig>(DebugConfigPath);
        if (debugConfig != null && debugConfig.IsEnabled(DebugLogChannel.Mission))
            Debug.Log("[DebugLog][Mission] [MissionBinding] " + message);
    }
}
