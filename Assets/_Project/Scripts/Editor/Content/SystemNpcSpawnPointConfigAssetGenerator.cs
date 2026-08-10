using System;
using UnityEditor;
using UnityEngine;

public static class SystemNpcSpawnPointConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/SystemNpcSpawnPoints";

    [MenuItem("STAR FRONTIER/Content/10. System NPC Spawn Points/Create default SystemNpcSpawnPointConfig asset")]
    public static void CreateDefaultSystemNpcSpawnPointConfig()
    {
        EnsureFolder(OutputRoot);

        string id =
            "system_npc_spawn_points_default_enemy_edge_01";

        string path =
            $"{OutputRoot}/{id}.asset";

        SystemNpcSpawnPointConfig asset =
            AssetDatabase.LoadAssetAtPath<SystemNpcSpawnPointConfig>(path);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SystemNpcSpawnPointConfig>();
            AssetDatabase.CreateAsset(asset, path);
        }

        SerializedObject serializedObject =
            new SerializedObject(asset);

        SetString(serializedObject, "id", id);
        SetString(serializedObject, "displayName", "Точки спавна NPC: враги у края системы");
        SetString(serializedObject, "description", "Дефолтные точки появления вражеских групп вне центра системы.");

        SerializedProperty pointsProperty =
            serializedObject.FindProperty("enemySpawnPoints");

        if (pointsProperty == null || !pointsProperty.isArray)
            throw new InvalidOperationException("SystemNpcSpawnPointConfig.enemySpawnPoints field was not found.");

        pointsProperty.arraySize = 4;
        pointsProperty.GetArrayElementAtIndex(0).vector3Value = new Vector3(6f, 0f, 0f);
        pointsProperty.GetArrayElementAtIndex(1).vector3Value = new Vector3(-6f, 0f, 0f);
        pointsProperty.GetArrayElementAtIndex(2).vector3Value = new Vector3(0f, 6f, 0f);
        pointsProperty.GetArrayElementAtIndex(3).vector3Value = new Vector3(0f, -6f, 0f);

        SerializedProperty radiusProperty =
            serializedObject.FindProperty("enemyRandomRadius");

        if (radiusProperty != null)
            radiusProperty.floatValue = 200f;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemNpcSpawnPointConfig generator",
            $"Created or updated:\n{path}",
            "OK");
    }

    private static void EnsureFolder(
        string folder)
    {
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

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }
}