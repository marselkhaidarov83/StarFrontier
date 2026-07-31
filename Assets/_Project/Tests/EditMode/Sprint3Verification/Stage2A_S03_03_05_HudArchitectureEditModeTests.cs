#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Stage2A_S03_03_05_HudArchitectureEditModeTests
{
    [SetUp]
    public void OpenScene()
    {
        Stage2A_S03_03_TestUtility.OpenSystemScene();
    }

    [Test]
    public void HudRoots_DoNotContainActiveLegacyNamedObjects()
    {
        GameObject top =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_TopHud");

        GameObject bottom =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_BottomHud");

        Assert.NotNull(top);
        Assert.NotNull(bottom);

        List<GameObject> hudObjects =
            Stage2A_S03_03_TestUtility
                .GetChildrenRecursive(top)
                .Concat(
                    Stage2A_S03_03_TestUtility
                        .GetChildrenRecursive(bottom))
                .Where(gameObject =>
                    gameObject.activeInHierarchy)
                .ToList();

        List<string> activeLegacyObjects =
            hudObjects
                .Where(gameObject =>
                    gameObject.name
                        .ToLowerInvariant()
                        .Contains("legacy"))
                .Select(gameObject => gameObject.name)
                .ToList();

        Assert.IsEmpty(
            activeLegacyObjects,
            "Active legacy HUD objects found: " +
            string.Join(", ", activeLegacyObjects));
    }

    [Test]
    public void HudViewComponents_DoNotDeriveFromCustomService()
    {
        GameObject top =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_TopHud");

        GameObject bottom =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_BottomHud");

        Assert.NotNull(top);
        Assert.NotNull(bottom);

        IEnumerable<MonoBehaviour> hudBehaviours =
            top.GetComponentsInChildren<MonoBehaviour>(true)
                .Concat(
                    bottom.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component =>
                    component != null &&
                    ShouldInspectProjectComponent(component.GetType()));

        List<string> invalidComponents =
            hudBehaviours
                .Where(component =>
                    InheritsFromTypeNamed(
                        component.GetType(),
                        "CustomService"))
                .Select(component =>
                    component.GetType().Name +
                    " on " +
                    component.gameObject.name)
                .ToList();

        Assert.IsEmpty(
            invalidComponents,
            "HUD/View components must not be services: " +
            string.Join(", ", invalidComponents));
    }

    [Test]
    public void HudViewComponents_DoNotStoreDirectGameplayStateFields()
    {
        GameObject top =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_TopHud");

        GameObject bottom =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("SafeAreaRoot_BottomHud");

        Assert.NotNull(top);
        Assert.NotNull(bottom);

        IEnumerable<MonoBehaviour> hudBehaviours =
            top.GetComponentsInChildren<MonoBehaviour>(true)
                .Concat(
                    bottom.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component =>
                    component != null &&
                    ShouldInspectProjectComponent(component.GetType()));

        List<string> invalidFields =
            new List<string>();

        foreach (MonoBehaviour component in hudBehaviours)
        {
            FieldInfo[] fields =
                component
                    .GetType()
                    .GetFields(
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                if (ShouldIgnoreField(field))
                    continue;

                string fieldTypeName =
                    field.FieldType.Name;

                bool looksLikeDirectGameplayState =
                    fieldTypeName == "GameState" ||
                    fieldTypeName == "PlayerState" ||
                    fieldTypeName == "ShipState" ||
                    fieldTypeName == "GalaxyRuntimeState" ||
                    fieldTypeName == "SectorRuntimeState" ||
                    fieldTypeName == "StarSystemRuntimeState" ||
                    fieldTypeName == "RouteRuntimeState" ||
                    fieldTypeName.EndsWith("RuntimeState",
                        StringComparison.Ordinal);

                bool allowedServiceReference =
                    fieldTypeName.StartsWith("I",
                        StringComparison.Ordinal) &&
                    fieldTypeName.EndsWith("Service",
                        StringComparison.Ordinal);

                bool allowedUnityReference =
                    IsUnityOrPackageType(field.FieldType);

                if (looksLikeDirectGameplayState &&
                    !allowedServiceReference &&
                    !allowedUnityReference)
                {
                    invalidFields.Add(
                        component.GetType().Name +
                        "." +
                        field.Name +
                        " : " +
                        fieldTypeName);
                }
            }
        }

        Assert.IsEmpty(
            invalidFields,
            "HUD/View must not store direct gameplay State fields: " +
            string.Join(", ", invalidFields));
    }

    private static bool ShouldInspectProjectComponent(
        Type componentType)
    {
        if (componentType == null)
            return false;

        if (IsUnityOrPackageType(componentType))
            return false;

        string namespaceName =
            componentType.Namespace ?? string.Empty;

        if (namespaceName.StartsWith("TMPro",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (namespaceName.StartsWith("Unity.",
                StringComparison.Ordinal) ||
            namespaceName.StartsWith("UnityEngine.",
                StringComparison.Ordinal) ||
            namespaceName.StartsWith("UnityEditor.",
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldIgnoreField(
        FieldInfo field)
    {
        if (field == null)
            return true;

        if (field.IsStatic)
            return true;

        Type fieldType =
            field.FieldType;

        if (fieldType == null)
            return true;

        if (IsUnityOrPackageType(fieldType))
            return true;

        if (fieldType.IsPrimitive ||
            fieldType.IsEnum ||
            fieldType == typeof(string))
        {
            return true;
        }

        if (typeof(UnityEngine.Object)
            .IsAssignableFrom(fieldType))
        {
            return true;
        }

        string fieldTypeName =
            fieldType.Name;

        if (fieldTypeName == "SpriteState" ||
            fieldTypeName == "ColorBlock" ||
            fieldTypeName == "Navigation" ||
            fieldTypeName == "RectOffset")
        {
            return true;
        }

        return false;
    }

    private static bool IsUnityOrPackageType(
        Type type)
    {
        if (type == null)
            return false;

        string namespaceName =
            type.Namespace ?? string.Empty;

        if (namespaceName.StartsWith("UnityEngine",
                StringComparison.Ordinal) ||
            namespaceName.StartsWith("UnityEditor",
                StringComparison.Ordinal) ||
            namespaceName.StartsWith("Unity.",
                StringComparison.Ordinal) ||
            namespaceName.StartsWith("TMPro",
                StringComparison.Ordinal))
        {
            return true;
        }

        Assembly assembly =
            type.Assembly;

        if (assembly == null)
            return false;

        string assemblyName =
            assembly.GetName().Name;

        return
            assemblyName.StartsWith("UnityEngine",
                StringComparison.Ordinal) ||
            assemblyName.StartsWith("UnityEditor",
                StringComparison.Ordinal) ||
            assemblyName.StartsWith("Unity.",
                StringComparison.Ordinal) ||
            assemblyName.StartsWith("Unity.TextMeshPro",
                StringComparison.Ordinal) ||
            assemblyName == "UnityEngine.UI";
    }

    private static bool InheritsFromTypeNamed(
        Type type,
        string baseTypeName)
    {
        Type current = type;

        while (current != null)
        {
            if (current.Name == baseTypeName)
                return true;

            current = current.BaseType;
        }

        return false;
    }
}
#endif