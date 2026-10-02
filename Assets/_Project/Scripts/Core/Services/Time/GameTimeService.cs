using UnityEngine;

// Сервис пошаговых тиков.
public sealed class GameTimeService : CustomService, IGameTimeService
{
    private int _previousFrameGc0Count;
    private int _previousFrameGc1Count;
    private int _previousFrameGc2Count;
    private int _fpsSampleFrameCount;
    private double _fpsSampleElapsedMs;
    private double _fpsSampleMinFrameMs = double.MaxValue;
    private double _fpsSampleMaxFrameMs;
    private double _fpsSampleMinFps = double.MaxValue;
    private double _fpsSampleMaxFps;
    private double _fpsRollingAverageFrameMs;
    private double _previousFrameMs;
    private double _lastGameTimeTickMs;
    private double _lastSaveServicesMs;
    private double _lastTickAllServicesMs;
    private double _lastServicesSumMs;
    private double _lastGalaxyNpcCombatMs;
    private double _lastOrbitalMotionMs;
    private double _lastGalaxyPopulationMs;
    private double _lastGalaxyNpcBehaviorMs;
    private double _lastGalaxyNpcMovementMs;
    private double _lastSystemEnemyMovementMs;
    private double _lastSystemTravelMs;
    private int _lastGameTimeTick = -1;
    private int _lastGameTimeUnityFrame = -1;
    private bool _lastGameTimeSkippedByPause;
    private int _simpleFrameTimeLogFrameCounter;

    private readonly SimpleEventBus _eventBus;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemTravelService _systemTravelService;
    private readonly IGalaxyPopulationService _galaxyPopulationService;
    private readonly IGalaxyNpcMovementService _galaxyNpcMovementService;
    private readonly IGalaxyNpcCombatService _galaxyNpcCombatService;
    private readonly ISystemEnemyService _systemEnemyService;
    private readonly ISystemEnemyMovementService _systemEnemyMovementService;
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
        _systemEnemyMovementService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyMovementService>();
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
        long gameTimeTickStartedAt =
            BeginPerfMeasure();

        double fpsAnalyticsMs = 0d;
        double saveServicesMs = 0d;
        double startCurrentTickMs = 0d;
        double accumulatorUpdateMs = 0d;
        double tickAllServicesCallMs = 0d;
        double completeCurrentTickMs = 0d;
        double accumulatorSubtractMs = 0d;
        double pauseOrStepCompletionMs = 0d;

        long phaseStartedAt =
            BeginPerfMeasure();

        TickFrameFpsAnalytics(deltaTime);

        fpsAnalyticsMs =
            EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt =
            BeginPerfMeasure();

        saveServicesMs =
            TickSaveServices(deltaTime);

        saveServicesMs =
            EndPerfMeasureMs(phaseStartedAt);

        _lastSaveServicesMs = saveServicesMs;
        _lastGameTimeTick = State.CurrentQuantTick;
        _lastGameTimeUnityFrame = Time.frameCount;

        if (State.IsPaused && !_singleStepInProgress)
        {
            _lastTickAllServicesMs = 0d;
            _lastServicesSumMs = 0d;
            _lastGalaxyNpcCombatMs = 0d;
            _lastOrbitalMotionMs = 0d;
            _lastGalaxyPopulationMs = 0d;
            _lastGalaxyNpcBehaviorMs = 0d;
            _lastGalaxyNpcMovementMs = 0d;
            _lastSystemEnemyMovementMs = 0d;
            _lastSystemTravelMs = 0d;
            _lastGameTimeSkippedByPause = true;
            _lastGameTimeTickMs = EndPerfMeasureMs(gameTimeTickStartedAt);

            LogGameTimeTickDetailIfNeeded(
                deltaTime,
                _lastGameTimeTickMs,
                fpsAnalyticsMs,
                saveServicesMs,
                startCurrentTickMs,
                accumulatorUpdateMs,
                tickAllServicesCallMs,
                completeCurrentTickMs,
                accumulatorSubtractMs,
                pauseOrStepCompletionMs,
                true);

            return;
        }

        _lastGameTimeSkippedByPause = false;

        phaseStartedAt =
            BeginPerfMeasure();

        StartCurrentTickIfNeeded();

        startCurrentTickMs =
            EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt =
            BeginPerfMeasure();

        State.SimulationTimeSeconds += deltaTime;
        State.Accumulator += deltaTime;

        accumulatorUpdateMs =
            EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt =
            BeginPerfMeasure();

        TickAllServices(deltaTime);

        tickAllServicesCallMs =
            EndPerfMeasureMs(phaseStartedAt);

        if (State.Accumulator < GameTimeState.SecondsPerDay)
        {
            _lastGameTimeTickMs = EndPerfMeasureMs(gameTimeTickStartedAt);

            LogGameTimeTickDetailIfNeeded(
                deltaTime,
                _lastGameTimeTickMs,
                fpsAnalyticsMs,
                saveServicesMs,
                startCurrentTickMs,
                accumulatorUpdateMs,
                tickAllServicesCallMs,
                completeCurrentTickMs,
                accumulatorSubtractMs,
                pauseOrStepCompletionMs,
                false);

            return;
        }

        phaseStartedAt =
            BeginPerfMeasure();

        CompleteCurrentTickIfNeeded();

        completeCurrentTickMs =
            EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt =
            BeginPerfMeasure();

        State.Accumulator -= GameTimeState.SecondsPerDay;

        accumulatorSubtractMs =
            EndPerfMeasureMs(phaseStartedAt);

        if (_singleStepInProgress)
        {
            phaseStartedAt =
                BeginPerfMeasure();

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

            pauseOrStepCompletionMs =
                EndPerfMeasureMs(phaseStartedAt);

            _lastGameTimeTickMs = EndPerfMeasureMs(gameTimeTickStartedAt);

            LogGameTimeTickDetailIfNeeded(
                deltaTime,
                _lastGameTimeTickMs,
                fpsAnalyticsMs,
                saveServicesMs,
                startCurrentTickMs,
                accumulatorUpdateMs,
                tickAllServicesCallMs,
                completeCurrentTickMs,
                accumulatorSubtractMs,
                pauseOrStepCompletionMs,
                false);

            return;
        }

        if (_pauseAfterCurrentTick)
        {
            phaseStartedAt =
                BeginPerfMeasure();

            _pauseAfterCurrentTick = false;
            State.Accumulator = 0f;
            State.IsPaused = true;
            _eventBus.Publish(new GameTimePauseChangedEvent(State.IsPaused));

            LogGameTimeDebug(
                "[TickDebug] Paused after completed tick | CurrentTick=" +
                State.CurrentQuantTick +
                " | IsPaused=" +
                State.IsPaused +
                " | Accumulator=" +
                State.Accumulator.ToString("F2"));

            pauseOrStepCompletionMs =
                EndPerfMeasureMs(phaseStartedAt);
        }

        _lastGameTimeTickMs = EndPerfMeasureMs(gameTimeTickStartedAt);

        LogGameTimeTickDetailIfNeeded(
            deltaTime,
            _lastGameTimeTickMs,
            fpsAnalyticsMs,
            saveServicesMs,
            startCurrentTickMs,
            accumulatorUpdateMs,
            tickAllServicesCallMs,
            completeCurrentTickMs,
            accumulatorSubtractMs,
            pauseOrStepCompletionMs,
            false);
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

        GameTickStartedEvent tickStartedEvent =
            new GameTickStartedEvent(State.CurrentQuantTick);

        if (IsGameTimeLoadAnalyticsEnabled())
        {
            _eventBus.PublishProfiled(
                tickStartedEvent,
                0.25,
                1.0,
                LogGameTickStartedSubscriberIfSlow,
                LogGameTickStartedPublishIfSlow);

            return;
        }

        _eventBus.Publish(tickStartedEvent);
    }

    private void LogGameTickStartedSubscriberIfSlow(
        string subscriberName,
        double subscriberMs,
        int subscriberIndex,
        int subscriberCount)
    {
        if (!IsGameTimeLoadAnalyticsEnabled())
            return;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[EVENT_BUS_SUBSCRIBER]" +
            " Event=GameTickStartedEvent" +
            " | Tick=" + State.CurrentQuantTick +
            " | UnityFrame=" + Time.frameCount +
            " | Subscriber=" + subscriberName +
            " | SubscriberIndex=" + subscriberIndex +
            " | SubscriberCount=" + subscriberCount +
            " | Ms=" + subscriberMs.ToString("F3"));
    }

    private void LogGameTickStartedPublishIfSlow(
        double publishMs,
        int subscriberCount,
        string maxSubscriberName,
        double maxSubscriberMs)
    {
        if (!IsGameTimeLoadAnalyticsEnabled())
            return;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[EVENT_BUS_PUBLISH]" +
            " Event=GameTickStartedEvent" +
            " | Tick=" + State.CurrentQuantTick +
            " | UnityFrame=" + Time.frameCount +
            " | SubscriberCount=" + subscriberCount +
            " | Ms=" + publishMs.ToString("F3") +
            " | MaxSubscriber=" + maxSubscriberName +
            " | MaxSubscriberMs=" + maxSubscriberMs.ToString("F3"));
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

    private double TickSaveServices(float deltaTime)
    {
        long startedAt =
            BeginPerfMeasure();

        _saveService.Tick(deltaTime);

        return EndPerfMeasureMs(startedAt);
    }

    private void LogGameTimeTickDetailIfNeeded(
        float deltaTime,
        double totalMs,
        double fpsAnalyticsMs,
        double saveServicesMs,
        double startCurrentTickMs,
        double accumulatorUpdateMs,
        double tickAllServicesCallMs,
        double completeCurrentTickMs,
        double accumulatorSubtractMs,
        double pauseOrStepCompletionMs,
        bool skippedByPause)
    {
        if (!IsGameTimeLoadAnalyticsEnabled())
            return;

        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance.DebugLogConfig;

        if (debugLogConfig == null)
            return;

        double frameMs =
            deltaTime > 0f
                ? deltaTime * 1000.0
                : 0.0;

        double approxFps =
            deltaTime > 0f
                ? 1.0 / deltaTime
                : 0.0;

        bool tickExpensive =
            totalMs >= debugLogConfig.NpcMovementLoadAnalyticsTickWarningMs;

        bool frameExpensive =
            frameMs >= debugLogConfig.GameTimeFrameSpikeWarningMs;

        bool fpsNear30 =
            approxFps <= debugLogConfig.NpcMovementLoadAnalyticsFpsWarningThreshold;

        if (!tickExpensive && !frameExpensive && !fpsNear30)
            return;

        string maxPhaseName =
            "FpsAnalytics";

        double maxPhaseMs =
            fpsAnalyticsMs;

        UpdateMaxService(
            "SaveServices",
            saveServicesMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        UpdateMaxService(
            "StartCurrentTick",
            startCurrentTickMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        UpdateMaxService(
            "AccumulatorUpdate",
            accumulatorUpdateMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        UpdateMaxService(
            "TickAllServicesCall",
            tickAllServicesCallMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        UpdateMaxService(
            "CompleteCurrentTick",
            completeCurrentTickMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        UpdateMaxService(
            "AccumulatorSubtract",
            accumulatorSubtractMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        UpdateMaxService(
            "PauseOrStepCompletion",
            pauseOrStepCompletionMs,
            ref maxPhaseName,
            ref maxPhaseMs);

        double tickAllServicesLogAndWrapperMs =
            tickAllServicesCallMs - _lastTickAllServicesMs;

        if (tickAllServicesLogAndWrapperMs < 0d)
            tickAllServicesLogAndWrapperMs = 0d;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[GAME_TIME_TICK_DETAIL]" +
            " UnityFrame=" + Time.frameCount +
            " | Tick=" + State.CurrentQuantTick +
            " | SkippedByPause=" + skippedByPause +
            " | IsPaused=" + State.IsPaused +
            " | DeltaTimeMs=" + frameMs.ToString("F2") +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | MaxPhase=" + maxPhaseName +
            " | MaxPhaseMs=" + maxPhaseMs.ToString("F2") +
            " | FpsAnalyticsMs=" + fpsAnalyticsMs.ToString("F3") +
            " | SaveServicesMs=" + saveServicesMs.ToString("F3") +
            " | StartCurrentTickMs=" + startCurrentTickMs.ToString("F3") +
            " | AccumulatorUpdateMs=" + accumulatorUpdateMs.ToString("F3") +
            " | TickAllServicesCallMs=" + tickAllServicesCallMs.ToString("F3") +
            " | TickAllServicesMeasuredMs=" + _lastTickAllServicesMs.ToString("F3") +
            " | TickAllServicesLogAndWrapperMs=" + tickAllServicesLogAndWrapperMs.ToString("F3") +
            " | CompleteCurrentTickMs=" + completeCurrentTickMs.ToString("F3") +
            " | AccumulatorSubtractMs=" + accumulatorSubtractMs.ToString("F3") +
            " | PauseOrStepCompletionMs=" + pauseOrStepCompletionMs.ToString("F3") +
            " | ServicesSumMs=" + _lastServicesSumMs.ToString("F3") +
            " | GalaxyNpcCombatMs=" + _lastGalaxyNpcCombatMs.ToString("F3") +
            " | OrbitalMotionMs=" + _lastOrbitalMotionMs.ToString("F3") +
            " | GalaxyPopulationMs=" + _lastGalaxyPopulationMs.ToString("F3") +
            " | GalaxyNpcBehaviorMs=" + _lastGalaxyNpcBehaviorMs.ToString("F3") +
            " | GalaxyNpcMovementMs=" + _lastGalaxyNpcMovementMs.ToString("F3") +
            " | SystemEnemyMovementMs=" + _lastSystemEnemyMovementMs.ToString("F3") +
            " | SystemTravelMs=" + _lastSystemTravelMs.ToString("F3"));
    }

    private void TickAllServices(float deltaTime)
    {
        long totalStartedAt = BeginPerfMeasure();

        long galaxyNpcCombatStartedAt = BeginPerfMeasure();
        _galaxyNpcCombatService.Tick(deltaTime, State.CurrentQuantTick);
        double galaxyNpcCombatMs = EndPerfMeasureMs(galaxyNpcCombatStartedAt);

        long orbitalMotionStartedAt = BeginPerfMeasure();
        _orbitalMotionService.Tick(deltaTime);
        double orbitalMotionMs = EndPerfMeasureMs(orbitalMotionStartedAt);

        long galaxyPopulationStartedAt = BeginPerfMeasure();
        _galaxyPopulationService.Tick(deltaTime);
        double galaxyPopulationMs = EndPerfMeasureMs(galaxyPopulationStartedAt);

        long galaxyNpcBehaviorStartedAt = BeginPerfMeasure();
        _galaxyNpcBehaviorService.Tick(State.CurrentQuantTick);
        double galaxyNpcBehaviorMs = EndPerfMeasureMs(galaxyNpcBehaviorStartedAt);

        long galaxyNpcMovementStartedAt = BeginPerfMeasure();
        _galaxyNpcMovementService.Tick(deltaTime, State.CurrentQuantTick);
        double galaxyNpcMovementMs = EndPerfMeasureMs(galaxyNpcMovementStartedAt);

        long systemEnemyMovementStartedAt = BeginPerfMeasure();
        _systemEnemyMovementService.Tick(deltaTime, State.CurrentQuantTick);
        double systemEnemyMovementMs = EndPerfMeasureMs(systemEnemyMovementStartedAt);

        long systemTravelStartedAt = BeginPerfMeasure();
        _systemTravelService.Tick(deltaTime, State.CurrentQuantTick);
        double systemTravelMs = EndPerfMeasureMs(systemTravelStartedAt);

        double totalMs = EndPerfMeasureMs(totalStartedAt);

        double galaxyNpcCombatCurrentSystemMs =
            _galaxyNpcCombatService.LastCurrentSystemMs;

        double galaxyNpcCombatOffscreenMs =
            _galaxyNpcCombatService.LastOffscreenMs;

        _lastTickAllServicesMs = totalMs;
        _lastGalaxyNpcCombatMs = galaxyNpcCombatMs;
        _lastOrbitalMotionMs = orbitalMotionMs;
        _lastGalaxyPopulationMs = galaxyPopulationMs;
        _lastGalaxyNpcBehaviorMs = galaxyNpcBehaviorMs;
        _lastGalaxyNpcMovementMs = galaxyNpcMovementMs;
        _lastSystemEnemyMovementMs = systemEnemyMovementMs;
        _lastSystemTravelMs = systemTravelMs;
        _lastServicesSumMs =
            galaxyNpcCombatMs +
            orbitalMotionMs +
            galaxyPopulationMs +
            galaxyNpcBehaviorMs +
            galaxyNpcMovementMs +
            systemEnemyMovementMs +
            systemTravelMs;

        double currentSystemMs =
            galaxyNpcCombatCurrentSystemMs +
            _galaxyNpcBehaviorService.LastCurrentSystemMs +
            _galaxyNpcMovementService.LastCurrentSystemMs +
            systemEnemyMovementMs +
            systemTravelMs;

        double offscreenMs =
            galaxyNpcCombatOffscreenMs +
            _galaxyNpcBehaviorService.LastOffscreenMs +
            _galaxyNpcMovementService.LastOffscreenMs;

        double unsplitGameServicesMs =
            orbitalMotionMs +
            galaxyPopulationMs;

        LogGameSystemSplitAnalytics(
            State.CurrentQuantTick,
            deltaTime,
            totalMs,
            currentSystemMs,
            offscreenMs,
            unsplitGameServicesMs,
            galaxyNpcCombatMs,
            galaxyNpcCombatCurrentSystemMs,
            galaxyNpcCombatOffscreenMs,
            orbitalMotionMs,
            galaxyPopulationMs,
            galaxyNpcBehaviorMs,
            _galaxyNpcBehaviorService.LastCurrentSystemMs,
            _galaxyNpcBehaviorService.LastOffscreenMs,
            galaxyNpcMovementMs,
            _galaxyNpcMovementService.LastCurrentSystemMs,
            _galaxyNpcMovementService.LastOffscreenMs,
            systemEnemyMovementMs,
            systemTravelMs);

        LogGameTimeLoadAnalyticsIfNeeded(
            State.CurrentQuantTick,
            deltaTime,
            totalMs,
            galaxyNpcCombatMs,
            orbitalMotionMs,
            galaxyPopulationMs,
            galaxyNpcBehaviorMs,
            galaxyNpcMovementMs,
            systemEnemyMovementMs,
            systemTravelMs);
    }

    private void LogGameSystemSplitAnalytics(
        int currentTick,
        float deltaTime,
        double tickAllServicesMs,
        double currentSystemMs,
        double offscreenMs,
        double unsplitGameServicesMs,
        double galaxyNpcCombatMs,
        double galaxyNpcCombatCurrentSystemMs,
        double galaxyNpcCombatOffscreenMs,
        double orbitalMotionMs,
        double galaxyPopulationMs,
        double galaxyNpcBehaviorMs,
        double galaxyNpcBehaviorCurrentSystemMs,
        double galaxyNpcBehaviorOffscreenMs,
        double galaxyNpcMovementMs,
        double galaxyNpcMovementCurrentSystemMs,
        double galaxyNpcMovementOffscreenMs,
        double systemEnemyMovementMs,
        double systemTravelMs)
    {
        if (!IsGameTimeLoadAnalyticsEnabled())
            return;

        double frameMs =
            deltaTime > 0f
                ? deltaTime * 1000.0
                : 0.0;

        double otherFrameMs =
            frameMs - currentSystemMs - offscreenMs - unsplitGameServicesMs;

        if (otherFrameMs < 0d)
            otherFrameMs = 0d;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[GAME_SYSTEM_SPLIT]" +
            " Tick=" + currentTick +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | TickAllServicesMs=" + tickAllServicesMs.ToString("F2") +
            " | CurrentSystemMs=" + currentSystemMs.ToString("F2") +
            " | OffscreenMs=" + offscreenMs.ToString("F2") +
            " | UnsplitGameServicesMs=" + unsplitGameServicesMs.ToString("F2") +
            " | OtherFrameMs=" + otherFrameMs.ToString("F2") +
            " | GalaxyNpcCombatMs=" + galaxyNpcCombatMs.ToString("F2") +
            " | GalaxyNpcCombatCurrentSystemMs=" + galaxyNpcCombatCurrentSystemMs.ToString("F2") +
            " | GalaxyNpcCombatOffscreenMs=" + galaxyNpcCombatOffscreenMs.ToString("F2") +
            " | OrbitalMotionMs=" + orbitalMotionMs.ToString("F2") +
            " | GalaxyPopulationMs=" + galaxyPopulationMs.ToString("F2") +
            " | GalaxyNpcBehaviorMs=" + galaxyNpcBehaviorMs.ToString("F2") +
            " | GalaxyNpcBehaviorCurrentSystemMs=" + galaxyNpcBehaviorCurrentSystemMs.ToString("F2") +
            " | GalaxyNpcBehaviorOffscreenMs=" + galaxyNpcBehaviorOffscreenMs.ToString("F2") +
            " | GalaxyNpcMovementMs=" + galaxyNpcMovementMs.ToString("F2") +
            " | GalaxyNpcMovementCurrentSystemMs=" + galaxyNpcMovementCurrentSystemMs.ToString("F2") +
            " | GalaxyNpcMovementOffscreenMs=" + galaxyNpcMovementOffscreenMs.ToString("F2") +
            " | SystemEnemyMovementMs=" + systemEnemyMovementMs.ToString("F2") +
            " | SystemTravelMs=" + systemTravelMs.ToString("F2"));
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
        return;

        // bool previousDebugEnabled = _debugEnabled;
        // bool previousDebugStop = _debugStop;

        // _debugEnabled = true;
        // _debugStop = false;

        // LogCustom(message);

        // _debugEnabled = previousDebugEnabled;
        // _debugStop = previousDebugStop;
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private bool IsGameTimeLoadAnalyticsEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsPerformanceLogEnabled(
                   DebugLogPerformanceArea.GameTimeLoadAnalytics);
    }

    private void LogGameTimeLoadAnalyticsIfNeeded(
        int currentTick,
        float deltaTime,
        double totalMs,
        double galaxyNpcCombatMs,
        double orbitalMotionMs,
        double galaxyPopulationMs,
        double galaxyNpcBehaviorMs,
        double galaxyNpcMovementMs,
        double systemEnemyMovementMs,
        double systemTravelMs)
    {
        if (!IsGameTimeLoadAnalyticsEnabled())
            return;

        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance.DebugLogConfig;

        if (debugLogConfig == null)
            return;

        double frameMs =
            deltaTime > 0f
                ? deltaTime * 1000.0
                : 0.0;

        double approxFps =
            deltaTime > 0f
                ? 1.0 / deltaTime
                : 0.0;

        float fpsWarningThreshold =
            debugLogConfig.NpcMovementLoadAnalyticsFpsWarningThreshold;

        float fpsCriticalThreshold =
            debugLogConfig.NpcMovementLoadAnalyticsFpsCriticalThreshold;

        float tickWarningMs =
            debugLogConfig.NpcMovementLoadAnalyticsTickWarningMs;

        int regularLogIntervalTicks =
            debugLogConfig.NpcMovementLoadAnalyticsRegularLogIntervalTicks;

        bool fpsNear30 =
            approxFps <= fpsWarningThreshold;

        bool fpsBelow30 =
            approxFps <= fpsCriticalThreshold;

        bool tickExpensive =
            totalMs >= tickWarningMs;

        bool regularSample =
            regularLogIntervalTicks > 0 &&
            currentTick % regularLogIntervalTicks == 0;

        if (!fpsNear30 && !tickExpensive && !regularSample)
            return;

        string reason =
            fpsBelow30
                ? "FPS_BELOW_30"
                : fpsNear30
                    ? "FPS_NEAR_30"
                    : tickExpensive
                        ? "GAME_TIME_TICK_EXPENSIVE"
                        : "REGULAR_SAMPLE";

        string maxServiceName =
            "GalaxyNpcCombat";

        double maxServiceMs =
            galaxyNpcCombatMs;

        UpdateMaxService(
            "OrbitalMotion",
            orbitalMotionMs,
            ref maxServiceName,
            ref maxServiceMs);

        UpdateMaxService(
            "GalaxyPopulation",
            galaxyPopulationMs,
            ref maxServiceName,
            ref maxServiceMs);

        UpdateMaxService(
            "GalaxyNpcBehavior",
            galaxyNpcBehaviorMs,
            ref maxServiceName,
            ref maxServiceMs);

        UpdateMaxService(
            "GalaxyNpcMovement",
            galaxyNpcMovementMs,
            ref maxServiceName,
            ref maxServiceMs);

        UpdateMaxService(
            "SystemEnemyMovement",
            systemEnemyMovementMs,
            ref maxServiceName,
            ref maxServiceMs);

        UpdateMaxService(
            "SystemTravel",
            systemTravelMs,
            ref maxServiceName,
            ref maxServiceMs);

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[GAME_TIME_LOAD_ANALYTICS]" +
            " Reason=" + reason +
            " | Tick=" + currentTick +
            " | ApproxFps=" + approxFps.ToString("F1") +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | TickAllServicesMs=" + totalMs.ToString("F2") +
            " | MaxService=" + maxServiceName +
            " | MaxServiceMs=" + maxServiceMs.ToString("F2") +
            " | GalaxyNpcCombatMs=" + galaxyNpcCombatMs.ToString("F2") +
            " | OrbitalMotionMs=" + orbitalMotionMs.ToString("F2") +
            " | GalaxyPopulationMs=" + galaxyPopulationMs.ToString("F2") +
            " | GalaxyNpcBehaviorMs=" + galaxyNpcBehaviorMs.ToString("F2") +
            " | GalaxyNpcMovementMs=" + galaxyNpcMovementMs.ToString("F2") +
            " | SystemEnemyMovementMs=" + systemEnemyMovementMs.ToString("F2") +
            " | SystemTravelMs=" + systemTravelMs.ToString("F2"));
    }

    private static void UpdateMaxService(
    string serviceName,
    double serviceMs,
    ref string maxServiceName,
    ref double maxServiceMs)
    {
        if (serviceMs <= maxServiceMs)
            return;

        maxServiceName = serviceName;
        maxServiceMs = serviceMs;
    }

    private void TickFrameFpsAnalytics(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.DebugLogConfig == null)
        {
            return;
        }

        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance.DebugLogConfig;

        double frameMs = deltaTime * 1000.0;
        double fps = 1.0 / deltaTime;

        LogSimpleFrameTimeIfNeeded(
            frameMs,
            fps,
            debugLogConfig);

        if (!debugLogConfig.GameTimeFullFpsAnalyticsLogs)
            return;

        UpdateFrameFpsSample(
            frameMs,
            fps,
            debugLogConfig);

        LogFrameSpikeIfNeeded(
            frameMs,
            fps,
            debugLogConfig);

        _previousFrameMs = frameMs;
    }

    private void LogSimpleFrameTimeIfNeeded(
        double frameMs,
        double fps,
        DebugLogConfig debugLogConfig)
    {
        if (debugLogConfig == null ||
            !debugLogConfig.GameTimeSimpleFrameLogs)
        {
            return;
        }

        int interval =
            debugLogConfig.GameTimeSimpleFrameLogIntervalFrames;

        _simpleFrameTimeLogFrameCounter++;

        if (interval > 1 &&
            _simpleFrameTimeLogFrameCounter % interval != 0)
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[FRAME_TIME]" +
            " Frame=" + _simpleFrameTimeLogFrameCounter +
            " | Tick=" + State.CurrentQuantTick +
            " | Paused=" + State.IsPaused +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | Fps=" + fps.ToString("F1"));
    }

    private void UpdateFrameFpsSample(
    double frameMs,
    double fps,
    DebugLogConfig debugLogConfig)
    {
        _fpsSampleFrameCount++;
        _fpsSampleElapsedMs += frameMs;

        if (frameMs < _fpsSampleMinFrameMs)
            _fpsSampleMinFrameMs = frameMs;

        if (frameMs > _fpsSampleMaxFrameMs)
            _fpsSampleMaxFrameMs = frameMs;

        if (fps < _fpsSampleMinFps)
            _fpsSampleMinFps = fps;

        if (fps > _fpsSampleMaxFps)
            _fpsSampleMaxFps = fps;

        if (_fpsRollingAverageFrameMs <= 0.0)
        {
            _fpsRollingAverageFrameMs = frameMs;
        }
        else
        {
            _fpsRollingAverageFrameMs =
                _fpsRollingAverageFrameMs * 0.9 +
                frameMs * 0.1;
        }

        int interval =
            debugLogConfig.GameTimeFullFpsSampleIntervalFrames;

        if (_fpsSampleFrameCount < interval)
            return;

        double averageFrameMs =
            _fpsSampleElapsedMs / _fpsSampleFrameCount;

        double averageFps =
            averageFrameMs > 0.0
                ? 1000.0 / averageFrameMs
                : 0.0;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[FULL_FPS_SAMPLE]" +
            " Frames=" + _fpsSampleFrameCount +
            " | Tick=" + State.CurrentQuantTick +
            " | IsPaused=" + State.IsPaused +
            " | AvgFps=" + averageFps.ToString("F1") +
            " | MinFps=" + _fpsSampleMinFps.ToString("F1") +
            " | MaxFps=" + _fpsSampleMaxFps.ToString("F1") +
            " | AvgFrameMs=" + averageFrameMs.ToString("F2") +
            " | MinFrameMs=" + _fpsSampleMinFrameMs.ToString("F2") +
            " | MaxFrameMs=" + _fpsSampleMaxFrameMs.ToString("F2") +
            " | RollingAvgFrameMs=" + _fpsRollingAverageFrameMs.ToString("F2"));

        ResetFrameFpsSample();
    }

    private void LogFrameSpikeIfNeeded(
        double frameMs,
        double fps,
        DebugLogConfig debugLogConfig)
    {
        if (_fpsRollingAverageFrameMs <= 0.0)
            return;

        double relativeMultiplier =
            _fpsRollingAverageFrameMs > 0.0
                ? frameMs / _fpsRollingAverageFrameMs
                : 0.0;

        bool warningSpike =
            frameMs >= debugLogConfig.GameTimeFrameSpikeWarningMs;

        bool criticalSpike =
            frameMs >= debugLogConfig.GameTimeFrameSpikeCriticalMs;

        bool relativeSpike =
            relativeMultiplier >=
            debugLogConfig.GameTimeFrameSpikeRelativeMultiplier;

        int gc0Now =
            System.GC.CollectionCount(0);

        int gc1Now =
            System.GC.CollectionCount(1);

        int gc2Now =
            System.GC.CollectionCount(2);

        int gc0Delta =
            gc0Now - _previousFrameGc0Count;

        int gc1Delta =
            gc1Now - _previousFrameGc1Count;

        int gc2Delta =
            gc2Now - _previousFrameGc2Count;

        _previousFrameGc0Count = gc0Now;
        _previousFrameGc1Count = gc1Now;
        _previousFrameGc2Count = gc2Now;

        if (!warningSpike && !relativeSpike)
            return;

        string severity =
            criticalSpike
                ? "CRITICAL"
                : "WARNING";

        double previousAccountedMs =
            _lastGameTimeTickMs;

        double previousUnaccountedMs =
            Mathf.Max(
                0f,
                (float)(frameMs - previousAccountedMs));

        long managedMemory =
            System.GC.GetTotalMemory(false);

        long monoUsed =
            UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

        long totalAllocated =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        long totalReserved =
            UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[FRAME_SPIKE]" +
            " Severity=" + severity +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + State.CurrentQuantTick +
            " | IsPaused=" + State.IsPaused +
            " | ApproxFps=" + fps.ToString("F1") +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | PreviousFrameMs=" + _previousFrameMs.ToString("F2") +
            " | RollingAvgFrameMs=" + _fpsRollingAverageFrameMs.ToString("F2") +
            " | RelativeToRollingAvg=" + relativeMultiplier.ToString("F2") +
            " | WarningMs=" + debugLogConfig.GameTimeFrameSpikeWarningMs.ToString("F2") +
            " | CriticalMs=" + debugLogConfig.GameTimeFrameSpikeCriticalMs.ToString("F2") +
            " | PreviousGameTimeUnityFrame=" + _lastGameTimeUnityFrame +
            " | PreviousGameTimeTick=" + _lastGameTimeTick +
            " | PreviousGameTimeSkippedByPause=" + _lastGameTimeSkippedByPause +
            " | PreviousGameTimeTickMs=" + _lastGameTimeTickMs.ToString("F2") +
            " | PreviousSaveServicesMs=" + _lastSaveServicesMs.ToString("F2") +
            " | PreviousTickAllServicesMs=" + _lastTickAllServicesMs.ToString("F2") +
            " | PreviousServicesSumMs=" + _lastServicesSumMs.ToString("F2") +
            " | PreviousGalaxyNpcCombatMs=" + _lastGalaxyNpcCombatMs.ToString("F2") +
            " | PreviousOrbitalMotionMs=" + _lastOrbitalMotionMs.ToString("F2") +
            " | PreviousGalaxyPopulationMs=" + _lastGalaxyPopulationMs.ToString("F2") +
            " | PreviousGalaxyNpcBehaviorMs=" + _lastGalaxyNpcBehaviorMs.ToString("F2") +
            " | PreviousGalaxyNpcMovementMs=" + _lastGalaxyNpcMovementMs.ToString("F2") +
            " | PreviousSystemEnemyMovementMs=" + _lastSystemEnemyMovementMs.ToString("F2") +
            " | PreviousSystemTravelMs=" + _lastSystemTravelMs.ToString("F2") +
            " | PreviousAccountedByGameTimeMs=" + previousAccountedMs.ToString("F2") +
            " | PreviousUnaccountedFrameMs=" + previousUnaccountedMs.ToString("F2") +
            " | Gc0Now=" + gc0Now +
            " | Gc0Delta=" + gc0Delta +
            " | Gc1Now=" + gc1Now +
            " | Gc1Delta=" + gc1Delta +
            " | Gc2Now=" + gc2Now +
            " | Gc2Delta=" + gc2Delta +
            " | ManagedMemoryMb=" + BytesToMegabytes(managedMemory).ToString("F2") +
            " | MonoUsedMb=" + BytesToMegabytes(monoUsed).ToString("F2") +
            " | TotalAllocatedMb=" + BytesToMegabytes(totalAllocated).ToString("F2") +
            " | TotalReservedMb=" + BytesToMegabytes(totalReserved).ToString("F2"));
    }

    private static double BytesToMegabytes(long bytes)
    {
        return bytes / (1024.0 * 1024.0);
    }

    private void ResetFrameFpsSample()
    {
        _fpsSampleFrameCount = 0;
        _fpsSampleElapsedMs = 0.0;
        _fpsSampleMinFrameMs = double.MaxValue;
        _fpsSampleMaxFrameMs = 0.0;
        _fpsSampleMinFps = double.MaxValue;
        _fpsSampleMaxFps = 0.0;
    }
}