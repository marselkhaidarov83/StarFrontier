using UnityEngine;

public sealed class CombatBeamView2A : CustomMonoBehaviour
{
    private const string DefaultSortingLayerName = "SystemShipFX";

    [Header("Runtime")]
    [SerializeField]
    [Range(0.01f, 1f)]
    private float beamTickDuration01 = 0.9f;

    [Header("Line")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField][Min(0.01f)] private float lineWidth = 0.08f;
    [SerializeField][Min(0f)] private float endpointPadding = 0.45f;
    [SerializeField] private string sortingLayerName = DefaultSortingLayerName;
    [SerializeField] private int sortingOrder = 730;

    private ISystemNpcCombatService _combatService;
    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateCompletedCount;
    private static double _aggregateTotalMs;
    private static double _aggregateResolveServiceMs;
    private static double _aggregateTryGetBeamMs;
    private static double _aggregateSetEndpointsMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxBeamId = string.Empty;

    public string BeamId { get; private set; }

    public float BeamTickDuration01 =>
        Mathf.Clamp(beamTickDuration01, 0.01f, 1f);

    private void Awake()
    {
        ApplyRuntimeSettings();
    }

    public void ApplyRuntimeSettings()
    {
        CombatBeamRuntimeSettings2A.SetBeamTickDuration01(
            BeamTickDuration01);
    }

    public void Init(
        CombatBeamStartedEvent2A evt,
        Color color)
    {
        ApplyRuntimeSettings();

        BeamId = evt.BeamId;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
            ? DefaultSortingLayerName
            : sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        SetEndpoints(evt.StartPosition, evt.TargetPosition);
        ResolveCombatService();

        gameObject.SetActive(true);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveServiceMs = 0.0;
        double tryGetBeamMs = 0.0;
        double setEndpointsMs = 0.0;

        bool completed = false;

        try
        {
            if (string.IsNullOrWhiteSpace(BeamId))
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolveCombatService();

            resolveServiceMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (_combatService == null)
                return;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            bool beamFound =
                _combatService.TryGetBeam(
                    BeamId,
                    out CombatBeamRuntimeState2A beam);

            tryGetBeamMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!beamFound)
            {
                completed = true;
                Complete();
                return;
            }

            if (beam == null || beam.IsResolved)
            {
                completed = true;
                Complete();
                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            SetEndpoints(beam.StartPosition, beam.TargetPosition);

            setEndpointsMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordUpdateAggregate(
                elapsedMs,
                resolveServiceMs,
                tryGetBeamMs,
                setEndpointsMs,
                completed,
                BeamId);

            VisualUpdatePerfLog.LogIfSlow(
                "CombatBeamView2A.Update",
                startedAt,
                "BeamId=" + (BeamId ?? string.Empty) +
                " | Completed=" + completed +
                " | ResolveServiceMs=" + resolveServiceMs.ToString("F3") +
                " | TryGetBeamMs=" + tryGetBeamMs.ToString("F3") +
                " | SetEndpointsMs=" + setEndpointsMs.ToString("F3"));
        }
    }

    private static void RecordUpdateAggregate(
        double elapsedMs,
        double resolveServiceMs,
        double tryGetBeamMs,
        double setEndpointsMs,
        bool completed,
        string beamId)
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
        _aggregateTryGetBeamMs += tryGetBeamMs;
        _aggregateSetEndpointsMs += setEndpointsMs;

        if (completed)
            _aggregateCompletedCount++;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxBeamId = beamId ?? string.Empty;
        }
    }

    private static void ResetUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateCompletedCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateResolveServiceMs = 0.0;
        _aggregateTryGetBeamMs = 0.0;
        _aggregateSetEndpointsMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxBeamId = string.Empty;
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
            "CombatBeamView2A.Update.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | CompletedCount=" + _aggregateCompletedCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxBeamId=" + _aggregateMaxBeamId +
            " | ResolveServiceMs=" + _aggregateResolveServiceMs.ToString("F3") +
            " | TryGetBeamMs=" + _aggregateTryGetBeamMs.ToString("F3") +
            " | SetEndpointsMs=" + _aggregateSetEndpointsMs.ToString("F3"));
    }

    public void SetEndpoints(
        Vector3 from,
        Vector3 to)
    {
        if (lineRenderer == null)
            return;

        Vector3 direction = to - from;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float distance = direction.magnitude;
            float safePadding = Mathf.Min(endpointPadding, distance * 0.4f);
            Vector3 normalized = direction / distance;

            from += normalized * safePadding;
            to -= normalized * safePadding;
        }

        from.z = -0.05f;
        to.z = -0.05f;

        lineRenderer.SetPosition(0, from);
        lineRenderer.SetPosition(1, to);
    }

    public void Complete()
    {
        BeamId = string.Empty;

        if (lineRenderer != null)
            lineRenderer.enabled = false;

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
}