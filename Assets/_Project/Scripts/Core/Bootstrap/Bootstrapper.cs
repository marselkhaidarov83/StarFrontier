using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private List<ShipConfig> ships;
    [SerializeField] private List<EnemyConfig> enemies;
    [SerializeField] private List<AllyConfig> allies;
    [SerializeField] private List<AllySpawnRuleConfig> allySpawnRuleConfigs;
    [SerializeField] private List<PirateConfig> pirates;
    [SerializeField] private List<PirateGroupSpawnRuleConfig> pirateGroupSpawnRules;
    [SerializeField] private List<ModuleConfig> modules;
    [SerializeField] private List<WeaponConfig> weapons;
    [SerializeField] private List<ItemConfig> items;

#if UNITY_EDITOR
    [Header("Editor Auto Fill / Weapons")]
    [SerializeField] private bool autoCollectWeaponsFromFolders = true;
    [SerializeField] private bool autoAssignDefaultWeaponFolders = true;
    [SerializeField] private List<DefaultAsset> weaponConfigFolders;

    private static readonly string[] DefaultWeaponFolderPaths =
    {
        "Assets/_Project/Content/Configs/Weapons/Common",
        "Assets/_Project/Content/Configs/Weapons/AI",
        "Assets/_Project/Content/Configs/Weapons/Infected",
        "Assets/_Project/Content/Configs/Weapons/Ancients"
    };
#endif

    [SerializeField] public int MaxAcceptedMissionCount = 3;

    [Header("Debug")]
    [SerializeField] public bool SectorAllOpened = false;
    [SerializeField] public bool RouteAllUnlocked = false;
    [SerializeField] private bool _globalDebugEnabled;

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

#if UNITY_EDITOR
        RebuildWeaponListFromFoldersIfEnabled();
#endif

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
                ships,
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

        _playerControlService = RegisterService<IPlayerControlService, PlayerControlService2A>();
        _shipMovementService = RegisterService<IShipMovementService, ShipMovementService2A>();

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

        _interactionService = RegisterService<IInteractionService2A, InteractionService2A>();

        RegisterService<IRepairService, RepairService>();
        RegisterService<IRewardService, RewardService>();
        RegisterService<IPlanetMissionOfferStateService, PlanetMissionOfferStateService>();
        RegisterService<IPlanetMissionOfferGenerator, PlanetMissionOfferGenerator>();
        RegisterService<IGovernmentRewardPayoutService, DebugGovernmentRewardPayoutService>();
        RegisterService<IGovernmentRewardService, GovernmentRewardService>();
        RegisterService<ISystemNpcRuntimeService, SystemNpcRuntimeService>();
        RegisterService<ISystemSecurityService, SystemSecurityService>();
        RegisterService<ISystemNpcPopulationService, SystemNpcPopulationService>();
        RegisterService<IGalaxyPopulationService, GalaxyPopulationService>();
        RegisterService<ISystemNpcBehaviorService, SystemNpcBehaviorService>();
        RegisterService<IGalaxyNpcBehaviorService, GalaxyNpcBehaviorService>();
        RegisterService<ISystemNpcSimulationSaveService, SystemNpcSimulationSaveService>();

        _saveService = RegisterService<ISaveService, SaveService2A>();

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

        _tickService = RegisterService<ITickService, TickService>();
        _gameTimeService = RegisterService<IGameTimeService, GameTimeService>();

        _tickService.Register(_gameTimeService, TickOrder.GameTime);
        _tickService.Register(_playerControlService, TickOrder.PlayerControl);
        _tickService.Register(_shipMovementService, TickOrder.ShipMovement);
        _tickService.Register(_interactionService, TickOrder.Interaction);

        RegisterService<IGameTimePauseScopeService, GameTimePauseScopeService>();
    }

    /// <summary>
    /// Создаёт сервис через пустой конструктор
    /// и передаёт его в регистрацию готового экземпляра.
    /// </summary>
    private TInterface RegisterService<
        TInterface,
        TImplementation>()
        where TImplementation : TInterface, new()
    {
        TImplementation service =
            new TImplementation();

        return RegisterService<TInterface>(service);
    }

    /// <summary>
    /// Регистрирует уже созданный экземпляр сервиса.
    ///
    /// Используется, когда объект создан заранее
    /// или требует параметров конструктора.
    /// </summary>
    private TInterface RegisterService<TInterface>(
        TInterface service)
    {
        ServiceRegistry.Register<TInterface>(service);

        LogCustom($"{service.GetType().Name} registered " + $"as {typeof(TInterface).Name}");

        return service;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoCollectWeaponsFromFolders)
            return;

        RebuildWeaponListFromFoldersIfEnabled();
    }

    [ContextMenu("STAR FRONTIER/Rebuild Weapon List From Folders")]
    private void RebuildWeaponListFromFolders()
    {
        EnsureDefaultWeaponFoldersAssigned();

        if (weaponConfigFolders == null || weaponConfigFolders.Count == 0)
        {
            Debug.LogWarning(
                "[Bootstrapper] WeaponConfig folders are empty. " +
                "Assign folders manually or keep Auto Assign Default Weapon Folders enabled.");

            return;
        }

        var collectedWeapons =
            new List<WeaponConfig>();

        var addedGuids =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < weaponConfigFolders.Count; i++)
        {
            DefaultAsset folder =
                weaponConfigFolders[i];

            if (folder == null)
                continue;

            string folderPath =
                AssetDatabase.GetAssetPath(folder);

            if (string.IsNullOrWhiteSpace(folderPath))
                continue;

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning(
                    "[Bootstrapper] WeaponConfig folder is not a valid folder: " +
                    folderPath);

                continue;
            }

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:WeaponConfig",
                    new[] { folderPath });

            Array.Sort(guids, StringComparer.Ordinal);

            for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
            {
                string guid =
                    guids[guidIndex];

                if (!addedGuids.Add(guid))
                    continue;

                string assetPath =
                    AssetDatabase.GUIDToAssetPath(guid);

                WeaponConfig weaponConfig =
                    AssetDatabase.LoadAssetAtPath<WeaponConfig>(assetPath);

                if (weaponConfig == null)
                    continue;

                collectedWeapons.Add(weaponConfig);
            }
        }

        collectedWeapons.Sort(CompareWeaponConfigsById);

        if (AreSameWeaponLists(weapons, collectedWeapons))
            return;

        weapons =
            collectedWeapons;

        EditorUtility.SetDirty(this);

        Debug.Log(
            "[Bootstrapper] WeaponConfig list rebuilt from folders. Count: " +
            weapons.Count);
    }

    private void RebuildWeaponListFromFoldersIfEnabled()
    {
        if (!autoCollectWeaponsFromFolders)
            return;

        RebuildWeaponListFromFolders();
    }

    private void EnsureDefaultWeaponFoldersAssigned()
    {
        if (!autoAssignDefaultWeaponFolders)
            return;

        if (weaponConfigFolders == null)
            weaponConfigFolders = new List<DefaultAsset>();

        bool hasAnyAssignedFolder =
            false;

        for (int i = 0; i < weaponConfigFolders.Count; i++)
        {
            if (weaponConfigFolders[i] != null)
            {
                hasAnyAssignedFolder = true;
                break;
            }
        }

        if (hasAnyAssignedFolder)
            return;

        weaponConfigFolders.Clear();

        for (int i = 0; i < DefaultWeaponFolderPaths.Length; i++)
        {
            string folderPath =
                DefaultWeaponFolderPaths[i];

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning(
                    "[Bootstrapper] Default WeaponConfig folder not found: " +
                    folderPath);

                continue;
            }

            DefaultAsset folderAsset =
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);

            if (folderAsset == null)
            {
                Debug.LogWarning(
                    "[Bootstrapper] Default WeaponConfig folder asset not loaded: " +
                    folderPath);

                continue;
            }

            weaponConfigFolders.Add(folderAsset);
        }
    }

    private static int CompareWeaponConfigsById(
        WeaponConfig left,
        WeaponConfig right)
    {
        if (ReferenceEquals(left, right))
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        return string.Compare(
            left.Id,
            right.Id,
            StringComparison.Ordinal);
    }

    private static bool AreSameWeaponLists(
        List<WeaponConfig> current,
        List<WeaponConfig> collected)
    {
        int currentCount =
            current == null ? 0 : current.Count;

        int collectedCount =
            collected == null ? 0 : collected.Count;

        if (currentCount != collectedCount)
            return false;

        for (int i = 0; i < currentCount; i++)
        {
            if (!ReferenceEquals(current[i], collected[i]))
                return false;
        }

        return true;
    }
#endif

    private void StartGameFlow()
    {
        _gameStateMachine.Enter(new BootstrapState());
    }

    private void Update()
    {
        // float deltaTime = Time.deltaTime;

        // _gameTimeService?.Tick(deltaTime);
        _tickService?.Tick(Time.deltaTime);
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