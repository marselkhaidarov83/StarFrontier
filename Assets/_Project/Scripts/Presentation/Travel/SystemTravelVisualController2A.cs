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

    private bool _routeSnapshotCreated;
    private Vector3 _routeStartSnapshot;
    private Vector3 _staticDestinationSnapshot;
    private TravelDestinationType _routeDestinationType;
    private SimpleEventBus _eventBus;

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

        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        if (_eventBus != null)
            _eventBus.Subscribe<DestinationSelectedEvent>(OnDestinationSelected);
    }

    private void OnDestroy()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<DestinationSelectedEvent>(OnDestinationSelected);
    }

    private void OnDestinationSelected(DestinationSelectedEvent evt)
    {
        _routeSnapshotCreated = false;
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double travelLineMs = 0.0;
        double engineGlowMs = 0.0;
        double readPositionMs = 0.0;

        bool hasTravelService = false;

        try
        {
            hasTravelService =
                _travelService != null;

            if (!hasTravelService)
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            UpdateTravelLine();

            travelLineMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            UpdateEngineGlow();

            engineGlowMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _lastShipPosition =
                _travelService.State.GetCurrentPosition();

            readPositionMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + name +
                " | HasTravelService=" + hasTravelService +
                " | TravelLineMs=" + travelLineMs.ToString("F3") +
                " | EngineGlowMs=" + engineGlowMs.ToString("F3") +
                " | ReadPositionMs=" + readPositionMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "SystemTravelVisualController2A.Update",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "SystemTravelVisualController2A.Update",
                startedAt,
                details);
        }
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

        TravelRoutePreview2A preview =
    _travelService.GetCurrentRoutePreview2A(
        travelLineView.SmallDotSpacing,
        travelLineView.MaxBigDots,
        travelLineView.MaxSmallDots,
        GetSecondsPerTick()
    );

        travelLineView.ShowPreview(preview);
    }

    private void CreateRouteSnapshotIfNeeded(SystemTravelState state)
    {
        if (_routeSnapshotCreated)
            return;

        _routeStartSnapshot = state.GetCurrentPosition();
        _staticDestinationSnapshot = _travelService.GetCurrentDestinationPosition();

        if (state.Destination != null)
            _routeDestinationType = state.Destination.Type;

        _routeSnapshotCreated = true;
    }

    private Vector3 GetVisualDestinationPosition(SystemTravelState state)
    {
        if (ShouldUpdateDestinationEveryFrame(state))
            return _travelService.GetCurrentDestinationPosition();

        return _staticDestinationSnapshot;
    }

    private bool ShouldUpdateDestinationEveryFrame(SystemTravelState state)
    {
        if (state == null)
            return false;

        if (state.Destination == null)
            return false;

        return state.Destination.Type == TravelDestinationType.Planet;
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