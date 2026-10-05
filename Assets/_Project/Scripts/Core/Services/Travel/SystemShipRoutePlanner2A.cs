using System.Collections.Generic;
using UnityEngine;

public sealed class SystemShipRoutePlanner2A : CustomService, ISystemShipRoutePlanner2A
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;

    private readonly IConfigService _configService;

    private readonly List<Vector3> _candidateWaypoints = new();
    private readonly List<Vector3> _bestPath = new();

    public SystemShipRoutePlanner2A()
    {
        _debugEnabled = false;
        _debugStop = true;

        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public bool TryBuildTurnRadiusPreviewRoute(
        SystemSharedShipRoutePlanRequest2A request,
        SystemSharedShipRoutePlanResult2A result)
    {
        if (result == null)
            return false;

        result.Clear();

        if (request == null)
        {
            result.RejectReason = "RequestNull";
            return false;
        }

        if (request.Waypoints == null ||
            request.Waypoints.Count <= 1)
        {
            result.RejectReason = "WaypointsEmpty";
            return false;
        }

        if (request.StartFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            result.RejectReason = "StartFacingInvalid";
            return false;
        }

        Vector3 startPosition =
            request.Waypoints[0];

        Vector3 destinationPosition =
            request.Waypoints[request.Waypoints.Count - 1];

        startPosition.z = -2f;
        destinationPosition.z = -2f;

        Vector2 firstSegmentDirection =
            GetFirstSegmentDirection(
                request.Waypoints,
                request.StartFacingDirection);

        result.StartTurnAngleDegrees =
            Vector2.Angle(
                request.StartFacingDirection.normalized,
                firstSegmentDirection.normalized);

        _bestPath.Clear();

        string lastRejectReason = string.Empty;

        bool built =
            TryBuildAdjustedPreviewRoute(
                request,
                request.Waypoints,
                "RawWaypoints",
                result,
                out lastRejectReason);

        if (!built &&
            TryBuildForwardEntryWaypoints(
                request,
                startPosition,
                destinationPosition,
                _candidateWaypoints))
        {
            built =
                TryBuildAdjustedPreviewRoute(
                    request,
                    _candidateWaypoints,
                    "ForwardEntry",
                    result,
                    out lastRejectReason);
        }

        if (!built &&
            TryBuildSunAvoidanceEntryWaypoints(
                request,
                startPosition,
                destinationPosition,
                0f,
                _candidateWaypoints))
        {
            built =
                TryBuildAdjustedPreviewRoute(
                    request,
                    _candidateWaypoints,
                    "SunAvoidance",
                    result,
                    out lastRejectReason);
        }

        if (!built &&
            TryBuildNearSunBehindAwayWaypoints(
                request,
                startPosition,
                destinationPosition,
                _candidateWaypoints))
        {
            built =
                TryBuildAdjustedPreviewRoute(
                    request,
                    _candidateWaypoints,
                    "NearSunBehindAway",
                    result,
                    out lastRejectReason);
        }

        if (!built)
        {
            float paddingStep = GetSunAvoidanceRoutePaddingStep();
            float paddingMax = GetSunAvoidanceRoutePaddingMax();

            int paddingAttemptCount =
                paddingStep > 0.001f
                    ? Mathf.CeilToInt(paddingMax / paddingStep)
                    : 0;

            for (int attempt = 1; attempt <= paddingAttemptCount; attempt++)
            {
                float padding =
                    attempt == paddingAttemptCount
                        ? paddingMax
                        : paddingStep * attempt;

                if (!TryBuildSunAvoidanceEntryWaypoints(
                        request,
                        startPosition,
                        destinationPosition,
                        padding,
                        _candidateWaypoints))
                {
                    continue;
                }

                built =
                    TryBuildAdjustedPreviewRoute(
                        request,
                        _candidateWaypoints,
                        "SunAvoidancePadding" + padding.ToString("0.###"),
                        result,
                        out lastRejectReason);

                if (built)
                    break;
            }
        }

        if (built)
            return true;

        result.RejectReason =
            string.IsNullOrWhiteSpace(lastRejectReason)
                ? "PreviewBuildFailed"
                : lastRejectReason;

        result.Path.Clear();
        result.PathLength = 0f;
        result.Built = false;

        return false;
    }

    private bool TryBuildAdjustedPreviewRoute(
        SystemSharedShipRoutePlanRequest2A request,
        IReadOnlyList<Vector3> waypoints,
        string source,
        SystemSharedShipRoutePlanResult2A result,
        out string lastRejectReason)
    {
        lastRejectReason = string.Empty;

        float turnStepFactor =
            Mathf.Clamp(
                request.TurnRadiusAdjustmentStepPercent * 0.01f,
                0.01f,
                1f);

        float speedStepFactor =
            Mathf.Clamp(
                request.SpeedAdjustmentStepPercent * 0.01f,
                0.01f,
                1f);

        float minTurnFactor =
            Mathf.Clamp(
                request.MinTurnRadiusAdjustmentFactor,
                0.01f,
                1f);

        float turnFactor = 1f;
        float speedFactor = 1f;

        while (turnFactor >= minTurnFactor - 0.0001f)
        {
            turnFactor =
                Mathf.Max(
                    minTurnFactor,
                    turnFactor);

            float effectiveTurnRadius =
                GetAdjustedTurnRadius(
                    request,
                    turnFactor);

            float effectiveSpeed =
                Mathf.Max(0f, request.Speed) *
                Mathf.Clamp01(speedFactor);

            result.AttemptCount++;

            bool built =
                TryBuildPreviewRouteOnce(
                    request,
                    waypoints,
                    source,
                    effectiveSpeed,
                    effectiveTurnRadius,
                    turnFactor,
                    speedFactor,
                    result,
                    out lastRejectReason);

            if (built)
                return true;

            turnFactor -= turnStepFactor;

            speedFactor =
                Mathf.Max(
                    minTurnFactor,
                    speedFactor - speedStepFactor);

            if (Mathf.Approximately(turnFactor, minTurnFactor))
                turnFactor = minTurnFactor;
        }

        return false;
    }

    private bool TryBuildPreviewRouteOnce(
        SystemSharedShipRoutePlanRequest2A request,
        IReadOnlyList<Vector3> waypoints,
        string source,
        float effectiveSpeed,
        float effectiveTurnRadius,
        float turnFactor,
        float speedFactor,
        SystemSharedShipRoutePlanResult2A result,
        out string rejectReason)
    {
        rejectReason = string.Empty;

        float routeStepDistance =
            GetRouteStepDistance(
                request,
                effectiveSpeed);

        float waypointPathLength =
            TurnRadiusRouteMath2A.GetPathLength(waypoints);

        int maxSteps =
            TurnRadiusRouteMath2A.GetRoutePlanMaxSteps(
                waypoints,
                waypointPathLength,
                effectiveTurnRadius,
                routeStepDistance,
                request.MaxRoutePlanSteps);

        maxSteps =
            Mathf.Clamp(
                maxSteps * 4,
                32,
                Mathf.Max(32, request.MaxRoutePlanSteps));

        float intermediateArrivalThreshold =
            TurnRadiusRouteMath2A.GetIntermediateWaypointArrivalDistanceThreshold(
                waypoints,
                routeStepDistance,
                request.ArrivalDistanceThreshold);

        bool built =
            TurnRadiusRouteMath2A.TryBuildWaypointPreviewPath(
                result.Path,
                waypoints,
                request.StartFacingDirection,
                routeStepDistance,
                effectiveTurnRadius,
                request.ArrivalDistanceThreshold,
                maxSteps,
                intermediateArrivalThreshold,
                request.StraightExitAngleDegrees,
                request.DebugLog,
                request.DebugPrefix);

        if (!built ||
            result.Path.Count <= 1)
        {
            rejectReason = source + ".PreviewBuildFailed";
            return false;
        }

        float routeLength =
            TurnRadiusRouteMath2A.GetPathLength(result.Path);

        float maxAllowedRouteLength =
            TurnRadiusRouteMath2A.GetMaxAllowedRouteLength(
                waypoints,
                waypointPathLength,
                effectiveTurnRadius,
                routeStepDistance,
                request.StartFacingDirection,
                request.SunAvoidanceTurnRouteReserveMultiplier);

        maxAllowedRouteLength =
            Mathf.Max(
                maxAllowedRouteLength,
                waypointPathLength +
                Mathf.Max(0f, effectiveTurnRadius) *
                Mathf.PI *
                2f +
                routeStepDistance * 16f);

        if (maxAllowedRouteLength > 0f &&
            routeLength > maxAllowedRouteLength)
        {
            rejectReason = source + ".PreviewRouteTooLong";
            return false;
        }

        if (!RouteAvoidsSunBlockingRadius(
                request.SystemId,
                result.Path,
                request.SunAvoidanceSafetyMargin))
        {
            rejectReason = source + ".PreviewTouchesSunBlock";
            return false;
        }

        result.Built = true;
        result.PathLength = routeLength;
        result.TurnRadiusFactor = turnFactor;
        result.SpeedFactor = speedFactor;
        result.EffectiveTurnRadius = effectiveTurnRadius;
        result.EffectiveSpeed = effectiveSpeed;
        result.MaxAllowedRouteLength = maxAllowedRouteLength;
        result.Source = source;
        result.RejectReason = string.Empty;

        return true;
    }

    private bool TryBuildForwardEntryWaypoints(
        SystemSharedShipRoutePlanRequest2A request,
        Vector3 startPosition,
        Vector3 destinationPosition,
        List<Vector3> waypoints)
    {
        if (waypoints == null)
            return false;

        waypoints.Clear();

        Vector2 facing =
            request.StartFacingDirection;

        if (facing.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        facing.Normalize();

        float forwardDistance =
            Mathf.Max(
                request.ArrivalDistanceThreshold * 4f,
                request.TurnRadius * 0.75f,
                request.Speed * 0.5f);

        Vector3 forwardPoint =
            startPosition +
            new Vector3(
                facing.x,
                facing.y,
                0f) * forwardDistance;

        forwardPoint.z = -2f;

        waypoints.Add(startPosition);
        waypoints.Add(forwardPoint);
        waypoints.Add(destinationPosition);

        return true;
    }

    private bool TryBuildSunAvoidanceEntryWaypoints(
        SystemSharedShipRoutePlanRequest2A request,
        Vector3 startPosition,
        Vector3 destinationPosition,
        float padding,
        List<Vector3> waypoints)
    {
        if (waypoints == null)
            return false;

        waypoints.Clear();

        if (!TryGetSunObstacle(
                request.SystemId,
                request.SunAvoidanceSafetyMargin,
                padding,
                out SharedSunObstacle obstacle))
        {
            return false;
        }

        SystemTravelSunAvoidancePath2A.BuildPath(
            waypoints,
            startPosition,
            destinationPosition,
            obstacle.Center,
            obstacle.Radius,
            18,
            false,
            request.StartFacingDirection,
            request.TurnRadius);

        if (waypoints.Count <= 1)
            return false;

        return true;
    }

    private bool TryBuildNearSunBehindAwayWaypoints(
        SystemSharedShipRoutePlanRequest2A request,
        Vector3 startPosition,
        Vector3 destinationPosition,
        List<Vector3> waypoints)
    {
        if (waypoints == null)
            return false;

        waypoints.Clear();

        if (!TryGetSunObstacle(
                request.SystemId,
                request.SunAvoidanceSafetyMargin,
                0f,
                out SharedSunObstacle obstacle))
        {
            return false;
        }

        Vector2 center =
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y);

        Vector2 start =
            new Vector2(
                startPosition.x,
                startPosition.y);

        Vector2 radialAway =
            start - center;

        if (radialAway.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        radialAway.Normalize();

        float awayDistance =
            Mathf.Max(
                request.ArrivalDistanceThreshold * 4f,
                request.TurnRadius * 0.5f,
                request.Speed * 0.5f);

        Vector2 awayPoint =
            start + radialAway * awayDistance;

        waypoints.Add(startPosition);
        waypoints.Add(
            new Vector3(
                awayPoint.x,
                awayPoint.y,
                -2f));
        waypoints.Add(destinationPosition);

        return true;
    }

    private bool TryGetSunObstacle(
        string systemId,
        float safetyMargin,
        float padding,
        out SharedSunObstacle obstacle)
    {
        obstacle = default;

        if (string.IsNullOrWhiteSpace(systemId) ||
            _configService == null)
        {
            return false;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            return false;
        }

        Vector3 center =
            new Vector3(
                starSystem.Sun.LocalOffset.x,
                starSystem.Sun.LocalOffset.y,
                -2f);

        float sunRadius =
            Mathf.Max(
                0f,
                GetSunWorldSize(starSystem.Sun) * 0.5f);

        float blockingRadius =
            sunRadius + Mathf.Max(0f, safetyMargin);

        float planningRadius =
            Mathf.Max(
                blockingRadius,
                sunRadius + Mathf.Max(0f, safetyMargin) + Mathf.Max(0f, padding));

        obstacle =
            new SharedSunObstacle(
                center,
                planningRadius,
                blockingRadius);

        return true;
    }

    private float GetRouteStepDistance(
        SystemSharedShipRoutePlanRequest2A request,
        float effectiveSpeed)
    {
        return Mathf.Max(
            Mathf.Max(0.001f, request.ArrivalDistanceThreshold),
            Mathf.Max(0f, effectiveSpeed) /
            Mathf.Max(1, request.RouteSubstepsPerTick));
    }

    private float GetAdjustedTurnRadius(
        SystemSharedShipRoutePlanRequest2A request,
        float factor)
    {
        float adjustedTurnRadius =
            Mathf.Max(0f, request.TurnRadius) *
            Mathf.Clamp01(factor);

        if (request.MinTurnRadiusAbsolute > 0f &&
            request.TurnRadius > DirectionThresholdSqrMagnitude)
        {
            adjustedTurnRadius =
                Mathf.Max(
                    adjustedTurnRadius,
                    request.MinTurnRadiusAbsolute);
        }

        return adjustedTurnRadius;
    }

    private Vector2 GetFirstSegmentDirection(
        IReadOnlyList<Vector3> waypoints,
        Vector2 fallbackDirection)
    {
        if (waypoints == null ||
            waypoints.Count <= 1)
        {
            return fallbackDirection.sqrMagnitude > DirectionThresholdSqrMagnitude
                ? fallbackDirection.normalized
                : Vector2.up;
        }

        Vector3 segment =
            waypoints[1] - waypoints[0];

        segment.z = 0f;

        if (segment.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return fallbackDirection.sqrMagnitude > DirectionThresholdSqrMagnitude
                ? fallbackDirection.normalized
                : Vector2.up;
        }

        return new Vector2(segment.x, segment.y).normalized;
    }

    private bool RouteAvoidsSunBlockingRadius(
        string systemId,
        IReadOnlyList<Vector3> path,
        float safetyMargin)
    {
        if (string.IsNullOrWhiteSpace(systemId) ||
            path == null ||
            path.Count == 0 ||
            _configService == null)
        {
            return true;
        }

        if (!TryGetSunObstacle(
                systemId,
                safetyMargin,
                0f,
                out SharedSunObstacle obstacle))
        {
            return true;
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (Vector3.Distance(path[i], obstacle.Center) <= obstacle.BlockingRadius)
                return false;
        }

        return true;
    }

    private float GetSunAvoidanceRoutePaddingStep()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 5f;

        return config.SunAvoidanceRoutePaddingStep;
    }

    private float GetSunAvoidanceRoutePaddingMax()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 40f;

        return config.SunAvoidanceRoutePaddingMax;
    }

    private float GetSunWorldSize(SunConfig sun)
    {
        if (sun == null)
            return 0f;

        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService.SystemVisualConfig.GetSunWorldSize(sun);
        }

        return sun.VisualSize;
    }

    private readonly struct SharedSunObstacle
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float BlockingRadius;

        public SharedSunObstacle(
            Vector3 center,
            float radius,
            float blockingRadius)
        {
            Center = center;
            Radius = radius;
            BlockingRadius = blockingRadius;
        }
    }
}