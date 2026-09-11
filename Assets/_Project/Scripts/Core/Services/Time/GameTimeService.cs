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
    private readonly IGalaxyNpcBehaviorService _galaxyNpcBehaviorService;
    private readonly ISaveService _saveService;

    private bool _currentTickStarted;
    private bool _pauseAfterCurrentTick;
    private bool _singleStepInProgress;

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
        _galaxyNpcBehaviorService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcBehaviorService>();
        _galaxyNpcMovementService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcMovementService>();
        _galaxyNpcCombatService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcCombatService>();
        _systemEnemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();

        ApplyGameTimeConfig();

        State = new GameTimeState
        {
            SecondsPerDayTimeout = GameTimeState.SecondsPerDay
        };
    }

    public void SetPaused(bool paused)
    {
        if (!paused)
        {
            _singleStepInProgress = false;

            if (!State.IsPaused)
            {
                _pauseAfterCurrentTick = false;
                return;
            }

            _pauseAfterCurrentTick = false;
            State.IsPaused = false;
            _eventBus.Publish(new GameTimePauseChangedEvent(State.IsPaused));

            LogGameTimeDebug(
                "[TickDebug] Pause changed | IsPaused=" +
                State.IsPaused +
                " | CurrentTick=" +
                State.CurrentQuantTick +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            return;
        }

        if (State.IsPaused)
            return;

        if (_currentTickStarted || State.Accumulator > 0f)
        {
            _pauseAfterCurrentTick = true;

            LogGameTimeDebug(
                "[TickDebug] Pause requested at tick end | CurrentTick=" +
                State.CurrentQuantTick +
                " | Accumulator=" +
                State.Accumulator.ToString("F2") +
                " | SecondsPerTick=" +
                GameTimeState.SecondsPerDay.ToString("F2"));

            return;
        }

        _pauseAfterCurrentTick = false;
        _singleStepInProgress = false;
        State.IsPaused = true;
        _eventBus.Publish(new GameTimePauseChangedEvent(State.IsPaused));

        LogGameTimeDebug(
            "[TickDebug] Pause changed | IsPaused=" +
            State.IsPaused +
            " | CurrentTick=" +
            State.CurrentQuantTick +
            " | Accumulator=" +
            State.Accumulator.ToString("F2"));
    }

    public void TogglePause()
    {
        if (State.IsPaused)
        {
            SetPaused(false);
            return;
        }

        if (_pauseAfterCurrentTick)
        {
            _pauseAfterCurrentTick = false;

            LogGameTimeDebug(
                "[TickDebug] Pending pause cancelled | CurrentTick=" +
                State.CurrentQuantTick +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            return;
        }

        SetPaused(true);
    }

    public void StepOneDay()
    {
        if (!State.IsPaused)
        {
            LogGameTimeDebug(
                "[TickDebug] StepOneDay ignored because game is already playing | CurrentTick=" +
                State.CurrentQuantTick +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            return;
        }

        if (_singleStepInProgress)
        {
            LogGameTimeDebug(
                "[TickDebug] StepOneDay ignored because single step is already running | CurrentTick=" +
                State.CurrentQuantTick +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            return;
        }

        State.Accumulator = 0f;
        _currentTickStarted = false;
        _pauseAfterCurrentTick = false;
        _singleStepInProgress = true;

        LogGameTimeDebug(
            "[TickDebug] Single step requested | CurrentTick=" +
            State.CurrentQuantTick +
            " | IsPaused=" +
            State.IsPaused +
            " | SecondsPerTick=" +
            GameTimeState.SecondsPerDay.ToString("F2") +
            " | Accumulator=" +
            State.Accumulator.ToString("F2"));
    }
    public void Tick(float deltaTime)
    {
        TickSaveServices(deltaTime);

        if (State.IsPaused && !_singleStepInProgress)
            return;

        StartCurrentTickIfNeeded();

        State.SimulationTimeSeconds += deltaTime;
        State.Accumulator += deltaTime;

        TickAllServices(deltaTime);

        if (State.Accumulator < GameTimeState.SecondsPerDay)
            return;

        CompleteCurrentTickIfNeeded();

        State.Accumulator -= GameTimeState.SecondsPerDay;

        if (_singleStepInProgress)
        {
            _singleStepInProgress = false;
            _pauseAfterCurrentTick = false;
            State.Accumulator = 0f;

            LogGameTimeDebug(
                "[TickDebug] Single step completed | CurrentTick=" +
                State.CurrentQuantTick +
                " | IsPaused=" +
                State.IsPaused +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            return;
        }

        if (_pauseAfterCurrentTick)
        {
            _pauseAfterCurrentTick = false;
            State.Accumulator = 0f;

            LogGameTimeDebug(
                "[TickDebug] Paused after completed tick | CurrentTick=" +
                State.CurrentQuantTick +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            SetPaused(true);
            return;
        }

        while (State.Accumulator >= GameTimeState.SecondsPerDay)
        {
            StartCurrentTickIfNeeded();
            CompleteCurrentTickIfNeeded();

            State.Accumulator -= GameTimeState.SecondsPerDay;

            if (_pauseAfterCurrentTick)
            {
                _pauseAfterCurrentTick = false;
                State.Accumulator = 0f;

                LogGameTimeDebug(
                    "[TickDebug] Paused after completed catch-up tick | CurrentTick=" +
                    State.CurrentQuantTick +
                    " | Accumulator=" +
                    State.Accumulator.ToString("F2"));

                SetPaused(true);
                return;
            }
        }
    }

    private void StartCurrentTickIfNeeded()
    {
        if (_currentTickStarted)
            return;

        State.CurrentQuantTick++;

        _currentTickStarted = true;

        LogGameTimeDebug(
            "[TickDebug] GameTickStarted | Tick=" +
            State.CurrentQuantTick +
            " | SecondsPerTick=" +
            GameTimeState.SecondsPerDay.ToString("F2") +
            " | Accumulator=" +
            State.Accumulator.ToString("F2"));

        _eventBus.Publish(new GameTickStartedEvent(State.CurrentQuantTick));
    }

    private void CompleteCurrentTickIfNeeded()
    {
        if (!_currentTickStarted)
            return;

        _eventBus.Publish(new GameTimeQuantumAdvancedEvent(State.CurrentQuantTick));
        _eventBus.Publish(new GameDayChangedEvent(State.CurrentQuantTick - 1, State.CurrentQuantTick));

        LogGameTimeDebug(
            "[TickDebug] GameTickCompleted | Tick=" +
            State.CurrentQuantTick +
            " | SecondsPerTick=" +
            GameTimeState.SecondsPerDay.ToString("F2") +
            " | Accumulator=" +
            State.Accumulator.ToString("F2"));

        _currentTickStarted = false;
    }

    private void ApplyGameTimeConfig()
    {
        float secondsPerTick = 1f;

        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet<IConfigService>(
                out IConfigService configService) &&
            configService.GameConfig != null)
        {
            secondsPerTick =
                configService.GameConfig.SecondsPerGameTick;
        }

        GameTimeState.SecondsPerDay =
            Mathf.Max(0.01f, secondsPerTick);
    }

    private void TickSaveServices(float deltaTime)
    {
        _saveService.Tick(deltaTime);
    }

    private void TickAllServices(float deltaTime)
    {
        _galaxyNpcCombatService.Tick(deltaTime);
        _orbitalMotionService.Tick(deltaTime);
        _galaxyPopulationService.Tick(deltaTime);

        _galaxyNpcBehaviorService.Tick(State.CurrentQuantTick);
        _galaxyNpcMovementService.Tick(deltaTime, State.CurrentQuantTick);

        _systemTravelService.Tick(deltaTime, State.CurrentQuantTick);
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
        State.SecondsPerDayTimeout = GameTimeState.SecondsPerDay;
        State.IsPaused = true;
        state.Meta.IsGameTimePaused = true;

        _currentTickStarted = false;
        _pauseAfterCurrentTick = false;
        _singleStepInProgress = false;

        _eventBus.Publish(new GameTimePauseChangedEvent(State.IsPaused));
    }

    private void LogGameTimeDebug(string message)
    {
        bool previousDebugEnabled = _debugEnabled;
        bool previousDebugStop = _debugStop;

        _debugEnabled = true;
        _debugStop = false;

        LogCustom(message);

        _debugEnabled = previousDebugEnabled;
        _debugStop = previousDebugStop;
    }
}