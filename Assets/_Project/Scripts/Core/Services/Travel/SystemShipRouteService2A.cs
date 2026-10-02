using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public enum SystemShipRouteTargetKind2A
{
    MapPoint,
    Planet,
    Station,
    SystemExit,
    Npc,
    Enemy,
    MovingTarget
}

public sealed class SystemShipRouteSettings2A
{
    public double RouteBuildBudgetStartedAt;
    public float RouteBuildTimeBudgetMs;
    public System.Func<double> GetRouteBuildBudgetUsedMs;
    public System.Action<double> AddRouteBuildBudgetUsedMs;
    public float Speed;
    public float TurnRadius;
    public float ArrivalDistanceThreshold = 3f;
    public float SunAvoidanceSafetyMargin = 80f;
    public int SunAvoidanceArcSegments = 18;
    public bool AllowSunAvoidance = true;
    public int RouteSubstepsPerTick = 10;
    public float RouteStraightExitAngleDegrees = 3f;
    public float TurnRadiusAdjustmentStepPercent = 5f;
    public float SpeedAdjustmentStepPercent = 2.5f;
    public float MinTurnRadiusAdjustmentFactor = 0.05f;
    public float MinTurnRadiusAbsolute = 30f;
    public float BehindSmallTurnAngleToleranceDegrees = 75f;
    public int MaxRoutePlanSteps = 8192;
    public float SunAvoidanceTurnRouteReserveMultiplier = 1.5f;
    public System.Action<string> DebugLog;
    public string DebugPrefix = string.Empty;
}
public sealed class SystemShipRouteRequest2A
{
    public string SystemId;
    public Vector3 StartPosition;
    public Vector3 DestinationPosition;
    public Vector2 StartFacingDirection;
    public SystemShipRouteTargetKind2A TargetKind;
    public SystemShipRouteSettings2A Settings;
}

public sealed class SystemShipRouteResult2A
{
    public readonly List<Vector3> Path = new List<Vector3>();

    public bool Built;
    public float PathLength;
    public float EffectiveSpeed;
    public float EffectiveTurnRadius;
    public float TurnRadiusFactor = 1f;
    public float SpeedFactor = 1f;
    public bool UsedSunAvoidance;
    public string DestinationCase;
    public bool UsedRawSafeFallback;
    public bool DestinationWasPushedOutsideSun;
    public bool StartWasPushedOutsideSun;

    public int CandidateAttemptCount;
    public int RawFallbackAttemptCount;
    public string SelectedRouteSource = string.Empty;
    public string LastRejectReason = string.Empty;

    public int LastPlannerMaxSteps;
    public float LastPlannerRouteStepDistance;
    public float LastPlannerIntermediateArrivalThreshold;
    public float LastPlannerRouteLength;
    public float LastPlannerMaxAllowedRouteLength;
    public float LastWaypointPathLength;
    public int LastWaypointCount;

    public bool DirectRouteCrossesSun;
    public bool HasSunObstacle;
    public Vector3 SunCenter;
    public float SunRadius;
    public float SunBlockingRadius;

    public void Clear()
    {
        Path.Clear();
        Built = false;
        PathLength = 0f;
        EffectiveSpeed = 0f;
        EffectiveTurnRadius = 0f;
        TurnRadiusFactor = 1f;
        SpeedFactor = 1f;
        UsedSunAvoidance = false;
        DestinationCase = string.Empty;
        UsedRawSafeFallback = false;
        DestinationWasPushedOutsideSun = false;
        StartWasPushedOutsideSun = false;

        CandidateAttemptCount = 0;
        RawFallbackAttemptCount = 0;
        SelectedRouteSource = string.Empty;
        LastRejectReason = string.Empty;

        LastPlannerMaxSteps = 0;
        LastPlannerRouteStepDistance = 0f;
        LastPlannerIntermediateArrivalThreshold = 0f;
        LastPlannerRouteLength = 0f;
        LastPlannerMaxAllowedRouteLength = 0f;
        LastWaypointPathLength = 0f;
        LastWaypointCount = 0;

        DirectRouteCrossesSun = false;
        HasSunObstacle = false;
        SunCenter = Vector3.zero;
        SunRadius = 0f;
        SunBlockingRadius = 0f;
    }
}

public sealed class SystemShipRouteService2A : CustomService, ISystemShipRouteService2A
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;
    private const float RouteSegmentEpsilon = 0.001f;
    private const double ExpensiveRouteBuildLogThresholdMs = 50.0;

    private readonly IConfigService _configService;

    private readonly List<Vector3> _waypointsBuffer =
        new List<Vector3>();

    private readonly List<Vector3> _routeProbeBuffer =
        new List<Vector3>();

    public SystemShipRouteService2A()
    {
        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public bool TryBuildRoute(
        SystemShipRouteRequest2A request,
        SystemShipRouteResult2A result)
    {
        long startedAt = Stopwatch.GetTimestamp();

        if (result == null)
            return false;

        result.Clear();

        if (request == null)
            return false;

        SystemShipRouteSettings2A settings =
            request.Settings ?? CreateDefaultSettings();

        if (IsRouteBuildTimeBudgetExceeded(settings, startedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "TryBuildRoute.Start");

        if (settings.Speed <= 0f)
            return false;

        Vector3 startPosition = request.StartPosition;
        Vector3 destinationPosition = request.DestinationPosition;

        startPosition.z = -2f;
        destinationPosition.z = -2f;

        Vector2 startFacingDirection =
            TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                request.StartFacingDirection);

        SunRouteObstacle2A obstacle =
            GetSunObstacle(
                request.SystemId,
                startPosition.z,
                settings);

        result.HasSunObstacle = obstacle.HasObstacle;
        result.SunCenter = obstacle.Center;
        result.SunRadius = obstacle.Radius;
        result.SunBlockingRadius = obstacle.BlockingRadius;

        Vector3 originalStartPosition = startPosition;
        Vector3 originalDestinationPosition = destinationPosition;

        startPosition =
            PushPositionOutsideSunBlockingRadius(
                startPosition,
                obstacle,
                startFacingDirection);

        destinationPosition =
            PushPositionOutsideSunBlockingRadius(
                destinationPosition,
                obstacle,
                destinationPosition - startPosition);

        result.StartWasPushedOutsideSun =
            Vector3.Distance(originalStartPosition, startPosition) > RouteSegmentEpsilon;

        result.DestinationWasPushedOutsideSun =
            Vector3.Distance(originalDestinationPosition, destinationPosition) > RouteSegmentEpsilon;

        if (IsRouteBuildTimeBudgetExceeded(settings, startedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "TryBuildRoute.AfterPrepare");

        if (Vector3.Distance(
                startPosition,
                destinationPosition) <= settings.ArrivalDistanceThreshold)
        {
            result.LastRejectReason = "AlreadyAtDestination";

            LogExpensiveRouteBuildIfNeeded(
                startedAt,
                request,
                settings,
                startPosition,
                destinationPosition,
                startFacingDirection,
                obstacle,
                false,
                result);

            return false;
        }

        bool directRouteCrossesSun =
            settings.AllowSunAvoidance &&
            obstacle.HasObstacle &&
            SegmentIntersectsCircle(
                startPosition,
                destinationPosition,
                obstacle.Center,
                obstacle.BlockingRadius);

        result.DirectRouteCrossesSun = directRouteCrossesSun;

        result.DestinationCase =
            ResolveDestinationCaseName(
                startPosition,
                destinationPosition,
                startFacingDirection,
                obstacle,
                directRouteCrossesSun);

        bool earlyRawFallbackTried = false;
        bool built = false;

        if (ShouldTryEarlyRawSunFallback(
                settings,
                obstacle,
                directRouteCrossesSun))
        {
            earlyRawFallbackTried = true;

            built =
                TryBuildRawSafeFallbackRoute(
                    startPosition,
                    destinationPosition,
                    startFacingDirection,
                    settings,
                    obstacle,
                    directRouteCrossesSun,
                    result,
                    startedAt);

            if (built)
            {
                result.SelectedRouteSource =
                    string.IsNullOrWhiteSpace(result.SelectedRouteSource)
                        ? "EarlyRawSunFallback"
                        : "Early" + result.SelectedRouteSource;
            }
        }

        if (IsRouteBuildTimeBudgetExceeded(settings, startedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "TryBuildRoute.AfterEarlyRawFallback");

        if (!built)
        {
            built =
                TryBuildAdjustedRoute(
                    startPosition,
                    destinationPosition,
                    startFacingDirection,
                    settings,
                    obstacle,
                    directRouteCrossesSun,
                    result,
                    startedAt);
        }

        if (IsRouteBuildTimeBudgetExceeded(settings, startedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "TryBuildRoute.AfterAdjustedRoute");

        if (!built &&
            !earlyRawFallbackTried &&
            TryBuildRawSafeFallbackRoute(
                startPosition,
                destinationPosition,
                startFacingDirection,
                settings,
                obstacle,
                directRouteCrossesSun,
                result,
                startedAt))
        {
            built = true;
        }

        if (!built)
        {
            result.LastRejectReason =
                string.IsNullOrWhiteSpace(result.LastRejectReason)
                    ? "RouteBuildFailed"
                    : result.LastRejectReason;

            LogExpensiveRouteBuildIfNeeded(
                startedAt,
                request,
                settings,
                startPosition,
                destinationPosition,
                startFacingDirection,
                obstacle,
                false,
                result);

            ClearFailedRoutePathForDiagnostics(result);
            return false;
        }

        if (IsRouteBuildTimeBudgetExceeded(settings, startedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "TryBuildRoute.BeforeFinalValidation");

        ClampPathOutsideSunBlockingRadius(
            result.Path,
            obstacle,
            startFacingDirection);

        if (!RouteAvoidsSun(
                result.Path,
                obstacle,
                obstacle.BlockingRadius))
        {
            result.LastRejectReason = "FinalPathTouchesSunBlock";

            LogExpensiveRouteBuildIfNeeded(
                startedAt,
                request,
                settings,
                startPosition,
                destinationPosition,
                startFacingDirection,
                obstacle,
                false,
                result);

            ClearFailedRoutePathForDiagnostics(result);
            return false;
        }

        result.Built = true;
        result.PathLength = GetPathLength(result.Path);

        if (result.EffectiveSpeed <= 0f)
            result.EffectiveSpeed = Mathf.Max(0.01f, settings.Speed);

        if (result.EffectiveTurnRadius <= 0f)
            result.EffectiveTurnRadius = Mathf.Max(0f, settings.TurnRadius);

        bool success =
            result.Path.Count > 1;

        if (!success)
            result.LastRejectReason = "PathTooShort";

        LogExpensiveRouteBuildIfNeeded(
            startedAt,
            request,
            settings,
            startPosition,
            destinationPosition,
            startFacingDirection,
            obstacle,
            success,
            result);

        ConsumeRouteBuildBudget(settings, startedAt);

        return success;
    }

    private static void ClearFailedRoutePathForDiagnostics(
        SystemShipRouteResult2A result)
    {
        if (result == null)
            return;

        result.Path.Clear();
        result.Built = false;
        result.PathLength = 0f;
    }

    private static bool ShouldTryEarlyRawSunFallback(
        SystemShipRouteSettings2A settings,
        SunRouteObstacle2A obstacle,
        bool directRouteCrossesSun)
    {
        if (settings == null)
            return false;

        if (!settings.AllowSunAvoidance)
            return false;

        if (!obstacle.HasObstacle)
            return false;

        return directRouteCrossesSun;
    }

    private bool TryBuildRawSafeFallbackRoute(
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        SystemShipRouteSettings2A settings,
        SunRouteObstacle2A obstacle,
        bool useSunAvoidance,
        SystemShipRouteResult2A result,
        long budgetStartedAt)
    {
        if (result == null)
            return false;

        if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "RawFallback.Start");

        if (!useSunAvoidance ||
            !settings.AllowSunAvoidance ||
            !obstacle.HasObstacle)
        {
            result.RawFallbackAttemptCount++;

            _waypointsBuffer.Clear();
            _waypointsBuffer.Add(startPosition);
            _waypointsBuffer.Add(destinationPosition);

            result.LastWaypointCount = _waypointsBuffer.Count;
            result.LastWaypointPathLength = GetPathLength(_waypointsBuffer);
            result.SelectedRouteSource = "RawFallbackDirect";

            if (!RouteAvoidsSun(
                    _waypointsBuffer,
                    obstacle,
                    obstacle.BlockingRadius))
            {
                result.LastRejectReason = "RawFallbackDirectTouchesSunBlock";

                LogRouteDebug(
                    settings,
                    "RawFallback.DirectRejected",
                    startPosition,
                    destinationPosition,
                    startFacingDirection,
                    obstacle,
                    _waypointsBuffer,
                    false);

                return false;
            }

            FillRawSafeFallbackResult(
                result,
                _waypointsBuffer,
                settings,
                false,
                "RawFallbackDirect");

            LogRouteDebug(
                settings,
                "RawFallback.DirectAccepted",
                startPosition,
                destinationPosition,
                startFacingDirection,
                obstacle,
                _waypointsBuffer,
                true);

            return true;
        }

        float paddingStep = 5f;
        float paddingMax = 40f;

        int attemptCount =
            Mathf.CeilToInt(
                paddingMax / paddingStep);

        for (int attempt = 0; attempt <= attemptCount; attempt++)
        {
            if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
                return RejectRouteBuildByTimeBudget(settings, result, "RawFallback.PaddingAttempt");

            result.RawFallbackAttemptCount++;

            float padding =
                attempt == attemptCount
                    ? paddingMax
                    : paddingStep * attempt;

            SunRouteObstacle2A planningObstacle =
                CreateSunRoutePlanningObstacle(
                    obstacle,
                    padding);

            _waypointsBuffer.Clear();

            SystemTravelSunAvoidancePath2A.BuildPath(
                _waypointsBuffer,
                startPosition,
                destinationPosition,
                planningObstacle.Center,
                planningObstacle.Radius,
                Mathf.Max(2, settings.SunAvoidanceArcSegments),
                false,
                startFacingDirection,
                Mathf.Max(0f, settings.TurnRadius));

            if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
                return RejectRouteBuildByTimeBudget(settings, result, "RawFallback.AfterBuildPath");

            ClampPathOutsideSunBlockingRadius(
                _waypointsBuffer,
                obstacle,
                startFacingDirection);

            result.LastWaypointCount = _waypointsBuffer.Count;
            result.LastWaypointPathLength = GetPathLength(_waypointsBuffer);
            result.SelectedRouteSource = "RawFallbackPadding" + padding.ToString("0.###");

            bool routeIsValid =
                _waypointsBuffer.Count > 1 &&
                RouteAvoidsSun(
                    _waypointsBuffer,
                    obstacle,
                    obstacle.BlockingRadius);

            LogRouteDebug(
                settings,
                routeIsValid
                    ? "RawFallback.Padding" + padding.ToString("0.###") + ".Accepted"
                    : "RawFallback.Padding" + padding.ToString("0.###") + ".Rejected",
                startPosition,
                destinationPosition,
                startFacingDirection,
                obstacle,
                _waypointsBuffer,
                routeIsValid);

            if (!routeIsValid)
            {
                result.LastRejectReason =
                    "RawFallbackPadding" + padding.ToString("0.###") + "Rejected";

                continue;
            }

            FillRawSafeFallbackResult(
                result,
                _waypointsBuffer,
                settings,
                true,
                "RawFallbackPadding" + padding.ToString("0.###"));

            return true;
        }

        return false;
    }

    private static SunRouteObstacle2A CreateSunRoutePlanningObstacle(
    SunRouteObstacle2A obstacle,
    float padding)
    {
        if (!obstacle.HasObstacle)
            return obstacle;

        float planningRadius =
            Mathf.Max(
                obstacle.BlockingRadius,
                obstacle.Radius + Mathf.Max(0f, padding));

        return new SunRouteObstacle2A(
            obstacle.Center,
            planningRadius,
            obstacle.BlockingRadius);
    }

    private void FillRawSafeFallbackResult(
        SystemShipRouteResult2A result,
        IReadOnlyList<Vector3> path,
        SystemShipRouteSettings2A settings,
        bool usedSunAvoidance,
        string source)
    {
        result.Path.Clear();

        if (path != null)
        {
            for (int i = 0; i < path.Count; i++)
                result.Path.Add(path[i]);
        }

        result.EffectiveSpeed = Mathf.Max(0.01f, settings.Speed);
        result.EffectiveTurnRadius = Mathf.Max(0f, settings.TurnRadius);
        result.TurnRadiusFactor = 1f;
        result.SpeedFactor = 1f;
        result.UsedSunAvoidance = usedSunAvoidance;
        result.UsedRawSafeFallback = true;
        result.SelectedRouteSource = source;
        result.LastRejectReason = string.Empty;
        result.LastWaypointCount = path != null ? path.Count : 0;
        result.LastWaypointPathLength = GetPathLength(path);
    }

    private void LogRouteDebug(
    SystemShipRouteSettings2A settings,
    string phase,
    Vector3 startPosition,
    Vector3 destinationPosition,
    Vector2 startFacingDirection,
    SunRouteObstacle2A obstacle,
    IReadOnlyList<Vector3> path,
    bool accepted)
    {
        if (settings == null ||
            settings.DebugLog == null)
        {
            return;
        }

        float pathLength =
            GetPathLength(path);

        settings.DebugLog(
            settings.DebugPrefix +
            "[ShipRoute2A] " +
            phase +
            " | Accepted=" + accepted +
            " | Start=" + startPosition +
            " | Destination=" + destinationPosition +
            " | Facing=" + startFacingDirection +
            " | PathCount=" + (path != null ? path.Count : 0) +
            " | PathLength=" + pathLength.ToString("0.###") +
            " | HasSun=" + obstacle.HasObstacle +
            " | SunCenter=" + obstacle.Center +
            " | SunRadius=" + obstacle.Radius.ToString("0.###") +
            " | SunBlockingRadius=" + obstacle.BlockingRadius.ToString("0.###"));
    }

    private string ResolveDestinationCaseName(
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        SunRouteObstacle2A obstacle,
        bool directRouteCrossesSun)
    {
        if (!obstacle.HasObstacle)
            return "DirectNoSun";

        float startDistanceFromSun =
            Vector2.Distance(
                new Vector2(startPosition.x, startPosition.y),
                new Vector2(obstacle.Center.x, obstacle.Center.y));

        float destinationDistanceFromSun =
            Vector2.Distance(
                new Vector2(destinationPosition.x, destinationPosition.y),
                new Vector2(obstacle.Center.x, obstacle.Center.y));

        if (destinationDistanceFromSun < obstacle.BlockingRadius)
            return "ForbiddenDestination";

        bool startNearSun =
            startDistanceFromSun <= obstacle.BlockingRadius * 1.35f;

        Vector2 toDestination =
            new Vector2(
                destinationPosition.x - startPosition.x,
                destinationPosition.y - startPosition.y);

        float bearingAngle =
            Vector2.Angle(
                TurnRadiusRouteMath2A.NormalizeDirectionOrUp(startFacingDirection),
                TurnRadiusRouteMath2A.NormalizeDirectionOrUp(toDestination));

        if (startNearSun && bearingAngle <= 60f)
            return directRouteCrossesSun ? "NearSunForwardBlocked" : "NearSunForward";

        if (startNearSun && bearingAngle >= 105f)
            return directRouteCrossesSun ? "NearSunBehindBlocked" : "NearSunBehind";

        if (directRouteCrossesSun && bearingAngle <= 60f)
            return "SunBlockedForward";

        if (directRouteCrossesSun && bearingAngle >= 105f)
            return "SunBlockedBehind";

        if (directRouteCrossesSun)
            return "SunBlockedSide";

        if (bearingAngle >= 105f)
            return "Behind";

        return "Direct";
    }

    private static Vector3 PushPositionOutsideSunBlockingRadius(
    Vector3 position,
    SunRouteObstacle2A obstacle,
    Vector2 fallbackDirection)
    {
        if (!obstacle.HasObstacle ||
            obstacle.BlockingRadius <= 0f)
        {
            return position;
        }

        Vector2 fromSunToPosition =
            new Vector2(
                position.x - obstacle.Center.x,
                position.y - obstacle.Center.y);

        float safeDistance =
            obstacle.BlockingRadius + RouteSegmentEpsilon;

        if (fromSunToPosition.magnitude >= safeDistance)
            return position;

        if (fromSunToPosition.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            fromSunToPosition =
                fallbackDirection.sqrMagnitude > DirectionThresholdSqrMagnitude
                    ? fallbackDirection.normalized
                    : Vector2.up;
        }

        Vector2 safePosition =
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y) +
            fromSunToPosition.normalized * safeDistance;

        return new Vector3(
            safePosition.x,
            safePosition.y,
            position.z);
    }

    private static void ClampPathOutsideSunBlockingRadius(
        List<Vector3> path,
        SunRouteObstacle2A obstacle,
        Vector2 fallbackDirection)
    {
        if (path == null ||
            path.Count == 0 ||
            !obstacle.HasObstacle ||
            obstacle.BlockingRadius <= 0f)
        {
            return;
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 fallback =
                i > 0
                    ? new Vector2(
                        path[i].x - path[i - 1].x,
                        path[i].y - path[i - 1].y)
                    : fallbackDirection;

            path[i] =
                PushPositionOutsideSunBlockingRadius(
                    path[i],
                    obstacle,
                    fallback);
        }
    }

    public bool TryBuildPreview(
        SystemShipRouteRequest2A request,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick,
        float distanceTravelled = 0f)
    {
        if (preview == null)
            return false;

        preview.Clear();

        SystemShipRouteResult2A routeResult =
            new SystemShipRouteResult2A();

        if (!TryBuildRoute(
                request,
                routeResult))
        {
            return false;
        }

        return FillPreviewFromPath(
            routeResult.Path,
            routeResult.EffectiveSpeed,
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            secondsPerTick,
            distanceTravelled);
    }

    private float GetFlyingPreviewRouteDistanceAtTick(
    int tickIndex,
    float passedDistance,
    float baseDistancePerTick,
    float totalPathLength,
    float currentTickRemainingFactor)
    {
        float distance =
            Mathf.Clamp(
                passedDistance,
                0f,
                totalPathLength);

        if (tickIndex <= 0)
            return distance;

        for (int currentTick = 1; currentTick <= tickIndex; currentTick++)
        {
            float tickDistance =
                currentTick == 1
                    ? baseDistancePerTick * Mathf.Clamp01(currentTickRemainingFactor)
                    : baseDistancePerTick;

            distance =
                Mathf.Min(
                    totalPathLength,
                    distance + tickDistance);

            if (distance >= totalPathLength)
                return totalPathLength;
        }

        return Mathf.Clamp(
            distance,
            0f,
            totalPathLength);
    }

    public bool FillPreviewFromPath(
    IReadOnlyList<Vector3> path,
    float speed,
    TravelRoutePreview2A preview,
    float smallDotSpacing,
    int maxBigDots,
    int maxSmallDots,
    float secondsPerTick,
    float distanceTravelled,
    float currentTickRemainingFactor = 1f)
    {
        if (preview == null)
            return false;

        preview.Clear();

        float totalPathLength =
            GetPathLength(path);

        if (totalPathLength <= 0f)
            return false;

        float safeSpeed =
            Mathf.Max(0.01f, speed);

        float safeSecondsPerTick =
            Mathf.Max(0.01f, secondsPerTick);

        float baseDistancePerTick =
            safeSpeed * safeSecondsPerTick;

        float passedDistance =
            Mathf.Clamp(
                distanceTravelled,
                0f,
                totalPathLength);

        float safeSmallDotSpacing =
            Mathf.Max(0.01f, smallDotSpacing);

        int safeMaxBigDots =
            Mathf.Max(1, maxBigDots);

        int safeMaxSmallDots =
            Mathf.Max(0, maxSmallDots);

        float safeCurrentTickRemainingFactor =
            Mathf.Clamp01(currentTickRemainingFactor);

        for (int tickIndex = 1; tickIndex <= safeMaxBigDots; tickIndex++)
        {
            float intervalStartDistance =
                GetFlyingPreviewRouteDistanceAtTick(
                    tickIndex - 1,
                    passedDistance,
                    baseDistancePerTick,
                    totalPathLength,
                    safeCurrentTickRemainingFactor);

            float distanceAtTick =
                GetFlyingPreviewRouteDistanceAtTick(
                    tickIndex,
                    passedDistance,
                    baseDistancePerTick,
                    totalPathLength,
                    safeCurrentTickRemainingFactor);

            if (distanceAtTick <= passedDistance + 0.001f)
                continue;

            AddSmallPreviewDots(
                preview,
                path,
                Mathf.Max(intervalStartDistance, passedDistance),
                distanceAtTick,
                passedDistance,
                tickIndex,
                safeSmallDotSpacing,
                safeMaxSmallDots);

            preview.AddBigDot(
                GetPointOnPathAtDistance(
                    path,
                    distanceAtTick),
                tickIndex);

            if (distanceAtTick >= totalPathLength)
                break;
        }

        return preview.HasDots;
    }

    private bool TryBuildAdjustedRoute(
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        SystemShipRouteSettings2A settings,
        SunRouteObstacle2A obstacle,
        bool useSunAvoidance,
        SystemShipRouteResult2A result,
        long budgetStartedAt)
    {
        if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "Adjusted.Start");

        float baseTurnRadius =
            Mathf.Max(0f, settings.TurnRadius);

        if (baseTurnRadius <= RouteSegmentEpsilon)
        {
            result.CandidateAttemptCount++;

            _routeProbeBuffer.Clear();
            _routeProbeBuffer.Add(startPosition);
            _routeProbeBuffer.Add(destinationPosition);

            result.LastWaypointCount = _routeProbeBuffer.Count;
            result.LastWaypointPathLength = GetPathLength(_routeProbeBuffer);
            result.SelectedRouteSource = "DirectZeroTurnRadius";

            if (!RouteAvoidsSun(
                    _routeProbeBuffer,
                    obstacle,
                    obstacle.BlockingRadius))
            {
                result.LastRejectReason = "DirectZeroTurnRadiusTouchesSunBlock";
                return false;
            }

            result.Path.Clear();
            result.Path.AddRange(_routeProbeBuffer);
            result.EffectiveSpeed = Mathf.Max(0.01f, settings.Speed);
            result.EffectiveTurnRadius = 0f;
            result.TurnRadiusFactor = 1f;
            result.SpeedFactor = 1f;
            result.UsedSunAvoidance = false;
            result.LastRejectReason = string.Empty;
            return true;
        }

        bool allowBehindSmallTurn =
            IsBehindSmallTurnCandidate(
                startPosition,
                destinationPosition,
                startFacingDirection,
                settings);

        float minFactorByAbsolute =
            baseTurnRadius > 0f
                ? Mathf.Clamp01(settings.MinTurnRadiusAbsolute / baseTurnRadius)
                : 0f;

        float minFactor =
            Mathf.Clamp01(
                Mathf.Max(
                    settings.MinTurnRadiusAdjustmentFactor,
                    minFactorByAbsolute));

        float turnStep =
            Mathf.Clamp(settings.TurnRadiusAdjustmentStepPercent, 0.1f, 50f) /
            100f;

        float speedStep =
            Mathf.Clamp(settings.SpeedAdjustmentStepPercent, 0.1f, 50f) /
            100f;

        float turnFactor =
            allowBehindSmallTurn
                ? minFactor
                : 1f;

        float speedFactor =
            allowBehindSmallTurn
                ? Mathf.Max(
                    minFactor,
                    1f - Mathf.CeilToInt((1f - minFactor) / turnStep) * speedStep)
                : 1f;

        while (allowBehindSmallTurn
                   ? turnFactor <= 1f + 0.0001f
                   : turnFactor >= minFactor - 0.0001f)
        {
            if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
                return RejectRouteBuildByTimeBudget(settings, result, "Adjusted.AttemptLoop");

            result.CandidateAttemptCount++;

            turnFactor =
                Mathf.Clamp01(turnFactor);

            speedFactor =
                Mathf.Clamp01(speedFactor);

            float adjustedTurnRadius =
                GetAdjustedTurnRadius(
                    baseTurnRadius,
                    turnFactor,
                    settings);

            float adjustedSpeed =
                Mathf.Max(
                    0.01f,
                    settings.Speed * speedFactor);

            BuildWaypoints(
                _waypointsBuffer,
                startPosition,
                destinationPosition,
                startFacingDirection,
                adjustedTurnRadius,
                settings,
                obstacle,
                useSunAvoidance);

            if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
                return RejectRouteBuildByTimeBudget(settings, result, "Adjusted.AfterBuildWaypoints");

            result.LastWaypointCount = _waypointsBuffer.Count;
            result.LastWaypointPathLength = GetPathLength(_waypointsBuffer);
            result.SelectedRouteSource =
                useSunAvoidance
                    ? "AdjustedSunAvoidance"
                    : "AdjustedDirect";

            bool routeBuilt =
                TryBuildPathFromWaypoints(
                    _waypointsBuffer,
                    startFacingDirection,
                    adjustedSpeed,
                    adjustedTurnRadius,
                    settings,
                    _routeProbeBuffer,
                    result,
                    budgetStartedAt);

            if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
                return RejectRouteBuildByTimeBudget(settings, result, "Adjusted.AfterPlanner");

            bool routeAvoidsSun =
                routeBuilt &&
                RouteAvoidsSun(
                    _routeProbeBuffer,
                    obstacle,
                    obstacle.BlockingRadius);

            if (routeBuilt && routeAvoidsSun)
            {
                result.Path.Clear();
                result.Path.AddRange(_routeProbeBuffer);
                result.EffectiveSpeed = adjustedSpeed;
                result.EffectiveTurnRadius = adjustedTurnRadius;
                result.TurnRadiusFactor = turnFactor;
                result.SpeedFactor = speedFactor;
                result.UsedSunAvoidance = useSunAvoidance;
                result.UsedRawSafeFallback = false;
                result.LastRejectReason = string.Empty;
                return true;
            }

            if (!routeBuilt)
            {
                result.LastRejectReason =
                    string.IsNullOrWhiteSpace(result.LastRejectReason)
                        ? "AdjustedPreviewBuildFailed"
                        : result.LastRejectReason;
            }
            else if (!routeAvoidsSun)
            {
                result.LastRejectReason = "AdjustedRouteTouchesSunBlock";
            }

            if (allowBehindSmallTurn)
            {
                turnFactor += turnStep;

                speedFactor =
                    Mathf.Min(
                        1f,
                        speedFactor + speedStep);

                if (Mathf.Approximately(turnFactor, 1f))
                    turnFactor = 1f;
            }
            else
            {
                turnFactor -= turnStep;

                speedFactor =
                    Mathf.Max(
                        minFactor,
                        speedFactor - speedStep);

                if (Mathf.Approximately(turnFactor, minFactor))
                    turnFactor = minFactor;
            }
        }

        return false;
    }

    private static bool IsBehindSmallTurnCandidate(
    Vector3 startPosition,
    Vector3 destinationPosition,
    Vector2 startFacingDirection,
    SystemShipRouteSettings2A settings)
    {
        Vector2 toDestination =
            new Vector2(
                destinationPosition.x - startPosition.x,
                destinationPosition.y - startPosition.y);

        if (toDestination.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        Vector2 facing =
            TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                startFacingDirection);

        float angle =
            Vector2.Angle(
                facing,
                toDestination.normalized);

        float tolerance =
            settings != null
                ? Mathf.Clamp(settings.BehindSmallTurnAngleToleranceDegrees, 0f, 90f)
                : 75f;

        return angle >= 180f - tolerance;
    }

    private static float GetAdjustedTurnRadius(
    float baseTurnRadius,
    float turnFactor,
    SystemShipRouteSettings2A settings)
    {
        float radius =
            Mathf.Max(
                RouteSegmentEpsilon,
                baseTurnRadius * Mathf.Clamp01(turnFactor));

        float minAbsolute =
            settings != null
                ? Mathf.Max(0f, settings.MinTurnRadiusAbsolute)
                : 0f;

        return Mathf.Max(
            minAbsolute,
            radius);
    }

    private void BuildWaypoints(
        List<Vector3> waypoints,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        float turnRadius,
        SystemShipRouteSettings2A settings,
        SunRouteObstacle2A obstacle,
        bool useSunAvoidance)
    {
        waypoints.Clear();

        if (!useSunAvoidance ||
            !settings.AllowSunAvoidance ||
            !obstacle.HasObstacle)
        {
            waypoints.Add(startPosition);
            waypoints.Add(destinationPosition);
            return;
        }

        SystemTravelSunAvoidancePath2A.BuildPath(
            waypoints,
            startPosition,
            destinationPosition,
            obstacle.Center,
            obstacle.Radius,
            Mathf.Max(2, settings.SunAvoidanceArcSegments),
            false,
            startFacingDirection,
            turnRadius);
    }

    private bool TryBuildPathFromWaypoints(
        IReadOnlyList<Vector3> waypoints,
        Vector2 startFacingDirection,
        float speed,
        float turnRadius,
        SystemShipRouteSettings2A settings,
        List<Vector3> routePath,
        SystemShipRouteResult2A result,
        long budgetStartedAt)
    {
        if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "Planner.Start");

        float routeStepDistance =
            Mathf.Max(
                settings.ArrivalDistanceThreshold,
                speed / Mathf.Max(1, settings.RouteSubstepsPerTick));

        bool built =
            TurnRadiusRouteMath2A.TryBuildLimitedWaypointPreviewPath(
                routePath,
                waypoints,
                startFacingDirection,
                routeStepDistance,
                turnRadius,
                settings.ArrivalDistanceThreshold,
                settings.MaxRoutePlanSteps,
                settings.SunAvoidanceTurnRouteReserveMultiplier,
                out int maxSteps,
                out float intermediateWaypointArrivalDistanceThreshold,
                out float routeLength,
                out float maxAllowedRouteLength,
                settings.RouteStraightExitAngleDegrees,
                settings.DebugLog,
                settings.DebugPrefix,
                () => IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt));

        if (result != null)
        {
            result.LastPlannerRouteStepDistance = routeStepDistance;
            result.LastPlannerMaxSteps = maxSteps;
            result.LastPlannerIntermediateArrivalThreshold = intermediateWaypointArrivalDistanceThreshold;
            result.LastPlannerRouteLength = routeLength;
            result.LastPlannerMaxAllowedRouteLength = maxAllowedRouteLength;
            result.LastWaypointCount = waypoints != null ? waypoints.Count : 0;
            result.LastWaypointPathLength = GetPathLength(waypoints);

            if (!built)
            {
                result.LastRejectReason =
                    routePath == null || routePath.Count <= 1
                        ? "TryBuildLimitedWaypointPreviewPathFailed"
                        : "TryBuildLimitedWaypointPreviewPathRejected";
            }
        }

        if (IsRouteBuildTimeBudgetExceeded(settings, budgetStartedAt))
            return RejectRouteBuildByTimeBudget(settings, result, "Planner.AbortedByTimeBudget");

        return built;
    }

    private static double GetElapsedMilliseconds(long startedAt)
    {
        long elapsedTicks =
            Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000.0 / Stopwatch.Frequency;
    }

    private void LogExpensiveRouteBuildIfNeeded(
        long startedAt,
        SystemShipRouteRequest2A request,
        SystemShipRouteSettings2A settings,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        SunRouteObstacle2A obstacle,
        bool success,
        SystemShipRouteResult2A result)
    {
        double elapsedMs =
            GetElapsedMilliseconds(startedAt);

        float thresholdMs =
            GetExpensiveRouteBuildLogThresholdMs();

        if (elapsedMs < thresholdMs)
            return;

        if (Bootstrapper.Instance == null)
            return;

        float directDistance =
            Vector3.Distance(
                startPosition,
                destinationPosition);

        float startToSunDistance =
            obstacle.HasObstacle
                ? Vector2.Distance(
                    new Vector2(startPosition.x, startPosition.y),
                    new Vector2(obstacle.Center.x, obstacle.Center.y))
                : 0f;

        float destinationToSunDistance =
            obstacle.HasObstacle
                ? Vector2.Distance(
                    new Vector2(destinationPosition.x, destinationPosition.y),
                    new Vector2(obstacle.Center.x, obstacle.Center.y))
                : 0f;

        float finalPathLength =
            result != null && result.Path != null
                ? GetPathLength(result.Path)
                : 0f;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovement,
            "[SystemShipRouteService2A] SHIP_ROUTE_BUILD_EXPENSIVE" +
            " | Success=" + success +
            " | Ms=" + elapsedMs.ToString("F2") +
            " | ThresholdMs=" + thresholdMs.ToString("0.###") +
            " | SystemId=" + (request != null ? request.SystemId : string.Empty) +
            " | TargetKind=" + (request != null ? request.TargetKind.ToString() : string.Empty) +
            " | Start=" + startPosition +
            " | Destination=" + destinationPosition +
            " | DirectDistance=" + directDistance.ToString("0.###") +
            " | StartFacing=" + startFacingDirection +
            " | HasSunObstacle=" + obstacle.HasObstacle +
            " | SunCenter=" + obstacle.Center +
            " | SunRadius=" + obstacle.Radius.ToString("0.###") +
            " | SunBlockingRadius=" + obstacle.BlockingRadius.ToString("0.###") +
            " | StartToSunDistance=" + startToSunDistance.ToString("0.###") +
            " | DestinationToSunDistance=" + destinationToSunDistance.ToString("0.###") +
            " | DestinationCase=" + (result != null ? result.DestinationCase : string.Empty) +
            " | SelectedRouteSource=" + (result != null ? result.SelectedRouteSource : string.Empty) +
            " | LastRejectReason=" + (result != null ? result.LastRejectReason : string.Empty) +
            " | CandidateAttemptCount=" + (result != null ? result.CandidateAttemptCount : 0) +
            " | RawFallbackAttemptCount=" + (result != null ? result.RawFallbackAttemptCount : 0) +
            " | UsedRawSafeFallback=" + (result != null && result.UsedRawSafeFallback) +
            " | UsedSunAvoidance=" + (result != null && result.UsedSunAvoidance) +
            " | DirectRouteCrossesSun=" + (result != null && result.DirectRouteCrossesSun) +
            " | PathCount=" + (result != null && result.Path != null ? result.Path.Count : 0) +
            " | PathLength=" + finalPathLength.ToString("0.###") +
            " | WaypointCount=" + (result != null ? result.LastWaypointCount : 0) +
            " | WaypointPathLength=" + (result != null ? result.LastWaypointPathLength.ToString("0.###") : "0") +
            " | PlannerMaxSteps=" + (result != null ? result.LastPlannerMaxSteps : 0) +
            " | PlannerRouteStepDistance=" + (result != null ? result.LastPlannerRouteStepDistance.ToString("0.###") : "0") +
            " | PlannerIntermediateArrivalThreshold=" + (result != null ? result.LastPlannerIntermediateArrivalThreshold.ToString("0.###") : "0") +
            " | PlannerRouteLength=" + (result != null ? result.LastPlannerRouteLength.ToString("0.###") : "0") +
            " | PlannerMaxAllowedRouteLength=" + (result != null ? result.LastPlannerMaxAllowedRouteLength.ToString("0.###") : "0") +
            " | BaseSpeed=" + (settings != null ? settings.Speed.ToString("0.###") : "0") +
            " | EffectiveSpeed=" + (result != null ? result.EffectiveSpeed.ToString("0.###") : "0") +
            " | SpeedFactor=" + (result != null ? result.SpeedFactor.ToString("0.###") : "0") +
            " | BaseTurnRadius=" + (settings != null ? settings.TurnRadius.ToString("0.###") : "0") +
            " | EffectiveTurnRadius=" + (result != null ? result.EffectiveTurnRadius.ToString("0.###") : "0") +
            " | TurnRadiusFactor=" + (result != null ? result.TurnRadiusFactor.ToString("0.###") : "0") +
            " | ArrivalThreshold=" + (settings != null ? settings.ArrivalDistanceThreshold.ToString("0.###") : "0") +
            " | RouteSubstepsPerTick=" + (settings != null ? settings.RouteSubstepsPerTick : 0) +
            " | MaxRoutePlanSteps=" + (settings != null ? settings.MaxRoutePlanSteps : 0) +
            " | StraightExitAngle=" + (settings != null ? settings.RouteStraightExitAngleDegrees.ToString("0.###") : "0") +
            " | TurnRadiusAdjustmentStepPercent=" + (settings != null ? settings.TurnRadiusAdjustmentStepPercent.ToString("0.###") : "0") +
            " | SpeedAdjustmentStepPercent=" + (settings != null ? settings.SpeedAdjustmentStepPercent.ToString("0.###") : "0"));
    }

    private float GetExpensiveRouteBuildLogThresholdMs()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.DebugLogConfig == null)
        {
            return (float)ExpensiveRouteBuildLogThresholdMs;
        }

        return Bootstrapper
            .Instance
            .DebugLogConfig
            .ShipRouteBuildDiagnosticsThresholdMs;
    }

    private SystemShipRouteSettings2A CreateDefaultSettings()
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return new SystemShipRouteSettings2A
        {
            Speed = 100f,
            TurnRadius = 60f,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f
        };
    }

    private SunRouteObstacle2A GetSunObstacle(
    string systemId,
    float z,
    SystemShipRouteSettings2A settings)
    {
        if (_configService == null ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return SunRouteObstacle2A.None;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            return SunRouteObstacle2A.None;
        }

        SunConfig sun =
            starSystem.Sun;

        Vector3 center =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                z);

        float sunRadius =
            Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);

        float safeRadius =
            sunRadius + settings.SunAvoidanceSafetyMargin;

        return new SunRouteObstacle2A(
            center,
            safeRadius,
            safeRadius);
    }

    private float GetSunWorldSize(SunConfig sun)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService.SystemVisualConfig.GetSunWorldSize(sun);
        }

        return sun != null
            ? sun.VisualSize
            : 0f;
    }

    private static void AddSmallPreviewDots(
        TravelRoutePreview2A preview,
        IReadOnlyList<Vector3> path,
        float intervalStartDistance,
        float intervalEndDistance,
        float passedDistance,
        int tickIndex,
        float smallDotSpacing,
        int maxSmallDots)
    {
        if (preview == null ||
            path == null ||
            path.Count <= 1)
        {
            return;
        }

        if (preview.SmallDotCount >= maxSmallDots)
            return;

        float firstDotDistance =
            Mathf.Ceil(
                (Mathf.Max(intervalStartDistance, passedDistance) + 0.001f) /
                smallDotSpacing) *
            smallDotSpacing;

        for (float dotDistance = firstDotDistance;
             dotDistance < intervalEndDistance - 0.001f;
             dotDistance += smallDotSpacing)
        {
            if (preview.SmallDotCount >= maxSmallDots)
                return;

            preview.AddSmallDot(
                GetPointOnPathAtDistanceStatic(path, dotDistance),
                tickIndex);
        }
    }

    public float GetPathLength(
        IReadOnlyList<Vector3> path)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
    }

    public Vector3 GetPointOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        return GetPointOnPathAtDistanceStatic(
            path,
            distance);
    }

    private static Vector3 GetPointOnPathAtDistanceStatic(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count == 0)
        {
            return Vector3.zero;
        }

        if (path.Count == 1)
            return path[0];

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                float t = remainingDistance / segmentDistance;
                return Vector3.Lerp(from, to, t);
            }

            remainingDistance -= segmentDistance;
        }

        return path[path.Count - 1];
    }

    public Vector2 GetDirectionOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return Vector2.up;
        }

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
                return NormalizeDirectionOrUp(to - from);

            remainingDistance -= segmentDistance;
        }

        return NormalizeDirectionOrUp(
            path[path.Count - 1] - path[path.Count - 2]);
    }

    private static Vector2 NormalizeDirectionOrUp(
        Vector3 direction)
    {
        direction.z = 0f;

        if (direction.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return Vector2.up;

        return new Vector2(direction.x, direction.y).normalized;
    }

    private static bool RouteAvoidsSun(
        IReadOnlyList<Vector3> path,
        SunRouteObstacle2A obstacle,
        float radius)
    {
        if (!obstacle.HasObstacle ||
            radius <= 0f ||
            path == null ||
            path.Count <= 1)
        {
            return true;
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (Vector3.Distance(path[i], obstacle.Center) < radius)
                return false;

            if (i == 0)
                continue;

            if (SegmentIntersectsCircle(
                    path[i - 1],
                    path[i],
                    obstacle.Center,
                    radius))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentIntersectsCircle(
        Vector3 start,
        Vector3 end,
        Vector3 center,
        float radius)
    {
        Vector2 start2 = new Vector2(start.x, start.y);
        Vector2 end2 = new Vector2(end.x, end.y);
        Vector2 center2 = new Vector2(center.x, center.y);

        Vector2 segment = end2 - start2;
        float segmentLengthSqr = segment.sqrMagnitude;

        if (segmentLengthSqr <= RouteSegmentEpsilon)
            return Vector2.Distance(start2, center2) <= radius;

        float t =
            Vector2.Dot(center2 - start2, segment) /
            segmentLengthSqr;

        t = Mathf.Clamp01(t);

        Vector2 closestPoint =
            start2 + segment * t;

        return Vector2.Distance(closestPoint, center2) < radius;
    }

    private readonly struct SunRouteObstacle2A
    {
        public static readonly SunRouteObstacle2A None =
            new SunRouteObstacle2A(Vector3.zero, 0f, 0f);

        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float BlockingRadius;

        public bool HasObstacle => Radius > 0f;

        public SunRouteObstacle2A(
            Vector3 center,
            float radius,
            float blockingRadius)
        {
            Center = center;
            Radius = radius;
            BlockingRadius = blockingRadius;
        }
    }

    private static bool IsRouteBuildTimeBudgetExceeded(
    SystemShipRouteSettings2A settings,
    long localStartedAt)
    {
        if (settings == null)
            return false;

        if (settings.RouteBuildTimeBudgetMs <= 0f)
            return false;

        double externalUsedMs =
            settings.GetRouteBuildBudgetUsedMs != null
                ? settings.GetRouteBuildBudgetUsedMs()
                : 0d;

        double localElapsedMs =
            GetElapsedMilliseconds(localStartedAt);

        return externalUsedMs + localElapsedMs >= settings.RouteBuildTimeBudgetMs;
    }

    private static bool RejectRouteBuildByTimeBudget(
        SystemShipRouteSettings2A settings,
        SystemShipRouteResult2A result,
        string stage)
    {
        if (result != null)
            result.LastRejectReason = "TimeBudgetExceeded:" + stage;

        return false;
    }

    private static void ConsumeRouteBuildBudget(
        SystemShipRouteSettings2A settings,
        long localStartedAt)
    {
        if (settings == null ||
            settings.AddRouteBuildBudgetUsedMs == null)
        {
            return;
        }

        settings.AddRouteBuildBudgetUsedMs(
            GetElapsedMilliseconds(localStartedAt));
    }
}