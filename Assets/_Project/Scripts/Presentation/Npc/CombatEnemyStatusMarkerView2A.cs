using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatEnemyStatusMarkerView2A : MonoBehaviour
{
    private const string SortingLayerName = "SystemShipFX";

    [Header("Root")]
    [SerializeField] private GameObject markerRoot;

    [Header("Bars")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer shieldFillRenderer;
    [SerializeField] private SpriteRenderer hullFillRenderer;

    [Header("Layout")]
    [SerializeField] private float widthMultiplier = 1.15f;
    [SerializeField] private float minWidth = 1.15f;
    [SerializeField] private float barHeight = 1.2f;
    [SerializeField] private float gap = 0.18f;
    [SerializeField] private float bottomOffsetMultiplier = 0.62f;

    [Header("Visual")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color shieldColor = new Color(0.25f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color hullColor = new Color(1f, 0.16f, 0.12f, 0.95f);
    [SerializeField] private int backgroundSortingOrder = 235;
    [SerializeField] private int shieldSortingOrder = 236;
    [SerializeField] private int hullSortingOrder = 237;

    private SimpleEventBus _eventBus;
    private ISystemNpcRuntimeService _runtimeService;
    private SystemNpcView _npcView;
    private SpriteRenderer _shipRenderer;

    private string _runtimeNpcId = string.Empty;
    private float _baseWidth = 1f;
    private bool _suppressUpdatesForNonHostile;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateSuppressedAtStartCount;
    private static int _aggregateSuppressedAfterCount;
    private static int _aggregateVisibleCount;
    private static int _aggregateHostileCount;
    private static int _aggregateNpcFoundCount;
    private static double _aggregateTotalMs;
    private static double _aggregateRefreshRuntimeNpcIdMs;
    private static double _aggregateRefreshFromStateMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxObjectName = string.Empty;
    private static string _aggregateMaxRuntimeNpcId = string.Empty;

    private void Awake()
    {
        _npcView = GetComponent<SystemNpcView>();
        ResolveObjects();
        ApplyStaticVisualSettings();
        SetVisible(false);
    }

    private void OnEnable()
    {
        _suppressUpdatesForNonHostile = false;

        ResolveObjects();
        ApplyStaticVisualSettings();
        ResolveServices();

        if (_eventBus != null)
        {
            _eventBus.Subscribe<SystemNpcDamagedEvent>(OnNpcDamaged);
            _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        }

        RefreshRuntimeNpcId();
        RefreshFromState();
    }

    private void OnDisable()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemNpcDamagedEvent>(OnNpcDamaged);
            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        }

        SetVisible(false);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double refreshRuntimeNpcIdMs = 0d;
        double refreshFromStateMs = 0d;

        bool suppressedAtStart =
            _suppressUpdatesForNonHostile;

        bool visibleAfter = false;
        bool hostileAfter = false;
        bool npcFoundAfter = false;

        try
        {
            if (_suppressUpdatesForNonHostile)
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshRuntimeNpcId();

            refreshRuntimeNpcIdMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshFromState(
                out visibleAfter,
                out hostileAfter,
                out npcFoundAfter);

            refreshFromStateMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordUpdateAggregate(
                elapsedMs,
                refreshRuntimeNpcIdMs,
                refreshFromStateMs,
                suppressedAtStart,
                _suppressUpdatesForNonHostile,
                visibleAfter,
                hostileAfter,
                npcFoundAfter,
                gameObject.name,
                _runtimeNpcId);

            if (VisualUpdatePerfLog.ShouldLog(elapsedMs))
            {
                VisualUpdatePerfLog.LogMeasured(
                    "CombatEnemyStatusMarkerView2A.Update",
                    elapsedMs,
                    "Name=" + gameObject.name +
                    " | RuntimeNpcId=" + (_runtimeNpcId ?? string.Empty) +
                    " | SuppressedAtStart=" + suppressedAtStart +
                    " | SuppressedAfter=" + _suppressUpdatesForNonHostile +
                    " | VisibleAfter=" + visibleAfter +
                    " | HostileAfter=" + hostileAfter +
                    " | NpcFoundAfter=" + npcFoundAfter +
                    " | RefreshRuntimeNpcIdMs=" + refreshRuntimeNpcIdMs.ToString("F3") +
                    " | RefreshFromStateMs=" + refreshFromStateMs.ToString("F3"));
            }
        }
    }

    private void OnNpcDamaged(SystemNpcDamagedEvent evt)
    {
        RefreshRuntimeNpcId();

        if (!string.Equals(evt.RuntimeNpcId, _runtimeNpcId, System.StringComparison.Ordinal))
            return;

        RefreshFromState();
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        RefreshRuntimeNpcId();

        if (!string.Equals(evt.RuntimeNpcId, _runtimeNpcId, System.StringComparison.Ordinal))
            return;

        SetVisible(false);
    }

    private void RefreshFromState()
    {
        RefreshFromState(
            out _,
            out _,
            out _);
    }

    private void RefreshFromState(
        out bool visibleAfter,
        out bool hostileAfter,
        out bool npcFoundAfter)
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double precheckMs = 0d;
        double tryGetNpcMs = 0d;
        double hostileCheckMs = 0d;
        double refreshLayoutMs = 0d;
        double applyShieldMs = 0d;
        double applyHullMs = 0d;
        double setVisibleMs = 0d;

        bool hasRuntimeNpcId = false;
        bool hasRuntimeService = false;
        bool npcFound = false;
        bool npcAlive = false;
        bool npcHostile = false;
        bool suppressedForNonHostile = false;

        visibleAfter = false;
        hostileAfter = false;
        npcFoundAfter = false;

        SystemNpcRuntimeState npc = null;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            hasRuntimeNpcId =
                !string.IsNullOrWhiteSpace(_runtimeNpcId);

            hasRuntimeService =
                _runtimeService != null;

            precheckMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!hasRuntimeNpcId || !hasRuntimeService)
            {
                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                SetVisible(false);
                visibleAfter = false;

                setVisibleMs +=
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            npcFound =
                _runtimeService.TryGetNpc(
                    _runtimeNpcId,
                    out npc);

            npcFoundAfter =
                npcFound;

            tryGetNpcMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!npcFound || npc == null)
            {
                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                SetVisible(false);
                visibleAfter = false;

                setVisibleMs +=
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            npcAlive =
                npc.IsAlive;

            npcHostile =
                npc.IsHostileToPlayer;

            hostileAfter =
                npcHostile;

            hostileCheckMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!npcAlive || !npcHostile)
            {
                suppressedForNonHostile =
                    npcAlive && !npcHostile;

                if (suppressedForNonHostile)
                    _suppressUpdatesForNonHostile = true;

                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                SetVisible(false);
                visibleAfter = false;

                setVisibleMs +=
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshLayout();

            refreshLayoutMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplyFill(
                shieldFillRenderer,
                GetNormalized(npc.CurrentShield, npc.MaxShield));

            applyShieldMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplyFill(
                hullFillRenderer,
                GetNormalized(npc.CurrentHull, npc.MaxHull));

            applyHullMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            SetVisible(true);
            visibleAfter = true;

            setVisibleMs +=
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            if (VisualUpdatePerfLog.ShouldLog(elapsedMs))
            {
                VisualUpdatePerfLog.LogMeasured(
                    "CombatEnemyStatusMarkerView2A.RefreshFromState",
                    elapsedMs,
                    "Name=" + gameObject.name +
                    " | RuntimeNpcId=" + (_runtimeNpcId ?? string.Empty) +
                    " | HasRuntimeNpcId=" + hasRuntimeNpcId +
                    " | HasRuntimeService=" + hasRuntimeService +
                    " | NpcFound=" + npcFound +
                    " | NpcAlive=" + npcAlive +
                    " | NpcHostile=" + npcHostile +
                    " | SuppressedForNonHostile=" + suppressedForNonHostile +
                    " | VisibleAfter=" + visibleAfter +
                    " | PrecheckMs=" + precheckMs.ToString("F3") +
                    " | TryGetNpcMs=" + tryGetNpcMs.ToString("F3") +
                    " | HostileCheckMs=" + hostileCheckMs.ToString("F3") +
                    " | RefreshLayoutMs=" + refreshLayoutMs.ToString("F3") +
                    " | ApplyShieldMs=" + applyShieldMs.ToString("F3") +
                    " | ApplyHullMs=" + applyHullMs.ToString("F3") +
                    " | SetVisibleMs=" + setVisibleMs.ToString("F3"));
            }
        }
    }

    private static void RecordUpdateAggregate(
        double elapsedMs,
        double refreshRuntimeNpcIdMs,
        double refreshFromStateMs,
        bool suppressedAtStart,
        bool suppressedAfter,
        bool visibleAfter,
        bool hostileAfter,
        bool npcFoundAfter,
        string objectName,
        string runtimeNpcId)
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
        _aggregateRefreshRuntimeNpcIdMs += refreshRuntimeNpcIdMs;
        _aggregateRefreshFromStateMs += refreshFromStateMs;

        if (suppressedAtStart)
            _aggregateSuppressedAtStartCount++;

        if (suppressedAfter)
            _aggregateSuppressedAfterCount++;

        if (visibleAfter)
            _aggregateVisibleCount++;

        if (hostileAfter)
            _aggregateHostileCount++;

        if (npcFoundAfter)
            _aggregateNpcFoundCount++;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxObjectName = objectName ?? string.Empty;
            _aggregateMaxRuntimeNpcId = runtimeNpcId ?? string.Empty;
        }
    }

    private static void ResetUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateSuppressedAtStartCount = 0;
        _aggregateSuppressedAfterCount = 0;
        _aggregateVisibleCount = 0;
        _aggregateHostileCount = 0;
        _aggregateNpcFoundCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateRefreshRuntimeNpcIdMs = 0.0;
        _aggregateRefreshFromStateMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxObjectName = string.Empty;
        _aggregateMaxRuntimeNpcId = string.Empty;
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
            "CombatEnemyStatusMarkerView2A.Update.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | SuppressedAtStartCount=" + _aggregateSuppressedAtStartCount +
            " | SuppressedAfterCount=" + _aggregateSuppressedAfterCount +
            " | VisibleCount=" + _aggregateVisibleCount +
            " | HostileCount=" + _aggregateHostileCount +
            " | NpcFoundCount=" + _aggregateNpcFoundCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxObject=" + _aggregateMaxObjectName +
            " | MaxRuntimeNpcId=" + _aggregateMaxRuntimeNpcId +
            " | RefreshRuntimeNpcIdMs=" + _aggregateRefreshRuntimeNpcIdMs.ToString("F3") +
            " | RefreshFromStateMs=" + _aggregateRefreshFromStateMs.ToString("F3"));
    }

    private void RefreshRuntimeNpcId()
    {
        if (_npcView == null)
            _npcView = GetComponent<SystemNpcView>();

        if (_npcView == null || !_npcView.IsBound)
        {
            _runtimeNpcId = string.Empty;
            return;
        }

        _runtimeNpcId = _npcView.RuntimeNpcId;
    }

    private void RefreshLayout()
    {
        float shipWorldSize = ResolveShipWorldSize();
        _baseWidth = Mathf.Max(minWidth, shipWorldSize * widthMultiplier);

        if (markerRoot != null)
        {
            float y = -shipWorldSize * bottomOffsetMultiplier;
            markerRoot.transform.localPosition = new Vector3(0f, y, -0.025f);
        }

        float totalHeight = barHeight * 2f + gap;

        ApplyBarTransform(backgroundRenderer, _baseWidth, totalHeight, Vector3.zero);
        ApplyBarTransform(
            shieldFillRenderer,
            _baseWidth,
            barHeight,
            new Vector3(0f, (barHeight + gap) * 0.5f, -0.01f));
        ApplyBarTransform(
            hullFillRenderer,
            _baseWidth,
            barHeight,
            new Vector3(0f, -(barHeight + gap) * 0.5f, -0.01f));
    }

    private void ApplyBarTransform(
        SpriteRenderer renderer,
        float width,
        float height,
        Vector3 localPosition)
    {
        if (renderer == null)
            return;

        renderer.transform.localPosition = localPosition;
        renderer.transform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplyFill(SpriteRenderer renderer, float value01)
    {
        if (renderer == null)
            return;

        float width = _baseWidth * Mathf.Clamp01(value01);

        renderer.transform.localScale =
            new Vector3(width, renderer.transform.localScale.y, 1f);

        renderer.transform.localPosition =
            new Vector3(
                -_baseWidth * 0.5f + width * 0.5f,
                renderer.transform.localPosition.y,
                renderer.transform.localPosition.z);
    }

    private float ResolveShipWorldSize()
    {
        if (_npcView != null && _npcView.WorldSize > 0f)
            return _npcView.WorldSize;

        SpriteRenderer shipRenderer = ResolveShipRenderer();

        if (shipRenderer == null)
            return minWidth;

        Bounds bounds = shipRenderer.bounds;

        return Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            minWidth);
    }

    private SpriteRenderer ResolveShipRenderer()
    {
        if (_shipRenderer != null)
            return _shipRenderer;

        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null ||
                renderer == backgroundRenderer ||
                renderer == shieldFillRenderer ||
                renderer == hullFillRenderer)
            {
                continue;
            }

            if (markerRoot != null &&
                renderer.transform.IsChildOf(markerRoot.transform))
            {
                continue;
            }

            _shipRenderer = renderer;
            return _shipRenderer;
        }

        return null;
    }

    private void ResolveObjects()
    {
        if (markerRoot == null)
        {
            Transform existing = transform.Find("CombatEnemyStatusMarker");

            if (existing != null)
                markerRoot = existing.gameObject;
        }

        if (backgroundRenderer == null && markerRoot != null)
        {
            Transform item = markerRoot.transform.Find("StatusBarBackground");

            if (item != null)
                backgroundRenderer = item.GetComponent<SpriteRenderer>();
        }

        if (shieldFillRenderer == null && markerRoot != null)
        {
            Transform item = markerRoot.transform.Find("ShieldFill");

            if (item != null)
                shieldFillRenderer = item.GetComponent<SpriteRenderer>();
        }

        if (hullFillRenderer == null && markerRoot != null)
        {
            Transform item = markerRoot.transform.Find("HullFill");

            if (item != null)
                hullFillRenderer = item.GetComponent<SpriteRenderer>();
        }
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (_eventBus == null)
        {
            _eventBus =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<SimpleEventBus>();
        }

        if (_runtimeService == null)
        {
            _runtimeService =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<ISystemNpcRuntimeService>();
        }
    }

    private void ApplyStaticVisualSettings()
    {
        ApplyRenderer(backgroundRenderer, backgroundColor, backgroundSortingOrder);
        ApplyRenderer(shieldFillRenderer, shieldColor, shieldSortingOrder);
        ApplyRenderer(hullFillRenderer, hullColor, hullSortingOrder);
    }

    private void ApplyRenderer(
        SpriteRenderer renderer,
        Color color,
        int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.color = color;
        renderer.sortingLayerName = SortingLayerName;
        renderer.sortingOrder = sortingOrder;
    }

    private void SetVisible(bool visible)
    {
        if (markerRoot != null && markerRoot.activeSelf != visible)
            markerRoot.SetActive(visible);
    }

    private static float GetNormalized(int current, int max)
    {
        if (max <= 0)
            return 0f;

        return Mathf.Clamp01((float)current / max);
    }
}
