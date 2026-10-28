using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class S04_02_SystemMapCombatLifecycleTests
{
    private GameObject _bootstrapRoot;
    private ServiceRegistry _registry;
    private SimpleEventBus _eventBus;
    private SystemEncounterService _encounterService;
    private SystemEnemyService _enemyService;
    private TestPlayerCombatTargetService _playerTargetService;
    private StarSystemConfig _rewardSystem;
    private PlanetConfig _rewardPlanet;

    [SetUp]
    public void SetUp()
    {
        _bootstrapRoot = new GameObject("S04_02_TestBootstrap");
        _bootstrapRoot.SetActive(false);

        Bootstrapper bootstrapper = _bootstrapRoot.AddComponent<Bootstrapper>();
        _registry = new ServiceRegistry();

        bootstrapper.ServiceRegistry = _registry;
        Bootstrapper.Instance = bootstrapper;

        _eventBus = new SimpleEventBus();
        _registry.Register(_eventBus);

        _encounterService = new SystemEncounterService();
        _registry.Register<ISystemEncounterService>(_encounterService);

        _playerTargetService = new TestPlayerCombatTargetService(
            "system_test",
            Vector3.zero,
            hull: 40,
            shield: 10,
            _encounterService);

        _registry.Register<IPlayerCombatTargetService>(_playerTargetService);

        _enemyService = new SystemEnemyService();
        _registry.Register<ISystemEnemyService>(_enemyService);
    }

    [TearDown]
    public void TearDown()
    {
        Bootstrapper.Instance = null;

        if (_rewardSystem != null)
            Object.DestroyImmediate(_rewardSystem);
        if (_rewardPlanet != null)
            Object.DestroyImmediate(_rewardPlanet);

        if (_bootstrapRoot != null)
            Object.DestroyImmediate(_bootstrapRoot);
    }

    [Test]
    public void EnemyTick_DamagesPlayer_OnSystemMap()
    {
        _encounterService.StartEncounter(
            "encounter_test",
            "system_test",
            enemyCount: 1,
            allyCount: 0);

        SystemEnemyRuntimeState enemy =
            CreateReadyEnemy("enemy_test_01", Vector3.zero);

        _enemyService.TickSystemMapCombat(0.1f);

        Assert.AreEqual(
            1,
            _playerTargetService.DamageCallCount);

        Assert.AreEqual(
            5,
            _playerTargetService.CurrentShield);

        Assert.AreEqual(
            40,
            _playerTargetService.CurrentHull);

        Assert.IsTrue(enemy.IsAlive);
    }

    [Test]
    public void EnemyDeath_ByPlayer_MovesEncounterToVictoryPendingReward()
    {
        int destroyedEventCount = 0;

        _eventBus.Subscribe<SystemEnemyDestroyedEvent>(
            _ => destroyedEventCount++);

        _encounterService.StartEncounter(
            "encounter_test",
            "system_test",
            enemyCount: 1,
            allyCount: 0);

        SystemEnemyRuntimeState enemy =
            CreateReadyEnemy("enemy_test_01", Vector3.zero);

        _enemyService.ApplyDamage(
            enemy.RuntimeEnemyId,
            damage: 9999,
            fromPlayer: true);

        Assert.IsFalse(enemy.IsAlive);

        Assert.AreEqual(
            SystemEncounterState.VictoryPendingReward,
            _encounterService.Current.State);

        Assert.AreEqual(
            1,
            _encounterService.Current.PlayerKills);

        Assert.AreEqual(
            1,
            destroyedEventCount);
    }

    [Test]
    public void PlayerDestroyed_ByEnemy_MovesEncounterToDefeated()
    {
        _playerTargetService.SetCombatState(
            hull: 4,
            shield: 0);

        _encounterService.StartEncounter(
            "encounter_test",
            "system_test",
            enemyCount: 1,
            allyCount: 0);

        CreateReadyEnemy("enemy_test_01", Vector3.zero);

        _enemyService.TickSystemMapCombat(0.1f);

        Assert.AreEqual(
            SystemEncounterState.Defeated,
            _encounterService.Current.State);

        Assert.AreEqual(
            SystemEncounterDefeatReason.PlayerDestroyed,
            _encounterService.Current.DefeatReason);

        Assert.IsTrue(
            _encounterService.Current.PlayerDestroyed);
    }

    [Test]
    public void PlayerLeavesSystem_WithAliveEnemies_MovesEncounterToDefeated()
    {
        _encounterService.StartEncounter(
            "encounter_test",
            "system_test",
            enemyCount: 1,
            allyCount: 0);

        CreateReadyEnemy("enemy_test_01", Vector3.zero);

        _encounterService.RegisterPlayerLeftSystem("system_test");

        Assert.AreEqual(
            SystemEncounterState.Defeated,
            _encounterService.Current.State);

        Assert.AreEqual(
            SystemEncounterDefeatReason.PlayerLeftSystem,
            _encounterService.Current.DefeatReason);
    }

    [Test]
    public void Reward_CanBeClaimedOnlyOnce()
    {
        _encounterService.StartEncounter(
            "encounter_test",
            "system_test",
            enemyCount: 1,
            allyCount: 0);

        SystemEnemyRuntimeState enemy =
            CreateReadyEnemy("enemy_test_01", Vector3.zero);

        _enemyService.ApplyDamage(
            enemy.RuntimeEnemyId,
            damage: 9999,
            fromPlayer: true);

        Assert.AreEqual(SystemEncounterState.VictoryPendingReward, _encounterService.Current.State);

        // Government rewards now use the saved mission ledger and the actual player wallet.
        var state = new GameRuntimeState
        {
            Player = new PlayerState(),
            Galaxy = new GalaxyRuntimeState(),
            MissionBlock = new RuntimeMissionSaveBlock()
        };
        state.Player.CurrentSystemId = "system_test";
        state.Player.CurrentPlanetId = "planet_test";
        state.Player.Credits = 1000;
        state.Player.Experience = 0;
        state.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "system_test", InvasionGeneration = 0 });
        var mission = new MissionInstanceData
        {
            MissionRuntimeId = "mission_reward_once", MissionType = MissionType.Defense,
            Status = MissionStatus.ReadyToComplete, IsReadyToTurnIn = true,
            SourceSystemId = "system_test", TargetSystemId = "system_test",
            Reward = new MissionRewardData { Credits = 100, Xp = 0 },
            Objective = new MissionObjectiveData { RequiredAmount = 1, CurrentAmount = 1,
                BattleEncounterId = "encounter_test", BattleWon = true, BattleInvasionGeneration = 0, BattleRewardDeflator = 100 }
        };
        state.MissionBlock.ActiveMissions.Add(mission);
        _registry.Register(RewardDependencyProxy.Make<IGameSessionService>((method, args) =>
            method.Name == "get_State" ? state : RewardDependencyProxy.DefaultValue(method)));
        bool saving = true;
        _registry.Register(RewardDependencyProxy.Make<ISaveService>((method, args) =>
        {
            if (method.Name == "EnableSave") { saving = (bool)args[0]; return null; }
            return method.Name == "get_IsSaveEnabled" ? saving : RewardDependencyProxy.DefaultValue(method);
        }));
        _rewardPlanet = ScriptableObject.CreateInstance<PlanetConfig>();
        _rewardSystem = ScriptableObject.CreateInstance<StarSystemConfig>();
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BaseConfig).GetField("id", fields).SetValue(_rewardPlanet, "planet_test");
        typeof(BaseConfig).GetField("id", fields).SetValue(_rewardSystem, "system_test");
        typeof(PlanetConfig).GetField("isInhabited", fields).SetValue(_rewardPlanet, true);
        typeof(StarSystemConfig).GetField("planetRefs", fields).SetValue(_rewardSystem,
            new[] { _rewardPlanet });
        _registry.Register(RewardDependencyProxy.Make<IConfigService>((method, args) =>
        {
            if (method.Name == "GetStarSystemConfigById")
                return (string)args[0] == "system_test" ? _rewardSystem : null;
            if (method.Name == "GetPlanetConfigById")
                return (string)args[0] == "planet_test" ? _rewardPlanet : null;
            return RewardDependencyProxy.DefaultValue(method);
        }));
        _registry.Register(RewardDependencyProxy.Make<IRouteService>());
        _registry.Register(RewardDependencyProxy.Make<ITravelService>());
        _registry.Register(RewardDependencyProxy.Make<ISystemNpcRuntimeService>());
        _registry.Register<IInventoryService>(new InventoryService());
        _registry.Register<IRewardService>(new RewardService());
        _registry.Register<IMissionService>(new MissionService());

        // Keep a legacy payout observer to detect an accidental duplicate payment path.
        var payoutService = new TestGovernmentRewardPayoutService();
        _registry.Register<IGovernmentRewardPayoutService>(payoutService);

        GovernmentRewardService rewardService =
            new GovernmentRewardService();

        GovernmentRewardResult firstClaim =
            rewardService.ClaimReward(
                "system_test",
                isCurrentPlanetInhabited: true);

        GovernmentRewardResult secondClaim =
            rewardService.ClaimReward(
                "system_test",
                isCurrentPlanetInhabited: true);

        Assert.IsTrue(firstClaim.Success);
        Assert.IsFalse(secondClaim.Success);

        Assert.AreEqual(100, firstClaim.Credits);
        Assert.AreEqual(0, firstClaim.Xp);
        Assert.AreEqual(0, secondClaim.Credits);
        Assert.AreEqual(1100, state.Player.Credits);
        Assert.AreEqual(0, state.Player.Experience);
        Assert.AreEqual(1, state.MissionBlock.RewardGrantedMissionIds.Count);
        Assert.Contains(mission.MissionRuntimeId, state.MissionBlock.RewardGrantedMissionIds);
        Assert.IsEmpty(state.MissionBlock.ActiveMissions);
        Assert.AreEqual(0, payoutService.GrantCallCount);
        Assert.IsTrue(saving);

        Assert.AreEqual(
            SystemEncounterState.Resolved,
            _encounterService.Current.State);
    }

    [Test]
    public void ShieldAndHull_DoNotAutoRestore_DuringActiveEncounter()
    {
        _encounterService.StartEncounter(
            "encounter_test",
            "system_test",
            enemyCount: 1,
            allyCount: 0);

        SystemEnemyRuntimeState enemy =
            CreateReadyEnemy("enemy_test_01", new Vector3(999f, 0f, 0f));

        _playerTargetService.ApplyDamage(6);

        int shieldAfterDamage =
            _playerTargetService.CurrentShield;

        int hullAfterDamage =
            _playerTargetService.CurrentHull;

        enemy.AttackTimerSeconds = 10f;

        _enemyService.TickSystemMapCombat(0.1f);
        _enemyService.TickSystemMapCombat(0.1f);
        _enemyService.TickSystemMapCombat(0.1f);

        Assert.AreEqual(
            shieldAfterDamage,
            _playerTargetService.CurrentShield);

        Assert.AreEqual(
            hullAfterDamage,
            _playerTargetService.CurrentHull);
    }

    private SystemEnemyRuntimeState CreateReadyEnemy(
        string configId,
        Vector3 position)
    {
        EnemyConfig config =
            CreateEnemyConfig(
                configId,
                hull: 20,
                shield: 0,
                creditReward: 100,
                xpReward: 25,
                dangerTier: 1);

        SystemEnemyRuntimeState enemy =
            _enemyService.CreateEnemy(
                config,
                "system_test",
                position);

        enemy.HasTarget = true;
        enemy.CurrentTargetId = "player";
        enemy.AttackCooldownSeconds = 1f;
        enemy.AttackTimerSeconds = 0f;
        enemy.BaseAttackDamage = 5;
        enemy.AttackRange = 50f;

        return enemy;
    }

    private static EnemyConfig CreateEnemyConfig(
        string id,
        int hull,
        int shield,
        int creditReward,
        int xpReward,
        int dangerTier)
    {
        EnemyConfig config =
            ScriptableObject.CreateInstance<EnemyConfig>();

        SetPrivateField(config, "id", id);
        SetPrivateField(config, "displayName", id);
        SetPrivateField(config, "baseHullMin", hull);
        SetPrivateField(config, "baseHullMax", hull);
        SetPrivateField(config, "baseShieldMin", shield);
        SetPrivateField(config, "baseShieldMax", shield);
        SetPrivateField(config, "baseEnergyMin", 10);
        SetPrivateField(config, "baseEnergyMax", 10);
        SetPrivateField(config, "creditRewardMin", creditReward);
        SetPrivateField(config, "creditRewardMax", creditReward);
        SetPrivateField(config, "xpRewardMin", xpReward);
        SetPrivateField(config, "xpRewardMax", xpReward);
        SetPrivateField(config, "dangerTier", dangerTier);

        return config;
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            field =
                target.GetType().BaseType.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        }

        Assert.NotNull(
            field,
            "Missing private field: " + fieldName);

        field.SetValue(target, value);
    }

    private sealed class TestPlayerCombatTargetService :
        IPlayerCombatTargetService
    {
        private readonly ISystemEncounterService _encounterService;

        public string SystemId { get; private set; }
        public Vector3 Position { get; private set; }

        public int CurrentHull { get; private set; }
        public int CurrentShield { get; private set; }
        public int DamageCallCount { get; private set; }

        public TestPlayerCombatTargetService(
            string systemId,
            Vector3 position,
            int hull,
            int shield,
            ISystemEncounterService encounterService)
        {
            SystemId = systemId;
            Position = position;
            CurrentHull = hull;
            CurrentShield = shield;
            _encounterService = encounterService;
        }

        public bool IsPlayerAvailableInSystem(string systemId)
        {
            return SystemId == systemId && CurrentHull > 0;
        }

        public Vector3 GetPlayerPosition()
        {
            return Position;
        }

        public CombatDamageResult2A ApplyDamage(int damage)
        {
            if (damage <= 0 || CurrentHull <= 0)
                return default;

            DamageCallCount++;

            int remainingDamage = damage;
            int shieldDamage = 0;
            int hullDamage = 0;

            if (CurrentShield > 0)
            {
                shieldDamage =
                    Mathf.Min(CurrentShield, remainingDamage);

                CurrentShield -= shieldDamage;
                remainingDamage -= shieldDamage;
            }

            if (remainingDamage > 0)
            {
                hullDamage =
                    Mathf.Min(CurrentHull, remainingDamage);

                CurrentHull -= hullDamage;
            }

            if (CurrentHull < 0)
                CurrentHull = 0;

            int appliedDamage =
                shieldDamage + hullDamage;

            CombatDamageResult2A result = new CombatDamageResult2A(
                appliedDamage,
                shieldDamage,
                hullDamage,
                CurrentShield,
                CurrentHull,
                CurrentHull <= 0);

            if (result.IsDestroyed)
                _encounterService.RegisterPlayerDestroyed();

            return result;
        }
        public void SetCombatState(int hull, int shield)
        {
            CurrentHull = hull;
            CurrentShield = shield;
        }
    }

    // Public/unsealed so DispatchProxy can derive a dependency stub in the Editor.
    public class RewardDependencyProxy : DispatchProxy
    {
        public System.Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) =>
            Handler == null ? DefaultValue(method) : Handler(method, args);

        public static T Make<T>(System.Func<MethodInfo, object[], object> handler = null) where T : class
        {
            var proxy = Create<T, RewardDependencyProxy>();
            ((RewardDependencyProxy)(object)proxy).Handler = handler;
            return proxy;
        }

        public static object DefaultValue(MethodInfo method) => method.ReturnType == typeof(void) ? null :
            method.ReturnType.IsValueType ? System.Activator.CreateInstance(method.ReturnType) : null;
    }

    private sealed class TestGovernmentRewardPayoutService :
        IGovernmentRewardPayoutService
    {
        public int GrantCallCount { get; private set; }
        public int TotalCredits { get; private set; }
        public int TotalXp { get; private set; }

        public void Grant(int credits, int xp)
        {
            GrantCallCount++;
            TotalCredits += credits;
            TotalXp += xp;
        }
    }
}
