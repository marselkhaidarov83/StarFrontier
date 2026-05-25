using UnityEngine;

public sealed class SystemTravelArrivalHandler : MonoBehaviour
{
    [Header("FX")]
    [SerializeField] private ArrivalFxView arrivalFlash;
    [SerializeField] private ArrivalFxView systemJumpFlash;

    // [Header("Optional UI / Routing")]
    // [SerializeField] private GameObject planetScreenPanel;

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;

    private void Start()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        if (_eventBus == null)
        {
            Debug.LogError("[SystemTravelArrivalHandler] SimpleEventBus not found.");
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

            case TravelDestinationType.MapPoint:
                HandleMapPointArrival(evt);
                break;

            case TravelDestinationType.SystemExit:
                HandleSystemExitArrival(evt);
                break;

            default:
                Debug.Log("[SystemTravelArrivalHandler] Arrival with no destination type.");
                break;
        }
    }

    private void HandlePlanetArrival(SystemTravelCompletedEvent evt)
    {
        Debug.Log($"[SystemTravelArrivalHandler] Arrived at planet: {evt.PlanetId}");

        if (arrivalFlash != null)
            arrivalFlash.Play(evt.FinalPosition);

        // if (planetScreenPanel != null)
        //     planetScreenPanel.SetActive(true);

        _gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId = evt.PlanetId;
        _eventBus.Publish(new PlanetEnteredEvent(evt.PlanetId));

        // Позже в E1-S7-006 здесь будет MetaSceneRouter.OpenPlanetScreen(evt.PlanetId)
    }

    private void HandleMapPointArrival(SystemTravelCompletedEvent evt)
    {
        Debug.Log($"[SystemTravelArrivalHandler] Arrived at map point: {evt.FinalPosition}");

        if (arrivalFlash != null)
            arrivalFlash.Play(evt.FinalPosition);

        // Ничего не открываем. Корабль просто стоит.
    }

    private void HandleSystemExitArrival(SystemTravelCompletedEvent evt)
    {
        Debug.Log($"[SystemTravelArrivalHandler] Reached system exit. Target system: {evt.TargetSystemId}");

        if (systemJumpFlash != null)
            systemJumpFlash.Play(evt.FinalPosition);

        // Полный переход в новую систему будет в E1-S7-012.
    }
}
