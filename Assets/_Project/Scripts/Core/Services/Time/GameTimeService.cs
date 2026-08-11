using UnityEngine;

// Сервис пошаговых тиков.
public sealed class GameTimeService : CustomService, IGameTimeService
{
    private readonly SimpleEventBus _eventBus;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemTravelService _systemTravelService;
    private readonly IGalaxyPopulationService _galaxyPopulationService;
    private readonly IGalaxyNpcMovementService _galaxyNpcMovementService;
    private readonly IGalaxyNpcCombatService _galaxyNpcCombatService;
    private readonly ISystemEnemyService _systemEnemyService;
    private readonly ISaveService _saveService;

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
        _systemEnemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();

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
    }

    public void Tick(float deltaTime)
    {
        TickSaveServices(deltaTime);

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

        if (State.IsPaused)
            State.Accumulator = 0;
    }

    private void TickSaveServices(float deltaTime)
    {
        _saveService.Tick(deltaTime);
    }

    private void TickAllServices(float deltaTime)
    {
        _galaxyNpcCombatService.Tick(deltaTime);
        _systemEnemyService.TickSystemMapCombat(deltaTime);
        _orbitalMotionService.Tick(deltaTime);
        _systemTravelService.Tick(deltaTime, State.CurrentQuantTick);
        _galaxyPopulationService.Tick(deltaTime);
        _galaxyNpcMovementService.Tick(deltaTime, State.CurrentQuantTick);
    }

    private void AdvanceOneQuantTick()
    {
        _previousTick = State.CurrentQuantTick;

        _eventBus.Publish(new GameTimeQuantumAdvancedEvent(State.CurrentQuantTick));
        _eventBus.Publish(new GameDayChangedEvent(_previousTick, State.CurrentQuantTick));
    }

    public void WriteTimeToSave(GameRuntimeState state)
    {
        if (state == null)
            return;

        if (state.Meta == null)
            state.Meta = new GameRuntimeMetaState();

        state.Meta.CurrentGameDay = Mathf.Max(1, State.CurrentQuantTick);
        state.Meta.GameSimulationTimeSeconds = Mathf.Max(0f, State.SimulationTimeSeconds);
        state.Meta.GameTimeAccumulator = Mathf.Clamp(
            State.Accumulator,
            0f,
            GameTimeState.SecondsPerDay
        );
        state.Meta.IsGameTimePaused = State.IsPaused;
    }

    public void RestoreTimeFromSave(GameRuntimeState state)
    {
        if (state == null || state.Meta == null)
            return;

        State.CurrentQuantTick = Mathf.Max(1, state.Meta.CurrentGameDay);
        State.SimulationTimeSeconds = Mathf.Max(0f, state.Meta.GameSimulationTimeSeconds);
        State.Accumulator = Mathf.Clamp(
            state.Meta.GameTimeAccumulator,
            0f,
            GameTimeState.SecondsPerDay
        );
        State.IsPaused = state.Meta.IsGameTimePaused;

        _previousTick = State.CurrentQuantTick;
    }
}