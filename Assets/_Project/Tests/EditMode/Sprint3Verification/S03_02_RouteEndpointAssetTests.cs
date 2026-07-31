#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_RouteEndpointAssetTests
{
    private const float DirectionDotThreshold = 0.5f;

    [Test]
    public void RouteEndpoint_Schema_ContainsEntryAndExit()
    {
        Type endpointType =
            SfTestReflection.RequireType(
                "RouteEndpointConfig");

        AssertMember(endpointType, "ExitPoint");
        AssertMember(endpointType, "EntryPoint");

        Type routeType =
            SfTestReflection.RequireType(
                "RouteConfig");

        AssertMember(
            routeType,
            "FromSystemRouteEndpointConfig");

        AssertMember(
            routeType,
            "ToSystemRouteEndpointConfig");

        string[] methods =
        {
            "GetDepartureEndpoint",
            "GetArrivalEndpoint",
            "GetExitPoint",
            "GetEntryPoint"
        };

        foreach (string methodName in methods)
        {
            Assert.That(
                routeType.GetMethods()
                    .Any(method =>
                        method.Name == methodName),
                Is.True,
                "RouteConfig must contain " +
                methodName + ".");
        }
    }

    [Test]
    public void RouteValidator_Exists()
    {
        Type validatorType =
            SfTestReflection.RequireType(
                "RouteEndpointValidator2A");

        bool hasValidationEntry =
            validatorType.GetMethods(
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Any(method =>
                    method.Name.Contains(
                        "Validate",
                        StringComparison.Ordinal));

        Assert.That(
            hasValidationEntry,
            Is.True);
    }

    [Test]
    public void AllActiveRoutes_HaveValidSystemsDistanceAndEndpoints()
    {
        Type routeType =
            SfTestReflection.RequireType(
                "RouteConfig");

        UnityEngine.Object[] routes =
            LoadActiveAssets(routeType);

        Assert.That(
            routes.Length,
            Is.GreaterThan(0),
            "No active RouteConfig assets were found.");

        HashSet<string> routeIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        HashSet<string> pairs =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (UnityEngine.Object route in routes)
        {
            string path =
                AssetDatabase.GetAssetPath(route);

            string routeId =
                Convert.ToString(
                    SfTestReflection.GetMemberValue(
                        route,
                        "Id"));

            Assert.That(
                string.IsNullOrWhiteSpace(routeId),
                Is.False,
                "Empty Route ID: " + path);

            Assert.That(
                routeIds.Add(routeId.Trim()),
                Is.True,
                "Duplicate Route ID: " + routeId);

            object fromSystem =
                SfTestReflection.GetMemberValue(
                    route,
                    "FromSystem");

            object toSystem =
                SfTestReflection.GetMemberValue(
                    route,
                    "ToSystem");

            Assert.That(
                fromSystem,
                Is.Not.Null,
                "FromSystem is missing: " + path);

            Assert.That(
                toSystem,
                Is.Not.Null,
                "ToSystem is missing: " + path);

            string fromId =
                Convert.ToString(
                    SfTestReflection.GetMemberValue(
                        fromSystem,
                        "Id"));

            string toId =
                Convert.ToString(
                    SfTestReflection.GetMemberValue(
                        toSystem,
                        "Id"));

            Assert.That(
                fromId,
                Is.Not.EqualTo(toId),
                "Route connects a system to itself: " +
                path);

            string pair =
                string.CompareOrdinal(
                    fromId,
                    toId) <= 0
                    ? fromId + "|" + toId
                    : toId + "|" + fromId;

            Assert.That(
                pairs.Add(pair),
                Is.True,
                "Duplicate route pair: " + pair);

            int parsecDistance =
                Convert.ToInt32(
                    SfTestReflection.GetMemberValue(
                        route,
                        "ParsecDistance"));

            Assert.That(
                parsecDistance,
                Is.GreaterThan(0),
                "ParsecDistance must be > 0: " +
                path);

            object fromEndpoint =
                SfTestReflection.GetMemberValue(
                    route,
                    "FromSystemRouteEndpointConfig");

            object toEndpoint =
                SfTestReflection.GetMemberValue(
                    route,
                    "ToSystemRouteEndpointConfig");

            Assert.That(
                fromEndpoint,
                Is.Not.Null,
                "From endpoint is missing: " + path);

            Assert.That(
                toEndpoint,
                Is.Not.Null,
                "To endpoint is missing: " + path);

            AssertRouteIsBoundToSystem(
                fromSystem,
                route,
                "FromSystem",
                path);

            AssertRouteIsBoundToSystem(
                toSystem,
                route,
                "ToSystem",
                path);

            ValidateEndpointDirections(
                route,
                fromSystem,
                toSystem,
                fromEndpoint,
                toEndpoint,
                path);
        }
    }

    [Test]
    public void ActiveRouteGraph_IsConnected()
    {
        Type routeType =
            SfTestReflection.RequireType(
                "RouteConfig");

        UnityEngine.Object[] routes =
            LoadActiveAssets(routeType);

        Dictionary<string, HashSet<string>>
            graph =
                new Dictionary<
                    string,
                    HashSet<string>>(
                        StringComparer.Ordinal);

        foreach (UnityEngine.Object route in routes)
        {
            object fromSystem =
                SfTestReflection.GetMemberValue(
                    route,
                    "FromSystem");

            object toSystem =
                SfTestReflection.GetMemberValue(
                    route,
                    "ToSystem");

            if (fromSystem == null ||
                toSystem == null)
            {
                continue;
            }

            string fromId =
                Convert.ToString(
                    SfTestReflection.GetMemberValue(
                        fromSystem,
                        "Id"));

            string toId =
                Convert.ToString(
                    SfTestReflection.GetMemberValue(
                        toSystem,
                        "Id"));

            AddEdge(graph, fromId, toId);
            AddEdge(graph, toId, fromId);
        }

        Assert.That(graph.Count, Is.GreaterThan(0));

        string start =
            graph.Keys
                .OrderBy(id => id)
                .First();

        HashSet<string> visited =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                start
            };

        Queue<string> queue =
            new Queue<string>();

        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            foreach (string next in graph[current])
            {
                if (visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        CollectionAssert.AreEquivalent(
            graph.Keys,
            visited,
            "Route graph is disconnected.");
    }

    private static void ValidateEndpointDirections(
        object route,
        object fromSystem,
        object toSystem,
        object fromEndpoint,
        object toEndpoint,
        string path)
    {
        Vector2 fromPosition;
        Vector2 toPosition;

        bool hasFromPosition =
            TryGetMapPosition(
                fromSystem,
                out fromPosition);

        bool hasToPosition =
            TryGetMapPosition(
                toSystem,
                out toPosition);

        if (!hasFromPosition ||
            !hasToPosition)
        {
            Assert.Inconclusive(
                "mapPosition was not readable for route: " +
                path);

            return;
        }

        Vector2 direction =
            toPosition - fromPosition;

        Assert.That(
            direction.sqrMagnitude,
            Is.GreaterThan(0.0001f),
            "Systems have equal mapPosition: " +
            path);

        direction.Normalize();

        Vector3 fromExit =
            (Vector3)SfTestReflection
                .GetMemberValue(
                    fromEndpoint,
                    "ExitPoint");

        Vector3 fromEntry =
            (Vector3)SfTestReflection
                .GetMemberValue(
                    fromEndpoint,
                    "EntryPoint");

        Vector3 toExit =
            (Vector3)SfTestReflection
                .GetMemberValue(
                    toEndpoint,
                    "ExitPoint");

        Vector3 toEntry =
            (Vector3)SfTestReflection
                .GetMemberValue(
                    toEndpoint,
                    "EntryPoint");

        AssertDirection(
            fromExit,
            direction,
            "From.Exit",
            path);

        AssertDirection(
            toEntry,
            -direction,
            "To.Entry",
            path);

        AssertDirection(
            toExit,
            -direction,
            "To.Exit",
            path);

        AssertDirection(
            fromEntry,
            direction,
            "From.Entry",
            path);
    }

    private static void AssertDirection(
        Vector3 point,
        Vector2 expected,
        string label,
        string path)
    {
        Vector2 actual =
            new Vector2(point.x, point.y);

        Assert.That(
            actual.sqrMagnitude,
            Is.GreaterThan(0.0001f),
            label + " is at the system center: " +
            path);

        actual.Normalize();

        float dot =
            Vector2.Dot(actual, expected);

        Assert.That(
            dot,
            Is.GreaterThanOrEqualTo(
                DirectionDotThreshold),
            label + " has incorrect direction. " +
            "dot=" + dot.ToString("0.000") +
            " route=" + path);
    }

    private static bool TryGetMapPosition(
        object system,
        out Vector2 value)
    {
        value = Vector2.zero;

        if (SfTestReflection.TryGetMemberValue(
                system,
                out object raw,
                "MapPosition",
                "mapPosition"))
        {
            if (raw is Vector2 vector2)
            {
                value = vector2;
                return true;
            }

            if (raw is Vector3 vector3)
            {
                value =
                    new Vector2(
                        vector3.x,
                        vector3.y);

                return true;
            }
        }

        SerializedObject serialized =
            new SerializedObject(
                (UnityEngine.Object)system);

        SerializedProperty property =
            serialized.FindProperty(
                "mapPosition");

        if (property == null)
            return false;

        if (property.propertyType ==
            SerializedPropertyType.Vector2)
        {
            value = property.vector2Value;
            return true;
        }

        if (property.propertyType ==
            SerializedPropertyType.Vector3)
        {
            Vector3 vector3 =
                property.vector3Value;

            value =
                new Vector2(
                    vector3.x,
                    vector3.y);

            return true;
        }

        return false;
    }

    private static void AssertRouteIsBoundToSystem(
        object system,
        object route,
        string side,
        string path)
    {
        object routesValue =
            SfTestReflection.GetMemberValue(
                system,
                "Routes");

        Assert.That(
            routesValue,
            Is.InstanceOf<IEnumerable>(),
            side + ".Routes is unavailable: " +
            path);

        bool contains =
            ((IEnumerable)routesValue)
                .Cast<object>()
                .Any(item =>
                    ReferenceEquals(item, route));

        Assert.That(
            contains,
            Is.True,
            "Route is missing from " +
            side + ".Routes: " + path);
    }

    private static UnityEngine.Object[]
        LoadActiveAssets(Type type)
    {
        return AssetDatabase
            .FindAssets("t:" + type.Name)
            .Select(
                AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                !ShouldSkip(path))
            .Select(path =>
                AssetDatabase.LoadAssetAtPath(
                    path,
                    type))
            .Where(asset =>
                asset != null)
            .ToArray();
    }

    private static bool ShouldSkip(
        string path)
    {
        string normalized =
            path.Replace('\\', '/')
                .ToLowerInvariant();

        return
            normalized.Contains("/old/") ||
            normalized.Contains("/olds/") ||
            normalized.Contains("/archive/") ||
            normalized.Contains("/archived/") ||
            normalized.Contains("/deprecated/");
    }

    private static void AddEdge(
        IDictionary<string, HashSet<string>>
            graph,
        string from,
        string to)
    {
        if (!graph.TryGetValue(
                from,
                out HashSet<string> neighbors))
        {
            neighbors =
                new HashSet<string>(
                    StringComparer.Ordinal);

            graph.Add(from, neighbors);
        }

        neighbors.Add(to);
    }

    private static void AssertMember(
        Type type,
        string memberName)
    {
        bool found =
            type.GetMember(
                memberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Length > 0;

        Assert.That(
            found,
            Is.True,
            type.Name + " must expose " +
            memberName + ".");
    }
}

#endif
