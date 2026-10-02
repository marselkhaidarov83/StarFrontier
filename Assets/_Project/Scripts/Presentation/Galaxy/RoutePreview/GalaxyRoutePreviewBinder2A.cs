using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отображает preview выбранного
/// межсистемного маршрута.
///
/// Не рассчитывает Fuel Cost самостоятельно.
/// Не изменяет Fuel.
/// Не изменяет CurrentSystemId.
///
/// Все gameplay-данные получает
/// через ITravelService.
/// </summary>
[DisallowMultipleComponent]
public sealed class GalaxyRoutePreviewBinder2A :
    MonoBehaviour
{
    [Header("Root")]
    [Tooltip(
        "Корневой объект панели preview. " +
        "Может совпадать с GameObject этого компонента.")]
    [SerializeField]
    private GameObject panelRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text routeText;
    [SerializeField] private TMP_Text fuelCostText;
    [SerializeField] private TMP_Text availabilityText;

    [Header("Actions")]
    [Tooltip("Кнопка фактического межсистемного перелёта.")]
    [SerializeField]
    private Button travelButton;

    [Header("Display")]
    [SerializeField] private string fuelCostPrefix = "Топливо: ";
    [SerializeField] private string noRouteText = "Маршрут не выбран";
    [SerializeField] private bool hidePanelWithoutRoute = true;

    [Header("Diagnostics")]
    [SerializeField] private bool logInitializationErrors = true;

    private ITravelService _travelService;
    private IGameStateMachine _gameStateMachine;

    private string _fromSystemId;
    private string _toSystemId;
    private bool _initialized;

    public string FromSystemId => _fromSystemId;
    public string ToSystemId => _toSystemId;

    public bool HasSelectedRoute =>
        !string.IsNullOrWhiteSpace(_fromSystemId) &&
        !string.IsNullOrWhiteSpace(_toSystemId);

    public bool IsTravelAvailable
    {
        get
        {
            if (!_initialized && !TryInitialize())
                return false;

            if (!HasSelectedRoute)
                return false;

            return _travelService.GetTravelFailReason(
                       _fromSystemId,
                       _toSystemId) ==
                   TravelFailReason.None;
        }
    }

    private void Awake()
    {
        TryInitialize();
        ClearRoute();
    }

    public void SetRoute(
        string fromSystemId,
        string toSystemId)
    {
        _fromSystemId = NormalizeId(fromSystemId);
        _toSystemId = NormalizeId(toSystemId);

        Refresh();
    }

    public void SetFromSystemId(
        string fromSystemId)
    {
        _fromSystemId = NormalizeId(fromSystemId);
        Refresh();
    }

    public void SetToSystemId(
        string toSystemId)
    {
        _toSystemId = NormalizeId(toSystemId);
        Refresh();
    }

    public void Refresh()
    {
        if (!TryInitialize())
        {
            ShowServiceUnavailable();
            return;
        }

        if (!HasSelectedRoute)
        {
            ShowNoRoute();
            return;
        }

        SetPanelVisible(true);

        TravelFailReason failReason =
            _travelService.GetTravelFailReason(
                _fromSystemId,
                _toSystemId);

        int fuelCost =
            _travelService.GetTravelCost(
                _fromSystemId,
                _toSystemId);

        if (routeText != null)
        {
            routeText.text =
                _fromSystemId +
                " → " +
                _toSystemId;
        }

        if (fuelCostText != null)
        {
            fuelCostText.text =
                fuelCostPrefix +
                fuelCost;
        }

        bool canTravel =
            failReason == TravelFailReason.None;

        if (availabilityText != null)
        {
            availabilityText.text =
                canTravel
                    ? "Маршрут доступен"
                    : GetPlayerFacingReason(failReason);
        }

        if (travelButton != null)
        {
            travelButton.interactable =
                canTravel;
        }
    }

    public void TryTravelSelectedRoute()
    {
        if (!TryInitialize())
        {
            ShowServiceUnavailable();
            return;
        }

        if (!HasSelectedRoute)
        {
            ShowNoRoute();
            return;
        }

        if (!CanEnterSystemAfterTravel())
        {
            ShowSystemEnterUnavailable();
            return;
        }

        TravelFailReason failReason =
            _travelService.GetTravelFailReason(
                _fromSystemId,
                _toSystemId);

        if (failReason != TravelFailReason.None)
        {
            Refresh();
            return;
        }

        IGameSessionService gameSessionService = null;

        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out gameSessionService);
        }

        LoadingSceneContext.SetSystemTravel(
            gameSessionService != null
                ? gameSessionService.State
                : null);

        string targetSystemId =
            _toSystemId;

        ClearRoute();

        _gameStateMachine.Enter(
            new SystemState(targetSystemId));
    }

    public void ClearRoute()
    {
        _fromSystemId = string.Empty;
        _toSystemId = string.Empty;

        if (routeText != null)
            routeText.text = noRouteText;

        if (fuelCostText != null)
            fuelCostText.text = fuelCostPrefix + "—";

        if (availabilityText != null)
            availabilityText.text = string.Empty;

        if (travelButton != null)
            travelButton.interactable = false;

        if (hidePanelWithoutRoute)
            SetPanelVisible(false);
    }

    private bool TryInitialize()
    {
        if (_initialized &&
            _travelService != null &&
            _gameStateMachine != null)
        {
            return true;
        }

        _initialized = false;
        _travelService = null;
        _gameStateMachine = null;

        if (Bootstrapper.Instance == null)
        {
            LogInitializationError("Bootstrapper.Instance is null.");
            return false;
        }

        if (Bootstrapper.Instance.ServiceRegistry == null)
        {
            LogInitializationError("ServiceRegistry is null.");
            return false;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        bool travelFound =
            registry.TryGet<ITravelService>(
                out ITravelService travelService);

        if (!travelFound || travelService == null)
        {
            LogInitializationError("ITravelService is not registered.");
            return false;
        }

        bool stateMachineFound =
            registry.TryGet<IGameStateMachine>(
                out IGameStateMachine gameStateMachine);

        if (!stateMachineFound || gameStateMachine == null)
        {
            LogInitializationError("IGameStateMachine is not registered.");
            return false;
        }

        _travelService = travelService;
        _gameStateMachine = gameStateMachine;
        _initialized = true;

        return true;
    }

    private bool CanEnterSystemAfterTravel()
    {
        if (_gameStateMachine != null)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        return Bootstrapper.Instance.ServiceRegistry.TryGet<IGameStateMachine>(
                   out _gameStateMachine) &&
               _gameStateMachine != null;
    }

    private void ShowNoRoute()
    {
        if (routeText != null)
            routeText.text = noRouteText;

        if (fuelCostText != null)
            fuelCostText.text = fuelCostPrefix + "—";

        if (availabilityText != null)
            availabilityText.text = string.Empty;

        if (travelButton != null)
            travelButton.interactable = false;

        if (hidePanelWithoutRoute)
            SetPanelVisible(false);
    }

    private void ShowServiceUnavailable()
    {
        SetPanelVisible(true);

        if (routeText != null && HasSelectedRoute)
        {
            routeText.text =
                _fromSystemId +
                " → " +
                _toSystemId;
        }

        if (fuelCostText != null)
            fuelCostText.text = fuelCostPrefix + "—";

        if (availabilityText != null)
            availabilityText.text = "Сервис перелёта недоступен";

        if (travelButton != null)
            travelButton.interactable = false;
    }

    private void ShowSystemEnterUnavailable()
    {
        SetPanelVisible(true);

        if (availabilityText != null)
            availabilityText.text = "Переход в систему недоступен";

        if (travelButton != null)
            travelButton.interactable = false;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null &&
            panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }
    }

    private static string NormalizeId(string systemId)
    {
        return string.IsNullOrWhiteSpace(systemId)
            ? string.Empty
            : systemId.Trim();
    }

    private static string GetPlayerFacingReason(
        TravelFailReason reason)
    {
        switch (reason)
        {
            case TravelFailReason.None:
                return "Маршрут доступен";

            case TravelFailReason.CurrentSystemMissing:
                return "Текущая система не определена";

            case TravelFailReason.TargetSystemMissing:
                return "Система назначения недоступна";

            case TravelFailReason.TargetSystemIsCurrent:
                return "Корабль уже находится в этой системе";

            case TravelFailReason.SystemsAreNotNeighbors:
                return "Между системами нет доступного маршрута";

            case TravelFailReason.NotEnoughFuel:
                return "Недостаточно топлива";

            default:
                return "Перелёт недоступен: " + reason;
        }
    }

    private void LogInitializationError(string message)
    {
        if (!logInitializationErrors)
            return;

        Debug.LogWarning(
            "[GalaxyRoutePreviewBinder2A] " +
            message,
            this);
    }
}