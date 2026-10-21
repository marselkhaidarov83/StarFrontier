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

    [Header("Fuel Badge")]
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private SpriteRenderer fuelImage;
    [SerializeField] private Sprite fuelNormalSprite;
    [SerializeField] private Sprite fuelEnemySprite;
    [SerializeField] private Sprite fuelNotEnoughSprite;
    [SerializeField, Min(0.1f)] private float fuelBadgeWidthFactor = 0.85f;
    [SerializeField, Min(0f)] private float fuelBadgeGapFactor = 0.1f;
    [SerializeField, Min(0.05f)] private float fuelRefreshInterval = 0.2f;

    [Header("War State Icon")]
    [SerializeField] private SpriteRenderer warStateIcon;
    [SerializeField] private Sprite warThreatSprite;
    [SerializeField] private Sprite warInvasionSprite;
    [SerializeField] private Color warIconThreatColor = new Color(1f, 0.82f, 0.18f, 1f);
    [SerializeField] private Color warIconInvasionColor = new Color(1f, 0.08f, 0.04f, 1f);
    [SerializeField, Min(0.1f)] private float warIconSizeFactor = 0.55f;
    [SerializeField, Min(0f)] private float warIconGapFactor = 0.01f;

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
    private IRefuelService _refuelService;
    private IGameSessionService _gameSessionService;
    private Vector2 _sunCenter;
    private float _nextFuelRefreshTime;
    private Renderer _fuelTextRenderer;

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

        _sunCenter = center;
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
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "RouteConfig is null.");

            return;
        }

        if (_endpointConfig == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "RouteEndpointConfig is null.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _currentSystemId))
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Current system ID is empty.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _targetSystemId))
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Target system ID is empty.");

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

        RefreshFuelBadge();
        RefreshFuelBadgeLayout();
        RefreshWarStateIcon();
        RefreshWarStateIconLayout();

        /*
         * Передаём ID системы назначения
         * interaction-компоненту.
         */
        ResolveInteractionTarget();

        if (interactionTarget == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "TravelPointInteractionTarget2A " +
                "component is missing.");

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
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "TargetSelectableView2A component is missing.");
        }

        ResolveSystemTransitionController();

        if (IsDebug())
        {
            AppLog.Info(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Initialized route = " +
                _routeConfig.Id +
                " | current = " +
                _currentSystemId +
                " | target = " +
                _targetSystemId +
                " | exitPoint = " +
                _endpointConfig.ExitPoint);
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
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Cannot click exit. " +
                "RouteConfig is null.");

            return;
        }

        if (_endpointConfig == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Cannot click exit. " +
                "RouteEndpointConfig is null.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _currentSystemId))
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Cannot click exit. " +
                "Current system ID is empty.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _targetSystemId))
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Cannot click exit. " +
                "Target system ID is empty.");

            return;
        }

        if (_simpleEventBus == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Cannot click exit. " +
                "SimpleEventBus is null.");

            return;
        }

        if (_travelService == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemExitNodeView2A),
                "Cannot check travel. " +
                "ITravelService is null.");

            return;
        }

        ResolveSystemTransitionController();
        if (_systemTransitionController == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                "[SystemExitNodeView2A] SystemTransitionController2 was not found on SystemScene.");
            return;
        }

        if (!_systemTransitionController.ValidateSystemExitSelection(_currentSystemId, _targetSystemId))
            return;

        /*
         * Нехватка топлива предупреждает игрока,
         * но не мешает выбрать выход и построить локальный маршрут.
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

    private void LateUpdate()
    {
        if (_endpointConfig == null)
            return;

        if (Time.unscaledTime >= _nextFuelRefreshTime)
        {
            _nextFuelRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, fuelRefreshInterval);
            RefreshFuelBadge();
            RefreshWarStateIcon();
        }
        RefreshFuelBadgeLayout();
        RefreshWarStateIconLayout();
    }

    private void RefreshWarStateIcon()
    {
        ResolveRuntimeDependencies();
        StarSystemRuntimeState targetState = null;
        var systems = _gameSessionService?.State?.Galaxy?.Systems;
        if (systems != null && !string.IsNullOrWhiteSpace(_targetSystemId))
        {
            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i] != null && systems[i].SystemId == _targetSystemId)
                {
                    targetState = systems[i];
                    break;
                }
            }
        }

        SystemWarStateIcon2A.Apply(
            warStateIcon, targetState, warThreatSprite, warInvasionSprite,
            warIconThreatColor, warIconInvasionColor);
    }

    private void RefreshWarStateIconLayout()
    {
        if (warStateIcon == null || !warStateIcon.gameObject.activeSelf ||
            warStateIcon.sprite == null || systemExitImage == null || systemExitImage.sprite == null)
            return;

        Vector2 exitSize = systemExitImage.sprite.bounds.size;
        float diameter = Mathf.Max(exitSize.x, exitSize.y);
        float iconSize = diameter * Mathf.Max(0.1f, warIconSizeFactor);
        // Place the icon beyond the exit, along the line from the sun to the exit.
        Vector2 radial = (Vector2)transform.position - _sunCenter;
        Vector2 direction = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector2.up;
        Vector3 localDirection = transform.InverseTransformVector(new Vector3(direction.x, direction.y, 0f)).normalized;

        Vector2 iconBounds = warStateIcon.sprite.bounds.size;
        float iconLongestSide = Mathf.Max(iconBounds.x, iconBounds.y);
        float iconScale = iconLongestSide > 0f ? iconSize / iconLongestSide : 0f;
        float iconHalfExtent = (Mathf.Abs(localDirection.x) * iconBounds.x +
            Mathf.Abs(localDirection.y) * iconBounds.y) * iconScale * 0.5f;
        float distance = diameter * 0.5f + iconHalfExtent + diameter * Mathf.Max(0f, warIconGapFactor);
        Vector3 localOffset = localDirection * distance;
        warStateIcon.transform.localPosition = new Vector3(localOffset.x, localOffset.y, -0.01f);
        warStateIcon.transform.rotation = Quaternion.identity;

        Vector2 spriteSize = warStateIcon.sprite.bounds.size;
        float longestSide = Mathf.Max(spriteSize.x, spriteSize.y);
        if (longestSide > 0f)
            warStateIcon.transform.localScale = new Vector3(iconSize / longestSide, iconSize / longestSide, 1f);

        warStateIcon.sortingLayerID = systemExitImage.sortingLayerID;
        warStateIcon.sortingOrder = systemExitImage.sortingOrder + 3;
    }

    private void RefreshFuelBadge()
    {
        if (fuelImage == null || fuelText == null)
            return;
        ResolveRuntimeDependencies();
        if (_travelService == null || _refuelService == null ||
            string.IsNullOrWhiteSpace(_currentSystemId) || string.IsNullOrWhiteSpace(_targetSystemId))
            return;

        int fuelCost = _travelService.GetTravelCost(_currentSystemId, _targetSystemId);
        bool enoughFuel = _refuelService.GetCurrentFuel() >= fuelCost;
        bool enemySystem = SystemExitTargetColor2A.IsEnemySystem(
            _gameSessionService?.State?.Galaxy, _targetSystemId);
        fuelImage.sprite = !enoughFuel
            ? fuelNotEnoughSprite
            : enemySystem && fuelEnemySprite != null ? fuelEnemySprite : fuelNormalSprite;
        fuelImage.color = Color.white;
        fuelText.color = Color.white;
        fuelText.SetText(fuelCost.ToString());
    }

    private static Vector2 GetUpperFuelBadgeDirection(Vector2 radial)
    {
        if (radial.sqrMagnitude <= 0.0001f)
            return Vector2.up;

        Vector2 direction = radial.normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        if (perpendicular.y < 0f || (perpendicular.y == 0f && perpendicular.x < 0f))
            perpendicular = -perpendicular;
        return perpendicular;
    }

    private void RefreshFuelBadgeLayout()
    {
        if (fuelImage == null || fuelText == null || systemExitImage == null || systemExitImage.sprite == null)
            return;

        Vector2 exitSize = systemExitImage.sprite.bounds.size;
        float diameter = Mathf.Max(exitSize.x, exitSize.y);
        float width = diameter * Mathf.Max(0.1f, fuelBadgeWidthFactor);
        float height = width * (23f / 46.3312f);
        float textHeight = fuelText.GetPreferredValues(fuelText.text).y * Mathf.Abs(fuelText.transform.localScale.y);
        Vector2 direction = GetUpperFuelBadgeDirection((Vector2)transform.position - _sunCenter);
        Vector3 localDirection = transform.InverseTransformVector(new Vector3(direction.x, direction.y, 0f)).normalized;
        float badgeHalfExtent = (Mathf.Abs(localDirection.x) * width +
            Mathf.Abs(localDirection.y) * Mathf.Max(height, textHeight)) * 0.5f;
        float distance = diameter * 0.5f + badgeHalfExtent +
            diameter * Mathf.Max(0f, fuelBadgeGapFactor);
        Vector3 localOffset = localDirection * distance;
        fuelImage.transform.localPosition = new Vector3(localOffset.x, localOffset.y, -0.01f);
        fuelText.transform.localPosition = new Vector3(localOffset.x, localOffset.y, -0.02f);
        fuelImage.transform.rotation = Quaternion.identity;
        fuelText.transform.rotation = Quaternion.identity;

        if (fuelImage.sprite != null)
        {
            Vector2 spriteSize = fuelImage.sprite.bounds.size;
            if (spriteSize.x > 0f && spriteSize.y > 0f)
                fuelImage.transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
        }

        Vector3 textScale = fuelText.transform.localScale;
        fuelText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            width * 0.9f / Mathf.Max(0.001f, Mathf.Abs(textScale.x)));
        fuelText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            height * 0.9f / Mathf.Max(0.001f, Mathf.Abs(textScale.y)));
        fuelImage.sortingLayerID = systemExitImage.sortingLayerID;
        fuelImage.sortingOrder = systemExitImage.sortingOrder + 1;
        if (_fuelTextRenderer == null)
            _fuelTextRenderer = fuelText.GetComponent<Renderer>();
        if (_fuelTextRenderer != null)
        {
            _fuelTextRenderer.sortingLayerID = systemExitImage.sortingLayerID;
            _fuelTextRenderer.sortingOrder = systemExitImage.sortingOrder + 2;
        }
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

        if (_refuelService == null)
            registry.TryGet(out _refuelService);
        if (_gameSessionService == null)
            registry.TryGet(out _gameSessionService);

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
