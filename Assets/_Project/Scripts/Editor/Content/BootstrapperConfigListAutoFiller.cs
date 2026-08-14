using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BootstrapperConfigListAutoFiller
{
    private const string MenuRoot =
        "STAR FRONTIER/Content/11. Bootstrapper";

    private const string ConfigRoot =
        "Assets/_Project/Content/Configs";

    private const string BootstrapperScriptPath =
        "Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs";

    [MenuItem(MenuRoot + "/Fill NPC and combat config lists from assets")]
    public static void FillAllBootstrappersFromAssets()
    {
        List<MonoBehaviour> bootstrappers =
            FindSceneBootstrappers();

        if (bootstrappers.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Bootstrapper config list fill",
                "Bootstrapper was not found in opened scenes.",
                "OK");

            return;
        }

        List<WeaponConfig> weapons =
            LoadAssetsSortedById<WeaponConfig>();

        List<EnemyConfig> enemies =
            LoadAssetsSortedById<EnemyConfig>();

        List<AllyConfig> allies =
            LoadAssetsSortedById<AllyConfig>();

        List<AllySpawnRuleConfig> allySpawnRuleConfigs =
            LoadAssetsSortedById<AllySpawnRuleConfig>();

        List<EnemyGroupSpawnRuleConfig> enemyGroupSpawnRules =
            LoadAssetsSortedById<EnemyGroupSpawnRuleConfig>();

        List<SystemPopulationRule> systemPopulationRules =
            LoadAssetsSortedById<SystemPopulationRule>();

        int updatedCount = 0;

        for (int i = 0; i < bootstrappers.Count; i++)
        {
            MonoBehaviour bootstrapper =
                bootstrappers[i];

            if (bootstrapper == null)
                continue;

            FillBootstrapper(
                bootstrapper,
                weapons,
                enemies,
                allies,
                allySpawnRuleConfigs,
                enemyGroupSpawnRules,
                systemPopulationRules);

            updatedCount++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog(
            "Bootstrapper config list fill",
            "Updated Bootstrappers: " + updatedCount + "\n" +
            "Weapons: " + weapons.Count + "\n" +
            "Enemies: " + enemies.Count + "\n" +
            "Allies: " + allies.Count + "\n" +
            "AllySpawnRuleConfigs: " + allySpawnRuleConfigs.Count + "\n" +
            "EnemyGroupSpawnRules: " + enemyGroupSpawnRules.Count + "\n" +
            "SystemPopulationRules: " + systemPopulationRules.Count,
            "OK");

        Debug.Log(
            "[BootstrapperConfigListAutoFiller] Updated Bootstrappers: " +
            updatedCount +
            ", Weapons: " + weapons.Count +
            ", Enemies: " + enemies.Count +
            ", Allies: " + allies.Count +
            ", AllySpawnRuleConfigs: " + allySpawnRuleConfigs.Count +
            ", EnemyGroupSpawnRules: " + enemyGroupSpawnRules.Count +
            ", SystemPopulationRules: " + systemPopulationRules.Count);
    }

    private static List<MonoBehaviour> FindSceneBootstrappers()
    {
        MonoScript bootstrapperScript =
            AssetDatabase.LoadAssetAtPath<MonoScript>(BootstrapperScriptPath);

        if (bootstrapperScript == null)
        {
            Debug.LogError(
                "[BootstrapperConfigListAutoFiller] Bootstrapper script was not found at: " +
                BootstrapperScriptPath);

            return new List<MonoBehaviour>();
        }

        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        List<MonoBehaviour> result =
            new List<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour =
                behaviours[i];

            if (behaviour == null)
                continue;

            if (EditorUtility.IsPersistent(behaviour))
                continue;

            GameObject gameObject =
                behaviour.gameObject;

            if (gameObject == null)
                continue;

            Scene scene =
                gameObject.scene;

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            MonoScript script =
                MonoScript.FromMonoBehaviour(behaviour);

            if (script != bootstrapperScript)
                continue;

            result.Add(behaviour);
        }

        result.Sort(CompareBehavioursBySceneAndName);

        return result;
    }

    private static int CompareBehavioursBySceneAndName(
        MonoBehaviour left,
        MonoBehaviour right)
    {
        if (ReferenceEquals(left, right))
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int sceneCompare =
            string.Compare(
                left.gameObject.scene.path,
                right.gameObject.scene.path,
                StringComparison.Ordinal);

        if (sceneCompare != 0)
            return sceneCompare;

        return string.Compare(
            left.name,
            right.name,
            StringComparison.Ordinal);
    }

    private static void FillBootstrapper(
        MonoBehaviour bootstrapper,
        IReadOnlyList<WeaponConfig> weapons,
        IReadOnlyList<EnemyConfig> enemies,
        IReadOnlyList<AllyConfig> allies,
        IReadOnlyList<AllySpawnRuleConfig> allySpawnRuleConfigs,
        IReadOnlyList<EnemyGroupSpawnRuleConfig> enemyGroupSpawnRules,
        IReadOnlyList<SystemPopulationRule> systemPopulationRules)
    {
        Undo.RecordObject(
            bootstrapper,
            "Fill Bootstrapper config lists from assets");

        SerializedObject serializedObject =
            new SerializedObject(bootstrapper);

        AssignObjectList(
            serializedObject,
            "weapons",
            weapons);

        AssignObjectList(
            serializedObject,
            "enemies",
            enemies);

        AssignObjectList(
            serializedObject,
            "allies",
            allies);

        AssignObjectList(
            serializedObject,
            "allySpawnRuleConfigs",
            allySpawnRuleConfigs);

        AssignObjectList(
            serializedObject,
            "enemyGroupSpawnRules",
            enemyGroupSpawnRules);

        AssignObjectList(
            serializedObject,
            "systemPopulationRules",
            systemPopulationRules);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(bootstrapper);
        PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrapper);
    }

    private static void AssignObjectReference<TAsset>(
        SerializedObject serializedObject,
        string propertyName,
        TAsset asset)
        where TAsset : UnityEngine.Object
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogError(
                "[BootstrapperConfigListAutoFiller] Field not found on Bootstrapper: " +
                propertyName);

            return;
        }

        property.objectReferenceValue = asset;
    }

    private static void AssignObjectList<TAsset>(
        SerializedObject serializedObject,
        string propertyName,
        IReadOnlyList<TAsset> assets)
        where TAsset : UnityEngine.Object
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogError(
                "[BootstrapperConfigListAutoFiller] Field not found on Bootstrapper: " +
                propertyName);

            return;
        }

        if (!property.isArray)
        {
            Debug.LogError(
                "[BootstrapperConfigListAutoFiller] Field is not a list/array: " +
                propertyName);

            return;
        }

        int count =
            assets != null
                ? assets.Count
                : 0;

        property.arraySize = count;

        for (int i = 0; i < count; i++)
        {
            SerializedProperty element =
                property.GetArrayElementAtIndex(i);

            element.objectReferenceValue =
                assets[i];
        }
    }

    private static List<TAsset> LoadAssetsSortedById<TAsset>()
        where TAsset : BaseConfig
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:" + typeof(TAsset).Name,
                new[] { ConfigRoot });

        List<TAsset> assets =
            new List<TAsset>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guids[i]);

            TAsset asset =
                AssetDatabase.LoadAssetAtPath<TAsset>(path);

            if (asset == null)
                continue;

            assets.Add(asset);
        }

        assets.Sort(CompareBaseConfigs);

        return assets;
    }

    private static TAsset FindById<TAsset>(
        IReadOnlyList<TAsset> assets,
        string id)
        where TAsset : BaseConfig
    {
        if (assets == null)
            return null;

        for (int i = 0; i < assets.Count; i++)
        {
            TAsset asset =
                assets[i];

            if (asset == null)
                continue;

            if (asset.Id == id)
                return asset;
        }

        return null;
    }

    private static int CompareBaseConfigs<TAsset>(
        TAsset left,
        TAsset right)
        where TAsset : BaseConfig
    {
        if (ReferenceEquals(left, right))
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int idCompare =
            string.Compare(
                left.Id,
                right.Id,
                StringComparison.Ordinal);

        if (idCompare != 0)
            return idCompare;

        string leftPath =
            AssetDatabase.GetAssetPath(left);

        string rightPath =
            AssetDatabase.GetAssetPath(right);

        return string.Compare(
            leftPath,
            rightPath,
            StringComparison.Ordinal);
    }
}
