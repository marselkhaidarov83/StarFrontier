using UnityEngine;

public sealed class Bootstrapper2A : CustomMonoBehaviour
{
    private static Bootstrapper2A _instance;
    public static Bootstrapper2A Instance => _instance;

    [SerializeField] private GameBootstrapConfig gameBootstrapConfig;
    [SerializeField] private SaveConfig saveConfig;
    [SerializeField] private DebugConfig debugConfig;

    public IServiceRegistry ServiceRegistry;
    private IGameStateMachine _gameStateMachine;
    private ISaveService _saveService;
    private TickService _tickService;

    [SerializeField] private bool _globalDebugEnabled;
    public bool GlobalDebugEnabled => _globalDebugEnabled;

    private void Awake()
    {
        if (_instance != null)
        {
            LogCustomWarning("Duplicate bootstrapper found. Destroying duplicate");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

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
        _gameStateMachine = RegisterService<IGameStateMachine, GameStateMachine>();
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

    private void InitializeServices()
    {
        RegisterService<SimpleEventBus, SimpleEventBus>();
        RegisterService<IGameSessionService, GameSessionService>();

        ServiceRegistry.Register<IConfigService>(
            new ConfigService(
                    gameBootstrapConfig,
                    saveConfig,
                    debugConfig
                    ));

        RegisterService<ISceneService, SceneService>();
        // RegisterService<IInventoryService, InventoryService>();
        // RegisterService<ISystemEncounterService, SystemEncounterService>();
        // RegisterService<ISystemEnemyService, SystemEnemyService>();
        // RegisterService<ISystemAllyService, SystemAllyService>();
        // RegisterService<ISystemEncounterSaveService, SystemEncounterSaveService>();
        // RegisterService<IEconomyService, EconomyService>();
        // RegisterService<IMarketTransactionService, MarketTransactionService>();
        // RegisterService<IRefuelService, RefuelService>();
        // RegisterService<ITravelService, TravelService>();
        // RegisterService<IRepairService, RepairService>();
        // RegisterService<IRewardService, RewardService>();
        // RegisterService<IPlanetMissionOfferStateService, PlanetMissionOfferStateService>();
        // RegisterService<IPlanetMissionOfferGenerator, PlanetMissionOfferGenerator>();
        // RegisterService<IOrbitalMotionService, OrbitalMotionService>();
        // RegisterService<IGovernmentRewardPayoutService, DebugGovernmentRewardPayoutService>();
        // RegisterService<IGovernmentRewardService, GovernmentRewardService>();
        // RegisterService<ISystemNpcRuntimeService, SystemNpcRuntimeService>();
        // RegisterService<ISystemNpcPopulationService, SystemNpcPopulationService>();
        // RegisterService<IGalaxyPopulationService, GalaxyPopulationService>();
        // RegisterService<ISystemNpcBehaviorService, SystemNpcBehaviorService>();
        // RegisterService<IGalaxyNpcBehaviorService, GalaxyNpcBehaviorService>();
        // RegisterService<ISystemNpcSimulationSaveService, SystemNpcSimulationSaveService>();
        _saveService = RegisterService<ISaveService, SaveService2A>();
        // RegisterService<IPlayerCombatTargetService, PlayerCombatTargetService>();
        // RegisterService<ISystemNpcMovementRouteService, SystemNpcMovementRouteService>();
        // RegisterService<ISystemNpcMovementService, SystemNpcMovementService>();
        // RegisterService<IGalaxyNpcMovementService, GalaxyNpcMovementService>();
        // RegisterService<ISystemNpcCombatService, SystemNpcCombatService>();
        // RegisterService<IGalaxyNpcCombatService, GalaxyNpcCombatService>();
        // RegisterService<IPlayerAttackService, PlayerAttackService>();
        RegisterService<IContinueGameService, ContinueGameService2A>();
        RegisterService<INewGameService, NewGameService2A>();
        // RegisterService<IMissionService, MissionService>();
        // RegisterService<IMissionTracker, MissionTracker>();
        // RegisterService<IPlanetGovernmentMissionService, PlanetGovernmentMissionService>();
        // RegisterService<IHangarService, HangarService>();
        // RegisterService<ISystemTravelService, SystemTravelService>();
        // _gameTimeService = RegisterService<IGameTimeService, GameTimeService>();
        _tickService = RegisterService<TickService, TickService>();
    }

    private void Update()
    {
        _tickService.Tick(Time.deltaTime);
    }

    private void StartGameFlow()
    {
        _gameStateMachine.Enter(new BootstrapState2A());
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        _tickService?.Stop();
        ServiceRegistry?.Clear();

        ServiceRegistry = null;
        _instance = null;
    }
}