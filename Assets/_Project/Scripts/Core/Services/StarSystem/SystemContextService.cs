using System.Linq;

public class SystemContextService : CustomService, ISystemContextService
{
    private GalaxyRuntimeState _galaxyRuntimeState;
    private readonly SimpleEventBus _eventBus;

    public string CurrentSystemId => _galaxyRuntimeState.CurrentSystemId;

    public SystemContextService()
    {
        GalaxyRuntimeStateUpdate();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _eventBus.Subscribe<StarSystemEnteredEvent>(OnStarSystemEntered);
        _eventBus.Subscribe<CurrentSystemEnteredEvent>(OnCurrentSystemChanged);
    }

    private void GalaxyRuntimeStateUpdate()
    {
        if (_galaxyRuntimeState == null && Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State != null)
            _galaxyRuntimeState = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State.Galaxy;        
    }

    public StarSystemRuntimeState GetCurrentSystemState()
    {
        GalaxyRuntimeStateUpdate();
        return GetSystemState(_galaxyRuntimeState.CurrentSystemId);
    }

    public StarSystemRuntimeState GetSystemState(string systemId)
    {
        GalaxyRuntimeStateUpdate();
        
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Systems == null)
            return null;

        return _galaxyRuntimeState.Systems.FirstOrDefault(
            system => system.SystemId == systemId
        );
    }

    public void RefreshCurrentSystem()
    {
        StarSystemRuntimeState state = GetCurrentSystemState();

        if (state == null)
        {
            LogCustom("[SystemContextService] Current system state not found: " + CurrentSystemId);
            return;
        }

        _eventBus.Publish(new StarSystemContextChangedEvent(
            state.SystemId,
            state.DevelopmentLevel,
            state.DangerLevel,
            state.Stability,
            state.IsDiscovered,
            state.IsVisited
        ));
    }

    private void OnStarSystemEntered(StarSystemEnteredEvent eventData)
    {
        RefreshCurrentSystem();
    }

    private void OnCurrentSystemChanged(CurrentSystemEnteredEvent eventData)
    {
        RefreshCurrentSystem();
    }
}