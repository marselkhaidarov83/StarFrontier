using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SystemMapMover))]
[RequireComponent(typeof(EnemySystemMapEntity))]
public sealed class EnemySystemMovementController : MonoBehaviour
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;
    private const float ArrivalDistanceThreshold = 3f;
    private const float DestinationRefreshDistance = 40f;
    private const int RoutePlanMaxSteps = 8192;

    [Header("Movement")]
    [SerializeField] private EnemyMovementMode movementMode = EnemyMovementMode.PatrolAroundSpawn;
    [SerializeField] private float patrolRadius = 2f;
    [SerializeField] private float pointReachedDistance = 3f;

    [Header("Debug")]
    [SerializeField] private Transform debugFollowTarget;

    private readonly List<Vector3> _activeRoutePath = new();
    private readonly SystemShipRouteResult2A _routeBuildResult = new();

    private SystemMapMover _mover;
    private EnemySystemMapEntity _enemyView;
    private ISystemShipRouteService2A _routeService;
    private ISystemEnemyService _enemyService;
    private IConfigService _configService;

    private Vector3 _spawnPosition;
    private Vector3 _currentDestination;
    private Vector3 _routeDestination;
    private Vector2 _facingDirection = Vector2.up;
    private float _routeDistanceTravelled;
    private bool _hasDestination;

    private void Awake()
    {
        _mover = GetComponent<SystemMapMover>();
        _enemyView = GetComponent<EnemySystemMapEntity>();
        ResolveServices();
    }

    private void Start()
    {
        _spawnPosition = transform.position;
        PickNewPatrolPoint();
    }

    private void Update()
    {
        if (_enemyView == null || !_enemyView.IsBound)
            return;

        ResolveServices();

        switch (movementMode)
        {
            case EnemyMovementMode.None:
                ClearActiveRoute();
                break;

            case EnemyMovementMode.MoveToPoint:
                TickMoveToPoint();
                break;

            case EnemyMovementMode.PatrolAroundSpawn:
                TickPatrolAroundSpawn();
                break;

            case EnemyMovementMode.FollowTarget:
                TickFollowTarget();
                break;
        }

        SyncRuntimePosition();
    }

    public void ApplyRuntimeConfig(SystemEnemyRuntimeState runtimeEnemy)
    {
        if (runtimeEnemy == null)
            return;

        _mover.SetSpeed(runtimeEnemy.Speed);
        _mover.SetStopDistance(pointReachedDistance);
    }

    public void SetMovementMode(EnemyMovementMode mode)
    {
        if (movementMode == mode)
            return;

        movementMode = mode;
        ClearActiveRoute();
    }

    public void SetDestination(Vector3 destination)
    {
        _currentDestination = destination;
        _currentDestination.z = transform.position.z;
        _hasDestination = true;
        movementMode = EnemyMovementMode.MoveToPoint;
        ClearActiveRoute();
    }

    public void SetFollowTarget(Transform target)
    {
        debugFollowTarget = target;
        movementMode = EnemyMovementMode.FollowTarget;
        ClearActiveRoute();
    }

    public bool TryBuildRoutePreview2A(
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick)
    {
        if (preview == null)
            return false;

        preview.Clear();

        Vector3 destination;
        if (!TryGetCurrentDestination(out destination))
            return false;

        if (!EnsureActiveRoute(destination))
            return false;

        return _routeService.FillPreviewFromPath(
            _activeRoutePath,
            Mathf.Max(0.01f, _mover.Speed),
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            secondsPerTick,
            _routeDistanceTravelled);
    }

    private void TickMoveToPoint()
    {
        if (!_hasDestination)
            return;

        if (MoveAlongRoute(_currentDestination))
            _hasDestination = false;
    }

    private void TickPatrolAroundSpawn()
    {
        if (!_hasDestination)
            PickNewPatrolPoint();

        if (MoveAlongRoute(_currentDestination))
            PickNewPatrolPoint();
    }

    private void TickFollowTarget()
    {
        if (debugFollowTarget == null)
            return;

        Vector3 destination = debugFollowTarget.position;
        destination.z = transform.position.z;

        MoveAlongRoute(destination);
    }

    private bool MoveAlongRoute(Vector3 destination)
    {
        destination.z = transform.position.z;

        if (!EnsureActiveRoute(destination))
            return false;

        float totalLength = _routeService.GetPathLength(_activeRoutePath);

        if (totalLength <= ArrivalDistanceThreshold)
            return true;

        float moveDistance =
            Mathf.Max(0f, _mover.Speed) *
            Mathf.Max(0f, Time.deltaTime);

        float nextDistance =
            Mathf.Clamp(
                _routeDistanceTravelled + moveDistance,
                0f,
                totalLength);

        Vector3 nextPosition =
            _routeService.GetPointOnPathAtDistance(
                _activeRoutePath,
                nextDistance);

        Vector2 nextDirection =
            _routeService.GetDirectionOnPathAtDistance(
                _activeRoutePath,
                nextDistance);

        if (nextDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            _facingDirection = nextDirection.normalized;

        nextPosition.z = transform.position.z;
        transform.position = nextPosition;
        _routeDistanceTravelled = nextDistance;

        if (totalLength - nextDistance <= ArrivalDistanceThreshold)
        {
            transform.position = _activeRoutePath[_activeRoutePath.Count - 1];
            ClearActiveRoute();
            return true;
        }

        return false;
    }

    private bool EnsureActiveRoute(Vector3 destination)
    {
        if (_routeService == null)
            return false;

        bool shouldRebuild =
            _activeRoutePath.Count <= 1 ||
            Vector3.Distance(_routeDestination, destination) > DestinationRefreshDistance;

        if (!shouldRebuild)
            return true;

        return RebuildRoute(destination);
    }

    private bool RebuildRoute(Vector3 destination)
    {
        if (_routeService == null)
            return false;

        SystemEnemyRuntimeState enemy = GetRuntimeEnemy();

        SystemShipRouteRequest2A request = new SystemShipRouteRequest2A
        {
            SystemId = enemy != null ? enemy.SystemId : string.Empty,
            StartPosition = transform.position,
            DestinationPosition = destination,
            StartFacingDirection = GetSafeFacingDirection(destination),
            TargetKind = SystemShipRouteTargetKind2A.Enemy,
            Settings = CreateRouteSettings(enemy)
        };

        if (!_routeService.TryBuildRoute(request, _routeBuildResult))
        {
            ClearActiveRoute();
            return false;
        }

        _activeRoutePath.Clear();
        _activeRoutePath.AddRange(_routeBuildResult.Path);
        _routeDestination = destination;
        _routeDistanceTravelled = 0f;

        return _activeRoutePath.Count > 1;
    }

    private SystemShipRouteSettings2A CreateRouteSettings(SystemEnemyRuntimeState enemy)
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, _mover.Speed),
            TurnRadius = enemy != null && enemy.EnemyConfig != null
                ? Mathf.Max(0f, enemy.EnemyConfig.TurnRadius)
                : 0f,
            ArrivalDistanceThreshold = ArrivalDistanceThreshold,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f,
            MaxRoutePlanSteps = RoutePlanMaxSteps
        };
    }

    private bool TryGetCurrentDestination(out Vector3 destination)
    {
        destination = Vector3.zero;

        if (movementMode == EnemyMovementMode.FollowTarget)
        {
            if (debugFollowTarget == null)
                return false;

            destination = debugFollowTarget.position;
            destination.z = transform.position.z;
            return true;
        }

        if (!_hasDestination)
            return false;

        destination = _currentDestination;
        destination.z = transform.position.z;
        return true;
    }

    private Vector2 GetSafeFacingDirection(Vector3 destination)
    {
        if (_facingDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return _facingDirection.normalized;

        Vector3 delta = destination - transform.position;
        delta.z = 0f;

        if (delta.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return Vector2.up;

        return new Vector2(delta.x, delta.y).normalized;
    }

    private void PickNewPatrolPoint()
    {
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;

        _currentDestination = _spawnPosition + new Vector3(
            randomCircle.x,
            randomCircle.y,
            0f);

        _currentDestination.z = transform.position.z;
        _hasDestination = true;
        ClearActiveRoute();
    }

    private SystemEnemyRuntimeState GetRuntimeEnemy()
    {
        if (_enemyView == null ||
            string.IsNullOrWhiteSpace(_enemyView.RuntimeEnemyId) ||
            _enemyService == null)
        {
            return null;
        }

        _enemyService.TryGetEnemy(
            _enemyView.RuntimeEnemyId,
            out SystemEnemyRuntimeState enemy);

        return enemy;
    }

    private void SyncRuntimePosition()
    {
        if (_enemyView == null ||
            !_enemyView.IsBound ||
            _enemyService == null)
        {
            return;
        }

        _enemyService.UpdateEnemyPosition(
            _enemyView.RuntimeEnemyId,
            transform.position);
    }

    private void ClearActiveRoute()
    {
        _activeRoutePath.Clear();
        _routeDestination = Vector3.zero;
        _routeDistanceTravelled = 0f;
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        Bootstrapper.Instance.ServiceRegistry.TryGet(out _routeService);
        Bootstrapper.Instance.ServiceRegistry.TryGet(out _enemyService);
        Bootstrapper.Instance.ServiceRegistry.TryGet(out _configService);
    }
}