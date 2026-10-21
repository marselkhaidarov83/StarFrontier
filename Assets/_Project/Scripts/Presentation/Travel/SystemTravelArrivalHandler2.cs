using UnityEngine;

public sealed class SystemTravelArrivalHandler2 : CustomMonoBehaviour
{
    [Header("FX")]
    [SerializeField] private ArrivalFxView2 arrivalFlash;
    [SerializeField] private ArrivalFxView2 systemJumpFlash;

    // [Header("Optional UI / Routing")]
    // [SerializeField] private GameObject planetScreenPanel;

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;

    private void Start()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        if (_eventBus == null)
        {
            AppLog.Error(DebugLogChannel.Travel, "[SystemTravelArrivalHandler] SimpleEventBus not found.");
            return;
        }

        _eventBus.Subscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
    }

    private void OnDestroy()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        switch (evt.DestinationType)
        {
            case TravelDestinationType.Planet:
                HandlePlanetArrival(evt);
                break;

            case TravelDestinationType.Station:
                HandleStationArrival(evt);
                break;

            case TravelDestinationType.MapPoint:
                HandleMapPointArrival(evt);
                break;

            case TravelDestinationType.Npc:
                HandleNpcArrival(evt);
                break;

            case TravelDestinationType.SystemExit:
                HandleSystemExitArrival(evt);
                break;

            default:
                AppLog.Info(DebugLogChannel.Travel, "[SystemTravelArrivalHandler] Arrival with no destination type.");
                break;
        }
    }

    private void HandlePlanetArrival(SystemTravelCompletedEvent evt)
    {
        if (PlanetLandingRules2A.IsBlocked(_gameSessionService.State, _configService.DebugSettings))
        {
            _gameSessionService.State.Player.CurrentPlanetId = string.Empty;
            _eventBus.Publish(new PlanetLandingBlockedEvent2A(evt.PlanetId));
            return;
        }

        AppLog.Info(DebugLogChannel.Travel, $"[SystemTravelArrivalHandler] Arrived at planet: {evt.PlanetId}");

        if (arrivalFlash != null)
            arrivalFlash.Play(evt.FinalPosition);

        // if (planetScreenPanel != null)
        //     planetScreenPanel.SetActive(true);

        _gameSessionService.State.Player.CurrentPlanetId = evt.PlanetId;
        _eventBus.Publish(new PlanetEnteredEvent(evt.PlanetId));

        // Позже в E1-S7-006 здесь будет MetaSceneRouter.OpenPlanetScreen(evt.PlanetId)
    }

    private void HandleStationArrival(SystemTravelCompletedEvent evt)
    {
        AppLog.Info(
            DebugLogChannel.Travel,
            "[SystemTravelArrivalHandler] Arrived at station point: " + evt.FinalPosition);

        if (arrivalFlash != null)
            arrivalFlash.Play(evt.FinalPosition);

        // Пока ничего не открываем. Корабль просто стоит у станции.
    }

    private void HandleMapPointArrival(SystemTravelCompletedEvent evt)
    {
        AppLog.Info(DebugLogChannel.Travel, $"[SystemTravelArrivalHandler] Arrived at map point: {evt.FinalPosition}");

        if (arrivalFlash != null)
            arrivalFlash.Play(evt.FinalPosition);

        // Ничего не открываем. Корабль просто стоит.
    }

    private void HandleNpcArrival(SystemTravelCompletedEvent evt)
    {
        AppLog.Info(
            DebugLogChannel.Travel,
            "[SystemTravelArrivalHandler] Arrived at NPC point: " + evt.FinalPosition);

        if (arrivalFlash != null)
            arrivalFlash.Play(evt.FinalPosition);

        // Ничего не открываем. Корабль просто стоит рядом с NPC.
    }

    private void HandleSystemExitArrival(SystemTravelCompletedEvent evt)
    {
        AppLog.Info(DebugLogChannel.Travel, $"[SystemTravelArrivalHandler] Reached system exit. Target system: {evt.TargetSystemId}");

        if (systemJumpFlash != null)
            systemJumpFlash.Play(evt.FinalPosition);

        // Полный переход в новую систему будет в E1-S7-012.
    }
}
