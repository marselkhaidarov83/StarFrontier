using System.Collections;
using UnityEngine;

//Сервис пошаговых тиков
public sealed class GameTimeService : CustomService, IGameTimeService
{
    private readonly SimpleEventBus _eventBus;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemTravelService _systemTravelService;
    private readonly IGalaxyPopulationService _galaxyPopulationService;
    private readonly IGalaxyNpcMovementService _galaxyNpcMovementService;
    private readonly IGalaxyNpcCombatService _galaxyNpcCombatService;
    private int _previousTick = 0;

    public GameTimeState State { get; }
    public int CurrentQuantTick => State.CurrentQuantTick;
    public bool IsPaused => State.IsPaused;
    public float SimulationTimeSeconds => State.SimulationTimeSeconds;
    public float DelayTime => State.SecondsPerDayTimeout;
    public static float SecondsPerDay => GameTimeState.SecondsPerDay;

    public GameTimeService()
    {
        _debugStop = true;
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _galaxyPopulationService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyPopulationService>();
        _galaxyNpcMovementService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcMovementService>();
        _galaxyNpcCombatService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcCombatService>();
        State = new GameTimeState();
    }

    public void SetPaused(bool paused)
    {
        if (State.IsPaused == paused)
            return;

        State.IsPaused = paused;
        _eventBus.Publish(new GameTimePauseChangedEvent(State.IsPaused));

        LogCustom($"[GameTimeService] Pause changed. IsPaused: {State.IsPaused}");
    }

    public void TogglePause()
    {
        SetPaused(!State.IsPaused);
    }

    public void StepOneDay()
    {
        // State.SimulationTimeSeconds += State.SecondsPerDay;
        // TickAllServices(State.SecondsPerDay);
        // AdvanceOneQuantTick();
    }

    public void Tick(float deltaTime)
    {
        // Делаем, чтобы могли додвигаться шаг
        if (State.IsPaused && State.Accumulator == 0)
            return;

        if (_previousTick == State.CurrentQuantTick)
        {
            State.CurrentQuantTick++;
            _eventBus.Publish(new GameTickStartedEvent(State.CurrentQuantTick));
        }

        State.SimulationTimeSeconds += deltaTime;
        State.Accumulator += deltaTime;
        TickAllServices(deltaTime);

        if (State.Accumulator < GameTimeState.SecondsPerDay)
            return;

        while (State.Accumulator >= GameTimeState.SecondsPerDay)
        {
            State.Accumulator -= GameTimeState.SecondsPerDay;
            AdvanceOneQuantTick();
        }

        // Делаем, чтобы могли додвигаться шаг
        if (State.IsPaused)
            State.Accumulator = 0;
    }

    private void TickAllServices(float deltaTime)
    {
        //Вызываем все сервисы, которые зависят от внутриигрового времени
        _galaxyNpcCombatService.Tick(deltaTime);
        _orbitalMotionService.Tick(deltaTime);
        _systemTravelService.Tick(deltaTime, State.CurrentQuantTick);
        _galaxyPopulationService.Tick(deltaTime);  
        _galaxyNpcMovementService.Tick(deltaTime, State.CurrentQuantTick);
    }

    private void AdvanceOneQuantTick()
    {
        _previousTick = State.CurrentQuantTick;
        // State.CurrentQuantTick++;

        _eventBus.Publish(new GameTimeQuantumAdvancedEvent(State.CurrentQuantTick));
        _eventBus.Publish(new GameDayChangedEvent(_previousTick, State.CurrentQuantTick));
    }
}