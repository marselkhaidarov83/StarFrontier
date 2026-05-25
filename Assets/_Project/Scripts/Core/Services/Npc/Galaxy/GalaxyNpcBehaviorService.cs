using UnityEngine;

public sealed class GalaxyNpcBehaviorService : CustomService, IGalaxyNpcBehaviorService
{
    //Параметр, чтобы по 1 тику логика запускалась только 1 раз
    private int _prevTick = 0;
    private SimpleEventBus _simpleEventBus;
    private readonly IConfigService _configService;
    private readonly ISystemNpcBehaviorService _systemNpcBehaviorService;

    public GalaxyNpcBehaviorService()
    {
        _debugStop = true;
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _systemNpcBehaviorService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();

        _simpleEventBus.Subscribe<GameDayChangedEvent>(OnGameDayChangedEvent);
    }

    private void OnGameDayChangedEvent(GameDayChangedEvent evt)
    {
        LogCustom("");
        Tick(evt.CurrentDay);
    }

    public void Tick(int quantTick)
    {
        if (_prevTick == quantTick)
            return;

        LogCustom("starSystem.Count = " + _configService.GetAllStarSystems().Count);

        foreach (StarSystemConfig starSystem in _configService.GetAllStarSystems())
        {
            _systemNpcBehaviorService.Tick(starSystem, quantTick);
            LogCustom("starSystem = " + starSystem.DisplayName);
        }

        LogCustom("QuantTick = " + quantTick);
        _prevTick = quantTick;
    }
}