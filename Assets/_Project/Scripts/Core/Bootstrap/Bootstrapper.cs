using System.Collections.Generic;
using UnityEngine;

public class Bootstrapper : CustomMonoBehaviour
{
    [Header("Game")]
    [SerializeField] private GameConfig gameConfig;
    [SerializeField] private DebugConfig debugConfig;
    [SerializeField] private SaveConfig saveConfig;
    [SerializeField] private NewGameConfig newGameConfig;

    [Header("Data")]
    [SerializeField] private GalaxyConfig galaxyConfig;
    [SerializeField] private List<SectorConfig> sectors;
    [SerializeField] private List<StarSystemConfig> starSystems;
    [SerializeField] private List<ShipConfig> ships;
    [SerializeField] private List<EnemyConfig> enemies;
    [SerializeField] private List<AllyConfig> allies;
    [SerializeField] private List<AllySpawnRuleConfig> allySpawnRuleConfigs;
    [SerializeField] private List<PirateConfig> pirates;
    [SerializeField] private List<PirateGroupSpawnRuleConfig> pirateGroupSpawnRules;
    [SerializeField] private List<ModuleConfig> modules;
    [SerializeField] private List<WeaponConfig> weapons;
    [SerializeField] private List<ItemConfig> items;

    [SerializeField] public int MaxAcceptedMissionCount = 3;

    public static Bootstrapper Instance;
    public IServiceRegistry ServiceRegistry;
    private IGameStateMachine _gameStateMachine;
    private ISaveService _saveService;
    private IGameTimeService _gameTimeService;

    [SerializeField] private bool _globalDebugEnabled;
    public bool GlobalDebugEnabled => _globalDebugEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (IsDebug())
            Debug.Log("Bootstrapper awaked");

        InitializeServiceRegistry();
        InitializeStateMachine();
        InitializeServices();
        StartGameFlow();
    }

    private void InitializeServiceRegistry()
    {
        ServiceRegistry = new ServiceRegistry();
        if (IsDebug())
            Debug.Log("ServiceRegistry created");
    }

    private void InitializeStateMachine()
    {
        _gameStateMachine = RegisterService<IGameStateMachine, GameStateMachine>();
    }

    private void InitializeServices()
    {
        RegisterService<SimpleEventBus, SimpleEventBus>();
        RegisterService<IGameSessionService, GameSessionService>();

        List<PlanetConfig> planets = new List<PlanetConfig>();
        foreach (StarSystemConfig config in starSystems)
            planets.AddRange(config.PlanetRefs);
        ServiceRegistry.Register<IConfigService>(new ConfigService(
                    gameConfig,
                    debugConfig,
                    saveConfig,
                    galaxyConfig,
                    newGameConfig,
                    // sectors,
                    // starSystems,
                    // planets,
                    items,
                    ships,
                    enemies,
                    allies,
                    allySpawnRuleConfigs,
                    pirates,
                    pirateGroupSpawnRules,
                    modules,
                    weapons));
        if (IsDebug())
            Debug.Log("ConfigService registered");

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
        RegisterService<ITravelService, TravelService>();
        RegisterService<IRepairService, RepairService>();
        RegisterService<IRewardService, RewardService>();
        RegisterService<IPlanetMissionOfferStateService, PlanetMissionOfferStateService>();
        RegisterService<IPlanetMissionOfferGenerator, PlanetMissionOfferGenerator>();
        RegisterService<IOrbitalMotionService, OrbitalMotionService>();
        RegisterService<IGovernmentRewardPayoutService, DebugGovernmentRewardPayoutService>();
        RegisterService<IGovernmentRewardService, GovernmentRewardService>();
        RegisterService<ISystemNpcRuntimeService, SystemNpcRuntimeService>();
        RegisterService<ISystemNpcPopulationService, SystemNpcPopulationService>();
        RegisterService<IGalaxyPopulationService, GalaxyPopulationService>();
        RegisterService<ISystemNpcBehaviorService, SystemNpcBehaviorService>();
        RegisterService<IGalaxyNpcBehaviorService, GalaxyNpcBehaviorService>();
        RegisterService<ISystemNpcSimulationSaveService, SystemNpcSimulationSaveService>();
        // _saveService = RegisterService<ISaveService, SaveService>();
        _saveService = RegisterService<ISaveService, SaveService2>();
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
        RegisterService<IHangarService, HangarService>();
        RegisterService<ISystemTravelService, SystemTravelService>();
        _gameTimeService = RegisterService<IGameTimeService, GameTimeService>();
    }

    private TInterface RegisterService<TInterface, TImplementation>()
            where TImplementation : TInterface, new()
    {
        var service = new TImplementation();

        ServiceRegistry.Register<TInterface>(service);

        if (IsDebug())
            Debug.Log($"{typeof(TImplementation).Name} registered");

        return service;
    }

    private void StartGameFlow()
    {
        _gameStateMachine.Enter(new BootstrapState());
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // _saveService?.Tick(deltaTime);
        _gameTimeService?.Tick(deltaTime);
    }

    private void OnApplicationPause(bool pause)
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

    public void SaveCurrentGame(string reason = "manual")
    {
        _saveService.Save();
    }
}