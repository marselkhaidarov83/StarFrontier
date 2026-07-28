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

    private bool
        _markerVisible;

    public void Initialize(
        string targetId,
        bool interactable,
        bool available)
    {
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
        SetMarkerVisible(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        ApplyCurrentTargetState();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_markerVisible)
            return;

        if (!ResolveDependencies())
            return;

        Vector3 position =
            transform.position;

        _targetService
            .TryRefreshCurrentTarget(
                _targetId,
                new Vector2(
                    position.x,
                    position.y),
                isInteractable,
                isAvailable,
                out _);

        AnimateMarker();
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
        if (markerRoot == null ||
            _targetingConfig == null)
        {
            return;
        }

        float pulse =
            1f +
            Mathf.Sin(
                Time.unscaledTime *
                _targetingConfig
                    .MarkerPulseSpeed *
                Mathf.PI *
                2f) *
            0.06f;

        float scale =
            _targetingConfig
                .MarkerWorldScale *
            pulse;

        markerRoot
            .transform
            .localScale =
                new Vector3(
                    scale,
                    scale,
                    1f);
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