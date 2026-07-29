#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Stage2A_S03_03_TestUtility
{
    public const string SystemScenePath =
        "Assets/_Project/Scenes/SystemScene.unity";

    public static Scene OpenSystemScene()
    {
        SceneAsset sceneAsset =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SystemScenePath);

        Assert.NotNull(
            sceneAsset,
            "SystemScene asset not found at " + SystemScenePath);

        return EditorSceneManager.OpenScene(
            SystemScenePath,
            OpenSceneMode.Single);
    }

    public static GameObject FindGameObjectInOpenScenes(
        string objectName)
    {
        return Resources
            .FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(gameObject =>
                gameObject != null &&
                gameObject.scene.IsValid() &&
                gameObject.name == objectName);
    }

    public static List<GameObject> FindGameObjectsInOpenScenes(
        Func<GameObject, bool> predicate)
    {
        return Resources
            .FindObjectsOfTypeAll<GameObject>()
            .Where(gameObject =>
                gameObject != null &&
                gameObject.scene.IsValid() &&
                predicate(gameObject))
            .ToList();
    }

    public static List<GameObject> GetChildrenRecursive(
        GameObject root)
    {
        List<GameObject> result =
            new List<GameObject>();

        if (root == null)
            return result;

        AddChildrenRecursive(
            root.transform,
            result);

        return result;
    }

    public static bool HasMonoBehaviourNamed(
        GameObject gameObject,
        string typeName)
    {
        if (gameObject == null)
            return false;

        return gameObject
            .GetComponents<MonoBehaviour>()
            .Any(component =>
                component != null &&
                component.GetType().Name == typeName);
    }

    public static MonoBehaviour FindMonoBehaviourInOpenScenes(
        string typeName)
    {
        return Resources
            .FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(component =>
                component != null &&
                component.gameObject.scene.IsValid() &&
                component.GetType().Name == typeName);
    }

    public static List<MonoBehaviour> FindMonoBehavioursInOpenScenes(
        string typeName)
    {
        return Resources
            .FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(component =>
                component != null &&
                component.gameObject.scene.IsValid() &&
                component.GetType().Name == typeName)
            .ToList();
    }

    public static Type FindType(
        string shortOrFullName)
    {
        foreach (Assembly assembly in
                 AppDomain.CurrentDomain.GetAssemblies())
        {
            Type match =
                SafeGetTypes(assembly)
                    .FirstOrDefault(type =>
                        type != null &&
                        (type.Name == shortOrFullName ||
                         type.FullName == shortOrFullName));

            if (match != null)
                return match;
        }

        return null;
    }

    public static List<Type> FindTypes(
        Func<Type, bool> predicate)
    {
        return AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(type =>
                type != null &&
                predicate(type))
            .ToList();
    }

    public static MethodInfo FindParameterlessMethod(
        Type type,
        params string[] methodNames)
    {
        if (type == null)
            return null;

        foreach (string methodName in methodNames)
        {
            MethodInfo method =
                type.GetMethod(
                    methodName,
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);

            if (method != null)
                return method;
        }

        return null;
    }

    public static object InvokeStaticParameterless(
        Type type,
        string methodName)
    {
        MethodInfo method =
            type.GetMethod(
                methodName,
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);

        Assert.NotNull(
            method,
            "Static method not found: " + type.Name + "." + methodName);

        return method.Invoke(null, null);
    }

    public static object InvokeStatic(
        Type type,
        string methodName,
        params object[] args)
    {
        MethodInfo method =
            type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == args.Length);

        Assert.NotNull(
            method,
            "Static method not found: " + type.Name + "." + methodName);

        return method.Invoke(null, args);
    }

    public static object GetSerializedObjectReference(
        UnityEngine.Object target,
        string propertyName)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            return null;

        return property.objectReferenceValue;
    }

    public static bool GetSerializedBool(
        UnityEngine.Object target,
        string propertyName,
        bool defaultValue)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            return defaultValue;

        return property.boolValue;
    }

    public static string[] FindAssetPathsByName(
        string assetName)
    {
        string[] guids =
            AssetDatabase.FindAssets(assetName);

        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                !string.IsNullOrWhiteSpace(path) &&
                Path.GetFileNameWithoutExtension(path)
                    .IndexOf(assetName, StringComparison.OrdinalIgnoreCase) >= 0)
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<Type> SafeGetTypes(
        Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static void AddChildrenRecursive(
        Transform parent,
        List<GameObject> result)
    {
        foreach (Transform child in parent)
        {
            result.Add(child.gameObject);
            AddChildrenRecursive(child, result);
        }
    }
}
#endif
