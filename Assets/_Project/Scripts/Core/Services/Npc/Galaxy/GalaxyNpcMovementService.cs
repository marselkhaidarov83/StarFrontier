using UnityEngine;

public sealed class GalaxyNpcMovementService : CustomService, IGalaxyNpcMovementService
{
    private readonly IConfigService _configService;
    private readonly ISystemNpcMovementService _systemNpcMovementService;

    public GalaxyNpcMovementService()
    {
        _debugStop = true;
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _systemNpcMovementService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementService>();
    }

    public void Tick(float deltaTime, int quantTick)
    {
        LogCustom("starSystem.Count = " + _configService.GetAllStarSystems().Count);

        foreach (StarSystemConfig starSystem in _configService.GetAllStarSystems())
        {
            _systemNpcMovementService.Tick(starSystem, deltaTime, quantTick);
            LogCustom("starSystem = " + starSystem.DisplayName);
        }

        LogCustom("Tick ended = " + deltaTime);
    }
}