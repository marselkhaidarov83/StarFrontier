using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RefuelPanelController : MonoBehaviour
{
    // Existing scene references supply the font; the shared layout replaces these controls at runtime.
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private TMP_Text refuelCostText;
    [SerializeField] private TMP_Text refuelWarningText;
    [SerializeField] private Slider refuelCountSlider;
    [SerializeField] private Button refuelButton;

    private SimpleEventBus eventBus;
    private IRefuelService _refuelService;
    private IGameSessionService gameSessionService;
    private Func<Sprite> _shipSprite, _headerSprite;
    private RefuelPanelView2A _view;
    private GameRuntimeState _boundState;
    private string _shipId, _location;
    private int _targetFuel, _currentFuel, _unitPrice;
    private bool _initialized, _busy, _rendering, _subscribed, _destroying;
    private bool CanUseView => this != null && !_destroying && _initialized && isActiveAndEnabled &&
        _view != null && _view.IsReady;

    public void Initialize()
    {
        if (_initialized) return;
        var r = Bootstrapper.Instance.ServiceRegistry;
        var config = r.Get<IConfigService>();
        var session = r.Get<IGameSessionService>();
        var government = FindFirstObjectByType<PlanetGovernmentMissionPresenter>(FindObjectsInactive.Include);
        Initialize(r.Get<IRefuelService>(), session, r.Get<SimpleEventBus>(),
            () => { var ship = session.State?.Player?.GetActiveShip(); var data = ship != null ? config.GetAllyConfigById(ship.AllyConfigId) : null;
                return data != null ? data.CombatSprite ?? data.MapSprite ?? data.Icon : null; },
            () => government != null ? government.CurrentLocationIllustration : null);
    }

    public void Initialize(IRefuelService service, IGameSessionService session, SimpleEventBus events,
        Func<Sprite> shipSprite, Func<Sprite> headerSprite)
    {
        if (_initialized) return;
        _refuelService = service; gameSessionService = session; eventBus = events;
        _shipSprite = shipSprite; _headerSprite = headerSprite;
        _view = GetComponent<RefuelPanelView2A>() ?? gameObject.AddComponent<RefuelPanelView2A>();
        var legacyHandle = refuelCountSlider != null && refuelCountSlider.handleRect != null
            ? refuelCountSlider.handleRect.GetComponent<Image>() : null;
        _view.Build(fuelText, legacyHandle != null ? legacyHandle.sprite : null);
        fuelText = _view.Level; refuelCostText = _view.Cost; refuelWarningText = _view.Warning;
        refuelCountSlider = _view.Slider; refuelButton = _view.Refuel;
        refuelCountSlider.onValueChanged.AddListener(UpdateQuantity);
        refuelButton.onClick.AddListener(HandleRefuelClicked);
        _initialized = true;
        SubscribeToEvents();
        Refresh(false, true);
    }
    private void OnEnable()
    {
        if (!_initialized) return;
        SubscribeToEvents();
        Refresh(false, true);
    }
    private void OnDisable()
    {
        UnsubscribeFromEvents();
        _boundState = null; _shipId = null; _location = null;
    }
    private void OnDestroy()
    {
        _destroying = true; _initialized = false;
        UnsubscribeFromEvents();
        if (refuelCountSlider != null) refuelCountSlider.onValueChanged.RemoveListener(UpdateQuantity);
        if (refuelButton != null) refuelButton.onClick.RemoveListener(HandleRefuelClicked);
        _shipSprite = null; _headerSprite = null; _boundState = null;
    }
    private void SubscribeToEvents()
    {
        if (_subscribed || eventBus == null || !CanUseView) return;
        eventBus.Subscribe<RefuelEnteredEvent>(OnRefuelEntered);
        eventBus.Subscribe<FuelChangedEvent>(OnFuelChanged);
        eventBus.Subscribe<CreditsChangedEvent>(OnCreditsChanged);
        eventBus.Subscribe<ActiveShipChangedEvent>(OnActiveShipChanged);
        _subscribed = true;
    }
    private void UnsubscribeFromEvents()
    {
        if (!_subscribed || eventBus == null) return;
        eventBus.Unsubscribe<RefuelEnteredEvent>(OnRefuelEntered);
        eventBus.Unsubscribe<FuelChangedEvent>(OnFuelChanged);
        eventBus.Unsubscribe<CreditsChangedEvent>(OnCreditsChanged);
        eventBus.Unsubscribe<ActiveShipChangedEvent>(OnActiveShipChanged);
        _subscribed = false;
    }
    private void RefreshFromEvent(bool keepSelection = false)
    {
        if (!CanUseView) { UnsubscribeFromEvents(); return; }
        if (!_busy) Refresh(keepSelection);
    }
    private void OnRefuelEntered(RefuelEnteredEvent evt)
    {
        if (!CanUseView) { UnsubscribeFromEvents(); return; }
        Refresh(false, true);
        AppLog.Info(DebugLogChannel.UI, typeof(RefuelPanelController), "Shared refuel panel opened | Location=" + _location + " | TargetFuel=" + _targetFuel);
    }
    private void OnFuelChanged(FuelChangedEvent evt) => RefreshFromEvent();
    private void OnCreditsChanged(CreditsChangedEvent evt) => RefreshFromEvent(true);
    private void OnActiveShipChanged(ActiveShipChangedEvent evt) => RefreshFromEvent();
    private static string Location(PlayerState player) => player == null ? string.Empty :
        player.CurrentSystemId + "|" + player.CurrentPlanetId + "|" + player.CurrentStationId;

    public void Refresh() => Refresh(false);
    private void Refresh(bool keepSelection, bool selectMaximum = false)
    {
        if (!CanUseView || _refuelService == null) return;
        var state = gameSessionService.State;
        var player = state?.Player;
        var ship = player?.GetActiveShip();
        bool same = ReferenceEquals(_boundState, state) && _shipId == ship?.ShipId && _location == Location(player);
        _boundState = state; _shipId = ship?.ShipId; _location = Location(player);
        int capacity = Mathf.Max(0, ship?.FuelCapacity ?? 0);
        _currentFuel = Mathf.Clamp(ship?.CurrentFuel ?? 0, 0, capacity);
        _unitPrice = _refuelService.GetFuelUnitPrice();
        _targetFuel = keepSelection && same ? Mathf.Clamp(_targetFuel, _currentFuel, capacity) : selectMaximum ? capacity : _currentFuel;
        _view.SetSprites(_headerSprite?.Invoke(), ship != null ? _shipSprite?.Invoke() : null);
        Render();
    }
    private void UpdateQuantity(float value)
    {
        if (!CanUseView || _busy || _rendering) return;
        var player = gameSessionService.State?.Player; var ship = player?.GetActiveShip();
        if (!ReferenceEquals(_boundState, gameSessionService.State) || _shipId != ship?.ShipId ||
            _location != Location(player) || _currentFuel != Mathf.Clamp(ship?.CurrentFuel ?? 0, 0, Mathf.Max(0, ship?.FuelCapacity ?? 0)))
        { Refresh(); return; }
        _targetFuel = Mathf.Clamp(Mathf.RoundToInt(value), _currentFuel, Mathf.Max(0, ship?.FuelCapacity ?? 0));
        Render();
    }
    private void Render()
    {
        if (!CanUseView) return;
        var player = gameSessionService.State?.Player; var ship = player?.GetActiveShip();
        int capacity = Mathf.Max(0, ship?.FuelCapacity ?? 0);
        _targetFuel = Mathf.Clamp(_targetFuel, _currentFuel, capacity);
        int amount = Mathf.Max(0, _targetFuel - _currentFuel);
        long cost = (long)amount * Mathf.Max(0, _unitPrice);
        bool enough = player != null && cost <= player.Credits;
        bool canBuy = !_busy && ship != null && capacity > 0 && amount > 0 && _unitPrice > 0 && enough && _refuelService.CanRefuel(amount);
        string warning = ship == null ? "Корабль недоступен" : capacity <= 0 ? "Топливная ёмкость недоступна" :
            _currentFuel >= capacity ? "Бак заполнен" : _unitPrice <= 0 ? "Цена топлива недоступна" :
            !enough || (amount == 0 && player.Credits < _unitPrice) ? "Недостаточно кредитов" :
            !_refuelService.CanRefuel(amount > 0 ? amount : 1) ? "Заправка недоступна" : string.Empty;
        // Changing Slider.maxValue may emit onValueChanged when switching ships.
        _rendering = true;
        try { _view.SetPreview(_currentFuel, _targetFuel, capacity, cost, canBuy, warning); }
        finally { _rendering = false; }
    }
    private void HandleRefuelClicked()
    {
        if (!CanUseView || _busy || !refuelButton.interactable) return;
        var player = gameSessionService.State?.Player; var ship = player?.GetActiveShip();
        if (!ReferenceEquals(_boundState, gameSessionService.State) || ship == null || _shipId != ship.ShipId ||
            _location != Location(player) || ship.CurrentFuel != _currentFuel || _refuelService.GetFuelUnitPrice() != _unitPrice ||
            _targetFuel > ship.FuelCapacity)
        { Refresh(); _view.SetWarning("Данные изменились. Выберите уровень топлива заново"); return; }
        int amount = _targetFuel - ship.CurrentFuel;
        if (amount <= 0 || !_refuelService.CanRefuel(amount) || (long)amount * _unitPrice > player.Credits)
        { Refresh(true); return; }
        _busy = true; refuelButton.interactable = false;
        RefuelResult result;
        try { result = _refuelService.Refuel(amount); }
        finally { _busy = false; Refresh(); }
        if (!CanUseView) return;
        if (result == null) { _view.SetWarning("Не удалось выполнить заправку"); return; }
        _view.SetWarning(result.IsSuccess ? string.Empty : Failure(result.resultType));
        AppLog.Info(DebugLogChannel.UI, typeof(RefuelPanelController),
            "Refuel result | Amount=" + amount + " | Result=" + result.resultType);
    }
    private static string Failure(RefuelResultType result)
    {
        switch (result)
        {
            case RefuelResultType.NotEnoughCredits: return "Недостаточно кредитов";
            case RefuelResultType.FuelAlreadyFull: return "Бак заполнен";
            case RefuelResultType.RefuelUnavailable: return "Заправка недоступна";
            case RefuelResultType.InvalidFuelPrice: return "Цена топлива недоступна";
            default: return "Не удалось выполнить заправку";
        }
    }
}
