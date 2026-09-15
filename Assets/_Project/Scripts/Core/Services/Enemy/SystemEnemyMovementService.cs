using System.Collections.Generic;
using UnityEngine;

public sealed class SystemEnemyMovementService : CustomService, ISystemEnemyMovementService
{
    private const float ArrivalDistanceThreshold = 3f;
    private const float DestinationRefreshDistance = 40f;
    private const float DirectionThresholdSqrMagnitude = 0.0001f;
    private const float SunAvoidanceSafetyMargin = 80f;
    private const int SunAvoidanceArcSegments = 18;
    private const int RoutePlanMaxSteps = 8192;
    private const float TurnSpikeAngleThresholdDegrees = 120f;

    private readonly Dictionary<string, EnemyRouteState> _routes = new();
    private readonly SystemShipRouteResult2A _routeBuildResult = new();
    private readonly List<Vector3> _previewRoutePathBuffer = new();

    private readonly ISystemEnemyService _enemyService;
    private readonly ISystemEncounterService _encounterService;
    private readonly IPlayerCombatTargetService _playerTargetService;
    private readonly ISystemShipRouteService2A _routeService;
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;

    public SystemEnemyMovementService()
    {
        _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        _playerTargetService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerCombatTargetService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemShipRouteService2A>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public void Tick(float deltaTime, int currentTick)
    {
        if (!_encounterService.HasActiveEncounter)
            return;

        ActiveSystemEncounter encounter = _encounterService.Current;

        if (encounter == null ||
            string.IsNullOrWhiteSpace(encounter.SystemId))
        {
            return;
        }

        if (!_playerTargetService.IsPlayerAvailableInSystem(encounter.SystemId))
            return;

        Vector3 playerPosition = _playerTargetService.GetPlayerPosition();

        IReadOnlyList<SystemEnemyRuntimeState> enemies =
            _enemyService.GetAliveEnemiesInSystem(encounter.SystemId);

        for (int i = 0; i < enemies.Count; i++)
        {
            SystemEnemyRuntimeState enemy = enemies[i];

            if (enemy == null || !enemy.IsAlive)
                continue;

            Vector3 destination = playerPosition;
            destination.z = enemy.Position.z;

            TickEnemy(enemy, destination, deltaTime);
        }
    }

    public bool TryBuildRoutePreview2A(
        string runtimeEnemyId,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick)
    {
        if (preview == null ||
            string.IsNullOrWhiteSpace(runtimeEnemyId) ||
            !_enemyService.TryGetEnemy(runtimeEnemyId, out SystemEnemyRuntimeState enemy) ||
            enemy == null ||
            !enemy.IsAlive)
        {
            return false;
        }

        preview.Clear();

        Vector3 destination = enemy.DestinationPosition;

        if (!IsUsableDestination(enemy, destination))
        {
            if (!_encounterService.HasActiveEncounter ||
                !_playerTargetService.IsPlayerAvailableInSystem(enemy.SystemId))
            {
                return false;
            }

            destination = _playerTargetService.GetPlayerPosition();
            destination.z = enemy.Position.z;
        }

        IReadOnlyList<Vector3> path;
        float passedDistance;

        if (_routes.TryGetValue(runtimeEnemyId, out EnemyRouteState activeRoute) &&
            activeRoute.Path.Count > 1)
        {
            path = activeRoute.Path;
            passedDistance = activeRoute.DistanceTravelled;
        }
        else
        {
            if (!TryBuildRoute(enemy, destination, _previewRoutePathBuffer))
                return false;

            path = _previewRoutePathBuffer;
            passedDistance = 0f;
        }

        return _routeService.FillPreviewFromPath(
            path,
            Mathf.Max(0.01f, enemy.Speed),
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            secondsPerTick,
            passedDistance);
    }

    private void TickEnemy(
        SystemEnemyRuntimeState enemy,
        Vector3 destination,
        float deltaTime)
    {
        if (_routeService == null)
            return;

        EnemyRouteState routeState =
            GetOrCreateRouteState(enemy.RuntimeEnemyId);

        bool debugMovement =
            IsEnemyMovementDebugEnabled();

        Vector3 previousPosition = enemy.Position;
        Vector3 previousFacingDirection = enemy.FacingDirection;

        bool shouldRebuild =
            routeState.Path.Count <= 1 ||
            Vector3.Distance(routeState.Destination, destination) >
            DestinationRefreshDistance;

        if (shouldRebuild)
        {
            Vector2 startFacingDirection =
                GetSafeFacingDirection(enemy, destination);

            if (debugMovement)
            {
                LogMovementDebug(
                    "[ENEMY-ROUTE-BUILD] Start" +
                    " | EnemyId=" + enemy.RuntimeEnemyId +
                    " | SystemId=" + enemy.SystemId +
                    " | Start=" + FormatVector3(enemy.Position) +
                    " | Destination=" + FormatVector3(destination) +
                    " | CurrentFacing=" + FormatVector3(enemy.FacingDirection) +
                    " | StartFacingForRoute=" + FormatVector2(startFacingDirection) +
                    " | Speed=" + enemy.Speed.ToString("0.###"));
            }

            if (!TryBuildRoute(enemy, destination, routeState.Path))
            {
                if (debugMovement)
                {
                    LogMovementDebug(
                        "[ENEMY-ROUTE-BUILD] Failed" +
                        " | EnemyId=" + enemy.RuntimeEnemyId +
                        " | Start=" + FormatVector3(enemy.Position) +
                        " | Destination=" + FormatVector3(destination) +
                        " | Facing=" + FormatVector2(startFacingDirection));
                }

                ClearRoute(enemy.RuntimeEnemyId);
                return;
            }

            routeState.Destination = destination;
            routeState.DistanceTravelled = 0f;

            if (debugMovement)
            {
                Vector2 firstPathDirection =
                    GetFirstPathDirection(routeState.Path);

                float firstTurnAngle =
                    GetSignedAngle(startFacingDirection, firstPathDirection);

                LogMovementDebug(
                    "[ENEMY-ROUTE-BUILD] Success" +
                    " | EnemyId=" + enemy.RuntimeEnemyId +
                    " | DestinationCase=" + _routeBuildResult.DestinationCase +
                    " | PathCount=" + routeState.Path.Count +
                    " | PathLength=" + _routeBuildResult.PathLength.ToString("0.###") +
                    " | EffectiveSpeed=" + _routeBuildResult.EffectiveSpeed.ToString("0.###") +
                    " | EffectiveTurnRadius=" + _routeBuildResult.EffectiveTurnRadius.ToString("0.###") +
                    " | UsedSunAvoidance=" + _routeBuildResult.UsedSunAvoidance +
                    " | FirstPathDirection=" + FormatVector2(firstPathDirection) +
                    " | FirstTurnAngle=" + firstTurnAngle.ToString("0.###") +
                    " | " + FormatPathHead(routeState.Path, 5));
            }
        }

        float totalLength =
            _routeService.GetPathLength(routeState.Path);

        if (totalLength <= ArrivalDistanceThreshold)
            return;

        float movementDistance =
            Mathf.Max(0f, enemy.Speed) *
            GetNormalizedTickDeltaTime(deltaTime);

        float nextDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled + movementDistance,
                0f,
                totalLength);

        Vector3 nextPosition =
            _routeService.GetPointOnPathAtDistance(
                routeState.Path,
                nextDistance);

        Vector2 routeDirection =
            _routeService.GetDirectionOnPathAtDistance(
                routeState.Path,
                nextDistance);

        Vector3 nextFacingDirection = enemy.FacingDirection;

        if (routeDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
        {
            nextFacingDirection =
                new Vector3(
                    routeDirection.x,
                    routeDirection.y,
                    0f).normalized;

            enemy.FacingDirection = nextFacingDirection;
        }

        nextPosition.z = enemy.Position.z;

        enemy.Position = nextPosition;
        enemy.DestinationPosition = destination;
        enemy.TravelProgress01 =
            totalLength > 0f
                ? Mathf.Clamp01(nextDistance / totalLength)
                : 1f;

        routeState.DistanceTravelled = nextDistance;

        if (debugMovement)
        {
            float turnAngle =
                GetSignedAngle(previousFacingDirection, nextFacingDirection);

            if (Mathf.Abs(turnAngle) >= GetMovementTurnSpikeAngleDegrees())
            {
                LogMovementDebug(
                    "[ENEMY-TURN-SPIKE]" +
                    " | EnemyId=" + enemy.RuntimeEnemyId +
                    " | TurnAngle=" + turnAngle.ToString("0.###") +
                    " | PreviousPosition=" + FormatVector3(previousPosition) +
                    " | NextPosition=" + FormatVector3(enemy.Position) +
                    " | PreviousFacing=" + FormatVector3(previousFacingDirection) +
                    " | NextFacing=" + FormatVector3(nextFacingDirection) +
                    " | RouteDirection=" + FormatVector2(routeDirection) +
                    " | DistanceTravelled=" + nextDistance.ToString("0.###") +
                    " | TotalLength=" + totalLength.ToString("0.###") +
                    " | Destination=" + FormatVector3(destination) +
                    " | " + FormatPathHead(routeState.Path, 5));
            }
        }

        _eventBus.Publish(new SystemEnemyPositionChangedEvent(
            enemy.RuntimeEnemyId,
            enemy.Position,
            enemy.FacingDirection));

        if (totalLength - nextDistance <= ArrivalDistanceThreshold)
            ClearRoute(enemy.RuntimeEnemyId);
    }

    private static Vector2 GetDirectionToDestination(
        Vector3 from,
        Vector3 to)
    {
        Vector3 direction = to - from;
        direction.z = 0f;

        if (direction.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return Vector2.up;

        return new Vector2(direction.x, direction.y).normalized;
    }

    private static float GetSignedAngle(
        Vector3 fromDirection,
        Vector2 toDirection)
    {
        Vector3 from = fromDirection;
        from.z = 0f;

        if (from.sqrMagnitude <= DirectionThresholdSqrMagnitude ||
            toDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return 0f;
        }

        Vector2 from2 =
            new Vector2(from.x, from.y).normalized;

        return Vector2.SignedAngle(
            from2,
            toDirection.normalized);
    }

    private static string FormatPathHead(
        IReadOnlyList<Vector3> path)
    {
        if (path == null)
            return "null";

        if (path.Count == 0)
            return "empty";

        int count = Mathf.Min(path.Count, 5);
        List<string> points = new List<string>();

        for (int i = 0; i < count; i++)
            points.Add(i + ":" + FormatVector3(path[i]));

        if (path.Count > count)
            points.Add("... last:" + FormatVector3(path[path.Count - 1]));

        return string.Join(" | ", points);
    }

    private static string FormatVector3(Vector3 value)
    {
        return "(" +
               value.x.ToString("0.###") + ", " +
               value.y.ToString("0.###") + ", " +
               value.z.ToString("0.###") + ")";
    }

    private static string FormatVector2(Vector2 value)
    {
        return "(" +
               value.x.ToString("0.###") + ", " +
               value.y.ToString("0.###") + ")";
    }

    private bool TryBuildRoute(
        SystemEnemyRuntimeState enemy,
        Vector3 destination,
        List<Vector3> routePath)
    {
        if (enemy == null ||
            routePath == null ||
            _routeService == null)
        {
            return false;
        }

        routePath.Clear();

        SystemShipRouteRequest2A request =
            new SystemShipRouteRequest2A
            {
                SystemId = enemy.SystemId,
                StartPosition = enemy.Position,
                DestinationPosition = destination,
                StartFacingDirection = GetSafeFacingDirection(enemy, destination),
                TargetKind = SystemShipRouteTargetKind2A.Enemy,
                Settings = CreateRouteSettings(enemy)
            };

        bool routeBuilt =
            _routeService.TryBuildRoute(
                request,
                _routeBuildResult);

        if (!routeBuilt ||
            _routeBuildResult.Path == null ||
            _routeBuildResult.Path.Count <= 1)
        {
            return false;
        }

        routePath.AddRange(_routeBuildResult.Path);
        return true;
    }

    private SystemShipRouteSettings2A CreateRouteSettings(
        SystemEnemyRuntimeState enemy)
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        System.Action<string> routeDebugLog =
            ShouldLogShipRouteInternals()
                ? Bootstrapper.Instance.CreateDebugLogAction(DebugLogChannel.ShipRoute)
                : null;

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, enemy != null ? enemy.Speed : 0f),
            TurnRadius = enemy != null && enemy.EnemyConfig != null
                ? Mathf.Max(0f, enemy.EnemyConfig.TurnRadius)
                : 0f,
            ArrivalDistanceThreshold = ArrivalDistanceThreshold,
            SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
            SunAvoidanceArcSegments = SunAvoidanceArcSegments,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f,
            MaxRoutePlanSteps = RoutePlanMaxSteps,
            SunAvoidanceTurnRouteReserveMultiplier = 1.5f,
            DebugLog = routeDebugLog,
            DebugPrefix = "[EnemyMovement] "
        };
    }

    private bool IsEnemyMovementDebugEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.EnemyMovement);
    }

    private bool ShouldLogShipRouteInternals()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.IncludeShipRouteInternalLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.ShipRoute);
    }

    private float GetMovementTurnSpikeAngleDegrees()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null
            ? debugLogConfig.MovementTurnSpikeAngleDegrees
            : 120f;
    }

    private void LogMovementDebug(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.EnemyMovement,
            "[SystemEnemyMovementService] " + message);
    }

    private Vector2 GetFirstPathDirection(IReadOnlyList<Vector3> path)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return Vector2.zero;
        }

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 direction = path[i] - path[i - 1];
            direction.z = 0f;

            if (direction.sqrMagnitude > DirectionThresholdSqrMagnitude)
                return new Vector2(direction.x, direction.y).normalized;
        }

        return Vector2.zero;
    }

    private float GetSignedAngle(Vector3 from, Vector3 to)
    {
        return GetSignedAngle(
            new Vector2(from.x, from.y),
            new Vector2(to.x, to.y));
    }

    private float GetSignedAngle(Vector2 from, Vector2 to)
    {
        if (from.sqrMagnitude <= DirectionThresholdSqrMagnitude ||
            to.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return 0f;
        }

        return Vector2.SignedAngle(from.normalized, to.normalized);
    }

    private string FormatPathHead(
        IReadOnlyList<Vector3> path,
        int maxPoints)
    {
        if (path == null)
            return "Path=null";

        int pointCount =
            Mathf.Min(
                Mathf.Max(0, maxPoints),
                path.Count);

        string result =
            "PathCount=" + path.Count +
            " | PathHead=";

        for (int i = 0; i < pointCount; i++)
        {
            if (i > 0)
                result += " -> ";

            result += FormatVector3(path[i]);
        }

        return result;
    }

    private Vector2 GetSafeFacingDirection(
        SystemEnemyRuntimeState enemy,
        Vector3 destination)
    {
        if (enemy != null)
        {
            Vector3 facing = enemy.FacingDirection;
            facing.z = 0f;

            if (facing.sqrMagnitude > DirectionThresholdSqrMagnitude)
                return new Vector2(facing.x, facing.y).normalized;

            Vector3 toDestination = destination - enemy.Position;
            toDestination.z = 0f;

            if (toDestination.sqrMagnitude > DirectionThresholdSqrMagnitude)
                return new Vector2(toDestination.x, toDestination.y).normalized;
        }

        return Vector2.up;
    }

    private float GetNormalizedTickDeltaTime(float deltaTime)
    {
        return Mathf.Max(0f, deltaTime) /
               Mathf.Max(0.01f, GameTimeState.SecondsPerDay);
    }

    private bool IsUsableDestination(
        SystemEnemyRuntimeState enemy,
        Vector3 destination)
    {
        if (enemy == null)
            return false;

        if (!IsFinite(destination))
            return false;

        return Vector3.Distance(enemy.Position, destination) >
               ArrivalDistanceThreshold;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) &&
               !float.IsInfinity(value.y) &&
               !float.IsInfinity(value.z);
    }

    private EnemyRouteState GetOrCreateRouteState(string runtimeEnemyId)
    {
        if (!_routes.TryGetValue(runtimeEnemyId, out EnemyRouteState routeState))
        {
            routeState = new EnemyRouteState();
            _routes[runtimeEnemyId] = routeState;
        }

        return routeState;
    }

    private void ClearRoute(string runtimeEnemyId)
    {
        if (string.IsNullOrWhiteSpace(runtimeEnemyId))
            return;

        _routes.Remove(runtimeEnemyId);
    }

    private sealed class EnemyRouteState
    {
        public readonly List<Vector3> Path = new();
        public Vector3 Destination;
        public float DistanceTravelled;
    }
}