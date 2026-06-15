using System.Linq;

public class GalaxyDiscoveryService : CustomService, IGalaxyDiscoveryService
{
    private GalaxyRuntimeState _galaxyRuntimeState;
    private readonly SimpleEventBus _eventBus;

    public GalaxyDiscoveryService()
    {
        GalaxyRuntimeStateUpdate();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    private void GalaxyRuntimeStateUpdate()
    {
        if (_galaxyRuntimeState == null && Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State != null)
            _galaxyRuntimeState = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State.Galaxy;        
    }

    public bool IsSystemDiscovered(string systemId)
    {
        StarSystemRuntimeState systemState = FindSystem(systemId);
        return systemState != null && systemState.IsDiscovered;
    }

    public void DiscoverSystem(string systemId)
    {
        StarSystemRuntimeState systemState = FindSystem(systemId);

        if (systemState == null)
        {
            LogCustom("System not found: " + systemId);
            return;
        }

        if (systemState.IsDiscovered)
            return;

        systemState.IsDiscovered = true;

        _eventBus.Publish(new StarSystemUnlockedEvent(systemId));

        LogCustom("System discovered: " + systemId);
    }

    public void VisitSystem(string systemId)
    {
        StarSystemRuntimeState systemState = FindSystem(systemId);

        if (systemState == null)
        {
            LogCustom("System not found: " + systemId);
            return;
        }

        systemState.IsDiscovered = true;
        systemState.IsVisited = true;

        LogCustom("System visited: " + systemId);
    }

    private StarSystemRuntimeState FindSystem(string systemId)
    {
        GalaxyRuntimeStateUpdate();

        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Systems == null)
            return null;

        return _galaxyRuntimeState.Systems.FirstOrDefault(
            system => system.SystemId == systemId
        );
    }
}