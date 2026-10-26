using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Public and unsealed: DispatchProxy generates a derived class in the Editor.
public class S08MissionTestProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler;
    protected override object Invoke(MethodInfo method, object[] args) => Handler(method, args);
    public static T Make<T>(Func<MethodInfo, object[], object> handler) where T : class
    {
        var instance = Create<T, S08MissionTestProxy>();
        ((S08MissionTestProxy)(object)instance).Handler = handler;
        return instance;
    }
}

public sealed class S08_02_RouteSurveyRuleTests
{
    [Test]
    public void UnlockSurvey_RequiresContinuousIndirectTripAndIgnoresReverseTrip()
    {
        var objective = new MissionObjectiveData();
        RouteMissionRules.ObserveHop(objective, "A", "B", "B", "C");
        RouteMissionRules.ObserveHop(objective, "A", "B", "C", "A");
        Assert.That(objective.RouteIndirectSurveyed, Is.False);
        RouteMissionRules.ObserveHop(objective, "A", "B", "A", "C");
        RouteMissionRules.ObserveHop(objective, "A", "B", "C", "B");
        Assert.That(objective.RouteIndirectSurveyed, Is.True);
        Assert.That(objective.RouteDirectSurveyed, Is.False);
    }

    [Test]
    public void DirectHopWithDetourLoop_DoesNotBecomeAnIndirectSurvey()
    {
        var objective = new MissionObjectiveData();
        foreach (var hop in new[] { ("A", "C"), ("C", "A"), ("A", "B") })
            RouteMissionRules.ObserveHop(objective, "A", "B", hop.Item1, hop.Item2);
        Assert.That(objective.RouteDirectSurveyed, Is.True);
        Assert.That(objective.RouteIndirectSurveyed, Is.False);
    }

    [Test]
    public void BrokenChain_CannotCreditUnrelatedArrival()
    {
        var objective = new MissionObjectiveData();
        RouteMissionRules.ObserveHop(objective, "A", "B", "A", "C");
        RouteMissionRules.ObserveHop(objective, "A", "B", "X", "B");
        Assert.That(objective.RouteIndirectSurveyed, Is.False);
    }

    [Test]
    public void BattleRewardLedger_OnlyExpiresOnLaterInvasionGeneration()
    {
        var objective = new MissionObjectiveData { BattleWon = true, BattleInvasionGeneration = 7 };
        Assert.That(MissionBattleRules.RewardExpired(objective, 7), Is.False);
        Assert.That(MissionBattleRules.RewardExpired(objective, 8), Is.True);
        Assert.That(MissionBattleRules.Credits(3, 125), Is.EqualTo(375));
        Assert.That(MissionBattleRules.Credits(int.MaxValue, 125), Is.EqualTo(int.MaxValue));
    }
}

public sealed partial class S08_02_BattleAndRouteEditModeTests
{
    private readonly List<UnityEngine.Object> _objects = new();
    private Bootstrapper _previousBootstrap;
    private ServiceRegistry _registry;
    private SimpleEventBus _bus;
    private GameRuntimeState _state;
    private MissionService _missions;
    private MissionTracker _tracker;
    private SystemEncounterService _encounter;
    private StarSystemConfig[] _systems;
    private SectorConfig _sector;
    private RouteConfig _direct;
    private bool _saving;
    private int _worldVictories;
    private readonly List<ItemConfig> _testItems = new();
    private readonly List<SystemNpcRuntimeState> _testNpcs = new();
    private PlanetConfig[] _testPlanets;
    private Vector3 _testPlayerPosition;

    [SetUp]
    public void SetUp()
    {
        _previousBootstrap = Bootstrapper.Instance;
        var host = new GameObject("S08 mission test"); _objects.Add(host); host.SetActive(false);
        var bootstrap = host.AddComponent<Bootstrapper>();
        Bootstrapper.Instance = bootstrap;
        _registry = new ServiceRegistry(); bootstrap.ServiceRegistry = _registry;
        _bus = new SimpleEventBus(); _registry.Register(_bus);
        _systems = new[] { Config<StarSystemConfig>("A"), Config<StarSystemConfig>("B"), Config<StarSystemConfig>("C") };
        _testItems.Clear(); _testNpcs.Clear(); _testPlayerPosition = Vector3.zero;
        _testPlanets = _systems.Select(s => Config<PlanetConfig>("planet_" + s.Id)).ToArray();
        for (int i = 0; i < _systems.Length; i++)
        {
            SetField(_testPlanets[i], "isInhabited", true);
            SetField(_systems[i], "planetRefs", new[] { _testPlanets[i] });
        }
        _sector = Config<SectorConfig>("sector"); SetField(_sector, "systems", _systems);
        _direct = Route("AB", _systems[0], _systems[1], true, 4);
        Route("AC", _systems[0], _systems[2], false, 2);
        Route("CB", _systems[2], _systems[1], false, 2);
        var gameplay = Config<MissionGameplayConfig>("missions"); gameplay.DefenseKillRewardDeflator = 125; gameplay.LiberationKillRewardDeflator = 175;
        var game = Config<GameConfig>("game"); game.MissionGameplay = gameplay;
        _state = new GameRuntimeState { Player = new PlayerState { CurrentSystemId = "A", CurrentPlanetId = "", Credits = 0 },
            Galaxy = new GalaxyRuntimeState(), MissionBlock = new RuntimeMissionSaveBlock() };
        _state.Galaxy.Systems = _systems.Select(s => new StarSystemRuntimeState { SystemId = s.Id, IsDiscovered = true }).ToList();
        _state.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "sector", IsUnlocked = true });
        _state.Galaxy.Routes = RouteMissionRules.AllRoutes(_systems).Select(r => new RouteRuntimeState { RouteId = r.Id, IsUnlocked = !r.IsLockedAtStart }).ToList();
        _registry.Register(S08MissionTestProxy.Make<IGameSessionService>((m, a) => m.Name == "get_State" ? _state : Default(m)));
        _registry.Register(S08MissionTestProxy.Make<IConfigService>((m, a) => m.Name switch
        {
            "get_GameConfig" => game, "GetAllStarSystems" => _systems, "GetAllSectors" => new[] { _sector },
            "GetStarSystemConfigById" => _systems.FirstOrDefault(s => s.Id == (string)a[0]),
            "GetItemConfigById" => _testItems.Find(i => i.Id == (string)a[0]),
            "GetAllItems" => _testItems,
            "GetAllPlanets" => _testPlanets,
            "GetPlanetConfigById" => _testPlanets.FirstOrDefault(p => p.Id == (string)a[0]),
            _ => Default(m)
        }));
        _saving = true;
        _registry.Register(S08MissionTestProxy.Make<ISaveService>((m, a) =>
        { if (m.Name == "EnableSave") { _saving = (bool)a[0]; return null; } return m.Name == "get_IsSaveEnabled" ? _saving : Default(m); }));
        _registry.Register<IInventoryService>(new InventoryService());
        _registry.Register(S08MissionTestProxy.Make<IRouteService>((m, a) =>
        {
            if (m.Name == "IsRouteUnlocked") return _state.Galaxy.Routes.Find(r => r.RouteId == (string)a[0])?.IsUnlocked == true;
            if (m.Name == "HasUnlockedRoute") return RouteMissionRules.AllRoutes(_systems).Any(r => r.ConnectsSystems((string)a[0], (string)a[1]) && _state.Galaxy.Routes.Find(s => s.RouteId == r.Id).IsUnlocked);
            return Default(m);
        }));
        _registry.Register(S08MissionTestProxy.Make<ITravelService>((m, a) =>
        {
            if (m.Name != "GetTravelCost") return Default(m);
            var route = RouteMissionRules.AllRoutes(_systems).Find(r => r.ConnectsSystems((string)a[0], (string)a[1]));
            return route == null ? 0 : route.GetFuelCost(_state.Galaxy.Routes.Find(r => r.RouteId == route.Id).DevelopmentLevel);
        }));
        _registry.Register(S08MissionTestProxy.Make<ISystemNpcRuntimeService>((m, a) =>
        {
            if (m.Name == "GetAliveNpcsInSystemByType")
                return _testNpcs.Where(n => n.IsAlive && n.CurrentSystemId == (string)a[0] && n.NpcType == (SystemNpcType)a[1]).ToList();
            if (m.Name == "GetAliveNpcsInSystem") return _testNpcs.Where(n => n.IsAlive && n.CurrentSystemId == (string)a[0]).ToList();
            return Default(m);
        }));
        _registry.Register(S08MissionTestProxy.Make<IPlayerCombatTargetService>((m, a) => m.Name switch
        {
            "IsPlayerAvailableInSystem" => _state.Player.CurrentSystemId == (string)a[0] && string.IsNullOrEmpty(_state.Player.CurrentPlanetId),
            "GetPlayerPosition" => _testPlayerPosition, _ => Default(m)
        }));
        _registry.Register(S08MissionTestProxy.Make<IInvasionService>((m, a) =>
        { if (m.Name == "ResolveInvasionFromCombatOutcome") { _worldVictories++; return true; } return Default(m); }));
        _registry.Register(S08MissionTestProxy.Make<ISystemSecurityService>((m, a) =>
        {
            var system = _state.Galaxy.Systems.Find(s => s.SystemId == (string)a[0]);
            if (m.Name == "TryGetSystemStatus") { a[1] = system.SystemStatus; return true; }
            if (m.Name == "LiberateSystemByPlayer") { system.SystemStatus = StarSystemStatus.Stable; return true; }
            return Default(m);
        }));
        _encounter = new SystemEncounterService(); _registry.Register<ISystemEncounterService>(_encounter);
        _registry.Register<IRewardService>(new RewardService());
        _missions = new MissionService(); _registry.Register<IMissionService>(_missions);
        _tracker = new MissionTracker();
        _worldVictories = 0;
    }

    [TearDown]
    public void TearDown()
    {
        _tracker?.Dispose();
        foreach (var item in _objects) UnityEngine.Object.DestroyImmediate(item);
        _objects.Clear(); Bootstrapper.Instance = _previousBootstrap;
    }

    [TestCase(false)] [TestCase(true)]
    public void AllyFinalHit_AfterOnePersonalKillWinsAndPaysExactlyOnce(bool liberation)
    {
        StartBattle(liberation);
        _missions.RecordBattleKill("enemy1:10", "A");
        _missions.RecordBattleKill("enemy1:10", "A"); // duplicate death notification
        _encounter.RegisterEnemyDestroyed(true);
        Assert.That(_missions.GetActiveMissions().Single().Objective.BattleWon, Is.False);
        _encounter.RegisterEnemyDestroyed(false); // ally delivers the final hit
        var mission = _missions.GetActiveMissions().Single();
        Assert.That(mission.Objective.CurrentAmount, Is.EqualTo(1));
        Assert.That(mission.Objective.BattleWon, Is.True);
        Assert.That(_worldVictories, Is.EqualTo(1));
        if (liberation) Assert.That(_state.Galaxy.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Stable));
        _state.Player.CurrentPlanetId = "any_planet"; _missions.ReevaluateActiveMissions("Landing");
        var government = new GovernmentRewardService();
        var firstClaim = government.ClaimReward("A", true);
        Assert.That(firstClaim.Success, Is.True);
        Assert.That(firstClaim.Credits, Is.EqualTo(liberation ? 175 : 125));
        Assert.That(_state.Player.Credits, Is.EqualTo(liberation ? 175 : 125));
        Assert.That(government.ClaimReward("A", true).Success, Is.False);
        Assert.That(_state.Player.Credits, Is.EqualTo(liberation ? 175 : 125));
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds.Count, Is.EqualTo(1));
        Assert.That(_encounter.Current.State, Is.EqualTo(SystemEncounterState.Resolved));
        Assert.That(_saving, Is.True);
    }

    [Test]
    public void NoPersonalKills_CreatesNoMissionAndPaysNothing()
    {
        StartBattle(false); _encounter.RegisterEnemyDestroyed(false); _encounter.RegisterEnemyDestroyed(false);
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(new GovernmentRewardService().CanClaimReward("A", true), Is.False);
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [TestCase(false)] [TestCase(true)]
    public void FlightBeforeBattleEnd_FailsAndLateShotsCannotRestoreMission(bool liberation)
    {
        StartBattle(liberation); _missions.RecordBattleKill("enemy1:10", "A"); _encounter.RegisterEnemyDestroyed(true);
        _state.Player.CurrentSystemId = "C";
        _missions.ObserveMissionTravel(new TravelFinishedEvent("A", "C", true, 2, default));
        _missions.RecordBattleKill("enemy2:11", "A");
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void ParticipationWithoutPersonalLastHitEndsWithoutReward()
    {
        StartBattle(false);
        _missions.ObserveBattleParticipation("A");
        Assert.That(_missions.GetActiveMissions().Single().Objective.CurrentAmount, Is.Zero);
        _encounter.RegisterEnemyDestroyed(false);
        _encounter.RegisterEnemyDestroyed(false);
        _missions.ResolveBattle("battle", "A", true);
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void DefeatWithPersonalKill_DoesNotPay()
    {
        StartBattle(false); _missions.RecordBattleKill("enemy1:10", "A"); _encounter.RegisterEnemyDestroyed(true);
        _encounter.RegisterPlayerDestroyed();
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void WonBattle_SurvivesTravelAndSaveButExpiresOnNextLocalInvasion()
    {
        StartBattle(false); _missions.RecordBattleKill("enemy1:10", "A"); _encounter.RegisterEnemyDestroyed(true); _encounter.RegisterEnemyDestroyed(false);
        var mission = _missions.GetActiveMissions().Single();
        _state.Player.CurrentSystemId = "C"; _missions.ObserveMissionTravel(new TravelFinishedEvent("A", "C", true, 2, default));
        var saved = JsonUtility.ToJson(MissionSaveMapper.ToSaveData(mission));
        _state.MissionBlock.ActiveMissions[0] = MissionSaveMapper.ToRuntimeData(JsonUtility.FromJson<MissionSaveData>(saved));
        _state.Galaxy.Systems[1].InvasionGeneration++; // another system must not invalidate this reward
        _missions.ReevaluateActiveMissions("OtherInvasion");
        Assert.That(_missions.GetActiveMissions().Single().Objective.BattleWon, Is.True);
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "another_planet";
        Assert.That(new GovernmentRewardService().CanClaimReward("A", false), Is.True);
        _state.Galaxy.Systems[0].InvasionGeneration++;
        _bus.Publish(new SystemInvasionStartedMissionEvent("A"));
        Assert.That(new GovernmentRewardService().CanClaimReward("A", true), Is.False);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Expired));
    }

    [Test]
    public void UnlockSurvey_DoesNotOpenOrPayUntilReturnToIssuerPlanet()
    {
        var mission = AcceptRoute(false); Hop("A", "C"); Hop("C", "B");
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked, Is.False);
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Hop("B", "C"); Hop("C", "A"); _state.Player.CurrentPlanetId = "wrong_planet";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        _state.Player.CurrentPlanetId = "issuer";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked, Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(150));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
    }

    [Test]
    public void Upgrade_RequiresBothSurveysAndKeepsFirstSurveyAcrossSaveLoad()
    {
        _state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked = true;
        var mission = AcceptRoute(true); Hop("A", "B"); Hop("B", "A");
        _state.Player.CurrentPlanetId = "issuer";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        var saved = JsonUtility.ToJson(MissionSaveMapper.ToSaveData(mission));
        mission = MissionSaveMapper.ToRuntimeData(JsonUtility.FromJson<MissionSaveData>(saved));
        _state.MissionBlock.ActiveMissions[0] = mission;
        Hop("A", "C"); Hop("C", "B"); Hop("B", "A"); _state.Player.CurrentPlanetId = "issuer";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").DevelopmentLevel, Is.EqualTo(2));
        Assert.That(_direct.GetFuelCost(2), Is.EqualTo(3));
    }

    [Test]
    public void FailedJumpDoesNotProduceSurveyAndHostileTargetCancelsMission()
    {
        var mission = AcceptRoute(false);
        _missions.ObserveMissionTravel(new TravelFinishedEvent("A", "C", false, 0, default));
        Assert.That(mission.Objective.RouteSurveySystemIds, Is.Empty);
        _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        _missions.ReevaluateActiveMissions("TargetCaptured");
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void CandidatesRequireRealOpenDetourAndExcludeMaxLevelOrHostileDestination()
    {
        List<RouteConfig> Candidates(bool upgrade) => RouteMissionRules.BuildCandidates(_state.Galaxy, _systems, "A", upgrade,
            r => _state.Galaxy.Routes.Find(s => s.RouteId == r.Id).IsUnlocked, id => true);
        Assert.That(Candidates(false).Select(r => r.Id), Does.Contain("AB"));
        _state.Galaxy.Routes.Find(r => r.RouteId == "CB").IsUnlocked = false;
        Assert.That(Candidates(false).Select(r => r.Id), Does.Not.Contain("AB"));
        _state.Galaxy.Routes.Find(r => r.RouteId == "CB").IsUnlocked = true;
        var direct = _state.Galaxy.Routes.Find(r => r.RouteId == "AB"); direct.IsUnlocked = true; direct.DevelopmentLevel = 5;
        Assert.That(Candidates(true).Select(r => r.Id), Does.Not.Contain("AB"));
        direct.DevelopmentLevel = 1; _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        Assert.That(Candidates(true).Select(r => r.Id), Does.Not.Contain("AB"));
    }

    [Test]
    public void RouteFuel_HasFiveLevelsAndNeverDropsBelowOne()
    {
        Assert.That(Enumerable.Range(1, 5).Select(_direct.GetFuelCost), Is.EqualTo(new[] { 4, 3, 2, 1, 1 }));
        SetField(_direct, "fuelByDevelopmentLevel", new List<int> { 9, 7, 5, 3, 1 });
        Assert.That(_direct.GetFuelCost(3), Is.EqualTo(5));
    }

    private void StartBattle(bool liberation)
    {
        if (liberation) _state.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;
        else _state.Galaxy.Invasions.Add(new InvasionState { InvasionId = "invasion", TargetSystemId = "A", LifecycleState = InvasionLifecycleState.Active });
        _encounter.StartEncounter("battle", "A", 2, 2);
    }
    private MissionInstanceData AcceptRoute(bool upgrade)
    {
        _state.Player.CurrentPlanetId = "issuer";
        var mission = new MissionInstanceData { MissionRuntimeId = "route_test", MissionType = MissionType.RouteSearch,
            Status = MissionStatus.Available, SourceSystemId = "A", SourcePlanetId = "issuer", TargetSystemId = "B",
            Reward = new MissionRewardData { Credits = 150 }, Objective = new MissionObjectiveData { TargetSystemId = "B",
                RequiredAmount = upgrade ? 2 : 1, RouteId = "AB", RouteIsUpgrade = upgrade, RouteExpectedLevel = 1 } };
        _missions.SetAvailableMissions(new List<MissionInstanceData> { mission });
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        return mission;
    }
    private void Hop(string from, string to)
    {
        _state.Player.CurrentPlanetId = ""; _state.Player.CurrentSystemId = to;
        _missions.ObserveMissionTravel(new TravelFinishedEvent(from, to, true, 1, default));
    }
    private RouteConfig Route(string id, StarSystemConfig a, StarSystemConfig b, bool locked, int fuel)
    {
        var route = Config<RouteConfig>(id); route.FromSystem = a; route.ToSystem = b; route.IsLockedAtStart = locked; route.ParsecDistance = fuel;
        a.Routes.Add(route); b.Routes.Add(route); return route;
    }
    private T Config<T>(string id) where T : BaseConfig
    {
        var config = ScriptableObject.CreateInstance<T>(); _objects.Add(config); SetField(config, "id", id); return config;
    }
    private static void SetField(object instance, string name, object value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(instance, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static object Default(MethodInfo method) => method.ReturnType == typeof(void) ? null :
        method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
}
