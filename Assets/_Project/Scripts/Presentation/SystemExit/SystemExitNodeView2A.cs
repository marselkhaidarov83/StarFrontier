using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Отображает точку выхода из текущей звёздной системы.
///
/// Поддерживает:
/// - отображение точки выхода;
/// - подпись системы назначения;
/// - выбор точки как локального пункта движения;
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
    private TravelPointInteractionTarget2A
        interactionTarget;

    [Header("Size")]

    [SerializeField]
    private float size = 50f;

    private RouteConfig
        _routeConfig;

    private RouteEndpointConfig
        _endpointConfig;

    private string
        _currentSystemId;

    private string
        _targetSystemId;

    private IGameSessionService
        _gameSessionService;

    private SimpleEventBus
        _simpleEventBus;

    /// <summary>
    /// Вызывается существующим кодом создания
    /// точек выхода системы.
    /// </summary>
    public void Initialize(
        RouteConfig routeConfig,
        string currentSystemId,
        string targetSystemId,
        RouteEndpointConfig endpointConfig,
        Vector2 center)
    {
        if (Bootstrapper.Instance == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "Bootstrapper.Instance is null.",
                this);

            return;
        }

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            Debug.LogError(
                "[SystemExitNodeView2A] " +
                "ServiceRegistry is null.",
                this);

            return;
        }

        _simpleEventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();

        _gameSessionService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IGameSessionService>();

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
             * Строка назначения sprite пока
             * сохраняется отключённой,
             * как в существующей реализации.
             *
             * systemExitImage.sprite =
             *     systemExitSprite;
             */

            SpriteRendererSizeUtility
                .SetWorldSize(
                    systemExitImage,
                    size);
        }

        /*
         * Сначала устанавливаем фактическую
         * координату выхода.
         */
        transform.position =
            _endpointConfig.ExitPoint;

        /*
         * Затем передаём ID системы назначения
         * interaction-компоненту.
         *
         * Это точное место вызова
         * interactionTarget.Initialize().
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

        interactionTarget.Initialize(_targetSystemId);

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
    /// Существующая обработка нажатия на exit.
    ///
    /// Публикует RouteExitMapChangedEvent,
    /// после чего SystemTravelService назначает
    /// локальное движение к точке выхода.
    /// </summary>
    public void OnPointerClick(
        PointerEventData eventData)
    {
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

        _simpleEventBus.Publish(
            new RouteExitMapChangedEvent(
                _routeConfig,
                _currentSystemId,
                _targetSystemId,
                _endpointConfig.ExitPoint,
                _routeConfig.GetEntryPoint(
                    _targetSystemId)));
    }

    private void ResolveInteractionTarget()
    {
        if (interactionTarget != null)
            return;

        interactionTarget =
            GetComponent<
                TravelPointInteractionTarget2A>();
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