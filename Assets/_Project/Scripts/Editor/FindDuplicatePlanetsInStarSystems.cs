using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FindDuplicatePlanetsInStarSystems
{
    private const string MenuPath =
        "STAR FRONTIER/Diagnostics/Find Duplicate Planets In Systems";

    [MenuItem(MenuPath)]
    public static void RunDiagnostics()
    {
        string[] systemGuids =
            AssetDatabase.FindAssets("t:StarSystemConfig");

        var allReferences = new List<PlanetReference>();

        foreach (string systemGuid in systemGuids)
        {
            string systemPath =
                AssetDatabase.GUIDToAssetPath(systemGuid);

            UnityEngine.Object systemAsset =
                AssetDatabase.LoadMainAssetAtPath(systemPath);

            if (systemAsset == null)
            {
                continue;
            }

            CollectPlanetReferences(
                systemAsset,
                systemPath,
                allReferences);
        }

        int duplicateGroups = 0;

        duplicateGroups += ReportDuplicatesInsideOneSystem(
            allReferences);

        duplicateGroups += ReportSamePlanetUsedInDifferentSystems(
            allReferences);

        duplicateGroups += ReportDifferentPlanetAssetsWithSameId(
            allReferences);

        int uniquePlanets = allReferences
            .Select(reference => reference.Planet)
            .Distinct()
            .Count();

        if (duplicateGroups == 0)
        {
            Debug.Log(
                "[STAR FRONTIER — PLANET DIAGNOSTICS]\n" +
                "Дублирующиеся планеты не найдены.\n" +
                $"Проверено конфигов систем: {systemGuids.Length}\n" +
                $"Найдено ссылок на планеты: {allReferences.Count}\n" +
                $"Уникальных PlanetConfig: {uniquePlanets}");

            return;
        }

        Debug.LogError(
            "[STAR FRONTIER — PLANET DIAGNOSTICS]\n" +
            "Обнаружены дублирующиеся планеты.\n" +
            $"Групп проблем: {duplicateGroups}\n" +
            $"Проверено конфигов систем: {systemGuids.Length}\n" +
            $"Найдено ссылок на планеты: {allReferences.Count}\n" +
            $"Уникальных PlanetConfig: {uniquePlanets}\n\n" +
            "Подробности находятся выше в Console.");
    }

    private static void CollectPlanetReferences(
        UnityEngine.Object systemAsset,
        string systemPath,
        List<PlanetReference> results)
    {
        var serializedSystem =
            new SerializedObject(systemAsset);

        SerializedProperty iterator =
            serializedSystem.GetIterator();

        while (iterator.Next(true))
        {
            if (iterator.propertyType !=
                SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            UnityEngine.Object referencedAsset =
                iterator.objectReferenceValue;

            if (referencedAsset == null)
            {
                continue;
            }

            if (!IsPlanetConfig(referencedAsset))
            {
                continue;
            }

            string planetPath =
                AssetDatabase.GetAssetPath(referencedAsset);

            results.Add(
                new PlanetReference
                {
                    System = systemAsset,
                    SystemPath = systemPath,
                    PropertyPath = iterator.propertyPath,
                    Planet = referencedAsset,
                    PlanetPath = planetPath,
                    PlanetId = ReadId(referencedAsset)
                });
        }
    }

    private static int ReportDuplicatesInsideOneSystem(
        List<PlanetReference> references)
    {
        int problemCount = 0;

        var groups = references
            .GroupBy(reference => new
            {
                reference.SystemPath,
                PlanetInstanceId =
                    reference.Planet.GetInstanceID()
            })
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.SystemPath);

        foreach (var group in groups)
        {
            problemCount++;

            PlanetReference first = group.First();

            string propertyPaths = string.Join(
                "\n",
                group.Select(reference =>
                    $"• {reference.PropertyPath}"));

            Debug.LogError(
                "[DUPLICATE PLANET INSIDE SYSTEM]\n" +
                $"Система: {first.System.name}\n" +
                $"Путь системы: {first.SystemPath}\n" +
                $"Планета: {first.Planet.name}\n" +
                $"Planet ID: {FormatId(first.PlanetId)}\n" +
                $"Путь планеты: {first.PlanetPath}\n" +
                $"Количество повторений: {group.Count()}\n" +
                "Поля в StarSystemConfig:\n" +
                propertyPaths,
                first.System);
        }

        return problemCount;
    }

    private static int ReportSamePlanetUsedInDifferentSystems(
        List<PlanetReference> references)
    {
        int problemCount = 0;

        var groups = references
            .GroupBy(reference => reference.Planet)
            .Where(group =>
                group.Select(reference => reference.SystemPath)
                    .Distinct()
                    .Count() > 1)
            .OrderBy(group => group.Key.name);

        foreach (var group in groups)
        {
            problemCount++;

            PlanetReference first = group.First();

            string systems = string.Join(
                "\n",
                group
                    .GroupBy(reference =>
                        reference.SystemPath)
                    .Select(systemGroup =>
                    {
                        PlanetReference location =
                            systemGroup.First();

                        return
                            $"• {location.System.name}\n" +
                            $"  {location.SystemPath}";
                    }));

            Debug.LogError(
                "[SAME PLANET USED IN MULTIPLE SYSTEMS]\n" +
                $"Планета: {first.Planet.name}\n" +
                $"Planet ID: {FormatId(first.PlanetId)}\n" +
                $"Путь планеты: {first.PlanetPath}\n" +
                "Используется в системах:\n" +
                systems,
                first.Planet);
        }

        return problemCount;
    }

    private static int ReportDifferentPlanetAssetsWithSameId(
        List<PlanetReference> references)
    {
        int problemCount = 0;

        var groups = references
            .Where(reference =>
                !string.IsNullOrWhiteSpace(
                    reference.PlanetId))
            .GroupBy(reference =>
                reference.PlanetId)
            .Select(group => new
            {
                PlanetId = group.Key,
                Assets = group
                    .GroupBy(reference =>
                        reference.PlanetPath)
                    .Select(assetGroup =>
                        assetGroup.First())
                    .ToList()
            })
            .Where(group => group.Assets.Count > 1)
            .OrderBy(group => group.PlanetId);

        foreach (var group in groups)
        {
            problemCount++;

            string assets = string.Join(
                "\n",
                group.Assets.Select(reference =>
                    $"• {reference.Planet.name}\n" +
                    $"  {reference.PlanetPath}\n" +
                    $"  Используется в системе: " +
                    $"{reference.System.name}\n" +
                    $"  {reference.SystemPath}"));

            Debug.LogError(
                "[DIFFERENT PLANET ASSETS WITH SAME ID]\n" +
                $"Повторяющийся Planet ID: {group.PlanetId}\n" +
                $"Количество разных PlanetConfig: " +
                $"{group.Assets.Count}\n" +
                "Объекты:\n" +
                assets,
                group.Assets[0].Planet);
        }

        return problemCount;
    }

    private static bool IsPlanetConfig(
        UnityEngine.Object asset)
    {
        Type currentType = asset.GetType();

        while (currentType != null)
        {
            if (string.Equals(
                    currentType.Name,
                    "PlanetConfig",
                    StringComparison.Ordinal))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }

    private static string ReadId(
        UnityEngine.Object asset)
    {
        var serializedObject =
            new SerializedObject(asset);

        string[] candidateNames =
        {
            "id",
            "Id",
            "_id",
            "m_Id",
            "configId",
            "ConfigId",
            "_configId"
        };

        foreach (string candidateName in candidateNames)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    candidateName);

            if (property != null &&
                property.propertyType ==
                SerializedPropertyType.String)
            {
                return property.stringValue;
            }
        }

        SerializedProperty iterator =
            serializedObject.GetIterator();

        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType !=
                SerializedPropertyType.String)
            {
                continue;
            }

            string normalizedName = iterator.name
                .Replace("_", string.Empty)
                .ToLowerInvariant();

            if (normalizedName == "id" ||
                normalizedName == "configid" ||
                normalizedName == "mid")
            {
                return iterator.stringValue;
            }
        }

        return null;
    }

    private static string FormatId(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? "<ID не найден>"
            : id;
    }

    private sealed class PlanetReference
    {
        public UnityEngine.Object System;
        public string SystemPath;
        public string PropertyPath;

        public UnityEngine.Object Planet;
        public string PlanetPath;
        public string PlanetId;
    }
}