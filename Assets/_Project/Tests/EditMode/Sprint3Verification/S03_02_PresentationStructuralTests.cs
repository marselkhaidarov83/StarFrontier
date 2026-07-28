#if UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_PresentationStructuralTests
{
    [Test]
    public void PlanetSelectableView_UsesPointerContractAndTargetService()
    {
        Type viewType =
            SfTestReflection.RequireType(
                "PlanetSelectableView2");

        Assert.That(
            typeof(IPointerClickHandler)
                .IsAssignableFrom(viewType),
            Is.True);

        MethodInfo click =
            SfTestReflection.RequireMethod(
                viewType,
                "OnPointerClick",
                1);

        Assert.That(
            SfTestReflection.MethodReferences(
                click,
                "ITargetService2A",
                "TrySelectTarget") ||
            SfTestReflection.MethodReferences(
                click,
                "TargetService2A",
                "TrySelectTarget"),
            Is.True,
            "PlanetSelectableView2 must convert pointer input into TargetService2A selection.");
    }

    [Test]
    public void TargetMarkerView_ExistsAsPresentationComponent()
    {
        Type markerType =
            SfTestReflection.RequireType(
                "TargetMarkerView2A");

        Assert.That(
            typeof(MonoBehaviour)
                .IsAssignableFrom(markerType),
            Is.True);

        bool hasRootReference =
            markerType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Any(field =>
                    field.FieldType ==
                    typeof(GameObject));

        Assert.That(
            hasRootReference,
            Is.True);
    }

    [Test]
    public void ProductionTargetMarkerSprite_IsImported()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "ui_system_selected_target_frame_01 t:Sprite");

        Assert.That(
            guids.Length,
            Is.GreaterThan(0),
            "Import ui_system_selected_target_frame_01 " +
            "as a Sprite.");
    }

    [Test]
    public void AtLeastOnePlanetPrefab_HasTargetMarkerBinding()
    {
        Type selectableType =
            SfTestReflection.RequireType(
                "PlanetSelectableView2");

        Type markerType =
            SfTestReflection.RequireType(
                "TargetMarkerView2A");

        string[] prefabGuids =
            AssetDatabase.FindAssets(
                "t:Prefab");

        bool foundPlanetSelectable =
            false;

        bool foundCompleteBinding =
            false;

        foreach (string guid in prefabGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(path);

            if (prefab == null)
                continue;

            Component selectable =
                prefab.GetComponentInChildren(
                    selectableType,
                    true);

            if (selectable == null)
                continue;

            foundPlanetSelectable = true;

            Component marker =
                selectable.GetComponent(
                    markerType);

            if (marker != null)
            {
                foundCompleteBinding = true;
                break;
            }
        }

        Assert.That(
            foundPlanetSelectable,
            Is.True,
            "No prefab with PlanetSelectableView2 was found.");

        Assert.That(
            foundCompleteBinding,
            Is.True,
            "The planet selectable prefab must also contain TargetMarkerView2A.");
    }
}

#endif
