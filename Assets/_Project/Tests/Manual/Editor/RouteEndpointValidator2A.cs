#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-validator маршрутов Stage 2A.
///
/// Проверяет:
/// - уникальность Route ID;
/// - уникальность пары систем;
/// - FromSystem и ToSystem;
/// - наличие Route в обеих системах;
/// - ParsecDistance;
/// - обе ссылки RouteEndpointConfig;
/// - валидность ExitPoint и EntryPoint;
/// - соответствие endpoint направлению систем
///   на карте галактики;
/// - связность графа систем;
/// - orphan routes и orphan systems.
///
/// Скрипт работает только в Unity Editor
/// и не попадает в Android build.
/// </summary>
public static class RouteEndpointValidator2A
{
    private const string MenuRoot =
        "STAR FRONTIER/Diagnostics/";

    private const float PositionEpsilon =
        0.001f;

    /*
     * cos(60°) = 0.5.
     *
     * Endpoint считается направленным правильно,
     * если отклонение от ожидаемого направления
     * не превышает примерно 60 градусов.
     */
    private const float MinimumDirectionDot =
        0.5f;

    private enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    private sealed class ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Code;
        public string Message;
        public UnityEngine.Object Context;

        public ValidationIssue(
            ValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Context = context;
        }
    }

    private sealed class ValidationReport
    {
        public int RouteCount;
        public int SystemCount;
        public int SkippedRouteCount;
        public int SkippedSystemCount;

        public readonly List<ValidationIssue>
            Issues =
                new List<ValidationIssue>();

        public int ErrorCount =>
            Issues.Count(
                issue =>
                    issue.Severity ==
                    ValidationSeverity.Error);

        public int WarningCount =>
            Issues.Count(
                issue =>
                    issue.Severity ==
                    ValidationSeverity.Warning);

        public bool HasErrors =>
            ErrorCount > 0;

        public void Add(
            ValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context = null)
        {
            Issues.Add(
                new ValidationIssue(
                    severity,
                    code,
                    message,
                    context));
        }

        public string BuildText()
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "STAR FRONTIER — ROUTE ENDPOINT VALIDATION");

            builder.AppendLine(
                "Generated UTC: " +
                DateTime.UtcNow.ToString("O"));

            builder.AppendLine();

            builder.AppendLine(
                "Routes checked: " +
                RouteCount);

            builder.AppendLine(
                "Systems checked: " +
                SystemCount);

            builder.AppendLine(
                "Archived routes skipped: " +
                SkippedRouteCount);

            builder.AppendLine(
                "Archived systems skipped: " +
                SkippedSystemCount);

            builder.AppendLine(
                "Errors: " +
                ErrorCount);

            builder.AppendLine(
                "Warnings: " +
                WarningCount);

            builder.AppendLine();

            if (Issues.Count == 0)
            {
                builder.AppendLine(
                    "RESULT: PASSED");

                builder.AppendLine(
                    "No validation issues found.");

                return builder.ToString();
            }

            builder.AppendLine(
                HasErrors
                    ? "RESULT: FAILED"
                    : "RESULT: PASSED WITH WARNINGS");

            builder.AppendLine();

            foreach (
                ValidationIssue issue
                in Issues)
            {
                string assetPath =
                    issue.Context != null
                        ? AssetDatabase
                            .GetAssetPath(
                                issue.Context)
                        : string.Empty;

                builder.Append(
                    "[" +
                    issue.Severity +
                    "] ");

                builder.Append(
                    issue.Code +
                    ": ");

                builder.AppendLine(
                    issue.Message);

                if (!string.IsNullOrWhiteSpace(
                        assetPath))
                {
                    builder.AppendLine(
                        "Asset: " +
                        assetPath);
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }

    [MenuItem(
        MenuRoot +
        "Validate Routes and Endpoints")]
    public static void ValidateAndLog()
    {
        ValidationReport report =
            BuildReport();

        LogReport(report);
        SelectFirstError(report);

        EditorUtility.DisplayDialog(
            "Route validation",
            BuildDialogMessage(report),
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Routes and Save Report")]
    public static void ValidateAndSaveReport()
    {
        ValidationReport report =
            BuildReport();

        LogReport(report);
        SelectFirstError(report);

        string reportPath =
            SaveReport(report);

        EditorUtility.RevealInFinder(
            reportPath);

        EditorUtility.DisplayDialog(
            "Route validation",
            BuildDialogMessage(report) +
            "\n\nReport:\n" +
            reportPath,
            "OK");
    }

    private static ValidationReport
        BuildReport()
    {
        ValidationReport report =
            new ValidationReport();

        List<RouteConfig> routes =
            LoadActiveAssets<RouteConfig>(
                out int skippedRoutes);

        List<StarSystemConfig> systems =
            LoadActiveAssets<StarSystemConfig>(
                out int skippedSystems);

        report.RouteCount =
            routes.Count;

        report.SystemCount =
            systems.Count;

        report.SkippedRouteCount =
            skippedRoutes;

        report.SkippedSystemCount =
            skippedSystems;

        if (routes.Count == 0)
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-000",
                "No active RouteConfig assets found.");
        }

        if (systems.Count == 0)
        {
            report.Add(
                ValidationSeverity.Error,
                "SYSTEM-000",
                "No active StarSystemConfig assets found.");
        }

        Dictionary<string, StarSystemConfig>
            systemsById =
                ValidateSystemIds(
                    systems,
                    report);

        ValidateRoutes(
            routes,
            systems,
            systemsById,
            report);

        ValidateGraphConnectivity(
            routes,
            systems,
            report);

        return report;
    }

    private static Dictionary<
        string,
        StarSystemConfig>
        ValidateSystemIds(
            IReadOnlyList<StarSystemConfig> systems,
            ValidationReport report)
    {
        Dictionary<string, StarSystemConfig>
            result =
                new Dictionary<
                    string,
                    StarSystemConfig>(
                        StringComparer.Ordinal);

        foreach (
            StarSystemConfig system
            in systems)
        {
            if (system == null)
                continue;

            string systemId =
                system.Id;

            if (string.IsNullOrWhiteSpace(
                    systemId))
            {
                report.Add(
                    ValidationSeverity.Error,
                    "SYSTEM-001",
                    "StarSystemConfig has an empty ID.",
                    system);

                continue;
            }

            systemId =
                systemId.Trim();

            if (result.TryGetValue(
                    systemId,
                    out StarSystemConfig
                        existingSystem))
            {
                report.Add(
                    ValidationSeverity.Error,
                    "SYSTEM-002",
                    "Duplicate system ID '" +
                    systemId +
                    "'. First asset: " +
                    AssetDatabase.GetAssetPath(
                        existingSystem),
                    system);

                continue;
            }

            result.Add(
                systemId,
                system);
        }

        return result;
    }

    private static void ValidateRoutes(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<StarSystemConfig> systems,
        IReadOnlyDictionary<
            string,
            StarSystemConfig> systemsById,
        ValidationReport report)
    {
        HashSet<StarSystemConfig>
            systemReferences =
                new HashSet<StarSystemConfig>(
                    systems);

        Dictionary<string, RouteConfig>
            routesById =
                new Dictionary<
                    string,
                    RouteConfig>(
                        StringComparer.Ordinal);

        Dictionary<string, RouteConfig>
            routesBySystemPair =
                new Dictionary<
                    string,
                    RouteConfig>(
                        StringComparer.Ordinal);

        foreach (
            RouteConfig route
            in routes)
        {
            if (route == null)
                continue;

            ValidateRouteId(
                route,
                routesById,
                report);

            ValidateRouteSystems(
                route,
                systemReferences,
                systemsById,
                routesBySystemPair,
                report);

            ValidateRouteDistance(
                route,
                report);

            ValidateRouteEndpoints(
                route,
                report);
        }
    }

    private static void ValidateRouteId(
        RouteConfig route,
        IDictionary<string, RouteConfig>
            routesById,
        ValidationReport report)
    {
        string routeId =
            route.Id;

        if (string.IsNullOrWhiteSpace(
                routeId))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-001",
                "RouteConfig has an empty ID.",
                route);

            return;
        }

        routeId =
            routeId.Trim();

        if (routesById.TryGetValue(
                routeId,
                out RouteConfig existingRoute))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-002",
                "Duplicate Route ID '" +
                routeId +
                "'. First asset: " +
                AssetDatabase.GetAssetPath(
                    existingRoute),
                route);

            return;
        }

        routesById.Add(
            routeId,
            route);
    }

    private static void ValidateRouteSystems(
        RouteConfig route,
        ISet<StarSystemConfig>
            activeSystems,
        IReadOnlyDictionary<
            string,
            StarSystemConfig> systemsById,
        IDictionary<string, RouteConfig>
            routesBySystemPair,
        ValidationReport report)
    {
        StarSystemConfig fromSystem =
            route.FromSystem;

        StarSystemConfig toSystem =
            route.ToSystem;

        if (fromSystem == null)
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-010",
                "FromSystem is not assigned.",
                route);
        }

        if (toSystem == null)
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-011",
                "ToSystem is not assigned.",
                route);
        }

        if (fromSystem == null ||
            toSystem == null)
        {
            return;
        }

        string fromId =
            fromSystem.Id;

        string toId =
            toSystem.Id;

        if (string.IsNullOrWhiteSpace(
                fromId))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-012",
                "FromSystem has an empty ID.",
                route);
        }

        if (string.IsNullOrWhiteSpace(
                toId))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-013",
                "ToSystem has an empty ID.",
                route);
        }

        if (fromSystem == toSystem ||
            (!string.IsNullOrWhiteSpace(
                 fromId) &&
             string.Equals(
                 fromId,
                 toId,
                 StringComparison.Ordinal)))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-014",
                "Route connects a system to itself.",
                route);
        }

        if (!activeSystems.Contains(
                fromSystem))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-015",
                "FromSystem points to an archived, " +
                "excluded or missing system asset.",
                route);
        }

        if (!activeSystems.Contains(
                toSystem))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-016",
                "ToSystem points to an archived, " +
                "excluded or missing system asset.",
                route);
        }

        if (!string.IsNullOrWhiteSpace(
                fromId) &&
            !systemsById.ContainsKey(
                fromId.Trim()))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-017",
                "FromSystem ID was not found in " +
                "the active system catalog: " +
                fromId,
                route);
        }

        if (!string.IsNullOrWhiteSpace(
                toId) &&
            !systemsById.ContainsKey(
                toId.Trim()))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-018",
                "ToSystem ID was not found in " +
                "the active system catalog: " +
                toId,
                route);
        }

        if (!ContainsRoute(
                fromSystem,
                route))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-019",
                "Route is not present in " +
                "FromSystem.Routes.",
                route);
        }

        if (!ContainsRoute(
                toSystem,
                route))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-020",
                "Route is not present in " +
                "ToSystem.Routes.",
                route);
        }

        if (string.IsNullOrWhiteSpace(
                fromId) ||
            string.IsNullOrWhiteSpace(
                toId))
        {
            return;
        }

        string pairKey =
            BuildSystemPairKey(
                fromId.Trim(),
                toId.Trim());

        if (routesBySystemPair.TryGetValue(
                pairKey,
                out RouteConfig existingRoute))
        {
            report.Add(
                ValidationSeverity.Error,
                "ROUTE-021",
                "Duplicate route between systems " +
                fromId +
                " and " +
                toId +
                ". First asset: " +
                AssetDatabase.GetAssetPath(
                    existingRoute),
                route);

            return;
        }

        routesBySystemPair.Add(
            pairKey,
            route);
    }

    private static void ValidateRouteDistance(
        RouteConfig route,
        ValidationReport report)
    {
        if (route.ParsecDistance > 0)
            return;

        report.Add(
            ValidationSeverity.Error,
            "ROUTE-030",
            "ParsecDistance must be greater " +
            "than zero. Current value: " +
            route.ParsecDistance,
            route);
    }

    private static void ValidateRouteEndpoints(
        RouteConfig route,
        ValidationReport report)
    {
        RouteEndpointConfig fromEndpoint =
            route
                .FromSystemRouteEndpointConfig;

        RouteEndpointConfig toEndpoint =
            route
                .ToSystemRouteEndpointConfig;

        if (fromEndpoint == null)
        {
            report.Add(
                ValidationSeverity.Error,
                "ENDPOINT-001",
                "From System Route Endpoint " +
                "Config is not assigned.",
                route);
        }

        if (toEndpoint == null)
        {
            report.Add(
                ValidationSeverity.Error,
                "ENDPOINT-002",
                "To System Route Endpoint " +
                "Config is not assigned.",
                route);
        }

        if (fromEndpoint == null ||
            toEndpoint == null)
        {
            return;
        }

        if (fromEndpoint == toEndpoint)
        {
            report.Add(
                ValidationSeverity.Warning,
                "ENDPOINT-003",
                "The same RouteEndpointConfig " +
                "is assigned to both systems. " +
                "Confirm this is intentional.",
                route);
        }

        ValidatePoint(
            fromEndpoint.ExitPoint,
            "FromSystem endpoint ExitPoint",
            "ENDPOINT-010",
            route,
            report);

        ValidatePoint(
            fromEndpoint.EntryPoint,
            "FromSystem endpoint EntryPoint",
            "ENDPOINT-011",
            route,
            report);

        ValidatePoint(
            toEndpoint.ExitPoint,
            "ToSystem endpoint ExitPoint",
            "ENDPOINT-012",
            route,
            report);

        ValidatePoint(
            toEndpoint.EntryPoint,
            "ToSystem endpoint EntryPoint",
            "ENDPOINT-013",
            route,
            report);

        if (route.FromSystem == null ||
            route.ToSystem == null)
        {
            return;
        }

        bool hasFromMapPosition =
            TryGetMapPosition(
                route.FromSystem,
                out Vector2 fromMapPosition);

        bool hasToMapPosition =
            TryGetMapPosition(
                route.ToSystem,
                out Vector2 toMapPosition);

        if (hasFromMapPosition &&
            hasToMapPosition)
        {
            ValidateEndpointsAgainstGalaxyMap(
                route,
                fromEndpoint,
                toEndpoint,
                fromMapPosition,
                toMapPosition,
                report);

            return;
        }

        /*
         * Fallback:
         * даже если mapPosition недоступен,
         * проверяем, что прямой выход и вход
         * находятся на противоположных сторонах.
         */
        report.Add(
            ValidationSeverity.Warning,
            "ENDPOINT-020",
            "mapPosition could not be read from " +
            "one or both StarSystemConfig assets. " +
            "Direction is checked only by " +
            "endpoint opposition.",
            route);

        ValidateOppositeDirections(
            fromEndpoint.ExitPoint,
            toEndpoint.EntryPoint,
            "From.Exit → To.Entry",
            "ENDPOINT-021",
            route,
            report);

        ValidateOppositeDirections(
            toEndpoint.ExitPoint,
            fromEndpoint.EntryPoint,
            "To.Exit → From.Entry",
            "ENDPOINT-022",
            route,
            report);
    }

    private static void
        ValidateEndpointsAgainstGalaxyMap(
            RouteConfig route,
            RouteEndpointConfig fromEndpoint,
            RouteEndpointConfig toEndpoint,
            Vector2 fromMapPosition,
            Vector2 toMapPosition,
            ValidationReport report)
    {
        Vector2 directMapDirection =
            toMapPosition -
            fromMapPosition;

        if (directMapDirection.sqrMagnitude <=
            PositionEpsilon *
            PositionEpsilon)
        {
            report.Add(
                ValidationSeverity.Error,
                "ENDPOINT-030",
                "FromSystem and ToSystem have " +
                "the same mapPosition.",
                route);

            return;
        }

        directMapDirection.Normalize();

        /*
         * A → B:
         *
         * A.Exit смотрит в сторону B.
         * B.Entry находится на стороне A.
         */
        ValidateDirectionAlignment(
            fromEndpoint.ExitPoint,
            directMapDirection,
            "FromSystem.ExitPoint",
            "ENDPOINT-031",
            route,
            report);

        ValidateDirectionAlignment(
            toEndpoint.EntryPoint,
            -directMapDirection,
            "ToSystem.EntryPoint",
            "ENDPOINT-032",
            route,
            report);

        /*
         * B → A:
         *
         * B.Exit смотрит в сторону A.
         * A.Entry находится на стороне B.
         */
        ValidateDirectionAlignment(
            toEndpoint.ExitPoint,
            -directMapDirection,
            "ToSystem.ExitPoint",
            "ENDPOINT-033",
            route,
            report);

        ValidateDirectionAlignment(
            fromEndpoint.EntryPoint,
            directMapDirection,
            "FromSystem.EntryPoint",
            "ENDPOINT-034",
            route,
            report);
    }

    private static void ValidateDirectionAlignment(
        Vector3 endpointPoint,
        Vector2 expectedDirection,
        string pointName,
        string issueCode,
        RouteConfig route,
        ValidationReport report)
    {
        Vector2 actualDirection =
            new Vector2(
                endpointPoint.x,
                endpointPoint.y);

        if (actualDirection.sqrMagnitude <=
            PositionEpsilon *
            PositionEpsilon)
        {
            return;
        }

        actualDirection.Normalize();

        float dot =
            Vector2.Dot(
                actualDirection,
                expectedDirection);

        if (dot >= MinimumDirectionDot)
            return;

        float angle =
            Vector2.Angle(
                actualDirection,
                expectedDirection);

        report.Add(
            ValidationSeverity.Error,
            issueCode,
            pointName +
            " is directed incorrectly. " +
            "Angle from expected route direction: " +
            angle.ToString("0.0") +
            "°. Check whether endpoint assets " +
            "were swapped.",
            route);
    }

    private static void ValidateOppositeDirections(
        Vector3 firstPoint,
        Vector3 secondPoint,
        string pairName,
        string issueCode,
        RouteConfig route,
        ValidationReport report)
    {
        Vector2 firstDirection =
            new Vector2(
                firstPoint.x,
                firstPoint.y);

        Vector2 secondDirection =
            new Vector2(
                secondPoint.x,
                secondPoint.y);

        if (firstDirection.sqrMagnitude <=
                PositionEpsilon *
                PositionEpsilon ||
            secondDirection.sqrMagnitude <=
                PositionEpsilon *
                PositionEpsilon)
        {
            return;
        }

        firstDirection.Normalize();
        secondDirection.Normalize();

        float dot =
            Vector2.Dot(
                firstDirection,
                secondDirection);

        /*
         * Для противоположных направлений:
         * dot должен быть близок к -1.
         */
        if (dot <= -MinimumDirectionDot)
            return;

        report.Add(
            ValidationSeverity.Error,
            issueCode,
            pairName +
            " are not on opposite sides. " +
            "Direction dot = " +
            dot.ToString("0.000") +
            ".",
            route);
    }

    private static void ValidatePoint(
        Vector3 point,
        string pointName,
        string issueCode,
        RouteConfig route,
        ValidationReport report)
    {
        if (!IsFinite(point))
        {
            report.Add(
                ValidationSeverity.Error,
                issueCode,
                pointName +
                " contains NaN or Infinity: " +
                point,
                route);

            return;
        }

        Vector2 point2D =
            new Vector2(
                point.x,
                point.y);

        if (point2D.sqrMagnitude >
            PositionEpsilon *
            PositionEpsilon)
        {
            return;
        }

        report.Add(
            ValidationSeverity.Warning,
            issueCode,
            pointName +
            " is located near the system center: " +
            point +
            ". Confirm this is intentional.",
            route);
    }

    private static bool TryGetMapPosition(
        StarSystemConfig system,
        out Vector2 mapPosition)
    {
        mapPosition =
            Vector2.zero;

        if (system == null)
            return false;

        SerializedObject serializedSystem =
            new SerializedObject(
                system);

        SerializedProperty property =
            serializedSystem.FindProperty(
                "mapPosition");

        if (property == null)
        {
            property =
                serializedSystem.FindProperty(
                    "MapPosition");
        }

        if (property == null)
            return false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Vector2:
                mapPosition =
                    property.vector2Value;

                return IsFinite(
                    mapPosition);

            case SerializedPropertyType.Vector3:
                Vector3 value =
                    property.vector3Value;

                mapPosition =
                    new Vector2(
                        value.x,
                        value.y);

                return IsFinite(
                    mapPosition);

            default:
                return false;
        }
    }

    private static bool ContainsRoute(
        StarSystemConfig system,
        RouteConfig route)
    {
        if (system == null ||
            route == null ||
            system.Routes == null)
        {
            return false;
        }

        return system.Routes.Any(
            item =>
                item == route);
    }

    private static void ValidateGraphConnectivity(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<StarSystemConfig> systems,
        ValidationReport report)
    {
        Dictionary<string, HashSet<string>>
            adjacency =
                new Dictionary<
                    string,
                    HashSet<string>>(
                        StringComparer.Ordinal);

        Dictionary<string, StarSystemConfig>
            systemsById =
                new Dictionary<
                    string,
                    StarSystemConfig>(
                        StringComparer.Ordinal);

        foreach (
            StarSystemConfig system
            in systems)
        {
            if (system == null ||
                string.IsNullOrWhiteSpace(
                    system.Id))
            {
                continue;
            }

            string systemId =
                system.Id.Trim();

            if (!adjacency.ContainsKey(
                    systemId))
            {
                adjacency.Add(
                    systemId,
                    new HashSet<string>(
                        StringComparer.Ordinal));
            }

            if (!systemsById.ContainsKey(
                    systemId))
            {
                systemsById.Add(
                    systemId,
                    system);
            }
        }

        foreach (
            RouteConfig route
            in routes)
        {
            if (route == null ||
                route.FromSystem == null ||
                route.ToSystem == null)
            {
                continue;
            }

            string fromId =
                route.FromSystem.Id;

            string toId =
                route.ToSystem.Id;

            if (string.IsNullOrWhiteSpace(
                    fromId) ||
                string.IsNullOrWhiteSpace(
                    toId))
            {
                continue;
            }

            fromId =
                fromId.Trim();

            toId =
                toId.Trim();

            if (!adjacency.ContainsKey(
                    fromId) ||
                !adjacency.ContainsKey(
                    toId))
            {
                continue;
            }

            adjacency[fromId].Add(
                toId);

            adjacency[toId].Add(
                fromId);
        }

        if (adjacency.Count == 0)
            return;

        string startSystemId =
            adjacency.Keys
                .OrderBy(
                    id => id,
                    StringComparer.Ordinal)
                .First();

        HashSet<string> visited =
            new HashSet<string>(
                StringComparer.Ordinal);

        Queue<string> queue =
            new Queue<string>();

        visited.Add(
            startSystemId);

        queue.Enqueue(
            startSystemId);

        while (queue.Count > 0)
        {
            string current =
                queue.Dequeue();

            foreach (
                string neighbor
                in adjacency[current])
            {
                if (!visited.Add(
                        neighbor))
                {
                    continue;
                }

                queue.Enqueue(
                    neighbor);
            }
        }

        foreach (
            string systemId
            in adjacency.Keys)
        {
            if (visited.Contains(
                    systemId))
            {
                continue;
            }

            systemsById.TryGetValue(
                systemId,
                out StarSystemConfig
                    systemContext);

            report.Add(
                ValidationSeverity.Error,
                "GRAPH-001",
                "System '" +
                systemId +
                "' is disconnected from the " +
                "route graph. Start system used " +
                "for validation: '" +
                startSystemId +
                "'.",
                systemContext);
        }

        foreach (
            KeyValuePair<
                string,
                HashSet<string>> pair
            in adjacency)
        {
            if (pair.Value.Count > 0)
                continue;

            systemsById.TryGetValue(
                pair.Key,
                out StarSystemConfig
                    systemContext);

            report.Add(
                ValidationSeverity.Error,
                "GRAPH-002",
                "System '" +
                pair.Key +
                "' has no active routes.",
                systemContext);
        }
    }

    private static List<T>
        LoadActiveAssets<T>(
            out int skippedCount)
        where T : UnityEngine.Object
    {
        skippedCount =
            0;

        List<T> result =
            new List<T>();

        string[] assetGuids =
            AssetDatabase.FindAssets(
                "t:" +
                typeof(T).Name);

        foreach (
            string guid
            in assetGuids)
        {
            string path =
                AssetDatabase
                    .GUIDToAssetPath(
                        guid);

            if (ShouldSkipAssetPath(
                    path))
            {
                skippedCount++;
                continue;
            }

            T asset =
                AssetDatabase
                    .LoadAssetAtPath<T>(
                        path);

            if (asset != null)
                result.Add(asset);
        }

        return result;
    }

    private static bool ShouldSkipAssetPath(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(
                assetPath))
        {
            return true;
        }

        string normalized =
            assetPath
                .Replace(
                    '\\',
                    '/')
                .ToLowerInvariant();

        return
            normalized.Contains(
                "/old/") ||
            normalized.Contains(
                "/olds/") ||
            normalized.Contains(
                "/archive/") ||
            normalized.Contains(
                "/archived/") ||
            normalized.Contains(
                "/deprecated/");
    }

    private static string BuildSystemPairKey(
        string firstSystemId,
        string secondSystemId)
    {
        int comparison =
            string.Compare(
                firstSystemId,
                secondSystemId,
                StringComparison.Ordinal);

        return comparison <= 0
            ? firstSystemId +
              "|" +
              secondSystemId
            : secondSystemId +
              "|" +
              firstSystemId;
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y);
    }

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private static void LogReport(
        ValidationReport report)
    {
        string summary =
            "[RouteEndpointValidator2A] " +
            "Routes = " +
            report.RouteCount +
            " | Systems = " +
            report.SystemCount +
            " | Errors = " +
            report.ErrorCount +
            " | Warnings = " +
            report.WarningCount;

        if (report.HasErrors)
            Debug.LogError(summary);
        else if (report.WarningCount > 0)
            Debug.LogWarning(summary);
        else
            Debug.Log(summary);

        foreach (
            ValidationIssue issue
            in report.Issues)
        {
            string message =
                "[" +
                issue.Code +
                "] " +
                issue.Message;

            switch (issue.Severity)
            {
                case ValidationSeverity.Error:
                    Debug.LogError(
                        message,
                        issue.Context);
                    break;

                case ValidationSeverity.Warning:
                    Debug.LogWarning(
                        message,
                        issue.Context);
                    break;

                default:
                    Debug.Log(
                        message,
                        issue.Context);
                    break;
            }
        }
    }

    private static void SelectFirstError(
        ValidationReport report)
    {
        ValidationIssue firstError =
            report.Issues.FirstOrDefault(
                issue =>
                    issue.Severity ==
                        ValidationSeverity.Error &&
                    issue.Context != null);

        if (firstError == null)
            return;

        Selection.activeObject =
            firstError.Context;

        EditorGUIUtility.PingObject(
            firstError.Context);
    }

    private static string SaveReport(
        ValidationReport report)
    {
        string projectRoot =
            Directory
                .GetParent(
                    Application.dataPath)
                ?.FullName;

        if (string.IsNullOrWhiteSpace(
                projectRoot))
        {
            projectRoot =
                Application.dataPath;
        }

        string reportDirectory =
            Path.Combine(
                projectRoot,
                "Logs",
                "StarFrontierQA");

        Directory.CreateDirectory(
            reportDirectory);

        string fileName =
            "RouteEndpointValidation_" +
            DateTime.UtcNow
                .ToString(
                    "yyyyMMdd_HHmmss") +
            ".txt";

        string reportPath =
            Path.Combine(
                reportDirectory,
                fileName);

        File.WriteAllText(
            reportPath,
            report.BuildText(),
            new UTF8Encoding(false));

        Debug.Log(
            "[RouteEndpointValidator2A] " +
            "Report saved: " +
            reportPath);

        return reportPath;
    }

    private static string BuildDialogMessage(
        ValidationReport report)
    {
        if (report.HasErrors)
        {
            return
                "Validation failed.\n\n" +
                "Errors: " +
                report.ErrorCount +
                "\nWarnings: " +
                report.WarningCount +
                "\n\nThe first problematic asset " +
                "was selected in Project.";
        }

        if (report.WarningCount > 0)
        {
            return
                "Validation completed with warnings.\n\n" +
                "Errors: 0\nWarnings: " +
                report.WarningCount;
        }

        return
            "Validation passed.\n\n" +
            "Errors: 0\nWarnings: 0";
    }
}

#endif