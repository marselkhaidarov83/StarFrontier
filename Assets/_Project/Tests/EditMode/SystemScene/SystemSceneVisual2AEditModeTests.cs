#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SystemSceneVisual2AEditModeTests
{
    private const string ScenePath =
        "Assets/_Project/Scenes/SystemScene.unity";

    [Test]
    public void SceneFile_Exists()
    {
        string fullPath =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                ScenePath
            );

        Assert.IsTrue(
            File.Exists(fullPath),
            $"Scene file not found: {ScenePath}"
        );
    }

    [Test]
    public void Scene_HasNoMissingScripts()
    {
        Scene scene = OpenSystemScene();

        foreach (GameObject gameObject in GetAllSceneGameObjects(scene))
        {
            Component[] components =
                gameObject.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Assert.IsNotNull(
                    components[i],
                    $"Missing Script found on object: {GetPath(gameObject)}"
                );
            }
        }
    }

    [Test]
    public void Scene_HasSystemMapRootAndExpectedControllers()
    {
        Scene scene = OpenSystemScene();

        GameObject root =
            GameObject.Find("SystemMapRoot");

        Assert.IsNotNull(root, "SystemMapRoot not found.");

        AssertHasComponent(scene, root, "SystemMapController2");
        AssertHasComponent(scene, root, "SystemShipMarkerController2");
        AssertHasComponent(scene, root, "SystemDestinationMarkerController2");
        AssertHasComponent(scene, root, "SystemMapDestinationController2");
        AssertHasComponent(scene, root, "SystemMapClickArea2");
        AssertHasComponent(scene, root, "SystemTravelVisualController2A");
        AssertHasComponent(scene, root, "SystemCameraController2A");
        AssertHasComponent(scene, root, "SystemCameraDragInput2A");
        AssertHasComponent(scene, root, "PlayerInputBridge2A");
        AssertHasComponent(scene, root, "SystemControlPresentationBridge2A");
        AssertHasComponent(scene, root, "SystemSceneRoot2A");
    }

    [Test]
    public void SystemSceneRoot2A_HasRequiredReferences_InRealScene()
    {
        Scene scene = OpenSystemScene();

        MonoBehaviour root =
            FindComponentByTypeName(scene, "SystemSceneRoot2A");

        Assert.IsNotNull(root, "SystemSceneRoot2A not found.");

        object result =
            root.GetType()
                .GetMethod("HasRequiredReferences")
                ?.Invoke(root, null);

        string report =
            root.GetType()
                .GetMethod("GetMissingReferencesReport")
                ?.Invoke(root, null)
                ?.ToString();

        Assert.IsTrue(
            result is bool value && value,
            "SystemSceneRoot2A has missing references:\n" + report
        );
    }

    [Test]
    public void SystemSceneRoot2A_VisualRoots_AreLinked()
    {
        Scene scene = OpenSystemScene();

        MonoBehaviour root =
            FindComponentByTypeName(scene, "SystemSceneRoot2A");

        AssertObjectReference(root, "systemMapRoot");
        AssertObjectReference(root, "mainCamera");
        AssertObjectReference(root, "systemBackground");

        AssertObjectReference(root, "sunNodesRoot");
        AssertObjectReference(root, "planetNodesRoot");
        AssertObjectReference(root, "systemExitsNodesRoot");

        AssertObjectReference(root, "playerRoot");
        AssertObjectReference(root, "enemiesRoot");
        AssertObjectReference(root, "alliesRoot");
        AssertObjectReference(root, "projectilesRoot");
        AssertObjectReference(root, "vfxRoot");

        AssertObjectReference(root, "boundsRoot");
        AssertObjectReference(root, "markersRoot");
        AssertObjectReference(root, "debugRoot");

        AssertObjectReference(root, "metaCanvas");
        AssertObjectReference(root, "systemMapHudRoot");
        AssertObjectReference(root, "planetRoot");
        AssertObjectReference(root, "metaHudRoot");
    }

    [Test]
    public void TravelCameraAndInputVisualBindings_AreLinked()
    {
        Scene scene = OpenSystemScene();

        MonoBehaviour travelVisual =
            FindComponentByTypeName(scene, "SystemTravelVisualController2A");

        AssertObjectReference(travelVisual, "travelLineView");
        AssertObjectReference(travelVisual, "engineGlowView");

        MonoBehaviour camera =
            FindComponentByTypeName(scene, "SystemCameraController2A");

        AssertObjectReference(camera, "targetCamera");
        AssertObjectReference(camera, "shipTarget");
        AssertObjectReference(camera, "cameraConfig");

        MonoBehaviour cameraDrag =
            FindComponentByTypeName(scene, "SystemCameraDragInput2A");

        AssertObjectReference(cameraDrag, "cameraController");

        MonoBehaviour input =
            FindComponentByTypeName(scene, "PlayerInputBridge2A");

        AssertObjectReference(input, "inputActions");
        AssertObjectReference(input, "destinationAdapter");
        AssertArrayHasItems(input, "blockingUiRoots");

        MonoBehaviour presentation =
            FindComponentByTypeName(scene, "SystemControlPresentationBridge2A");

        AssertObjectReference(presentation, "cameraController");
    }

    [Test]
    public void MainVisualReferences_AreLinked()
    {
        Scene scene = OpenSystemScene();

        MonoBehaviour root =
            FindComponentByTypeName(scene, "SystemSceneRoot2A");

        Object background =
            GetObjectReference(root, "systemBackground");

        AssertHasVisualComponent(background, "systemBackground");

        MonoBehaviour travelVisual =
            FindComponentByTypeName(scene, "SystemTravelVisualController2A");

        Object travelLine =
            GetObjectReference(travelVisual, "travelLineView");

        Object engineGlow =
            GetObjectReference(travelVisual, "engineGlowView");

        AssertSceneObjectReference(travelLine, "travelLineView");
        AssertHasVisualComponent(engineGlow, "engineGlowView");
    }

    private static void AssertSceneObjectReference(
     Object referencedObject,
     string label
 )
    {
        GameObject gameObject =
            ToGameObject(referencedObject);

        Assert.IsNotNull(
            gameObject,
            $"{label} не является объектом сцены или компонентом."
        );

        Assert.IsTrue(
            gameObject.scene.IsValid(),
            $"{label} не находится в действительной сцене."
        );
    }

    [Test]
    public void RequiredVisualAssets_ExistInProject()
    {
        AssertAssetExists("Assets/Art/SystemFrame/Backgrounds/system_background_deep_space_01.png");
        AssertAssetExists("Assets/Art/SystemFrame/Backgrounds/system_background_deep_space_02.png");
        AssertAssetExists("Assets/Art/SystemFrame/Backgrounds/system_nebula_far_overlay_01.png");
        AssertAssetExists("Assets/Art/SystemFrame/Backgrounds/system_nebula_far_overlay_02.png");
        AssertAssetExists("Assets/Art/SystemFrame/Backgrounds/system_stars_near_overlay_01.png");
        AssertAssetExists("Assets/Art/SystemFrame/Backgrounds/system_foreground_dust_vignette_01.png");

        AssertAssetExists("Assets/Art/SystemFrame/ShipFx/ship_engine_glow_blue_01.png");
        AssertAssetExists("Assets/Art/SystemFrame/ShipFx/ship_shadow_soft_ellipse_01.png");
        AssertAssetExists("Assets/Art/SystemFrame/Materials/M_ShipEngineTrail.mat");

        AssertAssetExists("Assets/Art/Shadows/shadow_ship_soft_oval_01.png");
        AssertAssetExists("Assets/Art/Shadows/shadow_object_contact_soft_01.png");
        AssertAssetExists("Assets/Art/Shadows/shadow_planet_volume_overlay_01.png");
        AssertAssetExists("Assets/Art/Shadows/shadow_station_soft_mass_01.png");

        AssertAssetExists("Assets/Art/SystemExit/exit_gate_pseudo3d_01.png");
        AssertAssetExists("Assets/Art/SystemExit/exit_gate_pseudo3d_02.png");

        AssertAssetExists("Assets/Art/Suns/SunSprites/sun_yellow_pseudo3d_01.png");
        AssertAssetExists("Assets/Art/Suns/SunSprites/sun_red_pseudo3d_01.png");
        AssertAssetExists("Assets/Art/Suns/SunSprites/sun_blue_pseudo3d_01.png");
    }

    private static Scene OpenSystemScene()
    {
        Scene scene =
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single
            );

        Assert.IsTrue(
            scene.IsValid(),
            $"Scene is not valid: {ScenePath}"
        );

        return scene;
    }

    private static void AssertHasComponent(
        Scene scene,
        GameObject gameObject,
        string componentTypeName
    )
    {
        bool found =
            gameObject
                .GetComponents<MonoBehaviour>()
                .Any(component =>
                    component != null &&
                    component.GetType().Name == componentTypeName
                );

        Assert.IsTrue(
            found,
            $"{componentTypeName} not found on {GetPath(gameObject)}."
        );
    }

    private static MonoBehaviour FindComponentByTypeName(
        Scene scene,
        string componentTypeName
    )
    {
        return Resources
            .FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(component =>
                component != null &&
                component.gameObject.scene == scene &&
                component.GetType().Name == componentTypeName
            );
    }

    private static void AssertObjectReference(
        MonoBehaviour component,
        string fieldName
    )
    {
        Object value =
            GetObjectReference(component, fieldName);

        Assert.IsNotNull(
            value,
            $"{component.GetType().Name}.{fieldName} is not linked."
        );
    }

    private static Object GetObjectReference(
        MonoBehaviour component,
        string fieldName
    )
    {
        Assert.IsNotNull(component, "Component is null.");

        SerializedObject serializedObject =
            new SerializedObject(component);

        SerializedProperty property =
            serializedObject.FindProperty(fieldName);

        Assert.IsNotNull(
            property,
            $"{component.GetType().Name}.{fieldName} property not found."
        );

        Assert.AreEqual(
            SerializedPropertyType.ObjectReference,
            property.propertyType,
            $"{component.GetType().Name}.{fieldName} is not object reference."
        );

        return property.objectReferenceValue;
    }

    private static void AssertArrayHasItems(
        MonoBehaviour component,
        string fieldName
    )
    {
        SerializedObject serializedObject =
            new SerializedObject(component);

        SerializedProperty property =
            serializedObject.FindProperty(fieldName);

        Assert.IsNotNull(
            property,
            $"{component.GetType().Name}.{fieldName} property not found."
        );

        Assert.IsTrue(
            property.isArray,
            $"{component.GetType().Name}.{fieldName} is not array."
        );

        Assert.Greater(
            property.arraySize,
            0,
            $"{component.GetType().Name}.{fieldName} is empty."
        );
    }

    private static void AssertHasVisualComponent(
        Object referencedObject,
        string label
    )
    {
        GameObject gameObject =
            ToGameObject(referencedObject);

        Assert.IsNotNull(
            gameObject,
            $"{label} is not GameObject or Component."
        );

        bool hasVisual =
            gameObject.GetComponentInChildren<SpriteRenderer>(true) != null ||
            gameObject.GetComponentInChildren<Image>(true) != null ||
            gameObject.GetComponentInChildren<TrailRenderer>(true) != null ||
            gameObject.GetComponentInChildren<LineRenderer>(true) != null ||
            gameObject.GetComponentInChildren<ParticleSystem>(true) != null;

        Assert.IsTrue(
            hasVisual,
            $"{label} has no visible renderer/image/trail/particle component."
        );
    }

    private static GameObject ToGameObject(Object value)
    {
        if (value is GameObject gameObject)
            return gameObject;

        if (value is Component component)
            return component.gameObject;

        return null;
    }

    private static void AssertAssetExists(string path)
    {
        Object asset =
            AssetDatabase.LoadAssetAtPath<Object>(path);

        Assert.IsNotNull(
            asset,
            $"Required visual asset not found: {path}"
        );
    }

    private static IEnumerable<GameObject> GetAllSceneGameObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                yield return child.gameObject;
        }
    }

    private static string GetPath(GameObject gameObject)
    {
        List<string> names =
            new List<string>();

        Transform current =
            gameObject.transform;

        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();

        return string.Join("/", names);
    }
}

#endif