using UnityEngine;

public sealed class GalaxyNpcCombatService : CustomService, IGalaxyNpcCombatService
{
    //Параметр, чтобы по 1 тику логика запускалась только 1 раз
    private int _prevTick = 0;
    private SimpleEventBus _simpleEventBus;
    private readonly IConfigService _configService;
    private readonly ISystemNpcCombatService _systemNpcCombatService;

    public GalaxyNpcCombatService()
    {
        _debugStop = true;
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _systemNpcCombatService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcCombatService>();

        _simpleEventBus.Subscribe<GameTickStartedEvent>(OnGameTickStartedEvent);
    }

    private void OnGameTickStartedEvent(GameTickStartedEvent evt)
    {
        TickQuant(evt.CurrentTick);
    }

    public void TickQuant(int quantTick)
    {
        if (_prevTick == quantTick)
            return;

        LogCustom("starSystem.Count = " + _configService.GetAllStarSystems().Count);

        foreach (StarSystemConfig starSystem in _configService.GetAllStarSystems())
        {
            _systemNpcCombatService.Tick(starSystem, quantTick);
            LogCustom("starSystem = " + starSystem.DisplayName);
        }

        LogCustom("QuantTick = " + quantTick);
        _prevTick = quantTick;
    }

    public void Tick(float deltaTime)
    {
        _systemNpcCombatService.TickProjectiles(deltaTime);
    }
}