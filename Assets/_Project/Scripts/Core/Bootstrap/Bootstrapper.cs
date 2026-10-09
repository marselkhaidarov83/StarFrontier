using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Profiling;

public class Bootstrapper : CustomMonoBehaviour
{
    [Header("Game")]
    [Range(30, 120)]
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private GameConfig gameConfig;
    [SerializeField] private DebugConfig debugConfig;
    [SerializeField] private SaveConfig saveConfig;
    [SerializeField] private NewGameConfig newGameConfig;
    [SerializeField] private DebugLogConfig debugLogConfig;

    [Header("Sprint 3 System Gameplay")]
    [SerializeField] private PlayerControlConfig playerControlConfig;
    [SerializeField] private ShipMovementConfig shipMovementConfig;
    [SerializeField] private SystemCameraConfig systemCameraConfig;
    [SerializeField] private TargetingConfig targetingConfig;
    [SerializeField] private InteractionConfig interactionConfig;
    [SerializeField] private SystemHudConfig systemHudConfig;
    [SerializeField] private SystemVisualConfig systemVisualConfig;
    [SerializeField] private CombatFxVisualConfig combatFxVisualConfig;
    [SerializeField] private CombatDamagePopupVisualConfig2A combatDamagePopupVisualConfig;
    [SerializeField] private NpcBehaviourTransitionMatrixConfig npcBehaviourTransitionMatrixConfig;
    [SerializeField] private OffscreenNpcSimulationScheduleConfig offscreenNpcSimulationScheduleConfig;
    [SerializeField] private CurrentSystemNpcSimulationConfig currentSystemNpcSimulationConfig;

    [Header("Data")]
    [SerializeField] private GalaxyConfig galaxyConfig;
    [SerializeField] private List<EnemyConfig> enemies;
    [SerializeField] private List<AllyConfig> allies;
    [SerializeField] private List<AllySpawnRuleConfig> allySpawnRuleConfigs;
    [SerializeField] private List<EnemyGroupSpawnRuleConfig> enemyGroupSpawnRules;
    [SerializeField] private List<SystemPopulationRule> systemPopulationRules;
    [SerializeField] private List<PirateConfig> pirates;
    [SerializeField] private List<PirateGroupSpawnRuleConfig> pirateGroupSpawnRules;
    [SerializeField] private List<ModuleConfig> modules;
    [SerializeField] private List<WeaponConfig> weapons;
    [SerializeField] private List<ItemConfig> items;

    [SerializeField] public int MaxAcceptedMissionCount = 3;

    [Header("Debug")]
    [SerializeField] public bool SectorAllOpened = false;
    [SerializeField] public bool RouteAllUnlocked = false;
    [SerializeField] private bool _globalDebugEnabled;

    [Header("Debug / NPC Population")]
    [Tooltip("Спавнить NPC только в текущей системе. Существующие NPC в других системах не удаляются.")]
    [SerializeField] private bool debugSpawnOnlyInCurrentSystem;
    [SerializeField] private bool stopAutomaticAllySpawns;
    [SerializeField] private bool stopAutomaticEnemySpawns;
    [SerializeField] private bool overrideAutomaticAllySpawnInterval;
    [SerializeField, Min(0.1f)] private float debugAutomaticAllySpawnIntervalSeconds = 5f;
    [SerializeField] private bool overrideNpcGalaxyLevel;
    [SerializeField, Range(1, 10)] private int debugNpcGalaxyLevel = 1;


    public static Bootstrapper Instance;
    public IServiceRegistry ServiceRegistry;
    private bool _debugLogRuntimeSettingsApplied;

    private IGameStateMachine _gameStateMachine;
    private ISaveService _saveService;
    private IGameTimeService _gameTimeService;
    private IPlayerControlService _playerControlService;
    private IShipMovementService _shipMovementService;
    private IInteractionService2A _interactionService;
    private ITickService _tickService;

    private ProfilerRecorder _mainThreadTimeRecorder;
    private ProfilerRecorder _renderThreadTimeRecorder;
    private ProfilerRecorder _gcAllocatedInFrameRecorder;
    private ProfilerRecorder _gcUsedMemoryRecorder;
    private ProfilerRecorder _totalUsedMemoryRecorder;
    private ProfilerRecorder _systemUsedMemoryRecorder;
    private ProfilerRecorder _playerLoopRecorder;
    private ProfilerRecorder _behaviourUpdateRecorder;
    private ProfilerRecorder _scriptRunBehaviourUpdateRecorder;
    private ProfilerRecorder _lateBehaviourUpdateRecorder;
    private ProfilerRecorder _scriptRunBehaviourLateUpdateRecorder;
    private ProfilerRecorder _cameraRenderRecorder;
    private ProfilerRecorder _canvasBuildBatchRecorder;
    private ProfilerRecorder _canvasSendWillRenderCanvasesRecorder;
    private ProfilerRecorder _gcCollectRecorder;

    private ProfilerRecorder _fixedBehaviourUpdateRecorder;
    private ProfilerRecorder _scriptRunDelayedStartupFrameRecorder;
    private ProfilerRecorder _scriptRunDelayedDynamicFrameRateRecorder;
    private ProfilerRecorder _scriptRunDelayedTasksRecorder;
    private ProfilerRecorder _unitySynchronizationContextExecuteTasksRecorder;
    private ProfilerRecorder _updateRectTransformRecorder;
    private ProfilerRecorder _updateCanvasRectTransformRecorder;
    private ProfilerRecorder _updateAllRenderersRecorder;
    private ProfilerRecorder _updateAllSkinnedMeshesRecorder;
    private ProfilerRecorder _finishFrameRenderingRecorder;
    private ProfilerRecorder _presentAfterDrawRecorder;

    private bool _externalFrameProfilersStarted;
    private double _lastBootstrapperUpdateMs;

    public bool GlobalDebugEnabled => _globalDebugEnabled;
    public bool StopAutomaticAllySpawns => stopAutomaticAllySpawns;
    public bool StopAutomaticEnemySpawns => stopAutomaticEnemySpawns;
    public bool DebugSpawnOnlyInCurrentSystem => debugSpawnOnlyInCurrentSystem;
    public bool OverrideAutomaticAllySpawnInterval => overrideAutomaticAllySpawnInterval;
    public OffscreenNpcSimulationScheduleConfig OffscreenNpcSimulationScheduleConfig =>
    offscreenNpcSimulationScheduleConfig;
    public CurrentSystemNpcSimulationConfig CurrentSystemNpcSimulationConfig =>
    currentSystemNpcSimulationConfig;

    public float DebugAutomaticAllySpawnIntervalSeconds =>
        Mathf.Max(0.1f, debugAutomaticAllySpawnIntervalSeconds);

    public bool OverrideNpcGalaxyLevel => overrideNpcGalaxyLevel;

    public int DebugNpcGalaxyLevel =>
        Mathf.Clamp(debugNpcGalaxyLevel, 1, 10);

    public DebugLogConfig DebugLogConfig => debugLogConfig;

    public bool IsDebugLogEnabled(DebugLogChannel channel)
    {
        EnsureDebugLogRuntimeSettingsApplied();

        return debugLogConfig != null &&
               debugLogConfig.IsEnabled(channel);
    }

    public void LogDebug(DebugLogChannel channel, string message)
    {
        EnsureDebugLogRuntimeSettingsApplied();

        if (!IsDebugLogEnabled(channel) ||
            string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log("[DebugLog][" + channel + "] " + message);
    }

    public Action<string> CreateDebugLogAction(DebugLogChannel channel)
    {
        if (!IsDebugLogEnabled(channel))
            return null;

        return message => LogDebug(channel, message);
    }

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LogDebug(
            DebugLogChannel.Bootstrap,
            "Bootstrapper awaked");

        InitializeServiceRegistry();
        InitializeStateMachine();
        InitializeServices();
        StartGameFlow();
    }

    private void InitializeServiceRegistry()
    {
        ServiceRegistry = new ServiceRegistry();

        LogDebug(
            DebugLogChannel.Bootstrap,
            "ServiceRegistry created");
    }

    private void InitializeStateMachine()
    {
        _gameStateMachine =
            RegisterService<IGameStateMachine, GameStateMachine>();
    }

    private void InitializeServices()
    {
        RegisterService<SimpleEventBus, SimpleEventBus>();
        RegisterService<IGameSessionService, GameSessionService>();

        RegisterService<IConfigService>(
            new ConfigService(
                gameConfig,
                debugConfig,
                saveConfig,
                galaxyConfig,
                newGameConfig,
                playerControlConfig,
                shipMovementConfig,
                systemCameraConfig,
                targetingConfig,
                interactionConfig,
                systemHudConfig,
                systemVisualConfig,
                items,
                enemies,
                allies,
                allySpawnRuleConfigs,
                pirates,
                pirateGroupSpawnRules,
                modules,
                weapons,
                combatFxVisualConfig,
                combatDamagePopupVisualConfig,
                npcBehaviourTransitionMatrixConfig));

        RegisterService<IShipStatsService, ShipStatsService>();
        RegisterService<ISystemGameplayStateService, SystemGameplayStateService>();
        RegisterService<ITargetService2A, TargetService2A>();
        RegisterService<ISystemBoundsService, SystemBoundsService2A>();

        _playerControlService =
            RegisterService<IPlayerControlService, PlayerControlService2A>();

        _shipMovementService =
            RegisterService<IShipMovementService, ShipMovementService2A>();

        RegisterService<IPlayerShipSaveSyncService, PlayerShipSaveSyncService2A>();
        RegisterService<ISystemContextService, SystemContextService>();
        RegisterService<ISceneService, SceneService>();
        RegisterService<IInventoryService, InventoryService>();
        RegisterService<ISystemEncounterService, SystemEncounterService>();
        RegisterService<ISystemEnemyService, SystemEnemyService>();
        RegisterService<ISystemAllyService, SystemAllyService>();
        RegisterService<ISystemEncounterSaveService, SystemEncounterSaveService>();
        RegisterService<IEconomyService, EconomyService>();
        RegisterService<IMarketTransactionService, MarketTransactionService>();
        RegisterService<IRefuelService, RefuelService>();
        RegisterService<IGalaxyDiscoveryService, GalaxyDiscoveryService>();
        RegisterService<IRouteService, RouteService>();
        RegisterService<IOrbitalMotionService, OrbitalMotionService>();
        RegisterService<ISystemShipRouteService2A, SystemShipRouteService2A>();
        RegisterService<IHangarService, HangarService>();
        RegisterService<ISystemTravelService, SystemTravelService>();
        RegisterService<ITravelService, TravelService2A>();

        _interactionService =
            RegisterService<IInteractionService2A, InteractionService2A>();

        RegisterService<IRepairService, RepairService>();
        RegisterService<IRewardService, RewardService>();
        RegisterService<IPlanetMissionOfferStateService, PlanetMissionOfferStateService>();
        RegisterService<IPlanetMissionOfferGenerator, PlanetMissionOfferGenerator>();
        RegisterService<IGovernmentRewardPayoutService, DebugGovernmentRewardPayoutService>();
        RegisterService<IGovernmentRewardService, GovernmentRewardService>();
        RegisterService<IDamageService2A, DamageService2A>();
        RegisterService<ISystemNpcRuntimeService, SystemNpcRuntimeService>();
        RegisterService<ISystemSecurityService, SystemSecurityService>();
        RegisterService<IEnemyFactionService, EnemyFactionService>();
        RegisterService<IEnemySpawnService, EnemySpawnService>();
        RegisterService<IInvasionService, InvasionService>();
        RegisterService<ISystemNpcPopulationService, SystemNpcPopulationService>();
        RegisterService<IGalaxyPopulationService, GalaxyPopulationService>();
        RegisterService<IGalaxyNpcSimulationScheduleService, GalaxyNpcSimulationScheduleService>();
        RegisterService<ISystemNpcBehaviorService, SystemNpcBehaviorService>();
        RegisterService<IGalaxyNpcBehaviorService, GalaxyNpcBehaviorService>();
        RegisterService<ISystemNpcSimulationSaveService, SystemNpcSimulationSaveService>();
        RegisterService<ISystemNpcOfflineRelocationService, SystemNpcOfflineRelocationService>();

        _saveService =
            RegisterService<ISaveService, SaveService2A>();

        RegisterService<IPlayerCombatTargetService, PlayerCombatTargetService>();
        RegisterService<ISystemEnemyMovementService, SystemEnemyMovementService>();
        RegisterService<ISystemNpcMovementRouteService, SystemNpcMovementRouteService>();
        RegisterService<ISystemShipRoutePlanner2A, SystemShipRoutePlanner2A>();
        RegisterService<ISystemNpcOffscreenSimulationService, SystemNpcOffscreenSimulationService>();
        RegisterService<ISystemNpcMovementService, SystemNpcMovementService>();
        RegisterService<IGalaxyNpcMovementService, GalaxyNpcMovementService>();
        RegisterService<IGalaxyNpcWarmupService, GalaxyNpcWarmupService>();
        RegisterService<ISystemNpcCombatService, SystemNpcCombatService>();
        RegisterService<IGalaxyNpcCombatService, GalaxyNpcCombatService>();
        RegisterService<IPlayerAttackService, PlayerAttackService>();
        RegisterService<IContinueGameService, ContinueGameService>();
        RegisterService<INewGameService, NewGameService>();
        RegisterService<IMissionService, MissionService>();
        RegisterService<IMissionTracker, MissionTracker>();
        RegisterService<IPlanetGovernmentMissionService, PlanetGovernmentMissionService>();

        _tickService =
            RegisterService<ITickService, TickService>();

        _gameTimeService =
            RegisterService<IGameTimeService, GameTimeService>();

        _tickService.Register(_gameTimeService, TickOrder.GameTime);
        _tickService.Register(_playerControlService, TickOrder.PlayerControl);
        _tickService.Register(_shipMovementService, TickOrder.ShipMovement);
        _tickService.Register(_interactionService, TickOrder.Interaction);

        RegisterService<IGameTimePauseScopeService, GameTimePauseScopeService>();
    }

    private TInterface RegisterService<TInterface, TImplementation>()
        where TImplementation : TInterface, new()
    {
        TImplementation service =
            new TImplementation();

        return RegisterService<TInterface>(service);
    }

    private TInterface RegisterService<TInterface>(
        TInterface service)
    {
        ServiceRegistry.Register<TInterface>(service);

        LogDebug(
            DebugLogChannel.Bootstrap,
            $"{service.GetType().Name} registered as {typeof(TInterface).Name}");

        return service;
    }

    private void StartGameFlow()
    {
        _gameStateMachine.Enter(new BootstrapState());
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double profilerStartMs = 0.0;
        double tickMs = 0.0;

        bool hasTickService = false;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            EnsureExternalFrameProfilersStarted();

            profilerStartMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            hasTickService =
                _tickService != null;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _tickService?.Tick(Time.deltaTime);

            tickMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            _lastBootstrapperUpdateMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            if (_lastBootstrapperUpdateMs >= 1.0 &&
                IsPerformanceLogEnabled(DebugLogPerformanceArea.GameTimeLoadAnalytics))
            {
                LogPerformance(
                    DebugLogPerformanceArea.GameTimeLoadAnalytics,
                    "[VISUAL_UPDATE_SPIKE]" +
                    " Marker=Bootstrapper.Update" +
                    " | UnityFrame=" + Time.frameCount +
                    " | Ms=" + _lastBootstrapperUpdateMs.ToString("F2") +
                    " | ThresholdMs=1.00" +
                    " | HasTickService=" + hasTickService +
                    " | ProfilerStartMs=" + profilerStartMs.ToString("F3") +
                    " | TickMs=" + tickMs.ToString("F3"));
            }
        }
    }

    private void LateUpdate()
    {
        LogExternalFrameDiagnosticsIfNeeded();
    }

    private void OnDestroy()
    {
        StopExternalFrameProfilers();
    }

    private void EnsureExternalFrameProfilersStarted()
    {
        if (_externalFrameProfilersStarted)
            return;

        _externalFrameProfilersStarted = true;

        _mainThreadTimeRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Internal,
                "Main Thread");

        _renderThreadTimeRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Internal,
                "Render Thread");

        _playerLoopRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Internal,
                "PlayerLoop");

        _behaviourUpdateRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "BehaviourUpdate");

        _scriptRunBehaviourUpdateRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "Update.ScriptRunBehaviourUpdate");

        _lateBehaviourUpdateRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "LateBehaviourUpdate");

        _scriptRunBehaviourLateUpdateRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "PreLateUpdate.ScriptRunBehaviourLateUpdate");

        _fixedBehaviourUpdateRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "FixedUpdate.ScriptRunBehaviourFixedUpdate");

        _scriptRunDelayedStartupFrameRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "EarlyUpdate.ScriptRunDelayedStartupFrame");

        _scriptRunDelayedDynamicFrameRateRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "Update.ScriptRunDelayedDynamicFrameRate");

        _scriptRunDelayedTasksRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "Update.ScriptRunDelayedTasks");

        _unitySynchronizationContextExecuteTasksRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Scripts,
                "UnitySynchronizationContext.ExecuteTasks");

        _updateRectTransformRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "UpdateRectTransform");

        _updateCanvasRectTransformRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "UpdateCanvasRectTransform");

        _cameraRenderRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "Camera.Render");

        _canvasBuildBatchRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "Canvas.BuildBatch");

        _canvasSendWillRenderCanvasesRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "Canvas.SendWillRenderCanvases");

        _updateAllRenderersRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "UpdateAllRenderers");

        _updateAllSkinnedMeshesRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "UpdateAllSkinnedMeshes");

        _finishFrameRenderingRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "FinishFrameRendering");

        _presentAfterDrawRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Render,
                "PresentAfterDraw");

        _gcCollectRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Memory,
                "GC.Collect");

        _gcAllocatedInFrameRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Memory,
                "GC Allocated In Frame");

        _gcUsedMemoryRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Memory,
                "GC Used Memory");

        _totalUsedMemoryRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Memory,
                "Total Used Memory");

        _systemUsedMemoryRecorder =
            StartProfilerRecorder(
                ProfilerCategory.Memory,
                "System Used Memory");
    }

    private ProfilerRecorder StartProfilerRecorder(
        ProfilerCategory category,
        string markerName)
    {
        try
        {
            return ProfilerRecorder.StartNew(
                category,
                markerName,
                1);
        }
        catch (Exception e)
        {
            LogPerformance(
                DebugLogPerformanceArea.GameTimeLoadAnalytics,
                "[FRAME_EXTERNAL_RECORDER_FAILED]" +
                " Marker=" + markerName +
                " | Error=" + e.Message);

            return default;
        }
    }

    private void StopExternalFrameProfilers()
    {
        StopProfilerRecorder(ref _mainThreadTimeRecorder);
        StopProfilerRecorder(ref _renderThreadTimeRecorder);
        StopProfilerRecorder(ref _playerLoopRecorder);
        StopProfilerRecorder(ref _behaviourUpdateRecorder);
        StopProfilerRecorder(ref _scriptRunBehaviourUpdateRecorder);
        StopProfilerRecorder(ref _lateBehaviourUpdateRecorder);
        StopProfilerRecorder(ref _scriptRunBehaviourLateUpdateRecorder);
        StopProfilerRecorder(ref _fixedBehaviourUpdateRecorder);
        StopProfilerRecorder(ref _scriptRunDelayedStartupFrameRecorder);
        StopProfilerRecorder(ref _scriptRunDelayedDynamicFrameRateRecorder);
        StopProfilerRecorder(ref _scriptRunDelayedTasksRecorder);
        StopProfilerRecorder(ref _unitySynchronizationContextExecuteTasksRecorder);
        StopProfilerRecorder(ref _updateRectTransformRecorder);
        StopProfilerRecorder(ref _updateCanvasRectTransformRecorder);
        StopProfilerRecorder(ref _cameraRenderRecorder);
        StopProfilerRecorder(ref _canvasBuildBatchRecorder);
        StopProfilerRecorder(ref _canvasSendWillRenderCanvasesRecorder);
        StopProfilerRecorder(ref _updateAllRenderersRecorder);
        StopProfilerRecorder(ref _updateAllSkinnedMeshesRecorder);
        StopProfilerRecorder(ref _finishFrameRenderingRecorder);
        StopProfilerRecorder(ref _presentAfterDrawRecorder);
        StopProfilerRecorder(ref _gcCollectRecorder);
        StopProfilerRecorder(ref _gcAllocatedInFrameRecorder);
        StopProfilerRecorder(ref _gcUsedMemoryRecorder);
        StopProfilerRecorder(ref _totalUsedMemoryRecorder);
        StopProfilerRecorder(ref _systemUsedMemoryRecorder);

        _externalFrameProfilersStarted = false;
    }

    private void StopProfilerRecorder(
        ref ProfilerRecorder recorder)
    {
        if (!recorder.Valid)
            return;

        recorder.Dispose();
        recorder = default;
    }

    private void LogExternalFrameDiagnosticsIfNeeded()
    {
        if (!IsPerformanceLogEnabled(DebugLogPerformanceArea.GameTimeLoadAnalytics))
            return;

        if (debugLogConfig == null)
            return;

        double frameMs =
            Time.unscaledDeltaTime > 0f
                ? Time.unscaledDeltaTime * 1000.0
                : 0.0;

        if (frameMs < debugLogConfig.GameTimeFrameSpikeWarningMs)
            return;

        int currentTick =
            _gameTimeService != null
                ? _gameTimeService.CurrentQuantTick
                : -1;

        int gc0 =
            GC.CollectionCount(0);

        int gc1 =
            GC.CollectionCount(1);

        int gc2 =
            GC.CollectionCount(2);

        long managedMemory =
            GC.GetTotalMemory(false);

        double mainThreadMs =
            ProfilerRecorderNanosecondsToMs(_mainThreadTimeRecorder);

        double playerLoopMs =
            ProfilerRecorderNanosecondsToMs(_playerLoopRecorder);

        double behaviourUpdateMs =
            ProfilerRecorderNanosecondsToMs(_behaviourUpdateRecorder);

        double scriptRunBehaviourUpdateMs =
            ProfilerRecorderNanosecondsToMs(_scriptRunBehaviourUpdateRecorder);

        double lateBehaviourUpdateMs =
            ProfilerRecorderNanosecondsToMs(_lateBehaviourUpdateRecorder);

        double scriptRunBehaviourLateUpdateMs =
            ProfilerRecorderNanosecondsToMs(_scriptRunBehaviourLateUpdateRecorder);

        double gcCollectMs =
            ProfilerRecorderNanosecondsToMs(_gcCollectRecorder);

        double cameraRenderMs =
            ProfilerRecorderNanosecondsToMs(_cameraRenderRecorder);

        double canvasBuildBatchMs =
            ProfilerRecorderNanosecondsToMs(_canvasBuildBatchRecorder);

        double canvasSendWillRenderCanvasesMs =
            ProfilerRecorderNanosecondsToMs(_canvasSendWillRenderCanvasesRecorder);

        double knownPlayerLoopMs =
            behaviourUpdateMs +
            lateBehaviourUpdateMs +
            cameraRenderMs +
            canvasBuildBatchMs +
            canvasSendWillRenderCanvasesMs +
            gcCollectMs;

        double mainThreadOutsidePlayerLoopMs =
            Mathf.Max(
                0f,
                (float)(mainThreadMs - playerLoopMs));

        double playerLoopOutsideKnownMs =
            Mathf.Max(
                0f,
                (float)(playerLoopMs - knownPlayerLoopMs));

        LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[FRAME_EXTERNAL]" +
            " UnityFrame=" + Time.frameCount +
            " | Tick=" + currentTick +
            " | IsPaused=" + (_gameTimeService != null && _gameTimeService.IsPaused) +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | BootstrapperUpdateMs=" + _lastBootstrapperUpdateMs.ToString("F2") +
            " | MainThreadMs=" + mainThreadMs.ToString("F2") +
            " | RenderThreadMs=" + ProfilerRecorderNanosecondsToMs(_renderThreadTimeRecorder).ToString("F2") +
            " | PlayerLoopMs=" + playerLoopMs.ToString("F2") +
            " | MainThreadOutsidePlayerLoopMs=" + mainThreadOutsidePlayerLoopMs.ToString("F2") +
            " | PlayerLoopOutsideKnownMs=" + playerLoopOutsideKnownMs.ToString("F2") +
            " | BehaviourUpdateMs=" + behaviourUpdateMs.ToString("F2") +
            " | ScriptRunBehaviourUpdateMs=" + scriptRunBehaviourUpdateMs.ToString("F2") +
            " | LateBehaviourUpdateMs=" + lateBehaviourUpdateMs.ToString("F2") +
            " | ScriptRunBehaviourLateUpdateMs=" + scriptRunBehaviourLateUpdateMs.ToString("F2") +
            " | FixedBehaviourUpdateMs=" + ProfilerRecorderNanosecondsToMs(_fixedBehaviourUpdateRecorder).ToString("F2") +
            " | ScriptRunDelayedStartupFrameMs=" + ProfilerRecorderNanosecondsToMs(_scriptRunDelayedStartupFrameRecorder).ToString("F2") +
            " | ScriptRunDelayedDynamicFrameRateMs=" + ProfilerRecorderNanosecondsToMs(_scriptRunDelayedDynamicFrameRateRecorder).ToString("F2") +
            " | ScriptRunDelayedTasksMs=" + ProfilerRecorderNanosecondsToMs(_scriptRunDelayedTasksRecorder).ToString("F2") +
            " | UnitySynchronizationContextExecuteTasksMs=" + ProfilerRecorderNanosecondsToMs(_unitySynchronizationContextExecuteTasksRecorder).ToString("F2") +
            " | UpdateRectTransformMs=" + ProfilerRecorderNanosecondsToMs(_updateRectTransformRecorder).ToString("F2") +
            " | UpdateCanvasRectTransformMs=" + ProfilerRecorderNanosecondsToMs(_updateCanvasRectTransformRecorder).ToString("F2") +
            " | CameraRenderMs=" + cameraRenderMs.ToString("F2") +
            " | CanvasBuildBatchMs=" + canvasBuildBatchMs.ToString("F2") +
            " | CanvasSendWillRenderCanvasesMs=" + canvasSendWillRenderCanvasesMs.ToString("F2") +
            " | UpdateAllRenderersMs=" + ProfilerRecorderNanosecondsToMs(_updateAllRenderersRecorder).ToString("F2") +
            " | UpdateAllSkinnedMeshesMs=" + ProfilerRecorderNanosecondsToMs(_updateAllSkinnedMeshesRecorder).ToString("F2") +
            " | FinishFrameRenderingMs=" + ProfilerRecorderNanosecondsToMs(_finishFrameRenderingRecorder).ToString("F2") +
            " | PresentAfterDrawMs=" + ProfilerRecorderNanosecondsToMs(_presentAfterDrawRecorder).ToString("F2") +
            " | GcCollectMs=" + gcCollectMs.ToString("F2") +
            " | GcAllocatedInFrameMb=" + ProfilerRecorderBytesToMb(_gcAllocatedInFrameRecorder).ToString("F2") +
            " | GcUsedMemoryMb=" + ProfilerRecorderBytesToMb(_gcUsedMemoryRecorder).ToString("F2") +
            " | TotalUsedMemoryMb=" + ProfilerRecorderBytesToMb(_totalUsedMemoryRecorder).ToString("F2") +
            " | SystemUsedMemoryMb=" + ProfilerRecorderBytesToMb(_systemUsedMemoryRecorder).ToString("F2") +
            " | ManagedMemoryMb=" + BytesToMegabytes(managedMemory).ToString("F2") +
            " | Gc0=" + gc0 +
            " | Gc1=" + gc1 +
            " | Gc2=" + gc2);
    }

    private static double ProfilerRecorderNanosecondsToMs(
        ProfilerRecorder recorder)
    {
        if (!recorder.Valid)
            return 0.0;

        return recorder.LastValue / 1000000.0;
    }

    private static double ProfilerRecorderBytesToMb(
        ProfilerRecorder recorder)
    {
        if (!recorder.Valid)
            return 0.0;

        return BytesToMegabytes(recorder.LastValue);
    }

    private static double BytesToMegabytes(long bytes)
    {
        return bytes / (1024.0 * 1024.0);
    }

    private void OnApplicationPause(
    bool pause)
    {
        if (!pause)
            return;

        if (saveConfig != null && saveConfig.AutoSaveOnPause)
            SaveCurrentGame("app_pause");
    }

    private void OnApplicationQuit()
    {
        if (saveConfig != null && saveConfig.AutoSaveOnQuit)
            SaveCurrentGame("app_quit");
    }

    public void SaveCurrentGame(
        string reason = "manual")
    {
        string normalizedReason =
            string.IsNullOrWhiteSpace(reason)
                ? "manual"
                : reason;

        int unityFrame =
            Time.frameCount;

        int tick =
            _gameTimeService != null
                ? _gameTimeService.CurrentQuantTick
                : -1;

        LogSaveLifecycle(
            "[Bootstrapper] SAVE_CURRENT_GAME_START" +
            " | Reason=" + normalizedReason +
            " | UnityFrame=" + unityFrame +
            " | Tick=" + tick);

        if (_saveService == null)
        {
            AppLog.Warning(
                "[Bootstrapper] SaveCurrentGame skipped: ISaveService is missing.");

            LogSaveLifecycle(
                "[Bootstrapper] SAVE_CURRENT_GAME_SKIPPED" +
                " | Reason=" + normalizedReason +
                " | UnityFrame=" + unityFrame +
                " | Tick=" + tick +
                " | Cause=SaveServiceMissing");

            return;
        }

        _saveService.Save(normalizedReason);

        LogSaveLifecycle(
            "[Bootstrapper] SAVE_CURRENT_GAME_COMPLETE" +
            " | Reason=" + normalizedReason +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" +
            (_gameTimeService != null ? _gameTimeService.CurrentQuantTick : -1) +
            " | HasSave=" + _saveService.HasSave());
    }

    private void LogSaveLifecycle(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (IsPerformanceLogEnabled(DebugLogPerformanceArea.Save))
        {
            LogPerformance(
                DebugLogPerformanceArea.Save,
                message);
        }
    }

    private void EnsureDebugLogRuntimeSettingsApplied()
    {
        if (_debugLogRuntimeSettingsApplied)
            return;

        _debugLogRuntimeSettingsApplied = true;

        if (debugLogConfig == null)
            return;

        if (debugLogConfig.DisableInfoLogStackTrace)
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    public bool IsPerformanceLogEnabled(DebugLogPerformanceArea area)
    {
        EnsureDebugLogRuntimeSettingsApplied();

        return debugLogConfig != null &&
               debugLogConfig.IsPerformanceEnabled(area);
    }

    public void LogPerformance(
    DebugLogPerformanceArea area,
    string message,
    [CallerMemberName] string callerMemberName = "",
    [CallerFilePath] string callerFilePath = "",
    [CallerLineNumber] int callerLineNumber = 0)
    {
        EnsureDebugLogRuntimeSettingsApplied();

        if (!IsPerformanceLogEnabled(area) ||
            string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string callerFileName = callerFilePath;

        if (!string.IsNullOrWhiteSpace(callerFileName))
        {
            int slashIndex = callerFileName.LastIndexOf('/');
            int backslashIndex = callerFileName.LastIndexOf('\\');
            int separatorIndex = Math.Max(slashIndex, backslashIndex);

            if (separatorIndex >= 0 && separatorIndex + 1 < callerFileName.Length)
                callerFileName = callerFileName.Substring(separatorIndex + 1);
        }

        Debug.Log(
            "[PerfLog][" + area + "] " +
            message +
            " | Source=" + callerFileName +
            ":" + callerLineNumber +
            ":" + callerMemberName);
    }
}
