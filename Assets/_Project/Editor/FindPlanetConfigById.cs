using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FindDuplicatePlanetConfigById
{
    private const string TargetId = "planet_siege_point_dead01_01";

    [MenuItem("STAR FRONTIER/Diagnostics/FindDuplicatePlanetConfigById")]
    public static void FindDuplicates()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlanetConfig");

        var matches = new List<UnityEngine.Object>();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            UnityEngine.Object asset =
                AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (asset == null)
            {
                continue;
            }

            string configId = ReadId(asset);

            if (!string.Equals(
                    configId,
                    TargetId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            matches.Add(asset);

            Debug.Log(
                $"[MATCH] PlanetConfig с ID '{TargetId}'\n" +
                $"Имя объекта: {asset.name}\n" +
                $"Путь: {assetPath}\n" +
                $"GUID: {guid}",
                asset);
        }

        if (matches.Count == 0)
        {
            Debug.LogWarning(
                $"PlanetConfig с ID '{TargetId}' не найден.\n" +
                $"Всего проверено конфигов: {guids.Length}");

            return;
        }

        Selection.objects = matches.ToArray();
        EditorGUIUtility.PingObject(matches[0]);

        if (matches.Count == 1)
        {
            Debug.Log(
                $"Дубликаты не найдены.\n" +
                $"Обнаружен один PlanetConfig с ID '{TargetId}'.",
                matches[0]);

            return;
        }

        Debug.LogError(
            $"ОБНАРУЖЕНЫ ДУБЛИКАТЫ PLANET CONFIG.\n" +
            $"ID: '{TargetId}'\n" +
            $"Количество объектов с одинаковым ID: {matches.Count}\n" +
            $"Все найденные объекты выделены в окне Project.");
    }

    private static string ReadId(UnityEngine.Object asset)
    {
        var serializedObject = new SerializedObject(asset);

        string[] possiblePropertyNames =
        {
            "id",
            "Id",
            "_id",
            "m_Id",
            "configId",
            "ConfigId",
            "_configId"
        };

        foreach (string propertyName in possiblePropertyNames)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);

            if (property != null &&
                property.propertyType ==
                SerializedPropertyType.String)
            {
                return property.stringValue;
            }
        }

        SerializedProperty iterator =
            serializedObject.GetIterator();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyType !=
                SerializedPropertyType.String)
            {
                continue;
            }

            string normalizedName = iterator.name
                .Replace("_", string.Empty)
                .ToLowerInvariant();

            if (normalizedName == "id" ||
                normalizedName == "configid")
            {
                return iterator.stringValue;
            }
        }

        return null;
    }
}