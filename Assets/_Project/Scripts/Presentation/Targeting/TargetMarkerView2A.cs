using UnityEngine;

/// <summary>
/// Отображает маркер над выбранной целью.
///
/// Скрипт размещается на объекте цели.
/// Marker Root должен быть дочерним объектом.
///
/// Сам скрипт нельзя размещать на Marker Root,
/// потому что Marker Root выключается,
/// когда объект не выбран.
/// </summary>
[DisallowMultipleComponent]
public sealed class TargetMarkerView2A :
    MonoBehaviour
{
    [Header("Marker")]

    [SerializeField]
    private GameObject markerRoot;

    [SerializeField]
    private SpriteRenderer markerRenderer;

    [Header("Target State")]

    [SerializeField]
    private bool isInteractable =
        true;

    [SerializeField]
    private bool isAvailable =
        true;

    private string
        _targetId =
            string.Empty;

    private ITargetService2A
        _targetService;

    private TargetingConfig
        _targetingConfig;

    private SimpleEventBus
        _eventBus;

    private bool
        _subscribed;

    private bool _markerVisible;
    private Transform _markerRootTransform;
    private Vector3
    _baseMarkerLocalScale =
        Vector3.one;

    private bool
        _baseMarkerScaleCaptured;

    public void Initialize(
        string targetId,
        bool interactable,
        bool available)
    {
        CaptureBaseMarkerScale();

        _targetId =
            targetId ?? string.Empty;

        isInteractable =
            interactable;

        isAvailable =
            available;

        ResolveDependencies();
        ApplyCurrentTargetState();
        RefreshVisual();
    }

    public void ConfigureMarker(
        GameObject root,
        SpriteRenderer renderer)
    {
        markerRoot =
            root;

        _markerRootTransform =
            markerRoot != null
                ? markerRoot.transform
                : null;

        markerRenderer =
            renderer;

        _baseMarkerScaleCaptured =
            false;

        CaptureBaseMarkerScale();
        SetMarkerVisible(false);
        RefreshVisual();
    }

    public void SetAvailable(
        bool available)
    {
        isAvailable =
            available;

        RefreshVisual();

        if (!available &&
            _targetService != null &&
            _targetService.IsCurrentTarget(
                _targetId))
        {
            _targetService.ClearTarget();
        }
    }

    public void SetInteractable(
        bool interactable)
    {
        isInteractable =
            interactable;

        RefreshVisual();
    }

    private void Awake()
    {
        _markerRootTransform =
            markerRoot != null
                ? markerRoot.transform
                : null;

        CaptureBaseMarkerScale();
        SetMarkerVisible(false);
    }

    private void OnEnable()
    {
        CaptureBaseMarkerScale();
        ResolveDependencies();
        ApplyCurrentTargetState();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double visibleCheckMs = 0.0;
        double resolveDependenciesMs = 0.0;
        double readPositionMs = 0.0;
        double refreshCurrentTargetMs = 0.0;
        double animateMarkerMs = 0.0;

        bool markerVisibleAtStart = false;
        bool markerVisibleAtEnd = false;
        bool dependenciesResolved = false;
        bool refreshedTarget = false;
        bool animatedMarker = false;

        Vector3 position = Vector3.zero;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            markerVisibleAtStart =
                _markerVisible;

            visibleCheckMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!markerVisibleAtStart)
                return;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            dependenciesResolved =
                ResolveDependencies();

            resolveDependenciesMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!dependenciesResolved)
                return;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            position =
                transform.position;

            readPositionMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _targetService
                .TryRefreshCurrentTarget(
                    _targetId,
                    new Vector2(
                        position.x,
                        position.y),
                    isInteractable,
                    isAvailable,
                    out _);

            refreshedTarget =
                true;

            refreshCurrentTargetMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            AnimateMarker();

            animatedMarker =
                true;

            animateMarkerMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            markerVisibleAtEnd =
                _markerVisible;

            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            VisualUpdateAggregateLog.Record(
                "TargetMarkerView2A.Update",
                elapsedMs,
                "Name=" + name +
                " | TargetId=" + _targetId +
                " | MarkerVisibleAtStart=" + markerVisibleAtStart +
                " | MarkerVisibleAtEnd=" + markerVisibleAtEnd +
                " | DependenciesResolved=" + dependenciesResolved +
                " | RefreshedTarget=" + refreshedTarget +
                " | AnimatedMarker=" + animatedMarker +
                " | Interactable=" + isInteractable +
                " | Available=" + isAvailable +
                " | VisibleCheckMs=" + visibleCheckMs.ToString("F3") +
                " | ResolveDependenciesMs=" + resolveDependenciesMs.ToString("F3") +
                " | ReadPositionMs=" + readPositionMs.ToString("F3") +
                " | RefreshCurrentTargetMs=" + refreshCurrentTargetMs.ToString("F3") +
                " | AnimateMarkerMs=" + animateMarkerMs.ToString("F3"));

            if (VisualUpdatePerfLog.ShouldLog(elapsedMs))
            {
                VisualUpdatePerfLog.LogMeasured(
                    "TargetMarkerView2A.Update",
                    elapsedMs,
                    "Name=" + name +
                    " | TargetId=" + _targetId +
                    " | MarkerVisibleAtStart=" + markerVisibleAtStart +
                    " | MarkerVisibleAtEnd=" + markerVisibleAtEnd +
                    " | DependenciesResolved=" + dependenciesResolved +
                    " | RefreshedTarget=" + refreshedTarget +
                    " | AnimatedMarker=" + animatedMarker +
                    " | Interactable=" + isInteractable +
                    " | Available=" + isAvailable +
                    " | Position=" + position +
                    " | VisibleCheckMs=" + visibleCheckMs.ToString("F3") +
                    " | ResolveDependenciesMs=" + resolveDependenciesMs.ToString("F3") +
                    " | ReadPositionMs=" + readPositionMs.ToString("F3") +
                    " | RefreshCurrentTargetMs=" + refreshCurrentTargetMs.ToString("F3") +
                    " | AnimateMarkerMs=" + animateMarkerMs.ToString("F3"));
            }
        }
    }

    private bool ResolveDependencies()
    {
        if (_targetService != null &&
            _targetingConfig != null &&
            _eventBus != null)
        {
            Subscribe();
            return true;
        }

        if (Bootstrapper.Instance == null)
            return false;

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            return false;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance
                .ServiceRegistry;

        _targetService =
            registry
                .Get<ITargetService2A>();

        IConfigService configService =
            registry
                .Get<IConfigService>();

        _targetingConfig =
            configService
                .TargetingConfig;

        _eventBus =
            registry
                .Get<SimpleEventBus>();

        Subscribe();

        return
            _targetService != null &&
            _targetingConfig != null &&
            _eventBus != null;
    }

    private void Subscribe()
    {
        if (_subscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus
            .Subscribe<TargetChangedEvent2A>(
                OnTargetChanged);

        _subscribed =
            true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus
            .Unsubscribe<TargetChangedEvent2A>(
                OnTargetChanged);

        _subscribed =
            false;
    }

    private void OnTargetChanged(
        TargetChangedEvent2A evt)
    {
        if (evt == null ||
            !evt.HasTarget)
        {
            SetMarkerVisible(false);
            return;
        }

        bool isThisTarget =
            string.Equals(
                evt.TargetId,
                _targetId,
                System.StringComparison.Ordinal);

        SetMarkerVisible(
            isThisTarget);

        RefreshVisual();
    }

    private void ApplyCurrentTargetState()
    {
        if (_targetService == null ||
            string.IsNullOrWhiteSpace(
                _targetId))
        {
            SetMarkerVisible(false);
            return;
        }

        SetMarkerVisible(
            _targetService
                .IsCurrentTarget(
                    _targetId));

        RefreshVisual();
    }

    private void SetMarkerVisible(
    bool visible)
    {
        if (_markerVisible == visible &&
            markerRoot != null &&
            markerRoot.activeSelf == visible)
        {
            return;
        }

        _markerVisible =
            visible;

        if (markerRoot != null &&
            markerRoot.activeSelf != visible)
        {
            markerRoot.SetActive(
                visible);
        }
    }

    private void AnimateMarker()
    {
        if (_markerRootTransform == null)
        {
            _markerRootTransform =
                markerRoot != null
                    ? markerRoot.transform
                    : null;
        }

        if (_markerRootTransform == null)
            return;

        CaptureBaseMarkerScale();

        float pulse =
            1f +
            Mathf.Sin(
                Time.unscaledTime *
                _targetingConfig
                    .MarkerPulseSpeed *
                Mathf.PI *
                2f) *
            0.06f;

        _markerRootTransform.localScale =
            new Vector3(
                _baseMarkerLocalScale.x *
                pulse,

                _baseMarkerLocalScale.y *
                pulse,

                _baseMarkerLocalScale.z);
    }

    private void CaptureBaseMarkerScale()
    {
        if (_baseMarkerScaleCaptured)
            return;

        if (_markerRootTransform == null)
        {
            _markerRootTransform =
                markerRoot != null
                    ? markerRoot.transform
                    : null;
        }

        if (_markerRootTransform == null)
            return;

        _baseMarkerLocalScale =
            _markerRootTransform.localScale;

        _baseMarkerScaleCaptured =
            true;
    }

    private void RefreshVisual()
    {
        if (markerRenderer == null)
            return;

        Color color =
            markerRenderer.color;

        if (_targetingConfig == null)
        {
            color.a =
                isAvailable
                    ? 1f
                    : 0.35f;
        }
        else
        {
            color.a =
                isAvailable &&
                isInteractable
                    ? 1f
                    : _targetingConfig
                        .UnavailableTargetAlpha;
        }

        markerRenderer.color =
            color;
    }
}
