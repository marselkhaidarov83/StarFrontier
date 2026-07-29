using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View-компонент верхнего HUD SystemScene.
/// Показывает текущее топливо активного корабля:
/// - текст;
/// - процент;
/// - одну из трёх иконок состояния.
///
/// Gameplay-логику не содержит.
/// Источник данных: IGameSessionService -> PlayerState -> ActiveShip.
/// Обновляется на старте, в OnEnable и при FuelChangedEvent.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemTopHudFuelIndicator2A : MonoBehaviour
{
    [Header("UI References")]

    [SerializeField]
    private Image fuelIconImage;

    [SerializeField]
    private TMP_Text fuelText;

    [SerializeField]
    private TMP_Text fuelPercentText;

    [Header("Fuel State Icons")]

    [SerializeField]
    private Sprite iconFuelLow;

    [SerializeField]
    private Sprite iconFuelMedium;

    [SerializeField]
    private Sprite iconFuelNormal;

    [Header("Behaviour")]

    [SerializeField]
    private bool refreshEveryFrame = false;

    [SerializeField]
    private bool hideWhenServicesMissing = false;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logMissingServices = true;

    [SerializeField]
    private bool logMissingSprites = true;

    private IGameSessionService _gameSessionService;
    private SimpleEventBus _eventBus;

    private bool _initialized;
    private int _lastFuel = int.MinValue;
    private int _lastCapacity = int.MinValue;
    private FuelHudState2A _lastState = FuelHudState2A.Low;

    private void Awake()
    {
        TryInitialize();
    }

    private void OnEnable()
    {
        TryInitialize();
        SubscribeEvents();
        Refresh();
    }

    private void Start()
    {
        TryInitialize();
        Refresh();
    }

    private void Update()
    {
        if (!refreshEveryFrame)
            return;

        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void Refresh()
    {
        if (!TryInitialize())
        {
            ApplyMissingDataView();
            return;
        }

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
        {
            ApplyMissingDataView();
            return;
        }

        int currentFuel = activeShip.CurrentFuel;
        int fuelCapacity = activeShip.FuelCapacity;

        FuelHudState2A state =
            FuelHudStateUtility2A.GetState(
                currentFuel,
                fuelCapacity);

        if (_lastFuel == currentFuel &&
            _lastCapacity == fuelCapacity &&
            _lastState == state)
        {
            return;
        }

        _lastFuel = currentFuel;
        _lastCapacity = fuelCapacity;
        _lastState = state;

        ApplyFuelView(
            currentFuel,
            fuelCapacity,
            state);
    }

    public void ForceRefresh()
    {
        _lastFuel = int.MinValue;
        _lastCapacity = int.MinValue;
        Refresh();
    }

    private bool TryInitialize()
    {
        if (_initialized &&
            _gameSessionService != null)
        {
            return true;
        }

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            LogMissing(
                "[SystemTopHudFuelIndicator2A] Bootstrapper or ServiceRegistry not found.");

            _initialized = false;
            return false;
        }

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry
                .Get<SimpleEventBus>();

        if (_gameSessionService == null)
        {
            LogMissing(
                "[SystemTopHudFuelIndicator2A] IGameSessionService not found.");

            _initialized = false;
            return false;
        }

        _initialized = true;
        return true;
    }

    private void SubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<FuelChangedEvent>(
            OnFuelChanged);

        _eventBus.Subscribe<FuelChangedEvent>(
            OnFuelChanged);
    }

    private void UnsubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<FuelChangedEvent>(
            OnFuelChanged);
    }

    private void OnFuelChanged(FuelChangedEvent evt)
    {
        ForceRefresh();
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null ||
            _gameSessionService.State.Player.PlayerShipState == null)
        {
            return null;
        }

        return _gameSessionService
            .State
            .Player
            .PlayerShipState
            .GetActiveShip();
    }

    private void ApplyFuelView(
        int currentFuel,
        int fuelCapacity,
        FuelHudState2A state)
    {
        gameObject.SetActive(true);

        if (fuelText != null)
        {
            fuelText.text =
                FuelHudStateUtility2A.BuildFuelLabel(
                    currentFuel,
                    fuelCapacity);
        }

        if (fuelPercentText != null)
        {
            float percent =
                FuelHudStateUtility2A.GetPercent(
                    currentFuel,
                    fuelCapacity);

            fuelPercentText.text =
                $"{Mathf.RoundToInt(percent)}%";
        }

        if (fuelIconImage != null)
        {
            fuelIconImage.sprite =
                GetSpriteForState(state);

            fuelIconImage.enabled =
                fuelIconImage.sprite != null;
        }
    }

    private Sprite GetSpriteForState(
        FuelHudState2A state)
    {
        switch (state)
        {
            case FuelHudState2A.Normal:
                if (iconFuelNormal == null)
                    LogMissingSprite("iconFuelNormal");
                return iconFuelNormal;

            case FuelHudState2A.Medium:
                if (iconFuelMedium == null)
                    LogMissingSprite("iconFuelMedium");
                return iconFuelMedium;

            case FuelHudState2A.Low:
            default:
                if (iconFuelLow == null)
                    LogMissingSprite("iconFuelLow");
                return iconFuelLow;
        }
    }

    private void ApplyMissingDataView()
    {
        if (hideWhenServicesMissing)
        {
            gameObject.SetActive(false);
            return;
        }

        if (fuelText != null)
            fuelText.text = "Топливо: —";

        if (fuelPercentText != null)
            fuelPercentText.text = "—";

        if (fuelIconImage != null)
        {
            fuelIconImage.sprite = iconFuelLow;
            fuelIconImage.enabled = iconFuelLow != null;
        }
    }

    private void LogMissing(string message)
    {
        if (!logMissingServices)
            return;

        Debug.LogWarning(message, this);
    }

    private void LogMissingSprite(string fieldName)
    {
        if (!logMissingSprites)
            return;

        Debug.LogWarning(
            $"[SystemTopHudFuelIndicator2A] Missing sprite: {fieldName}",
            this);
    }
}