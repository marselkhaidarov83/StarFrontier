using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Отображает точку выхода из текущей звёздной системы.
///
/// Поддерживает:
/// - отображение точки выхода;
/// - подпись системы назначения;
/// - проверку топлива при нажатии;
/// - выбор точки как цели движения;
/// - передачу ID системы назначения
///   в TravelPointInteractionTarget2A.
/// </summary>
public sealed class SystemExitNodeView2A :
    CustomMonoBehaviour,
    IPointerClickHandler
{
    [Header("View")]

    [SerializeField]
    private SpriteRenderer systemExitImage;

    [SerializeField]
    private Sprite systemExitSprite;

    [SerializeField]
    private TMP_Text systemNameText;

    [Header("Interaction")]

    [Tooltip(
        "Компонент, который передаёт ID системы назначения " +
        "в TargetSelectableView2A и InteractionService2A.")]
    [SerializeField]
    private TravelPointInteractionTarget2A interactionTarget;

    [Header("Size")]

    [SerializeField]
    private float size =
        50f;

    private RouteConfig _routeConfig;
    private RouteEndpointConfig _endpointConfig;

    private string _currentSystemId =
        string.Empty;

    private string _targetSystemId =
        string.Empty;

    private SimpleEventBus _simpleEventBus;
    private ITravelService _travelService;
    private ISystemTravelService _systemTravelService;
    private ITargetService2A _targetService;

    private SystemTransitionController2
        _systemTransitionController;

    private TargetSelectableView2A
        _selectableView;

    /// <summary>
    /// Вызывается существующим кодом создания
    /// точек выхода системы.
    /// </summary>
    public void Initialize(
        RouteConfig routeConfig,
        string currentSystemId,
        string targetSystemId,
        RouteEndpointConfig endpointConfig,
        Vector2 center,
        SystemVisualConfig visualConfig = null)
    {
        ResolveRuntimeDependencies();

        _routeConfig =
            routeConfig;

        _endpointConfig =
            endpointConfig;

        _currentSystemId =
            NormalizeId(
                currentSystemId);

        _targetSystemId =
            NormalizeId(
                targetSystemId);

        if (_routeConfig == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "RouteConfig is null.",
                this);

            return;
        }

        if (_endpointConfig == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "RouteEndpointConfig is null.",
                this);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _currentSystemId))
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Current system ID is empty.",
                this);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _targetSystemId))
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Target system ID is empty.",
                this);

            return;
        }

        StarSystemConfig targetSystem =
            _routeConfig.GetOtherSystem(
                _currentSystemId);

        if (targetSystem != null &&
            systemNameText != null)
        {
            systemNameText.SetText(
                targetSystem.DisplayName);
        }

        if (systemExitImage != null)
        {
            /*
             * Назначение systemExitSprite пока
             * остаётся отключённым,
             * как в существующей реализации.
             *
             * systemExitImage.sprite =
             *     systemExitSprite;
             */

            SpriteRendererSizeUtility.SetWorldSize(
                systemExitImage,
                visualConfig != null
                    ? visualConfig.GetSystemExitWorldSize(
                        _endpointConfig)
                    : size);
        }

        /*
         * Устанавливаем фактическую координату
         * точки выхода.
         */
        transform.position =
            _endpointConfig.ExitPoint;

        /*
         * Передаём ID системы назначения
         * interaction-компоненту.
         */
        ResolveInteractionTarget();

        if (interactionTarget == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "TravelPointInteractionTarget2A " +
                "component is missing.",
                this);

            return;
        }

        interactionTarget.Initialize(
            _targetSystemId);

        /*
         * На префабе присутствует второй
         * IPointerClickHandler:
         * TargetSelectableView2A.
         *
         * Запрещаем ему самостоятельно обрабатывать
         * нажатие на точку выхода.
         *
         * Выбор цели будет вызван ниже вручную,
         * только после успешной проверки топлива.
         */
        ResolveSelectableView();

        if (_selectableView != null)
        {
            _selectableView
                .SetPointerClickHandlingEnabled(
                    false);
        }
        else
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "TargetSelectableView2A component is missing.",
                this);
        }

        ResolveSystemTransitionController();

        if (IsDebug())
        {
            Debug.Log(
                "[SystemExitNodeView2A] " +
                "Initialized route = " +
                _routeConfig.Id +
                " | current = " +
                _currentSystemId +
                " | target = " +
                _targetSystemId +
                " | exitPoint = " +
                _endpointConfig.ExitPoint,
                this);
        }
    }

    /// <summary>
    /// Обрабатывает нажатие на точку выхода.
    ///
    /// Проверка топлива выполняется до:
    /// - выбора цели;
    /// - появления маркера;
    /// - отправки RouteExitMapChangedEvent;
    /// - начала движения корабля.
    /// </summary>
    public void OnPointerClick(
        PointerEventData eventData)
    {
        ResolveRuntimeDependencies();
        ResolveSelectableView();

        _simpleEventBus?.Publish(
            new SystemObjectsPanelCloseRequestedEvent2A());

        /*
         * На случай повторной инициализации
         * ещё раз запрещаем второму компоненту
         * самостоятельно обрабатывать нажатие.
         */
        if (_selectableView != null)
        {
            _selectableView
                .SetPointerClickHandlingEnabled(
                    false);
        }

        LogCustom(
            "route = " +
            (_routeConfig != null
                ? _routeConfig.Id
                : "null") +
            " | endpoint = " +
            (_endpointConfig != null
                ? _endpointConfig.Id
                : "null") +
            " | current = " +
            _currentSystemId +
            " | target = " +
            _targetSystemId);

        if (_routeConfig == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Cannot click exit. " +
                "RouteConfig is null.",
                this);

            return;
        }

        if (_endpointConfig == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Cannot click exit. " +
                "RouteEndpointConfig is null.",
                this);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _currentSystemId))
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Cannot click exit. " +
                "Current system ID is empty.",
                this);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _targetSystemId))
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Cannot click exit. " +
                "Target system ID is empty.",
                this);

            return;
        }

        if (_simpleEventBus == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Cannot click exit. " +
                "SimpleEventBus is null.",
                this);

            return;
        }

        if (_travelService == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Cannot check travel. " +
                "ITravelService is null.",
                this);

            return;
        }

        /*
         * GetTravelFailReason только проверяет
         * возможность перелёта.
         *
         * Топливо здесь не списывается.
         */
        TravelFailReason failReason =
            _travelService.GetTravelFailReason(
                _currentSystemId,
                _targetSystemId);

        if (failReason ==
            TravelFailReason.NotEnoughFuel)
        {
            /*
             * Сбрасываем локальную цель движения.
             *
             * CancelTravel также отправляет
             * SystemTravelCancelledEvent,
             * благодаря которому существующие
             * маркеры маршрута скрываются.
             */
            if (_systemTravelService != null)
            {
                _systemTravelService.CancelTravel();
            }

            /*
             * Отдельно очищаем игровую цель,
             * чтобы точка выхода не оставалась
             * выбранной даже при другом порядке
             * вызова компонентов Unity.
             */
            if (_targetService != null)
            {
                _targetService.ClearTarget();
            }

            /*
             * Используем существующий объект
             * SystemTransitionController2
             * на SystemScene.
             */
            ResolveSystemTransitionController();

            if (_systemTransitionController != null)
            {
                _systemTransitionController
                    .ShowTravelFailReason(
                        failReason);
            }
            else
            {
                Debug.LogError(
                    "[SystemExitNodeView2A] " +
                    "SystemTransitionController2 " +
                    "was not found on SystemScene.",
                    this);
            }

            LogCustom(
                "System exit selection rejected. " +
                "Not enough fuel. " +
                "Route = " +
                _routeConfig.Id +
                " | From = " +
                _currentSystemId +
                " | To = " +
                _targetSystemId);

            /*
             * Важно:
             * RouteExitMapChangedEvent не публикуется.
             *
             * Поэтому новая цель движения
             * не назначается.
             */
            return;
        }

        /*
         * Если топлива хватает,
         * сохраняем прежнее поведение выбора цели.
         */
        if (_selectableView != null)
        {
            _selectableView.TrySelectTarget();
        }

        /*
         * Только после проверки топлива
         * назначаем локальный маршрут
         * к точке выхода.
         */
        _simpleEventBus.Publish(
            new RouteExitMapChangedEvent(
                _routeConfig,
                _currentSystemId,
                _targetSystemId,
                _endpointConfig.ExitPoint,
                _routeConfig.GetEntryPoint(
                    _targetSystemId),
                _endpointConfig.VisualSize));
    }

    private void ResolveRuntimeDependencies()
    {
        if (Bootstrapper.Instance == null)
            return;

        if (Bootstrapper.Instance.ServiceRegistry == null)
            return;

        IServiceRegistry registry =
            Bootstrapper.Instance
                .ServiceRegistry;

        if (_simpleEventBus == null)
        {
            _simpleEventBus =
                registry.Get<SimpleEventBus>();
        }

        if (_travelService == null)
        {
            _travelService =
                registry.Get<ITravelService>();
        }

        if (_systemTravelService == null)
        {
            _systemTravelService =
                registry.Get<ISystemTravelService>();
        }

        if (_targetService == null)
        {
            _targetService =
                registry.Get<ITargetService2A>();
        }
    }

    private void ResolveInteractionTarget()
    {
        if (interactionTarget != null)
            return;

        interactionTarget =
            GetComponent<TravelPointInteractionTarget2A>();
    }

    private void ResolveSelectableView()
    {
        if (_selectableView != null)
            return;

        _selectableView =
            GetComponent<TargetSelectableView2A>();
    }

    private void ResolveSystemTransitionController()
    {
        if (_systemTransitionController != null)
            return;

        _systemTransitionController =
            Object.FindFirstObjectByType<
                SystemTransitionController2>();
    }

    private static string NormalizeId(
        string value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? string.Empty
            : value.Trim();
    }
}
