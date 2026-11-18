using System;
using UnityEditor;
using UnityEngine;

public static class InhabitedPlanetMissionPoolBinder
{
    private const string PlanetRoot = "Assets/_Project/Content/Configs/Galaxy/Planets";
    private const string MissionRoot = "Assets/_Project/Content/Configs/Mission/MissionPool";
    private const string DebugConfigPath =
        "Assets/_Project/Content/Configs/Game/debugLogConfig01.asset";

    [MenuItem("STAR FRONTIER/Content/12. Missions/Bind mission configs by planet type")]
    public static void BindMissionConfigs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ReportFailure("Stop Play Mode before binding planet assets.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:PlanetTypeConfig", new[] { PlanetRoot });
        Array.Sort(guids, StringComparer.Ordinal);
        int updated = 0;
        int unchanged = 0;
        int invalid = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var planet = AssetDatabase.LoadAssetAtPath<PlanetTypeConfig>(path);
            if (planet == null)
            {
                invalid++;
                Log("Skipped missing PlanetTypeConfig | Path=" + path);
                continue;
            }
            if (!planet.IsInhabited)
                continue;

            string typeName = planet.PlanetType.ToString().ToLowerInvariant();
            string poolPath = $"{MissionRoot}/mission_pool_planet_{typeName}_01.asset";
            var pool = AssetDatabase.LoadAssetAtPath<MissionPoolConfig>(poolPath);
            if (!HasUsableEntry(pool))
            {
                invalid++;
                Log("Skipped mission pool without usable entries | Path=" + poolPath);
                continue;
            }

            var serialized = new SerializedObject(planet);
            SerializedProperty property = serialized.FindProperty("planetMissionConfig");
            if (property == null)
            {
                invalid++;
                Log("Skipped missing planetMissionConfig field | Planet=" + planet.Id);
                continue;
            }
            if (property.objectReferenceValue == pool)
            {
                unchanged++;
                continue;
            }

            property.objectReferenceValue = pool;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(planet);
            updated++;
            Log("Assigned | Planet=" + planet.Id + " | Config=" + pool.Id);
        }

        if (updated > 0)
            AssetDatabase.SaveAssets();

        Log("Finished | Updated=" + updated + " | AlreadyAssigned=" + unchanged +
            " | Invalid=" + invalid);
        EditorUtility.DisplayDialog("Mission config binder",
            "Updated: " + updated + "\nAlready assigned: " + unchanged + "\nInvalid: " + invalid,
            "OK");
    }

    private static bool HasUsableEntry(MissionPoolConfig pool)
    {
        if (pool?.PossibleMissionOffers == null)
            return false;

        foreach (var entry in pool.PossibleMissionOffers)
        {
            var template = entry != null ? entry.MissionTemplateConfig : null;
            if (entry != null && entry.IsEnabled && entry.Weight > 0 && template != null &&
                template.SourcePlanet == null && MissionLifecycleRules.IsPlayableType(template.MissionType))
                return true;
        }

        return false;
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
