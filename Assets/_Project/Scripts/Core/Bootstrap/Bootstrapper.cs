using System;
using System.Collections.Generic;
using System.Text;
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
    [SerializeField] private CombatFxVisualConfig combatFxVisualConfig;
    [SerializeField] private CombatDamagePopupVisualConfig2A combatDamagePopupVisualConfig;
    [SerializeField] private NpcBehaviourTransitionMatrixConfig npcBehaviourTransitionMatrixConfig;

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

    [Header("Debug / Combat Damage")]
    [SerializeField] private string debugCombatTargetRuntimeNpcId;
    [SerializeField, Min(1)] private int debugCombatTargetDamage = 10;
    [SerializeField, Min(1)] private int debugCombatPlayerDamage = 10;
    [SerializeField, Min(0.1f)] private float debugNpcOfflineStepHours = 1f;

    [Header("Debug / NPC Stress")]
    [SerializeField, Min(1)] private int debugNpcStressSpawnAttempts = 25;
    [SerializeField, Min(1)] private int debugNpcStressSpawnWaves = 1;
    [SerializeField] private bool debugNpcStressSpawnAllies = true;
    [SerializeField] private bool debugNpcStressSpawnEnemyGroups = true;

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
        RegisterService<ISystemNpcPopulationService, SystemNpcPopulationService>();
        RegisterService<IGalaxyPopulationService, GalaxyPopulationService>();
        RegisterService<ISystemNpcBehaviorService, SystemNpcBehaviorService>();
        RegisterService<IGalaxyNpcBehaviorService, GalaxyNpcBehaviorService>();
        RegisterService<ISystemNpcSimulationSaveService, SystemNpcSimulationSaveService>();
        RegisterService<ISystemNpcOfflineRelocationService, SystemNpcOfflineRelocationService>();

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

    [ContextMenu("STAR FRONTIER/Stress Spawn NPC")]
    private void DebugStressSpawnNpcs()
    {
        if (!TryGetDebugNpcStressServices(
                out ISystemNpcPopulationService populationService,
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        if (!debugNpcStressSpawnAllies &&
            !debugNpcStressSpawnEnemyGroups)
        {
            DebugCombatWarning("[Bootstrapper] NPC stress spawn skipped. No spawn type is enabled.");
            return;
        }

        int beforeCount =
            GetDebugNpcRuntimeCount(npcRuntimeService);

        int spawnedCommands = 0;
        int failedCommands = 0;

        int waveCount =
            Mathf.Max(1, debugNpcStressSpawnWaves);

        int attemptsPerWave =
            Mathf.Max(1, debugNpcStressSpawnAttempts);

        for (int wave = 0; wave < waveCount; wave++)
        {
            RunDebugNpcStressSpawnWave(
                populationService,
                attemptsPerWave,
                ref spawnedCommands,
                ref failedCommands);
        }

        int afterCount =
            GetDebugNpcRuntimeCount(npcRuntimeService);

        DebugCombatLog(
            "[Bootstrapper] NPC Stress Spawn completed. " +
            "Waves: " + waveCount +
            ", AttemptsPerWave: " + attemptsPerWave +
            ", SuccessfulCommands: " + spawnedCommands +
            ", FailedCommands: " + failedCommands +
            ", NpcCountBefore: " + beforeCount +
            ", NpcCountAfter: " + afterCount +
            ", Delta: " + (afterCount - beforeCount));
    }

    [ContextMenu("STAR FRONTIER/Stress Spawn NPC Wave")]
    private void DebugStressSpawnNpcWave()
    {
        if (!TryGetDebugNpcStressServices(
                out ISystemNpcPopulationService populationService,
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        int beforeCount =
            GetDebugNpcRuntimeCount(npcRuntimeService);

        int spawnedCommands = 0;
        int failedCommands = 0;

        RunDebugNpcStressSpawnWave(
            populationService,
            Mathf.Max(1, debugNpcStressSpawnAttempts),
            ref spawnedCommands,
            ref failedCommands);

        int afterCount =
            GetDebugNpcRuntimeCount(npcRuntimeService);

        DebugCombatLog(
            "[Bootstrapper] NPC Stress Spawn Wave completed. " +
            "Attempts: " + Mathf.Max(1, debugNpcStressSpawnAttempts) +
            ", SuccessfulCommands: " + spawnedCommands +
            ", FailedCommands: " + failedCommands +
            ", NpcCountBefore: " + beforeCount +
            ", NpcCountAfter: " + afterCount +
            ", Delta: " + (afterCount - beforeCount));
    }

    [ContextMenu("STAR FRONTIER/Print NPC Runtime Count")]
    private void DebugPrintNpcRuntimeCount()
    {
        if (!TryGetDebugNpcRuntimeServiceWithoutTarget(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        DebugCombatLog(
            "[Bootstrapper] NPC Runtime Count: " +
            GetDebugNpcRuntimeCount(npcRuntimeService));
    }

    [ContextMenu("STAR FRONTIER/Validate NPC Runtime State")]
    private void DebugValidateNpcRuntimeState()
    {
        if (!TryGetDebugNpcRuntimeServiceWithoutTarget(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        if (ServiceRegistry == null ||
            !ServiceRegistry.TryGet<IConfigService>(
                out IConfigService configService) ||
            configService == null)
        {
            DebugCombatWarning("[Bootstrapper] NPC validation failed. IConfigService is not registered.");
            return;
        }

        int checkedCount = 0;
        int issueCount = 0;

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            npcRuntimeService.Npcs;

        if (npcs == null || npcs.Count == 0)
        {
            DebugCombatLog("[Bootstrapper] NPC validation completed. NPCs: none.");
            return;
        }

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc =
                npcs[i];

            checkedCount++;

            ValidateDebugNpcRuntimeState(
                npc,
                i,
                configService,
                ref issueCount);
        }

        DebugCombatLog(
            "[Bootstrapper] NPC validation completed. " +
            "Checked: " + checkedCount +
            ", Issues: " + issueCount);
    }

    [ContextMenu("STAR FRONTIER/Damage First Enemy In Current System")]
    private void DebugDamageFirstEnemyInCurrentSystem()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        string runtimeNpcId =
            FindFirstAliveEnemyRuntimeIdInCurrentSystem(
                npcRuntimeService,
                configService);

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug damage target failed. " +
                "No alive enemy was found in current system.");

            return;
        }

        DamageDebugTarget(
            npcRuntimeService,
            runtimeNpcId,
            debugCombatTargetDamage);
    }

    [ContextMenu("STAR FRONTIER/Damage Target NPC By Runtime ID")]
    private void DebugDamageTargetNpcByRuntimeId()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug damage target failed. " +
                "debugCombatTargetRuntimeNpcId is empty.");

            return;
        }

        DamageDebugTarget(
            npcRuntimeService,
            debugCombatTargetRuntimeNpcId,
            debugCombatTargetDamage);
    }

    [ContextMenu("STAR FRONTIER/Damage Player")]
    private void DebugDamagePlayer()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        int safeDamage =
            Mathf.Max(1, debugCombatPlayerDamage);

        ShipRuntimeData activeShipBefore =
            GetDebugActiveShip();

        if (activeShipBefore == null)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug Damage Player failed. " +
                "Active ship is null.");

            return;
        }

        int shieldBefore =
            activeShipBefore.CurrentShield;

        int hullBefore =
            activeShipBefore.CurrentHull;

        playerCombatTargetService.ApplyDamage(safeDamage);

        ShipRuntimeData activeShipAfter =
            GetDebugActiveShip();

        if (activeShipAfter == null)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug Damage Player finished, " +
                "but active ship is null after damage.");

            return;
        }

        DebugCombatLog(
            "[Bootstrapper] Debug Damage Player requested. " +
            "Damage: " +
            safeDamage +
            ", Shield: " +
            shieldBefore +
            " -> " +
            activeShipAfter.CurrentShield +
            ", Hull: " +
            hullBefore +
            " -> " +
            activeShipAfter.CurrentHull);
    }

    [ContextMenu("STAR FRONTIER/Kill First Enemy In Current System")]
    private void DebugKillFirstEnemyInCurrentSystem()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        string runtimeNpcId =
            FindFirstAliveEnemyRuntimeIdInCurrentSystem(
                npcRuntimeService,
                configService);

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug kill enemy failed. " +
                "No alive enemy was found in current system.");

            return;
        }

        KillDebugEnemy(
            npcRuntimeService,
            runtimeNpcId);
    }

    private bool TryGetDebugNpcRuntimeService(
        out ISystemNpcRuntimeService npcRuntimeService)
    {
        npcRuntimeService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[Bootstrapper] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out npcRuntimeService))
        {
            DebugCombatWarning("[Bootstrapper] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            DebugCombatWarning("[Bootstrapper] debugCombatTargetRuntimeNpcId is empty.");
            return false;
        }

        return true;
    }

    [ContextMenu("STAR FRONTIER/Kill Target NPC By Runtime ID")]
    private void DebugKillTargetNpcByRuntimeId()
    {
        if (!TryGetDebugNpcRuntimeService(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        bool killed =
            npcRuntimeService.KillNpc(
                debugCombatTargetRuntimeNpcId,
                true);

        DebugCombatLog(
            "[Bootstrapper] Debug Kill Target NPC result: " +
            killed +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Despawn Target NPC By Runtime ID")]
    private void DebugDespawnTargetNpcByRuntimeId()
    {
        if (!TryGetDebugNpcRuntimeService(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        bool despawned =
            npcRuntimeService.DespawnNpc(
                debugCombatTargetRuntimeNpcId);

        DebugCombatLog(
            "[Bootstrapper] Debug Despawn Target NPC result: " +
            despawned +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Reset Target NPC By Runtime ID")]
    private void DebugResetTargetNpcByRuntimeId()
    {
        if (!TryGetDebugNpcRuntimeService(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        bool reset =
            npcRuntimeService.ResetNpc(
                debugCombatTargetRuntimeNpcId);

        DebugCombatLog(
            "[Bootstrapper] Debug Reset Target NPC result: " +
            reset +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Force Target NPC Route To Linked System")]
    private void DebugForceTargetNpcRouteToLinkedSystem()
    {
        if (!TryGetDebugNpcOfflineServices(
                out ISystemNpcOfflineRelocationService offlineRelocationService,
                out IGameSessionService gameSessionService))
        {
            return;
        }

        bool forced =
            offlineRelocationService.DebugForceTargetNpcRoute(
                debugCombatTargetRuntimeNpcId,
                gameSessionService.State);

        DebugCombatLog(
            "[Bootstrapper] Debug Force Target NPC Route result: " +
            forced +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Run NPC Offline Step")]
    private void DebugRunNpcOfflineStep()
    {
        if (!TryGetDebugNpcOfflineServices(
                out ISystemNpcOfflineRelocationService offlineRelocationService,
                out IGameSessionService gameSessionService))
        {
            return;
        }

        bool moved =
            offlineRelocationService.DebugProcessOfflineStep(
                gameSessionService.State,
                debugNpcOfflineStepHours);

        DebugCombatLog(
            "[Bootstrapper] Debug NPC Offline Step result: " +
            moved +
            ", Hours: " +
            debugNpcOfflineStepHours.ToString("0.00"));
    }

    [ContextMenu("STAR FRONTIER/Reset Current Encounter")]
    private void DebugResetCurrentEncounter()
    {
        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[Bootstrapper] ServiceRegistry is not initialized.");
            return;
        }

        if (!ServiceRegistry.TryGet<ISystemEncounterService>(
                out ISystemEncounterService encounterService) ||
            encounterService == null)
        {
            DebugCombatWarning("[Bootstrapper] ISystemEncounterService is not registered.");
            return;
        }

        encounterService.ClearEncounter();

        DebugCombatLog("[Bootstrapper] Debug Reset Current Encounter completed.");
    }

    [ContextMenu("STAR FRONTIER/Enable Player God Mode")]
    private void DebugEnablePlayerGodMode()
    {
        SetDebugPlayerGodMode(true);
    }

    [ContextMenu("STAR FRONTIER/Disable Player God Mode")]
    private void DebugDisablePlayerGodMode()
    {
        SetDebugPlayerGodMode(false);
    }

    private void SetDebugPlayerGodMode(
        bool enabled)
    {
        if (debugConfig == null)
        {
            DebugCombatWarning("[Bootstrapper] DebugConfig is not assigned.");
            return;
        }

        debugConfig.enableGodMode = enabled;

        DebugCombatLog(
            "[Bootstrapper] Player God Mode: " +
            enabled);
    }

    [ContextMenu("STAR FRONTIER/Print All NPC Debug State")]
    private void DebugPrintAllNpcDebugState()
    {
        if (!TryGetDebugNpcRuntimeServiceWithoutTarget(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        StringBuilder text =
            new StringBuilder(2048);

        text.AppendLine("[Bootstrapper] NPC Debug State");

        if (npcRuntimeService.Npcs == null ||
            npcRuntimeService.Npcs.Count == 0)
        {
            text.AppendLine("NPCs: none");
            DebugCombatLog(text.ToString());
            return;
        }

        for (int i = 0; i < npcRuntimeService.Npcs.Count; i++)
        {
            AppendNpcDebugState(
                text,
                npcRuntimeService.Npcs[i],
                i);
        }

        DebugCombatLog(text.ToString());
    }

    [ContextMenu("STAR FRONTIER/Print Target NPC Debug State")]
    private void DebugPrintTargetNpcDebugState()
    {
        if (!TryGetDebugNpcRuntimeService(
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        if (!npcRuntimeService.TryGetNpc(
                debugCombatTargetRuntimeNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            DebugCombatWarning(
                "[Bootstrapper] Target NPC debug failed. NPC was not found. RuntimeNpcId: " +
                debugCombatTargetRuntimeNpcId);

            return;
        }

        StringBuilder text =
            new StringBuilder(1024);

        text.AppendLine("[Bootstrapper] Target NPC Debug State");

        AppendNpcDebugState(
            text,
            npc,
            0);

        DebugCombatLog(text.ToString());
    }

    private bool TryGetDebugNpcRuntimeServiceWithoutTarget(
        out ISystemNpcRuntimeService npcRuntimeService)
    {
        npcRuntimeService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[Bootstrapper] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out npcRuntimeService))
        {
            DebugCombatWarning("[Bootstrapper] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        return true;
    }

    [ContextMenu("STAR FRONTIER/Print Combat Debug State")]
    private void DebugPrintCombatState()
    {
        StringBuilder text =
            new StringBuilder(512);

        text.AppendLine("[Bootstrapper] Combat Debug State");

        AppendDebugEncounterState(text);
        AppendDebugPlayerState(text);
        AppendDebugTargetState(text);
        AppendDebugNpcCombatState(text);

        DebugCombatLog(text.ToString());
    }

    private void DebugCombatLog(
        string message)
    {
        Debug.unityLogger.Log(
            LogType.Log,
            (object)message,
            this);
    }

    private void DebugCombatWarning(
        string message)
    {
        Debug.unityLogger.Log(
            LogType.Warning,
            (object)message,
            this);
    }

    private void AppendDebugEncounterState(
        StringBuilder text)
    {
        if (ServiceRegistry == null)
        {
            text.AppendLine("Encounter: ServiceRegistry unavailable");
            return;
        }

        if (!ServiceRegistry.TryGet<ISystemEncounterService>(
                out ISystemEncounterService encounterService) ||
            encounterService == null)
        {
            text.AppendLine("Encounter: service unavailable");
            return;
        }

        ActiveSystemEncounter encounter =
            encounterService.Current;

        if (encounter == null)
        {
            text.AppendLine("Encounter: none");
            return;
        }

        text.Append("Encounter: ")
            .Append(encounter.EncounterId)
            .Append(", System: ")
            .Append(encounter.SystemId)
            .Append(", State: ")
            .Append(encounter.State)
            .Append(", EnemiesAlive: ")
            .Append(encounter.EnemiesAlive)
            .Append(", AlliesAlive: ")
            .Append(encounter.AlliesAlive)
            .Append(", PlayerKills: ")
            .Append(encounter.PlayerKills)
            .Append(", DefeatReason: ")
            .AppendLine(encounter.DefeatReason.ToString());
    }

    private void AppendDebugPlayerState(
        StringBuilder text)
    {
        ShipRuntimeData activeShip =
            GetDebugActiveShip();

        if (activeShip == null)
        {
            text.AppendLine("Player: active ship unavailable");
            return;
        }

        ShipStats stats =
            GetDebugActiveShipStats();

        int maxHull =
            stats != null
                ? stats.MaxHull
                : activeShip.HullCapacity;

        int maxShield =
            stats != null
                ? stats.MaxShield
                : activeShip.CurrentShield;

        text.Append("Player: ShipId: ")
            .Append(activeShip.ShipId)
            .Append(", Hull: ")
            .Append(activeShip.CurrentHull)
            .Append(" / ")
            .Append(maxHull)
            .Append(", Shield: ")
            .Append(activeShip.CurrentShield)
            .Append(" / ")
            .Append(maxShield)
            .Append(", Energy: ")
            .Append(activeShip.CurrentEnergy)
            .AppendLine();
    }

    private void AppendDebugTargetState(
        StringBuilder text)
    {
        if (ServiceRegistry == null)
        {
            text.AppendLine("Target: ServiceRegistry unavailable");
            return;
        }

        if (ServiceRegistry.TryGet<IPlayerAttackService>(
                out IPlayerAttackService playerAttackService) &&
            playerAttackService != null &&
            !string.IsNullOrWhiteSpace(playerAttackService.CurrentTargetNpcId))
        {
            AppendDebugNpcTarget(
                text,
                "PlayerAttack target",
                playerAttackService.CurrentTargetNpcId);

            return;
        }

        if (ServiceRegistry.TryGet<ITargetService2A>(
                out ITargetService2A targetService) &&
            targetService != null &&
            targetService.State != null &&
            targetService.State.HasTarget)
        {
            text.Append("Targeting target: ")
                .Append(targetService.State.CurrentTargetId)
                .Append(", Type: ")
                .Append(targetService.State.CurrentTargetType)
                .Append(", Distance: ")
                .Append(targetService.State.CurrentTargetDistance.ToString("0.0"))
                .Append(", InRange: ")
                .Append(targetService.State.IsTargetInRange)
                .AppendLine();

            AppendDebugNpcTarget(
                text,
                "Targeting NPC state",
                targetService.State.CurrentTargetId);

            return;
        }

        text.AppendLine("Target: none");
    }

    private void AppendDebugNpcTarget(
        StringBuilder text,
        string label,
        string runtimeNpcId)
    {
        if (ServiceRegistry == null)
            return;

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out ISystemNpcRuntimeService npcRuntimeService) ||
            npcRuntimeService == null)
        {
            text.Append(label)
                .AppendLine(": NPC runtime service unavailable");

            return;
        }

        if (!npcRuntimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            text.Append(label)
                .Append(": not found. RuntimeNpcId: ")
                .AppendLine(runtimeNpcId);

            return;
        }

        text.Append(label)
            .Append(": ")
            .Append(npc.DisplayName)
            .Append(", RuntimeNpcId: ")
            .Append(npc.RuntimeNpcId)
            .Append(", Type: ")
            .Append(npc.NpcType)
            .Append(", Hull: ")
            .Append(npc.CurrentHull)
            .Append(" / ")
            .Append(npc.MaxHull)
            .Append(", Shield: ")
            .Append(npc.CurrentShield)
            .Append(" / ")
            .Append(npc.MaxShield)
            .Append(", CombatState: ")
            .Append(npc.CombatState)
            .Append(", Alive: ")
            .Append(npc.IsAlive)
            .AppendLine();
    }

    private void AppendNpcDebugState(
    StringBuilder text,
    SystemNpcRuntimeState npc,
    int index)
    {
        if (npc == null)
        {
            text.Append("#")
                .Append(index)
                .AppendLine(": null NPC");
            return;
        }

        text.Append("#")
            .Append(index)
            .Append(": ")
            .Append(npc.DisplayName)
            .Append(", RuntimeNpcId: ")
            .Append(npc.RuntimeNpcId)
            .Append(", Type: ")
            .Append(npc.NpcType)
            .Append(", Role: ")
            .Append(npc.AllyRole)
            .Append(", Faction: ")
            .Append(BuildDebugNpcFaction(npc))
            .AppendLine();

        text.Append("  Route: OriginSystem: ")
            .Append(npc.OriginSystemId)
            .Append(", CurrentSystem: ")
            .Append(npc.CurrentSystemId)
            .Append(", TargetSystem: ")
            .Append(npc.TargetSystemId)
            .Append(", CurrentPlanet: ")
            .Append(npc.CurrentPlanetId)
            .Append(", TargetPlanet: ")
            .Append(npc.TargetPlanetId)
            .Append(", TravelState: ")
            .Append(npc.TravelState)
            .AppendLine();

        text.Append("  Target: CurrentTargetRuntimeNpcId: ")
            .Append(npc.CurrentTargetRuntimeNpcId)
            .Append(", BehaviorTargetRuntimeNpcId: ")
            .Append(npc.BehaviorTargetRuntimeNpcId)
            .AppendLine();

        text.Append("  State: LifeState: ")
            .Append(npc.LifeState)
            .Append(", IsAlive: ")
            .Append(npc.IsAlive)
            .Append(", Behavior: ")
            .Append(npc.CurrentBehavior)
            .Append(", PrevBehavior: ")
            .Append(npc.PrevBehavior)
            .Append(", CombatState: ")
            .Append(npc.CombatState)
            .Append(", IsFighting: ")
            .Append(npc.IsFighting)
            .AppendLine();

        text.Append("  Timers: BehaviorStartedTick: ")
            .Append(npc.BehaviorStartedTick)
            .Append(", BehaviorEndsTick: ")
            .Append(npc.BehaviorEndsTick)
            .Append(", TravelStartTick: ")
            .Append(npc.TravelStartTick)
            .Append(", TravelEndTick: ")
            .Append(npc.TravelEndTick)
            .Append(", DestroyedAtTick: ")
            .Append(npc.DestroyedAtTick)
            .Append(", NextRespawnTick: ")
            .Append(npc.NextRespawnTick)
            .AppendLine();
    }

    private string BuildDebugNpcFaction(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "Unknown";

        if (npc.IsEnemy)
            return "Enemy";

        if (npc.IsPirate)
            return "Pirate";

        if (npc.IsAlly)
            return "Civilization";

        return "Unknown";
    }

    private void AppendDebugNpcCombatState(
    StringBuilder text)
    {
        if (ServiceRegistry == null)
        {
            text.AppendLine("NPC Debug: ServiceRegistry unavailable");
            return;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out ISystemNpcRuntimeService npcRuntimeService) ||
            npcRuntimeService == null)
        {
            text.AppendLine("NPC Debug: runtime service unavailable");
            return;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            text.AppendLine("NPC Debug: target RuntimeNpcId is empty");
            return;
        }

        if (!npcRuntimeService.TryGetNpc(
                debugCombatTargetRuntimeNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            text.Append("NPC Debug: target not found. RuntimeNpcId: ")
                .AppendLine(debugCombatTargetRuntimeNpcId);

            return;
        }

        AppendNpcDebugState(
            text,
            npc,
            0);
    }

    private bool TryGetDebugCombatServices(
        out ISystemNpcRuntimeService npcRuntimeService,
        out IPlayerCombatTargetService playerCombatTargetService,
        out IConfigService configService)
    {
        npcRuntimeService = null;
        playerCombatTargetService = null;
        configService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[Bootstrapper] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out npcRuntimeService))
        {
            DebugCombatWarning("[Bootstrapper] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet<IPlayerCombatTargetService>(
                out playerCombatTargetService))
        {
            DebugCombatWarning("[Bootstrapper] IPlayerCombatTargetService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet<IConfigService>(
                out configService))
        {
            DebugCombatWarning("[Bootstrapper] IConfigService is not registered.");
            return false;
        }

        return true;
    }

    private string FindFirstAliveEnemyRuntimeIdInCurrentSystem(
        ISystemNpcRuntimeService npcRuntimeService,
        IConfigService configService)
    {
        StarSystemConfig currentSystem =
            configService.GetCurrentSystemConfig();

        if (currentSystem == null ||
            string.IsNullOrWhiteSpace(currentSystem.Id))
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug damage target failed. " +
                "Current system was not resolved.");

            return null;
        }

        IReadOnlyList<SystemNpcRuntimeState> enemies =
            npcRuntimeService.GetAliveNpcsInSystemByType(
                currentSystem.Id,
                SystemNpcType.Enemy);

        if (enemies == null || enemies.Count == 0)
            return null;

        for (int i = 0; i < enemies.Count; i++)
        {
            SystemNpcRuntimeState enemy =
                enemies[i];

            if (enemy == null)
                continue;

            if (!enemy.IsAlive ||
                enemy.LifeState != SystemNpcLifeState.Alive)
            {
                continue;
            }

            return enemy.RuntimeNpcId;
        }

        return null;
    }

    private void DamageDebugTarget(
        ISystemNpcRuntimeService npcRuntimeService,
        string runtimeNpcId,
        int damage)
    {
        if (!npcRuntimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug damage target failed. " +
                "NPC was not found. RuntimeNpcId: " +
                runtimeNpcId);

            return;
        }

        if (!npc.IsAlive ||
            npc.LifeState != SystemNpcLifeState.Alive)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug damage target failed. " +
                "NPC is not alive. RuntimeNpcId: " +
                runtimeNpcId);

            return;
        }

        int safeDamage =
            Mathf.Max(1, damage);

        npcRuntimeService.ApplyDamage(
            runtimeNpcId,
            safeDamage,
            true,
            true);

        DebugCombatLog(
            "[Bootstrapper] Debug Damage Target completed. " +
            "RuntimeNpcId: " +
            runtimeNpcId +
            ", Damage: " +
            safeDamage);
    }

    private void KillDebugEnemy(
        ISystemNpcRuntimeService npcRuntimeService,
        string runtimeNpcId)
    {
        if (!npcRuntimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug kill enemy failed. " +
                "NPC was not found. RuntimeNpcId: " +
                runtimeNpcId);

            return;
        }

        if (!npc.IsEnemy)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug kill enemy failed. " +
                "NPC is not an enemy. RuntimeNpcId: " +
                runtimeNpcId +
                ", Type: " +
                npc.NpcType);

            return;
        }

        if (!npc.IsAlive ||
            npc.LifeState != SystemNpcLifeState.Alive)
        {
            DebugCombatWarning(
                "[Bootstrapper] Debug kill enemy failed. " +
                "Enemy is not alive. RuntimeNpcId: " +
                runtimeNpcId);

            return;
        }

        int lethalDamage =
            Mathf.Max(
                npc.CurrentHull + npc.CurrentShield,
                999999);

        npcRuntimeService.ApplyDamage(
            runtimeNpcId,
            lethalDamage,
            true,
            true);

        DebugCombatLog(
            "[Bootstrapper] Debug Kill Enemy completed. " +
            "RuntimeNpcId: " +
            runtimeNpcId +
            ", Damage: " +
            lethalDamage);
    }

    private ShipRuntimeData GetDebugActiveShip()
    {
        if (ServiceRegistry == null)
            return null;

        if (!ServiceRegistry.TryGet<IGameSessionService>(
                out IGameSessionService gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null ||
            gameSessionService.State.Player == null)
        {
            return null;
        }

        return gameSessionService.State.Player.GetActiveShip();
    }

    private ShipStats GetDebugActiveShipStats()
    {
        if (ServiceRegistry == null)
            return null;

        if (!ServiceRegistry.TryGet<IHangarService>(
                out IHangarService hangarService) ||
            hangarService == null)
        {
            return null;
        }

        return hangarService.GetActiveShipStats();
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

    private bool TryGetDebugNpcOfflineServices(
    out ISystemNpcOfflineRelocationService offlineRelocationService,
    out IGameSessionService gameSessionService)
    {
        offlineRelocationService = null;
        gameSessionService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[Bootstrapper] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcOfflineRelocationService>(
                out offlineRelocationService) ||
            offlineRelocationService == null)
        {
            DebugCombatWarning("[Bootstrapper] ISystemNpcOfflineRelocationService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet<IGameSessionService>(
                out gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null)
        {
            DebugCombatWarning("[Bootstrapper] IGameSessionService state is unavailable.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            DebugCombatWarning("[Bootstrapper] debugCombatTargetRuntimeNpcId is empty.");
            return false;
        }

        return true;
    }

    private void RunDebugNpcStressSpawnWave(
    ISystemNpcPopulationService populationService,
    int attempts,
    ref int spawnedCommands,
    ref int failedCommands)
    {
        AllyRole2A[] allyRoles =
        {
        AllyRole2A.Ranger,
        AllyRole2A.Military,
        AllyRole2A.Trader,
        AllyRole2A.Science,
        AllyRole2A.Medic
    };

        for (int i = 0; i < attempts; i++)
        {
            bool spawned;

            if (debugNpcStressSpawnAllies &&
                debugNpcStressSpawnEnemyGroups)
            {
                if (i % 2 == 0)
                {
                    AllyRole2A role =
                        allyRoles[i % allyRoles.Length];

                    spawned =
                        populationService.DebugSpawnAllyInCurrentSystem(role);
                }
                else
                {
                    spawned =
                        populationService.DebugSpawnEnemyAttackGroupInCurrentSystem();
                }
            }
            else if (debugNpcStressSpawnAllies)
            {
                AllyRole2A role =
                    allyRoles[i % allyRoles.Length];

                spawned =
                    populationService.DebugSpawnAllyInCurrentSystem(role);
            }
            else
            {
                spawned =
                    populationService.DebugSpawnEnemyAttackGroupInCurrentSystem();
            }

            if (spawned)
                spawnedCommands++;
            else
                failedCommands++;
        }
    }

    private bool TryGetDebugNpcStressServices(
        out ISystemNpcPopulationService populationService,
        out ISystemNpcRuntimeService npcRuntimeService)
    {
        populationService = null;
        npcRuntimeService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[Bootstrapper] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcPopulationService>(
                out populationService) ||
            populationService == null)
        {
            DebugCombatWarning("[Bootstrapper] ISystemNpcPopulationService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet<ISystemNpcRuntimeService>(
                out npcRuntimeService) ||
            npcRuntimeService == null)
        {
            DebugCombatWarning("[Bootstrapper] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        return true;
    }

    private int GetDebugNpcRuntimeCount(
        ISystemNpcRuntimeService npcRuntimeService)
    {
        if (npcRuntimeService == null ||
            npcRuntimeService.Npcs == null)
        {
            return 0;
        }

        return npcRuntimeService.Npcs.Count;
    }

    private void ValidateDebugNpcRuntimeState(
    SystemNpcRuntimeState npc,
    int index,
    IConfigService configService,
    ref int issueCount)
    {
        if (npc == null)
        {
            issueCount++;
            DebugCombatWarning("[Bootstrapper] NPC validation issue. Index: " + index + ", NPC is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
        {
            issueCount++;
            DebugCombatWarning("[Bootstrapper] NPC validation issue. RuntimeNpcId is empty. Index: " + index);
        }

        if (string.IsNullOrWhiteSpace(npc.ConfigId))
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. ConfigId is empty. RuntimeNpcId: " +
                npc.RuntimeNpcId);
        }
        else if (!IsDebugNpcConfigValid(npc, configService))
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. Config not found. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", Type: " +
                npc.NpcType +
                ", ConfigId: " +
                npc.ConfigId);
        }

        if (string.IsNullOrWhiteSpace(npc.CurrentSystemId) ||
            configService.GetStarSystemConfigById(npc.CurrentSystemId) == null)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. CurrentSystemId invalid. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", CurrentSystemId: " +
                npc.CurrentSystemId);
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId) &&
            configService.GetStarSystemConfigById(npc.TargetSystemId) == null)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. TargetSystemId invalid. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", TargetSystemId: " +
                npc.TargetSystemId);
        }

        if (npc.IsOnPlanet &&
            string.IsNullOrWhiteSpace(npc.CurrentPlanetId))
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. NPC is on planet but CurrentPlanetId is empty. RuntimeNpcId: " +
                npc.RuntimeNpcId);
        }

        if (!string.IsNullOrWhiteSpace(npc.CurrentPlanetId) &&
            configService.GetPlanetConfigById(npc.CurrentPlanetId) == null)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. CurrentPlanetId invalid. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", CurrentPlanetId: " +
                npc.CurrentPlanetId);
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId) &&
            configService.GetPlanetConfigById(npc.TargetPlanetId) == null)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. TargetPlanetId invalid. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", TargetPlanetId: " +
                npc.TargetPlanetId);
        }

        if (npc.IsAlive &&
            npc.LifeState != SystemNpcLifeState.Alive)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. IsAlive=true but LifeState is not Alive. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", LifeState: " +
                npc.LifeState);
        }

        if (!npc.IsAlive &&
            npc.LifeState == SystemNpcLifeState.Alive)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. IsAlive=false but LifeState is Alive. RuntimeNpcId: " +
                npc.RuntimeNpcId);
        }

        if (npc.CurrentHull < 0 ||
            npc.CurrentShield < 0 ||
            npc.CurrentEnergy < 0)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. Negative stats. RuntimeNpcId: " +
                npc.RuntimeNpcId +
                ", Hull: " +
                npc.CurrentHull +
                ", Shield: " +
                npc.CurrentShield +
                ", Energy: " +
                npc.CurrentEnergy);
        }

        if (npc.CurrentHull > npc.MaxHull ||
            npc.CurrentShield > npc.MaxShield ||
            npc.CurrentEnergy > npc.MaxEnergy)
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. Current stats exceed max stats. RuntimeNpcId: " +
                npc.RuntimeNpcId);
        }

        if (npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem &&
            string.IsNullOrWhiteSpace(npc.TargetSystemId))
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. TravelingToAnotherSystem without TargetSystemId. RuntimeNpcId: " +
                npc.RuntimeNpcId);
        }

        if (npc.IsFighting &&
            string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
        {
            issueCount++;
            DebugCombatWarning(
                "[Bootstrapper] NPC validation issue. IsFighting=true but CurrentTargetRuntimeNpcId is empty. RuntimeNpcId: " +
                npc.RuntimeNpcId);
        }
    }

    private bool IsDebugNpcConfigValid(
        SystemNpcRuntimeState npc,
        IConfigService configService)
    {
        if (npc == null ||
            configService == null ||
            string.IsNullOrWhiteSpace(npc.ConfigId))
        {
            return false;
        }

        if (npc.IsEnemy)
            return configService.GetEnemyConfigById(npc.ConfigId) != null;

        if (npc.IsAlly)
            return configService.GetAllyConfigById(npc.ConfigId) != null;

        if (npc.IsPirate)
            return configService.GetPirateConfigById(npc.ConfigId) != null;

        return false;
    }
}
