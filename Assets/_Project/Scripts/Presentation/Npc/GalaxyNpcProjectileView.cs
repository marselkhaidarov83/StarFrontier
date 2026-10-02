using UnityEngine;

public sealed class GalaxyNpcProjectileView : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float rotationOffsetDegrees = 0f;

    public string ProjectileId { get; private set; }
    public bool IsActive { get; private set; }

    private ISystemNpcCombatService _combatService;
    private ProjectileWeaponVisualSettings2A _settings;
    private Vector3 _lastPosition;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateCompletedCount;
    private static int _aggregateReleasedCount;
    private static double _aggregateTotalMs;
    private static double _aggregateResolveServiceMs;
    private static double _aggregateTryGetProjectileMs;
    private static double _aggregateSetPositionMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxProjectileId = string.Empty;

    public void Init(GalaxyNpcProjectileCreatedEvent evt)
    {
        ProjectileId = evt.ProjectileId;
        IsActive = true;

        ResolveSettings();
        ResolveSpriteRenderer();

        _lastPosition = evt.StartPosition;
        SetPosition(evt.StartPosition);

        ResolveCombatService();
        ApplyRenderOrder();

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        gameObject.SetActive(true);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveServiceMs = 0.0;
        double tryGetProjectileMs = 0.0;
        double setPositionMs = 0.0;

        bool completed = false;
        bool released = false;

        try
        {
            if (!IsActive)
                return;

            if (string.IsNullOrWhiteSpace(ProjectileId))
            {
                completed = true;
                Complete();
                return;
            }

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolveCombatService();

            resolveServiceMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (_combatService == null)
                return;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            bool projectileFound =
                _combatService.TryGetProjectile(
                    ProjectileId,
                    out GalaxyNpcProjectileRuntimeState projectile);

            tryGetProjectileMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!projectileFound)
            {
                completed = true;
                Complete();
                return;
            }

            if (projectile == null || projectile.IsResolved)
            {
                completed = true;
                Complete();
                return;
            }

            released =
                projectile.ElapsedSeconds >= projectile.StartDelaySeconds;

            if (spriteRenderer != null)
                spriteRenderer.enabled = released;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            SetPosition(projectile.CurrentPosition);

            setPositionMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordUpdateAggregate(
                elapsedMs,
                resolveServiceMs,
                tryGetProjectileMs,
                setPositionMs,
                completed,
                released,
                ProjectileId);

            VisualUpdatePerfLog.LogIfSlow(
                "GalaxyNpcProjectileView.Update",
                startedAt,
                "ProjectileId=" + (ProjectileId ?? string.Empty) +
                " | Completed=" + completed +
                " | Released=" + released +
                " | ResolveServiceMs=" + resolveServiceMs.ToString("F3") +
                " | TryGetProjectileMs=" + tryGetProjectileMs.ToString("F3") +
                " | SetPositionMs=" + setPositionMs.ToString("F3"));
        }
    }

    private static void RecordUpdateAggregate(
        double elapsedMs,
        double resolveServiceMs,
        double tryGetProjectileMs,
        double setPositionMs,
        bool completed,
        bool released,
        string projectileId)
    {
        int frame =
            Time.frameCount;

        if (_aggregateFrame != frame)
        {
            FlushUpdateAggregate();
            ResetUpdateAggregate(frame);
        }

        _aggregateCount++;
        _aggregateTotalMs += elapsedMs;
        _aggregateResolveServiceMs += resolveServiceMs;
        _aggregateTryGetProjectileMs += tryGetProjectileMs;
        _aggregateSetPositionMs += setPositionMs;

        if (completed)
            _aggregateCompletedCount++;

        if (released)
            _aggregateReleasedCount++;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxProjectileId = projectileId ?? string.Empty;
        }
    }

    private static void ResetUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateCompletedCount = 0;
        _aggregateReleasedCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateResolveServiceMs = 0.0;
        _aggregateTryGetProjectileMs = 0.0;
        _aggregateSetPositionMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxProjectileId = string.Empty;
    }

    private static void FlushUpdateAggregate()
    {
        if (_aggregateFrame < 0 ||
            _aggregateCount <= 0)
        {
            return;
        }

        if (!VisualUpdatePerfLog.ShouldLog(_aggregateTotalMs))
            return;

        VisualUpdatePerfLog.LogMeasured(
            "GalaxyNpcProjectileView.Update.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | CompletedCount=" + _aggregateCompletedCount +
            " | ReleasedCount=" + _aggregateReleasedCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxProjectileId=" + _aggregateMaxProjectileId +
            " | ResolveServiceMs=" + _aggregateResolveServiceMs.ToString("F3") +
            " | TryGetProjectileMs=" + _aggregateTryGetProjectileMs.ToString("F3") +
            " | SetPositionMs=" + _aggregateSetPositionMs.ToString("F3"));
    }

    public void SetPosition(Vector3 position)
    {
        ResolveSettings();

        if (_settings != null && _settings.ForceWorldZ)
            position.z = _settings.WorldZ;

        Vector3 direction = position - _lastPosition;

        transform.position = position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle + rotationOffsetDegrees);
        }

        _lastPosition = position;
    }

    public void Complete()
    {
        ProjectileId = null;
        IsActive = false;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        gameObject.SetActive(false);
    }

    private void ResolveCombatService()
    {
        if (_combatService != null)
            return;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return;

        bootstrapper.ServiceRegistry.TryGet(out _combatService);
    }

    private void ResolveSettings()
    {
        if (_settings != null)
            return;

        _settings = GetComponent<ProjectileWeaponVisualSettings2A>();
    }

    private void ResolveSpriteRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void ApplyRenderOrder()
    {
        if (spriteRenderer == null || _settings == null)
            return;

        spriteRenderer.sortingLayerName = _settings.SortingLayerName;
        spriteRenderer.sortingOrder = _settings.SortingOrder;
    }
}