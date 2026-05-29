using System.Collections.Generic;

public class StarSystemService : IStarSystemService
{
    private readonly IGameSessionService _gameSessionService;
    private GalaxyState _galaxyState;
    private readonly SimpleEventBus _eventBus;

    public StarSystemService()
    {
        _gameSessionService = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper2A.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    private void DetermineGalaxyState()
    {
        if (_galaxyState == null)
            _galaxyState = _gameSessionService.CurrentSave.GameState.Galaxy;
    }

    public StarSystemState GetCurrentSystem()
    {
        DetermineGalaxyState();

        if (_galaxyState == null)
            return null;

        return GetSystem(_galaxyState.CurrentSystemId);
    }

    public StarSystemState GetSystem(string systemId)
    {
        DetermineGalaxyState();

        if (_galaxyState == null || string.IsNullOrEmpty(systemId))
            return null;

        for (int i = 0; i < _galaxyState.Systems.Count; i++)
        {
            var system = _galaxyState.Systems[i];

            if (system != null && system.Id == systemId)
                return system;
        }

        return null;
    }

    public List<StarSystemState> GetSystemsInSector(string sectorId)
    {
        DetermineGalaxyState();

        var result = new List<StarSystemState>();

        if (_galaxyState == null || string.IsNullOrEmpty(sectorId))
            return result;

        for (int i = 0; i < _galaxyState.Systems.Count; i++)
        {
            var system = _galaxyState.Systems[i];

            if (system != null && system.SectorId == sectorId)
                result.Add(system);
        }

        return result;
    }

    public bool IsSystemUnlocked(string systemId)
    {
        var system = GetSystem(systemId);
        return system != null && system.IsUnlocked;
    }

    public bool IsSystemDiscovered(string systemId)
    {
        var system = GetSystem(systemId);
        return system != null && system.IsDiscovered;
    }

    public void DiscoverSystem(string systemId)
    {
        var system = GetSystem(systemId);

        if (system == null)
            return;

        if (system.IsDiscovered)
            return;

        system.IsDiscovered = true;
        _eventBus.Publish(new StarSystemDiscoveredEvent(systemId));
    }

    public void UnlockSystem(string systemId)
    {
        var system = GetSystem(systemId);

        if (system == null)
            return;

        var wasUnlocked = system.IsUnlocked;

        system.IsDiscovered = true;
        system.IsUnlocked = true;

        if (!wasUnlocked)
            _eventBus.Publish(new StarSystemUnlockedEvent(systemId));
    }

    public void SetCurrentSystem(string systemId)
    {
        DetermineGalaxyState();

        var system = GetSystem(systemId);

        if (system == null)
            return;

        _galaxyState.CurrentSystemId = systemId;
    }
}