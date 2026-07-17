using UnityEngine;

public sealed class SystemTravelVisualController2A : CustomMonoBehaviour
{
    [Header("Views")]
    [SerializeField] private TravelLineView2A travelLineView;
    [SerializeField] private ShipEngineGlowView2 engineGlowView;

    [Header("Options")]
    [SerializeField] private bool showLineWhenDestinationSelected = true;
    [SerializeField] private bool showLineWhileFlying = true;

    [Header("Fallback")]
    [SerializeField] private float fallbackShipSpeedUnitsPerSecond = 100f;
    [SerializeField] private float fallbackSecondsPerTick = 1f;

    private ISystemTravelService _travelService;
    private IHangarService _hangarService;
    private Vector3 _lastShipPosition;

    private void Start()
    {
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _hangarService = Bootstrapper.Instance.ServiceRegistry.Get<IHangarService>();

        if (_travelService == null)
        {
            Debug.LogError("[SystemTravelVisualController2] ISystemTravelService not found.");
            return;
        }

        if (_hangarService == null)
            Debug.LogWarning("[SystemTravelVisualController2] IHangarService not found. Fallback speed will be used.");

        _lastShipPosition = _travelService.State.GetCurrentPosition();

        if (travelLineView != null)
            travelLineView.Hide();

        if (engineGlowView != null)
            engineGlowView.Hide();
    }

    private void Update()
    {
        if (_travelService == null)
            return;

        UpdateTravelLine();
        UpdateEngineGlow();

        _lastShipPosition = _travelService.State.GetCurrentPosition();
    }

    private void UpdateTravelLine()
    {
        if (travelLineView == null)
            return;

        SystemTravelState state = _travelService.State;

        bool shouldShow =
            state.Status == SystemTravelStatus.DestinationSelected && showLineWhenDestinationSelected ||
            state.Status == SystemTravelStatus.Flying && showLineWhileFlying;

        if (!shouldShow || !state.HasDestination)
        {
            travelLineView.Hide();
            return;
        }

        Vector3 from = state.GetCurrentPosition();
        Vector3 to = _travelService.GetCurrentDestinationPosition();

        float shipSpeedUnitsPerSecond = GetCurrentShipTravelSpeed();
        float secondsPerTick = GetSecondsPerTick();

        travelLineView.Show(
            from,
            to,
            shipSpeedUnitsPerSecond,
            secondsPerTick
        );
    }

    private void UpdateEngineGlow()
    {
        if (engineGlowView == null)
            return;

        SystemTravelState state = _travelService.State;

        if (state.Status != SystemTravelStatus.Flying)
        {
            engineGlowView.Hide();
            return;
        }

        Vector3 current = state.GetCurrentPosition();
        Vector3 movementDirection = current - _lastShipPosition;

        engineGlowView.Show();
    }

    private float GetCurrentShipTravelSpeed()
    {
        if (_hangarService == null)
            return fallbackShipSpeedUnitsPerSecond;

        var stats = _hangarService.GetActiveShipStats();

        if (stats == null)
            return fallbackShipSpeedUnitsPerSecond;

        if (stats.Speed <= 0f)
            return fallbackShipSpeedUnitsPerSecond;

        return stats.Speed;
    }

    private float GetSecondsPerTick()
    {
        if (GameTimeService.SecondsPerDay <= 0f)
            return fallbackSecondsPerTick;

        return GameTimeService.SecondsPerDay;
    }
}