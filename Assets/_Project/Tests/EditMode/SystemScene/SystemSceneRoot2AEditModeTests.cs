using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SystemSceneRoot2AEditModeTests
{
    [Test]
    public void HasRequiredReferences_WithMissingReferences_ReturnsFalse()
    {
        GameObject rootObject =
            new GameObject("SystemMapRoot");

        SystemSceneRoot2A root =
            rootObject.AddComponent<SystemSceneRoot2A>();

        Assert.IsFalse(root.HasRequiredReferences());

        Object.DestroyImmediate(rootObject);
    }

    [Test]
    public void HasRequiredReferences_WithAllReferences_ReturnsTrue()
    {
        GameObject rootObject =
            new GameObject("SystemMapRoot");

        SystemSceneRoot2A root =
            rootObject.AddComponent<SystemSceneRoot2A>();

        Camera camera =
            new GameObject("MainCamera")
                .AddComponent<Camera>();

        GameObject background =
            new GameObject("SystemMapBackground");

        Transform sunNodesRoot =
            new GameObject("SunNodesRoot").transform;

        Transform planetNodesRoot =
            new GameObject("PlanetNodesRoot").transform;

        Transform exitsRoot =
            new GameObject("SystemExitsNodesRoot").transform;

        Transform playerRoot =
            new GameObject("PlayerRoot").transform;

        Transform enemiesRoot =
            new GameObject("EnemiesRoot").transform;

        Transform alliesRoot =
            new GameObject("AlliesRoot").transform;

        Transform projectilesRoot =
            new GameObject("ProjectilesRoot").transform;

        Transform vfxRoot =
            new GameObject("VfxRoot").transform;

        Transform boundsRoot =
            new GameObject("BoundsRoot").transform;

        Transform markersRoot =
            new GameObject("MarkersRoot").transform;

        Transform debugRoot =
            new GameObject("DebugRoot").transform;

        Canvas canvas =
            new GameObject("Canvas")
                .AddComponent<Canvas>();

        RectTransform systemMapHud =
            new GameObject("SystemMapHUD")
                .AddComponent<RectTransform>();

        RectTransform planetRoot =
            new GameObject("PlanetRoot")
                .AddComponent<RectTransform>();

        RectTransform metaHudRoot =
            new GameObject("MetaHudRoot")
                .AddComponent<RectTransform>();

        SetPrivateField(root, "systemMapRoot", rootObject);
        SetPrivateField(root, "mainCamera", camera);

        SetPrivateField(root, "systemBackground", background);
        SetPrivateField(root, "sunNodesRoot", sunNodesRoot);
        SetPrivateField(root, "planetNodesRoot", planetNodesRoot);
        SetPrivateField(root, "systemExitsNodesRoot", exitsRoot);
        SetPrivateField(root, "playerRoot", playerRoot);
        SetPrivateField(root, "enemiesRoot", enemiesRoot);
        SetPrivateField(root, "alliesRoot", alliesRoot);
        SetPrivateField(root, "projectilesRoot", projectilesRoot);
        SetPrivateField(root, "vfxRoot", vfxRoot);

        SetPrivateField(root, "boundsRoot", boundsRoot);
        SetPrivateField(root, "markersRoot", markersRoot);
        SetPrivateField(root, "debugRoot", debugRoot);

        SetPrivateField(root, "metaCanvas", canvas);
        SetPrivateField(root, "systemMapHudRoot", systemMapHud);
        SetPrivateField(root, "planetRoot", planetRoot);
        SetPrivateField(root, "metaHudRoot", metaHudRoot);

        Assert.IsTrue(root.HasRequiredReferences());

        Object.DestroyImmediate(rootObject);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(background);
        Object.DestroyImmediate(sunNodesRoot.gameObject);
        Object.DestroyImmediate(planetNodesRoot.gameObject);
        Object.DestroyImmediate(exitsRoot.gameObject);
        Object.DestroyImmediate(playerRoot.gameObject);
        Object.DestroyImmediate(enemiesRoot.gameObject);
        Object.DestroyImmediate(alliesRoot.gameObject);
        Object.DestroyImmediate(projectilesRoot.gameObject);
        Object.DestroyImmediate(vfxRoot.gameObject);
        Object.DestroyImmediate(boundsRoot.gameObject);
        Object.DestroyImmediate(markersRoot.gameObject);
        Object.DestroyImmediate(debugRoot.gameObject);
        Object.DestroyImmediate(canvas.gameObject);
        Object.DestroyImmediate(systemMapHud.gameObject);
        Object.DestroyImmediate(planetRoot.gameObject);
        Object.DestroyImmediate(metaHudRoot.gameObject);
    }

    [Test]
    public void GetMissingReferencesReport_ContainsMissingFieldNames()
    {
        GameObject rootObject =
            new GameObject("SystemMapRoot");

        SystemSceneRoot2A root =
            rootObject.AddComponent<SystemSceneRoot2A>();

        string report =
            root.GetMissingReferencesReport();

        Assert.IsTrue(report.Contains("systemMapRoot"));
        Assert.IsTrue(report.Contains("mainCamera"));
        Assert.IsTrue(report.Contains("systemBackground"));
        Assert.IsTrue(report.Contains("sunNodesRoot"));
        Assert.IsTrue(report.Contains("planetNodesRoot"));
        Assert.IsTrue(report.Contains("systemExitsNodesRoot"));

        Object.DestroyImmediate(rootObject);
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