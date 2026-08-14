using System;
using System.Collections.Generic;
using UnityEngine;

public class Bootstrapper : CustomMonoBehaviour
{
    [Header("Game")]
    [SerializeField] private GameConfig gameConfig;
    [SerializeField] private DebugConfig debugConfig;
    [SerializeField] private SaveConfig saveConfig;
    [SerializeField] private NewGameConfig newGameConfig;

    [Header("Sprint 3 System Gameplay")]
    [SerializeField] private PlayerControlConfig playerControlConfig;
    [SerializeField] private ShipMovementConfig shipMovementConfig;
    [SerializeField] private SystemCameraConfig systemCameraConfig;
    [SerializeField] private TargetingConfig targetingConfig;
    [SerializeField] private InteractionConfig interactionConfig;
    [SerializeField] private SystemHudConfig systemHudConfig;
    [SerializeField] private SystemVisualConfig systemVisualConfig;

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
    [SerializeField] private bool stopAutomaticAllySpawns;
    [SerializeField] private bool stopAutomaticEnemySpawns;
    [SerializeField] private bool overrideAutomaticAllySpawnInterval;
    [SerializeField, Min(0.1f)] private float debugAutomaticAllySpawnIntervalSeconds = 5f;
    [SerializeField] private bool overrideNpcGalaxyLevel;
    [SerializeField, Range(1, 10)] private int debugNpcGalaxyLevel = 1;

    public static Bootstrapper Instance;
    public IServiceRegistry ServiceRegistry;

    private IGameStateMachine _gameStateMachine;
    private ISaveService _saveService;
    private IGameTimeService _gameTimeService;
    private IPlayerControlService _playerControlService;
    private IShipMovementService _shipMovementService;
    private IInteractionService2A _interactionService;
    private ITickService _tickService;

    public bool GlobalDebugEnabled => _globalDebugEnabled;
    public bool StopAutomaticAllySpawns => stopAutomaticAllySpawns;
    public bool StopAutomaticEnemySpawns => stopAutomaticEnemySpawns;
    public bool OverrideAutomaticAllySpawnInterval => overrideAutomaticAllySpawnInterval;

    public float DebugAutomaticAllySpawnIntervalSeconds =>
        Mathf.Max(0.1f, debugAutomaticAllySpawnIntervalSeconds);

    public bool OverrideNpcGalaxyLevel => overrideNpcGalaxyLevel;

    public int DebugNpcGalaxyLevel =>
        Mathf.Clamp(debugNpcGalaxyLevel, 1, 10);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LogCustom("Bootstrapper awaked");

        InitializeServiceRegistry();
        InitializeStateMachine();
        InitializeServices();
        StartGameFlow();
    }

    private void InitializeServiceRegistry()
    {
        ServiceRegistry = new ServiceRegistry();
        LogCustom("ServiceRegistry created");
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
                weapons));

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
        RegisterService<ISystemNpcPopulationService, SystemNpcPopulationService>();
        RegisterService<IGalaxyPopulationService, GalaxyPopulationService>();
        RegisterService<ISystemNpcBehaviorService, SystemNpcBehaviorService>();
        RegisterService<IGalaxyNpcBehaviorService, GalaxyNpcBehaviorService>();
        RegisterService<ISystemNpcSimulationSaveService, SystemNpcSimulationSaveService>();

        _saveService =
            RegisterService<ISaveService, SaveService2A>();

        RegisterService<IPlayerCombatTargetService, PlayerCombatTargetService>();
        RegisterService<ISystemNpcMovementRouteService, SystemNpcMovementRouteService>();
        RegisterService<ISystemNpcMovementService, SystemNpcMovementService>();
        RegisterService<IGalaxyNpcMovementService, GalaxyNpcMovementService>();
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

        LogCustom(
            $"{service.GetType().Name} registered as {typeof(TInterface).Name}");

        return service;
    }

    [ContextMenu("STAR FRONTIER/Kill All NPCs")]
    private void DebugKillAllNpcs()
    {
        if (ServiceRegistry == null)
        {
            LogCustom("[Bootstrapper] ServiceRegistry is not initialized.");
            return;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            LogCustom("[Bootstrapper] ISystemNpcRuntimeService is not registered.");
            return;
        }

        int npcCount =
            npcRuntimeService.Npcs != null
                ? npcRuntimeService.Npcs.Count
                : 0;

        npcRuntimeService.ClearAll();

        if (ServiceRegistry.TryGet<ISystemNpcPopulationService>(
                out ISystemNpcPopulationService populationService))
        {
            populationService.ClearRuntimeState();
        }

        LogCustom(
            "[Bootstrapper] Debug Kill All NPCs completed. Removed NPCs: " +
            npcCount);
    }

    [ContextMenu("STAR FRONTIER/Spawn Enemy Attack Group In Current System")]
    private void DebugSpawnEnemyAttackGroupInCurrentSystem()
    {
        if (ServiceRegistry == null)
        {
            LogCustom("[Bootstrapper] ServiceRegistry is not initialized.");
            return;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcPopulationService>(
                out ISystemNpcPopulationService populationService))
        {
            LogCustom("[Bootstrapper] ISystemNpcPopulationService is not registered.");
            return;
        }

        bool spawned =
            populationService.DebugSpawnEnemyAttackGroupInCurrentSystem();

        LogCustom(
            "[Bootstrapper] Debug Spawn Enemy Attack Group In Current System result: " +
            spawned);
    }

    [ContextMenu("STAR FRONTIER/Spawn Ally Ranger In Current System")]
    private void DebugSpawnAllyRangerInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Ranger);
    }

    [ContextMenu("STAR FRONTIER/Spawn Ally Military In Current System")]
    private void DebugSpawnAllyMilitaryInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Military);
    }

    [ContextMenu("STAR FRONTIER/Spawn Ally Trader In Current System")]
    private void DebugSpawnAllyTraderInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Trader);
    }

    [ContextMenu("STAR FRONTIER/Spawn Ally Science In Current System")]
    private void DebugSpawnAllyScienceInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Science);
    }

    [ContextMenu("STAR FRONTIER/Spawn Ally Medic In Current System")]
    private void DebugSpawnAllyMedicInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Medic);
    }

    private void DebugSpawnAllyInCurrentSystem(
        AllyRole2A role)
    {
        if (ServiceRegistry == null)
        {
            LogCustom("[Bootstrapper] ServiceRegistry is not initialized.");
            return;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcPopulationService>(
                out ISystemNpcPopulationService populationService))
        {
            LogCustom("[Bootstrapper] ISystemNpcPopulationService is not registered.");
            return;
        }

        bool spawned =
            populationService.DebugSpawnAllyInCurrentSystem(role);

        LogCustom(
            "[Bootstrapper] Debug Spawn Ally In Current System result: " +
            spawned +
            ", Role: " +
            role);
    }

    private void StartGameFlow()
    {
        _gameStateMachine.Enter(new BootstrapState());
    }

    private void Update()
    {
        _tickService?.Tick(Time.deltaTime);
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
        _saveService.Save();
    }
}
