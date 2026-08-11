using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SystemNpcSpawnPointConfigAssetGenerator
{
    private const string OutputRoot =
        "Assets/_Project/Content/Configs/SystemNpcSpawnPoints";

    private const string StarSystemRoot =
        "Assets/_Project/Content/Configs/StarSystems";

    private const int TargetOrbitNumber =
        8;

    private const int SpawnPointCount =
        5;

    private const float FallbackOrbitStep =
        200f;

    private const float EnemyRandomRadius =
        150f;

    [MenuItem("STAR FRONTIER/Content/10. System NPC Spawn Points/Create generated SystemNpcSpawnPointConfig assets")]
    public static void CreateGeneratedSystemNpcSpawnPointConfigs()
    {
        EnsureFolder(OutputRoot);

        string[] guids =
            AssetDatabase.FindAssets("t:StarSystemConfig", new[] { StarSystemRoot });

        int createdOrUpdated = 0;
        int boundSystems = 0;
        int warnings = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string systemPath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            StarSystemConfig starSystem =
                AssetDatabase.LoadAssetAtPath<StarSystemConfig>(systemPath);

            if (starSystem == null)
                continue;

            if (!TryBuildSpawnPoints(
                    starSystem,
                    out Vector3[] enemySpawnPoints,
                    out float targetOrbitRadius,
                    out string warning))
            {
                warnings++;
                Debug.LogWarning(
                    $"SystemNpcSpawnPoint generator: skipped {starSystem.Id}. {warning}",
                    starSystem);
                continue;
            }

            string id =
                $"system_npc_spawn_points_{BuildSafeKey(starSystem)}_01";

            string path =
                $"{OutputRoot}/{id}.asset";

            SystemNpcSpawnPointConfig asset =
                AssetDatabase.LoadAssetAtPath<SystemNpcSpawnPointConfig>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SystemNpcSpawnPointConfig>();
                AssetDatabase.CreateAsset(asset, path);
            }

            WriteSpawnPointConfig(
                asset,
                id,
                starSystem,
                enemySpawnPoints,
                targetOrbitRadius);

            EditorUtility.SetDirty(asset);
            createdOrUpdated++;

            if (BindSpawnPointConfigToStarSystem(
                    starSystem,
                    asset))
            {
                EditorUtility.SetDirty(starSystem);
                boundSystems++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemNpcSpawnPointConfig generator",
            "Done. " +
            $"Spawn point configs: {createdOrUpdated}. " +
            $"Bound star systems: {boundSystems}. " +
            $"Warnings: {warnings}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/10. System NPC Spawn Points/Validate generated SystemNpcSpawnPointConfig assets")]
    public static void ValidateGeneratedSystemNpcSpawnPointConfigs()
    {
        int checkedAssets = 0;
        int errors = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:StarSystemConfig", new[] { StarSystemRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string systemPath =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            StarSystemConfig starSystem =
                AssetDatabase.LoadAssetAtPath<StarSystemConfig>(systemPath);

            if (starSystem == null)
                continue;

            checkedAssets++;

            if (starSystem.NpcSpawnPoints == null)
            {
                errors++;
                Debug.LogError(
                    $"SystemNpcSpawnPoint validation: missing npcSpawnPoints at {systemPath} ({starSystem.Id})",
                    starSystem);
                continue;
            }

            Vector3[] points =
                starSystem.NpcSpawnPoints.EnemySpawnPoints;

            if (points == null || points.Length != SpawnPointCount)
            {
                errors++;
                Debug.LogError(
                    $"SystemNpcSpawnPoint validation: expected {SpawnPointCount} enemy spawn points at {systemPath} ({starSystem.Id}), actual {points?.Length ?? 0}",
                    starSystem.NpcSpawnPoints);
                continue;
            }

            float requiredRadius =
                ResolveTargetOrbitRadius(starSystem);

            if (requiredRadius <= 0f)
                continue;

            float randomRadius =
                starSystem.NpcSpawnPoints.EnemyRandomRadius;

            if (randomRadius <= 0f)
            {
                errors++;
                Debug.LogError(
                    $"SystemNpcSpawnPoint validation: EnemyRandomRadius must be greater than 0 for visible enemy scatter. " +
                    $"Actual={randomRadius}, System={starSystem.Id}",
                    starSystem.NpcSpawnPoints);
            }

            for (int p = 0; p < points.Length; p++)
            {
                float pointRadius =
                    new Vector2(points[p].x, points[p].y).magnitude;

                if (Mathf.Abs(pointRadius - requiredRadius) <= 0.05f)
                    continue;

                errors++;
                Debug.LogError(
                    $"SystemNpcSpawnPoint validation: point {p} must be exactly on required orbit 8. " +
                    $"PointRadius={pointRadius}, RequiredRadius={requiredRadius}, System={starSystem.Id}",
                    starSystem.NpcSpawnPoints);
            }
        }

        EditorUtility.DisplayDialog(
            "SystemNpcSpawnPointConfig validation",
            $"Checked star systems: {checkedAssets}. Errors: {errors}.",
            "OK");
    }

    [MenuItem("STAR FRONTIER/Content/10. System NPC Spawn Points/Delete generated SystemNpcSpawnPointConfig assets")]
    public static void DeleteGeneratedSystemNpcSpawnPointConfigs()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete generated SystemNpcSpawnPointConfig assets",
                $"Delete generated SystemNpcSpawnPointConfig assets inside:\n{OutputRoot}",
                "Delete",
                "Cancel"))
        {
            return;
        }

        int deleted = 0;

        string[] guids =
            AssetDatabase.FindAssets("t:SystemNpcSpawnPointConfig", new[] { OutputRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!IsGeneratedSpawnPointPath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "SystemNpcSpawnPointConfig generator",
            $"Deleted: {deleted}.",
            "OK");
    }

    private static bool TryBuildSpawnPoints(
        StarSystemConfig starSystem,
        out Vector3[] enemySpawnPoints,
        out float targetOrbitRadius,
        out string warning)
    {
        enemySpawnPoints = null;
        targetOrbitRadius = 0f;
        warning = string.Empty;

        if (starSystem == null)
        {
            warning = "StarSystemConfig is null.";
            return false;
        }

        List<float> orbitRadii =
            CollectSortedOrbitRadii(starSystem.PlanetRefs);

        if (orbitRadii.Count == 0)
        {
            warning = "System has no planets with PlanetOrbitConfig.";
            return false;
        }

        if (orbitRadii.Count > TargetOrbitNumber)
        {
            warning =
                $"System has more than target orbit {TargetOrbitNumber} planet orbits. Orbit count: {orbitRadii.Count}.";
            return false;
        }

        float orbitStep =
            ResolveOuterOrbitStep(orbitRadii);

        float lastOrbitRadius =
            orbitRadii[orbitRadii.Count - 1];

        int missingOrbitCount =
            TargetOrbitNumber - orbitRadii.Count;

        targetOrbitRadius =
            lastOrbitRadius +
            orbitStep * missingOrbitCount;

        if (targetOrbitRadius <= 0f)
        {
            warning = "Resolved target orbit radius is zero.";
            return false;
        }

        enemySpawnPoints =
            BuildFivePointsOnOrbit(targetOrbitRadius);

        return true;
    }

    private static float ResolveTargetOrbitRadius(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return 0f;

        List<float> orbitRadii =
            CollectSortedOrbitRadii(starSystem.PlanetRefs);

        if (orbitRadii.Count == 0)
            return 0f;

        if (orbitRadii.Count > TargetOrbitNumber)
            return 0f;

        float orbitStep =
            ResolveOuterOrbitStep(orbitRadii);

        int missingOrbitCount =
            TargetOrbitNumber - orbitRadii.Count;

        return orbitRadii[orbitRadii.Count - 1] +
               orbitStep * missingOrbitCount;
    }

    private static List<float> CollectSortedOrbitRadii(
        PlanetConfig[] planets)
    {
        List<float> orbitRadii =
            new List<float>();

        if (planets == null)
            return orbitRadii;

        for (int i = 0; i < planets.Length; i++)
        {
            PlanetConfig planet =
                planets[i];

            if (planet == null || planet.PlanetOrbit == null)
                continue;

            float orbitRadius =
                planet.PlanetOrbit.OrbitRadius;

            if (orbitRadius <= 0f)
                continue;

            orbitRadii.Add(orbitRadius);
        }

        orbitRadii.Sort();

        return orbitRadii;
    }

    private static float ResolveOuterOrbitStep(
        IReadOnlyList<float> sortedOrbitRadii)
    {
        if (sortedOrbitRadii == null || sortedOrbitRadii.Count == 0)
            return FallbackOrbitStep;

        if (sortedOrbitRadii.Count >= 2)
        {
            float outerRadius =
                sortedOrbitRadii[sortedOrbitRadii.Count - 1];

            float previousRadius =
                sortedOrbitRadii[sortedOrbitRadii.Count - 2];

            float outerStep =
                outerRadius - previousRadius;

            if (outerStep > 0f)
                return outerStep;
        }

        float lastRadius =
            sortedOrbitRadii[sortedOrbitRadii.Count - 1];

        return Mathf.Max(
            FallbackOrbitStep,
            lastRadius / Mathf.Max(1, sortedOrbitRadii.Count));
    }

    private static Vector3[] BuildFivePointsOnOrbit(
        float radius)
    {
        Vector3[] points =
            new Vector3[SpawnPointCount];

        float startAngleDegrees =
            -90f;

        for (int i = 0; i < SpawnPointCount; i++)
        {
            float angleDegrees =
                startAngleDegrees +
                (360f / SpawnPointCount) * i;

            float angleRadians =
                angleDegrees * Mathf.Deg2Rad;

            points[i] =
                new Vector3(
                    Mathf.Cos(angleRadians) * radius,
                    Mathf.Sin(angleRadians) * radius,
                    0f);
        }

        return points;
    }

    private static void WriteSpawnPointConfig(
        SystemNpcSpawnPointConfig asset,
        string id,
        StarSystemConfig starSystem,
        Vector3[] enemySpawnPoints,
        float targetOrbitRadius)
    {
        SerializedObject serializedObject =
            new SerializedObject(asset);

        serializedObject.Update();

        SetString(serializedObject, "id", id);

        SetString(
            serializedObject,
            "displayName",
            $"Точки спавна NPC: {starSystem.DisplayName}");

        SetString(
            serializedObject,
            "description",
            $"5 точек появления врагов строго на условной 8-й орбите системы. " +
            $"Радиус 8-й орбиты: {targetOrbitRadius:0.##}. " +
            $"Касательный разброс врагов в группе: {EnemyRandomRadius:0.##}.");

        SerializedProperty pointsProperty =
            serializedObject.FindProperty("enemySpawnPoints");

        if (pointsProperty == null || !pointsProperty.isArray)
            throw new InvalidOperationException("SystemNpcSpawnPointConfig.enemySpawnPoints field was not found.");

        pointsProperty.arraySize = 0;
        pointsProperty.arraySize = enemySpawnPoints.Length;

        for (int i = 0; i < enemySpawnPoints.Length; i++)
        {
            pointsProperty
                .GetArrayElementAtIndex(i)
                .vector3Value = enemySpawnPoints[i];
        }

        SerializedProperty radiusProperty =
            serializedObject.FindProperty("enemyRandomRadius");

        if (radiusProperty != null)
            radiusProperty.floatValue = EnemyRandomRadius;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool BindSpawnPointConfigToStarSystem(
        StarSystemConfig starSystem,
        SystemNpcSpawnPointConfig spawnPointConfig)
    {
        if (starSystem == null || spawnPointConfig == null)
            return false;

        SerializedObject serializedObject =
            new SerializedObject(starSystem);

        serializedObject.Update();

        SerializedProperty npcSpawnPointsProperty =
            serializedObject.FindProperty("npcSpawnPoints");

        if (npcSpawnPointsProperty == null)
            throw new InvalidOperationException("StarSystemConfig.npcSpawnPoints field was not found.");

        npcSpawnPointsProperty.objectReferenceValue =
            spawnPointConfig;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        return true;
    }

    private static string BuildSafeKey(
        StarSystemConfig starSystem)
    {
        string source =
            !string.IsNullOrWhiteSpace(starSystem.Id)
                ? starSystem.Id
                : starSystem.name;

        if (string.IsNullOrWhiteSpace(source))
            source = "unknown_system";

        string result =
            source.Trim().ToLowerInvariant();

        char[] chars =
            result.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            bool valid =
                chars[i] >= 'a' && chars[i] <= 'z' ||
                chars[i] >= '0' && chars[i] <= '9';

            if (!valid)
                chars[i] = '_';
        }

        result =
            new string(chars);

        while (result.Contains("__"))
            result = result.Replace("__", "_");

        return result.Trim('_');
    }

    private static bool IsGeneratedSpawnPointPath(
        string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string fileName =
            System.IO.Path.GetFileNameWithoutExtension(path);

        return fileName.StartsWith(
            "system_npc_spawn_points_",
            StringComparison.OrdinalIgnoreCase);
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
            property.stringValue = value ?? string.Empty;
    }
}
