using UnityEngine;

/// <summary>
/// Связывает визуальный объект точки выхода
/// с ID системы назначения.
///
/// Target ID точки выхода равен ID системы,
/// в которую ведёт маршрут.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class
    TravelPointInteractionTarget2A :
        CustomMonoBehaviour
{
    [Header("Target")]

    [SerializeField]
    private SystemGameplayTargetType
        targetType =
            SystemGameplayTargetType.None;

    [SerializeField]
    private TargetSelectableView2A
        selectableView;

    [SerializeField]
    private TargetMarkerView2A
        markerView;

    [Header("Event Binding")]

    [SerializeField]
    [Min(0.01f)]
    private float positionTolerance =
        0.5f;

    private SimpleEventBus
        _eventBus;

    private bool
        _subscribed;

    private string
        _destinationSystemId =
            string.Empty;

    public string DestinationSystemId =>
        _destinationSystemId;

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        ResolveComponents();
        ResolveEventBus();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Инициализирует точку выхода напрямую
    /// через ID системы назначения.
    ///
    /// Этот вариант вызывается из
    /// SystemExitNodeView2A.Initialize().
    /// </summary>
    public void Initialize(
        string destinationSystemId)
    {
        BindDestination(
            destinationSystemId);
    }

    /// <summary>
    /// Инициализация через StarSystemLink.
    /// Оставлена для совместимости
    /// с существующими системными выходами.
    /// </summary>
    public void Initialize(
        StarSystemLink link)
    {
        if (link == null ||
            link.LinkedSystem == null)
        {
            Debug.LogWarning(
                "[TravelPointInteractionTarget2A] " +
                "StarSystemLink or LinkedSystem is null.",
                this);

            return;
        }

        BindDestination(
            link.LinkedSystem.Id);
    }

    /// <summary>
    /// Инициализация через событие изменения
    /// точки выхода маршрута.
    /// </summary>
    public void Initialize(
        RouteExitMapChangedEvent evt)
    {
        if (evt == null)
        {
            Debug.LogWarning(
                "[TravelPointInteractionTarget2A] " +
                "RouteExitMapChangedEvent is null.",
                this);

            return;
        }

        BindDestination(
            evt.ToSystemId);
    }

    private void OnRouteExitMapChanged(
        RouteExitMapChangedEvent evt)
    {
        if (evt == null)
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                evt.ExitPoint);

        /*
         * В системе может существовать несколько
         * точек выхода.
         *
         * Привязываем событие только к объекту,
         * который находится около ExitPoint события.
         */
        if (distance >
            positionTolerance)
        {
            return;
        }

        BindDestination(
            evt.ToSystemId);
    }

    private void BindDestination(
        string destinationSystemId)
    {
        if (string.IsNullOrWhiteSpace(
                destinationSystemId))
        {
            Debug.LogWarning(
                "[TravelPointInteractionTarget2A] " +
                "Destination system ID is empty.",
                this);

            return;
        }

        if (targetType ==
            SystemGameplayTargetType.None)
        {
            Debug.LogError(
                "[TravelPointInteractionTarget2A] " +
                "Target Type is None. " +
                "Select TravelPoint, SystemExit " +
                "or Route in Inspector.",
                this);

            return;
        }

        ResolveComponents();

        if (selectableView == null)
        {
            Debug.LogError(
                "[TravelPointInteractionTarget2A] " +
                "TargetSelectableView2A component " +
                "is missing.",
                this);

            return;
        }

        _destinationSystemId =
            destinationSystemId.Trim();

        selectableView.Initialize(
            _destinationSystemId,
            targetType,
            true,
            true);

        if (markerView != null)
        {
            markerView.Initialize(
                _destinationSystemId,
                true,
                true);
        }

        LogCustom(
            "[TravelPointInteractionTarget2A] " +
            "Destination bound. " +
            "Target system ID = " +
            _destinationSystemId);
    }

    private void ResolveComponents()
    {
        if (selectableView == null)
        {
            selectableView =
                GetComponent<
                    TargetSelectableView2A>();
        }

        if (markerView == null)
        {
            markerView =
                GetComponent<
                    TargetMarkerView2A>();
        }
    }

    private void ResolveEventBus()
    {
        if (_eventBus != null)
            return;

        if (Bootstrapper.Instance == null)
            return;

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            return;
        }

        _eventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();
    }

    private void Subscribe()
    {
        if (_subscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Subscribe<
            RouteExitMapChangedEvent>(
                OnRouteExitMapChanged);

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Unsubscribe<
            RouteExitMapChangedEvent>(
                OnRouteExitMapChanged);

        _subscribed = false;
    }
}