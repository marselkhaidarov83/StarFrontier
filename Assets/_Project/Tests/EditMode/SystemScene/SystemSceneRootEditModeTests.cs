using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SystemSceneRootEditModeTests
{
    [Test]
    public void HasRequiredReferences_WithMissingReferences_ReturnsFalse()
    {
        GameObject rootObject =
            new GameObject("SystemSceneRoot");

        SystemSceneRoot2A root =
            rootObject.AddComponent<SystemSceneRoot2A>();

        Assert.IsFalse(root.HasRequiredReferences());

        Object.DestroyImmediate(rootObject);
    }

    [Test]
    public void HasRequiredReferences_WithAllReferences_ReturnsTrue()
    {
        GameObject rootObject =
            new GameObject("SystemSceneRoot");

        SystemSceneRoot2A root =
            rootObject.AddComponent<SystemSceneRoot2A>();

        Camera camera =
            new GameObject("Main Camera")
                .AddComponent<Camera>();

        Transform worldRoot =
            new GameObject("WorldRoot").transform;

        Transform backgroundRoot =
            new GameObject("BackgroundRoot").transform;

        Transform boundsRoot =
            new GameObject("BoundsRoot").transform;

        Transform objectsRoot =
            new GameObject("ObjectsRoot").transform;

        Transform playerRoot =
            new GameObject("PlayerRoot").transform;

        Transform markersRoot =
            new GameObject("MarkersRoot").transform;

        Transform vfxRoot =
            new GameObject("VfxRoot").transform;

        Transform uiRoot =
            new GameObject("UiRoot").transform;

        Transform debugRoot =
            new GameObject("DebugRoot").transform;

        SetPrivateField(root, "mainCamera", camera);
        SetPrivateField(root, "worldRoot", worldRoot);
        SetPrivateField(root, "backgroundRoot", backgroundRoot);
        SetPrivateField(root, "boundsRoot", boundsRoot);
        SetPrivateField(root, "objectsRoot", objectsRoot);
        SetPrivateField(root, "playerRoot", playerRoot);
        SetPrivateField(root, "markersRoot", markersRoot);
        SetPrivateField(root, "vfxRoot", vfxRoot);
        SetPrivateField(root, "uiRoot", uiRoot);
        SetPrivateField(root, "debugRoot", debugRoot);

        Assert.IsTrue(root.HasRequiredReferences());

        Object.DestroyImmediate(rootObject);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(worldRoot.gameObject);
        Object.DestroyImmediate(backgroundRoot.gameObject);
        Object.DestroyImmediate(boundsRoot.gameObject);
        Object.DestroyImmediate(objectsRoot.gameObject);
        Object.DestroyImmediate(playerRoot.gameObject);
        Object.DestroyImmediate(markersRoot.gameObject);
        Object.DestroyImmediate(vfxRoot.gameObject);
        Object.DestroyImmediate(uiRoot.gameObject);
        Object.DestroyImmediate(debugRoot.gameObject);
    }

    [Test]
    public void ConfigureCamera_SetsOrthographicCamera()
    {
        GameObject rootObject =
            new GameObject("SystemSceneRoot");

        SystemSceneRoot2A root =
            rootObject.AddComponent<SystemSceneRoot2A>();

        Camera camera =
            new GameObject("Main Camera")
                .AddComponent<Camera>();

        camera.orthographic = false;
        camera.orthographicSize = 0f;
        camera.transform.position =
            new Vector3(10f, 20f, 30f);

        SetPrivateField(root, "mainCamera", camera);
        SetPrivateField(root, "defaultOrthographicSize", 6.5f);

        root.ConfigureCamera();

        Assert.IsTrue(camera.orthographic);
        Assert.AreEqual(6.5f, camera.orthographicSize);
        Assert.AreEqual(
            new Vector3(0f, 0f, -10f),
            camera.transform.position);

        Object.DestroyImmediate(rootObject);
        Object.DestroyImmediate(camera.gameObject);
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance
                | BindingFlags.NonPublic);

        Assert.IsNotNull(
            field,
            $"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }
}