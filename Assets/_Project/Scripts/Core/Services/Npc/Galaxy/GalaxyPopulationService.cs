using UnityEngine;

public sealed class GalaxyPopulationService : CustomService, IGalaxyPopulationService
{
    private readonly IConfigService _configService;
    private readonly ISystemNpcPopulationService _systemPopulationService;

    public GalaxyPopulationService()
    {
        _debugStop = true;
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _systemPopulationService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcPopulationService>();
    }

    public void Tick(float deltaTime)
    {
        if (Bootstrapper.Instance.DebugSpawnOnlyInCurrentSystem)
        {
            StarSystemConfig currentSystem = _configService.GetCurrentSystemConfig();

            if (currentSystem == null)
            {
                return;
            }

            _systemPopulationService.Tick(currentSystem, deltaTime);
            LogCustom("current starSystem = " + currentSystem.DisplayName);
            return;
        }

        LogCustom("starSystem.Count = " + _configService.GetAllStarSystems().Count);

        foreach (StarSystemConfig starSystem in _configService.GetAllStarSystems())
        {
            _systemPopulationService.Tick(starSystem, deltaTime);
            LogCustom("starSystem = " + starSystem.DisplayName);
        }

        LogCustom("Tick ended = " + deltaTime);
    }
}