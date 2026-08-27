using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatSceneBindingsValidator2A
{
    public const string MenuPath =
        "STAR FRONTIER/Combat/Validate Combat Scene Bindings";

    private const string SystemScenePath =
        "Assets/_Project/Scenes/SystemScene.unity";

    private const string CombatScenePath =
        "Assets/_Project/Scenes/CombatScene.unity";

    private const string NpcPrefabPath =
        "Assets/_Project/Prefabs/UI/Npc/NpcPrefab_01.prefab";

    private const string ProjectilePrefabPath =
        "Assets/_Project/Prefabs/UI/Npc/NpcProjectilePrefab_01.prefab";

    [MenuItem(MenuPath)]
    public static void ValidateFromMenu()
    {
        List<string> errors =
            ValidateProjectBindings();

        if (errors.Count == 0)
        {
            Debug.Log(
                "[2A-S04-05-T06] Combat scene bindings validation passed.");

            return;
        }

        Debug.LogError(
            "[2A-S04-05-T06] Combat scene bindings validation failed:\n" +
            string.Join("\n", errors));
    }

    public static List<string> ValidateProjectBindings()
    {
        List<string> errors =
            new List<string>();

        ValidateSystemScene(errors);
        ValidateCombatScene(errors);
        ValidateNpcPrefab(errors);
        ValidateProjectilePrefab(errors);

        return errors;
    }

    private static void ValidateSystemScene(
        List<string> errors)
    {
        Scene scene =
            OpenSceneForValidation(
                SystemScenePath,
                errors);

        if (!scene.IsValid())
            return;

        SystemHudCompositionRoot2A hudRoot =
            FindObjectInLoadedScene<SystemHudCompositionRoot2A>();

        if (hudRoot == null)
        {
            errors.Add(
                SystemScenePath +
                ": missing SystemHudCompositionRoot2A.");

            return;
        }

        RequireObjectField(
            hudRoot,
            "bindersRoot",
            SystemScenePath,
            errors);

        RequirePositiveFloatField(
            hudRoot,
            "registryWaitTimeoutSeconds",
            SystemScenePath,
            errors);
    }

    private static void ValidateCombatScene(
        List<string> errors)
    {
        Scene scene =
            OpenSceneForValidation(
                CombatScenePath,
                errors);

        if (!scene.IsValid())
            return;

        SystemEncounterRuntimeRoot encounterRoot =
            FindObjectInLoadedScene<SystemEncounterRuntimeRoot>();

        SystemNpcCombatVisualController visualController =
            FindObjectInLoadedScene<SystemNpcCombatVisualController>();

        if (encounterRoot == null &&
            visualController == null)
        {
            return;
        }

        if (encounterRoot == null)
        {
            errors.Add(
                CombatScenePath +
                ": missing SystemEncounterRuntimeRoot.");
        }
        else
        {
            RequireObjectField(
                encounterRoot,
                "enemiesRoot",
                CombatScenePath,
                errors);

            RequireObjectField(
                encounterRoot,
                "alliesRoot",
                CombatScenePath,
                errors);

            RequireObjectField(
                encounterRoot,
                "projectilesRoot",
                CombatScenePath,
                errors);

            RequireObjectField(
                encounterRoot,
                "vfxRoot",
                CombatScenePath,
                errors);
        }

        if (visualController == null)
            return;

        RequireObjectField(
            visualController,
            "projectileVisualPrefab",
            CombatScenePath,
            errors);

        RequirePositiveFloatField(
            visualController,
            "fallbackProjectileSpeed",
            CombatScenePath,
            errors);

        RequirePositiveFloatField(
            visualController,
            "hitscanVisualTravelSeconds",
            CombatScenePath,
            errors);

        RequirePositiveFloatField(
            visualController,
            "minProjectileVisualTravelSeconds",
            CombatScenePath,
            errors);

        RequirePositiveFloatField(
            visualController,
            "maxProjectileVisualTravelSeconds",
            CombatScenePath,
            errors);
    }

    private static void ValidateNpcPrefab(
        List<string> errors)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                NpcPrefabPath);

        if (prefab == null)
        {
            errors.Add(NpcPrefabPath + ": prefab not found.");
            return;
        }

        if (prefab.GetComponent<SystemNpcView>() == null)
            errors.Add(NpcPrefabPath + ": missing SystemNpcView.");

        if (prefab.GetComponent<CombatWeaponTargetMarkerView2A>() == null)
        {
            errors.Add(
                NpcPrefabPath +
                ": missing CombatWeaponTargetMarkerView2A.");
        }

        if (prefab.GetComponent<CombatEnemyStatusMarkerView2A>() == null)
        {
            errors.Add(
                NpcPrefabPath +
                ": missing CombatEnemyStatusMarkerView2A.");
        }

        CombatMarkerVisibilityScaler2A scaler =
            prefab.GetComponent<CombatMarkerVisibilityScaler2A>();

        if (scaler == null)
        {
            errors.Add(
                NpcPrefabPath +
                ": missing CombatMarkerVisibilityScaler2A.");
        }
        else
        {
            RequirePositiveFloatField(
                scaler,
                "fallbackPlayerWeaponRange",
                NpcPrefabPath,
                errors);

            RequirePositiveFloatField(
                scaler,
                "nearScale",
                NpcPrefabPath,
                errors);

            RequirePositiveFloatField(
                scaler,
                "farScale",
                NpcPrefabPath,
                errors);
        }

        RequireChild(
            prefab.transform,
            "CombatWeaponTargetMarker",
            NpcPrefabPath,
            errors);

        RequireChild(
            prefab.transform,
            "CombatEnemyStatusMarker",
            NpcPrefabPath,
            errors);

        RequireChild(
            prefab.transform,
            "WeaponAssignmentText",
            NpcPrefabPath,
            errors);
    }

    private static void ValidateProjectilePrefab(
        List<string> errors)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                ProjectilePrefabPath);

        if (prefab == null)
        {
            errors.Add(ProjectilePrefabPath + ": prefab not found.");
            return;
        }

        SystemNpcProjectileVisual projectile =
            prefab.GetComponent<SystemNpcProjectileVisual>();

        if (projectile == null)
        {
            errors.Add(
                ProjectilePrefabPath +
                ": missing SystemNpcProjectileVisual.");

            return;
        }

        RequirePositiveFloatField(
            projectile,
            "defaultSpeed",
            ProjectilePrefabPath,
            errors);

        RequirePositiveFloatField(
            projectile,
            "maxLifetime",
            ProjectilePrefabPath,
            errors);

        if (prefab.GetComponentInChildren<Renderer>(true) == null)
        {
            errors.Add(
                ProjectilePrefabPath +
                ": projectile prefab has no Renderer in children.");
        }
    }

    private static Scene OpenSceneForValidation(
        string scenePath,
        List<string> errors)
    {
        SceneAsset sceneAsset =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                scenePath);

        if (sceneAsset == null)
        {
            errors.Add(scenePath + ": scene not found.");
            return default;
        }

        return EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single);
    }

    private static T FindObjectInLoadedScene<T>()
        where T : Object
    {
        T[] objects =
            Object.FindObjectsOfType<T>(true);

        if (objects == null ||
            objects.Length == 0)
        {
            return null;
        }

        return objects[0];
    }

    private static void RequireChild(
        Transform root,
        string childName,
        string assetPath,
        List<string> errors)
    {
        if (root == null ||
            root.Find(childName) == null)
        {
            errors.Add(
                assetPath +
                ": missing child '" +
                childName +
                "'.");
        }
    }

    private static void RequireObjectField(
        Object target,
        string fieldName,
        string assetPath,
        List<string> errors)
    {
        SerializedProperty property =
            FindProperty(
                target,
                fieldName,
                assetPath,
                errors);

        if (property == null)
            return;

        if (property.objectReferenceValue == null)
        {
            errors.Add(
                assetPath +
                ": " +
                target.GetType().Name +
                "." +
                fieldName +
                " is not assigned.");
        }
    }

    private static void RequirePositiveFloatField(
        Object target,
        string fieldName,
        string assetPath,
        List<string> errors)
    {
        SerializedProperty property =
            FindProperty(
                target,
                fieldName,
                assetPath,
                errors);

        if (property == null)
            return;

        if (property.floatValue <= 0f)
        {
            errors.Add(
                assetPath +
                ": " +
                target.GetType().Name +
                "." +
                fieldName +
                " must be greater than 0.");
        }
    }

    private static SerializedProperty FindProperty(
        Object target,
        string fieldName,
        string assetPath,
        List<string> errors)
    {
        if (target == null)
        {
            errors.Add(
                assetPath +
                ": cannot validate field '" +
                fieldName +
                "' on null target.");

            return null;
        }

        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            serializedObject.FindProperty(fieldName);

        if (property == null)
        {
            errors.Add(
                assetPath +
                ": " +
                target.GetType().Name +
                "." +
                fieldName +
                " serialized field was not found.");
        }

        return property;
    }
}
