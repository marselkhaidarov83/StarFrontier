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

    [SerializeField]
    private TMP_Text routeText;

    [SerializeField]
    private TMP_Text fuelCostText;

    [SerializeField]
    private TMP_Text availabilityText;

    [Header("Actions")]

    [Tooltip(
        "Кнопка фактического межсистемного перелёта.")]
    [SerializeField]
    private Button travelButton;

    [Header("Display")]

    [SerializeField]
    private string fuelCostPrefix =
        "Топливо: ";

    [SerializeField]
    private string noRouteText =
        "Маршрут не выбран";

    [SerializeField]
    private bool hidePanelWithoutRoute =
        true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logInitializationErrors =
        true;

    private ITravelService
        _travelService;

    private string
        _fromSystemId;

    private string
        _toSystemId;

    private bool
        _initialized;

    public string FromSystemId =>
        _fromSystemId;

    public string ToSystemId =>
        _toSystemId;

    public bool HasSelectedRoute =>
        !string.IsNullOrWhiteSpace(
            _fromSystemId) &&
        !string.IsNullOrWhiteSpace(
            _toSystemId);

    public bool IsTravelAvailable
    {
        get
        {
            if (!_initialized &&
                !TryInitialize())
            {
                return false;
            }

            if (!HasSelectedRoute)
                return false;

            return _travelService
                       .GetTravelFailReason(
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

    /// <summary>
    /// Вызывается существующим контроллером
    /// карты после выбора системы назначения.
    /// </summary>
    public void SetRoute(
        string fromSystemId,
        string toSystemId)
    {
        _fromSystemId =
            NormalizeId(
                fromSystemId);

        _toSystemId =
            NormalizeId(
                toSystemId);

        Refresh();
    }

    /// <summary>
    /// Можно использовать, если исходная и конечная
    /// системы назначаются по отдельности.
    /// </summary>
    public void SetFromSystemId(
        string fromSystemId)
    {
        _fromSystemId =
            NormalizeId(
                fromSystemId);

        Refresh();
    }

    public void SetToSystemId(
        string toSystemId)
    {
        _toSystemId =
            NormalizeId(
                toSystemId);

        Refresh();
    }

    /// <summary>
    /// Повторно читает authoritative-данные
    /// из ITravelService.
    /// </summary>
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
            _travelService
                .GetTravelFailReason(
                    _fromSystemId,
                    _toSystemId);

        int fuelCost =
            _travelService
                .GetTravelCost(
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
            failReason ==
            TravelFailReason.None;

        if (availabilityText != null)
        {
            availabilityText.text =
                canTravel
                    ? "Маршрут доступен"
                    : GetPlayerFacingReason(
                        failReason);
        }

        if (travelButton != null)
        {
            travelButton.interactable =
                canTravel;
        }
    }

    /// <summary>
    /// Подключается к Button.onClick.
    ///
    /// Не подключайте параллельно другой вызов
    /// ITravelService.TryTravel, иначе Fuel
    /// может быть списан дважды.
    /// </summary>
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

        /*
         * Повторная проверка непосредственно
         * перед фактическим вызовом.
         */
        TravelFailReason failReason =
            _travelService
                .GetTravelFailReason(
                    _fromSystemId,
                    _toSystemId);

        if (failReason !=
            TravelFailReason.None)
        {
            Refresh();
            return;
        }

        TravelResult result =
            _travelService
                .TryTravel(
                    _toSystemId);

        if (result == null)
        {
            if (availabilityText != null)
            {
                availabilityText.text =
                    "Перелёт не выполнен";
            }

            if (travelButton != null)
            {
                travelButton.interactable =
                    false;
            }

            return;
        }

        if (!result.Success)
        {
            if (availabilityText != null)
            {
                availabilityText.text =
                    GetPlayerFacingReason(
                        result.FailReason);
            }

            Refresh();
            return;
        }

        /*
         * TravelService2A уже:
         * - списал Fuel;
         * - изменил систему;
         * - опубликовал события;
         * - запросил сохранение.
         */
        ClearRoute();
    }

    public void ClearRoute()
    {
        _fromSystemId =
            string.Empty;

        _toSystemId =
            string.Empty;

        if (routeText != null)
        {
            routeText.text =
                noRouteText;
        }

        if (fuelCostText != null)
        {
            fuelCostText.text =
                fuelCostPrefix +
                "—";
        }

        if (availabilityText != null)
        {
            availabilityText.text =
                string.Empty;
        }

        if (travelButton != null)
        {
            travelButton.interactable =
                false;
        }

        if (hidePanelWithoutRoute)
        {
            SetPanelVisible(false);
        }
    }

    private bool TryInitialize()
    {
        if (_initialized &&
            _travelService != null)
        {
            return true;
        }

        _initialized =
            false;

        _travelService =
            null;

        if (Bootstrapper.Instance == null)
        {
            LogInitializationError(
                "Bootstrapper.Instance is null.");

            return false;
        }

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            LogInitializationError(
                "ServiceRegistry is null.");

            return false;
        }

        bool found =
            Bootstrapper.Instance
                .ServiceRegistry
                .TryGet<ITravelService>(
                    out ITravelService
                        travelService);

        if (!found ||
            travelService == null)
        {
            LogInitializationError(
                "ITravelService is not registered.");

            return false;
        }

        _travelService =
            travelService;

        _initialized =
            true;

        return true;
    }

    private void ShowNoRoute()
    {
        if (routeText != null)
        {
            routeText.text =
                noRouteText;
        }

        if (fuelCostText != null)
        {
            fuelCostText.text =
                fuelCostPrefix +
                "—";
        }

        if (availabilityText != null)
        {
            availabilityText.text =
                string.Empty;
        }

        if (travelButton != null)
        {
            travelButton.interactable =
                false;
        }

        if (hidePanelWithoutRoute)
        {
            SetPanelVisible(false);
        }
    }

    private void ShowServiceUnavailable()
    {
        SetPanelVisible(true);

        if (routeText != null &&
            HasSelectedRoute)
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
                "—";
        }

        if (availabilityText != null)
        {
            availabilityText.text =
                "Сервис перелёта недоступен";
        }

        if (travelButton != null)
        {
            travelButton.interactable =
                false;
        }
    }

    private void SetPanelVisible(
        bool visible)
    {
        if (panelRoot != null &&
            panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(
                visible);
        }
    }

    private static string NormalizeId(
        string systemId)
    {
        return string.IsNullOrWhiteSpace(
                systemId)
            ? string.Empty
            : systemId.Trim();
    }

    private static string
        GetPlayerFacingReason(
            TravelFailReason reason)
    {
        switch (reason)
        {
            case TravelFailReason.None:
                return "Маршрут доступен";

            case TravelFailReason
                .CurrentSystemMissing:
                return
                    "Текущая система не определена";

            case TravelFailReason
                .TargetSystemMissing:
                return
                    "Система назначения недоступна";

            case TravelFailReason
                .TargetSystemIsCurrent:
                return
                    "Корабль уже находится " +
                    "в этой системе";

            case TravelFailReason
                .SystemsAreNotNeighbors:
                return
                    "Между системами нет " +
                    "доступного маршрута";

            case TravelFailReason
                .NotEnoughFuel:
                return
                    "Недостаточно топлива";

            default:
                /*
                 * Защита на случай добавления
                 * нового значения enum.
                 */
                return
                    "Перелёт недоступен: " +
                    reason;
        }
    }

    private void LogInitializationError(
        string message)
    {
        if (!logInitializationErrors)
            return;

        Debug.LogWarning(
            "[GalaxyRoutePreviewBinder2A] " +
            message,
            this);
    }
}