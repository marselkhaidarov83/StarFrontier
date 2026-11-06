using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StationNpcWeaponCombatEditModeTests
{
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Stations : IStationService
    {
        public StarSystemRuntimeState System;
        public int Notifications;
        public void InitializeSession(bool newGame) { }
        public bool TryGetStation(string id, out StationRuntimeState snapshot)
        { snapshot = System != null && id == System.SystemId ? System.Station.Copy() : null; return snapshot != null; }
        public bool TryStartOperation(string id, StationOperationKind kind, string option, int level, long revision, out string reason)
        { reason = "Unused"; return false; }
        public void ReconcileSystem(string id) { Notifications++; }
        public void AdvanceTime(double seconds) { }
        public void ProcessOffline(GameRuntimeState state, long now) { }
    }
    private Session _session;
    private StarSystemRuntimeState _system;
    private Stations _stations;
    private StationCombatService2A _combat;
    private StationConfig _stationConfig;
    private StationTypeConfig _typeConfig;
    private StarSystemConfig _systemConfig;
    private readonly List<Object> _assets = new List<Object>();
    private SimpleEventBus _events;
    private int _saveRequests;
    private T Asset<T>() where T : ScriptableObject { var result = ScriptableObject.CreateInstance<T>(); _assets.Add(result); return result; }
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static void Id(BaseConfig config, string id) => typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, id);
    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static object Call(object target, string name, params object[] args)
    {
        var matches = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => method.Name == name && MatchesSignature(method.GetParameters(), args)).ToArray();
        Assert.That(matches.Length, Is.EqualTo(1), "Expected one matching overload of " + target.GetType().Name + "." + name);
        return matches[0].Invoke(target, args);
    }
    private static bool MatchesSignature(ParameterInfo[] parameters, object[] args)
    {
        if (parameters.Length != args.Length) return false;
        for (int i = 0; i < parameters.Length; i++)
        {
            Type type = parameters[i].ParameterType;
            if (type.IsByRef) type = type.GetElementType();
            if (args[i] == null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) return false;
            }
            else if (!type.IsInstanceOfType(args[i])) return false;
        }
        return true;
    }
    [SetUp] public void Setup()
    {
        _session = new Session(); _events = new SimpleEventBus(); _saveRequests = 0;
        _events.Subscribe<SaveNeedEvent>(_ => _saveRequests++);
        _system = new StarSystemRuntimeState { SystemId = "sys", Station = new StationRuntimeState { ConfigId = "station",
            StationType = StationType.Military, Level = 1, HealthVersion = 1, HealthLevel = 1, Health = 100, MaxHealth = 100 } };
        _session.State.Galaxy.Systems.Add(_system);
        _typeConfig = Asset<StationTypeConfig>(); Field(_typeConfig, "stationType", StationType.Military);
        _stationConfig = Asset<StationConfig>(); Id(_stationConfig, "station"); Field(_stationConfig, "stationTypeConfig", _typeConfig);
        Field(_stationConfig, "overrideLocalOffset", true); Field(_stationConfig, "localOffsetOverride", new Vector2(20, 0));
        _systemConfig = Asset<StarSystemConfig>(); Id(_systemConfig, "sys"); Field(_systemConfig, "station", _stationConfig);
        _stations = new Stations { System = _system };
        _combat = new StationCombatService2A(_session, id => id == "sys" ? _systemConfig : null, _stations,
            new DamageService2A(), (id, amount) => { _system.ApplyInfrastructureDamageFromWar(amount, false); return true; }, _events);
    }
    [TearDown] public void Cleanup() { foreach (var asset in _assets) Object.DestroyImmediate(asset); _assets.Clear(); }
    private SystemNpcRuntimeState Enemy(SystemNpcBehaviorType behavior = SystemNpcBehaviorType.AttackMilitaryStation) =>
        new SystemNpcRuntimeState { RuntimeNpcId = "enemy", NpcType = SystemNpcType.Enemy, IsAlive = true,
            CurrentSystemId = "sys", CurrentBehavior = behavior, HasActiveBehavior = true, CurrentPosition = new Vector3(200, 0, -2),
            Weapons = new List<SystemNpcWeaponRuntimeState> { new SystemNpcWeaponRuntimeState { WeaponConfigId = "weapon", ShotDistance = 100 } } };
    private SystemNpcCombatService WeaponService()
    {
        var result = Uninitialized<SystemNpcCombatService>();
        Field(result, "_stationCombat", _combat); Field(result, "_eventBus", _events);
        Field(result, "_activeProjectiles", new List<GalaxyNpcProjectileRuntimeState>());
        return result;
    }
    [TestCase(SystemNpcBehaviorType.AttackMilitaryStation, StationType.Military)]
    [TestCase(SystemNpcBehaviorType.AttackRangerBaseStation, StationType.RangerBase)]
    [TestCase(SystemNpcBehaviorType.AttackTradeStation, StationType.Trade)]
    [TestCase(SystemNpcBehaviorType.AttackScienceStation, StationType.Science)]
    [TestCase(SystemNpcBehaviorType.AttackMedicalStation, StationType.Medical)]
    public void EveryStationBehavior_SelectsOnlyBuiltMatchingStationAndSetsActiveCombat(SystemNpcBehaviorType behavior, StationType type)
    {
        _system.Station.StationType = type; Field(_typeConfig, "stationType", type);
        var npc = Enemy(behavior);
        var target = _combat.FindTarget(npc);
        Assert.That(target.IsValid, Is.True); Assert.That(target.TargetType, Is.EqualTo(CombatTargetType.Station));
        Assert.That(target.TargetNpcId, Is.EqualTo("station"));
        var behaviors = Uninitialized<SystemNpcBehaviorService>(); Field(behaviors, "_stationCombat", _combat);
        Assert.That(Call(behaviors, "CanUseBehaviorInCurrentConditions", npc, behavior), Is.EqualTo(true));
        Call(behaviors, "SetupStationAttack", npc);
        Assert.That(npc.IsFighting, Is.True); Assert.That(npc.TravelState, Is.EqualTo(SystemNpcTravelState.EngagingEnemy));
        Assert.That(npc.CurrentTargetRuntimeNpcId, Is.Null); Assert.That(npc.BehaviorTargetRuntimeNpcId, Is.Null);
        Assert.That(SystemNpcMovementTime2A.IsMovingBehavior(behavior), Is.True);
        _system.Station.Level = 0;
        Assert.That(_combat.FindTarget(npc).IsValid, Is.False);
        Assert.That(Call(behaviors, "CanUseBehaviorInCurrentConditions", npc, behavior), Is.EqualTo(false));
        _system.Station.Level = 1; _system.Station.IsDestroyed = true;
        Assert.That(_combat.FindTarget(npc).IsValid, Is.False);
        Assert.That(Call(behaviors, "CanUseBehaviorInCurrentConditions", npc, behavior), Is.EqualTo(false));
    }
    [Test] public void StationAttack_ApproachesStationAtWeaponRangeInsteadOfChasingShips()
    {
        var route = Uninitialized<SystemNpcMovementRouteService>(); Field(route, "_stationCombat", _combat);
        var npc = Enemy(); Vector3 point = route.GetNextTargetPosition(npc);
        Vector3 station = _stationConfig.LocalOffset; station.z = point.z;
        Assert.That(Vector3.Distance(point, station), Is.EqualTo(90).Within(.01));
        Assert.That(point.x, Is.LessThan(npc.CurrentPosition.x));
    }
    [TestCase(WeaponShotType2A.RapidEnergyVolley)] [TestCase(WeaponShotType2A.MissileSwarm)] [TestCase(WeaponShotType2A.HeavyProjectile)]
    public void StationProjectile_UsesSharedImpactDamageAndHealthClamping(WeaponShotType2A shot)
    {
        var weapons = WeaponService(); int popups = 0; _events.Subscribe<CombatDamagePopupEvent2A>(e => { if (e.TargetType == CombatTargetType.Station) popups++; });
        var projectile = new GalaxyNpcProjectileRuntimeState { ProjectileId = "shot", SystemId = "sys", ShooterType = CombatShooterType.Npc,
            ShooterNpcId = "enemy", TargetType = CombatTargetType.Station, TargetNpcId = "station", WeaponConfigId = "weapon",
            ShotType = shot, Damage = 30, ShouldDamageOnArrival = true };
        Call(weapons, "ResolveProjectile", projectile, true);
        Assert.That(_system.Station.Health, Is.EqualTo(70)); Assert.That(popups, Is.EqualTo(1)); Assert.That(_saveRequests, Is.EqualTo(1));
        projectile = new GalaxyNpcProjectileRuntimeState { ProjectileId = "killing_shot", SystemId = "sys", ShooterType = CombatShooterType.Npc,
            ShooterNpcId = "enemy", TargetType = CombatTargetType.Station, TargetNpcId = "station", Damage = 999, ShouldDamageOnArrival = true };
        Call(weapons, "ResolveProjectile", projectile, true);
        Assert.That(_system.Station.IsDestroyed, Is.True); Assert.That(_system.Station.Health, Is.Zero);
        Assert.That(_combat.ApplyDamage("sys", "station", 30, "enemy", "weapon", "Projectile", "late"), Is.Zero);
        Assert.That(_saveRequests, Is.EqualTo(2));
    }
    [Test] public void StationBeam_UsesSharedBeamDamageAndRejectsDestroyedTargetPosition()
    {
        var weapons = WeaponService();
        var beam = new CombatBeamRuntimeState2A { BeamId = "beam", SystemId = "sys", TargetType = CombatTargetType.Station,
            TargetNpcId = "station", ShooterType = CombatShooterType.Npc, ShooterNpcId = "enemy" };
        Call(weapons, "ApplyBeamDamage", beam, 17);
        Assert.That(_system.Station.Health, Is.EqualTo(83));
        _system.Station.IsDestroyed = true;
        var args = new object[] { beam, Vector3.zero };
        Assert.That(Call(weapons, "TryGetBeamTargetPosition", args), Is.EqualTo(false));
    }
    [Test] public void StationWave_HitsOnlyWhenRadiusReachesStationAndOnlyOnce()
    {
        var weapons = WeaponService();
        var wave = new CombatWaveRuntimeState2A { WaveId = "wave", SystemId = "sys", TargetType = CombatTargetType.Station,
            PrimaryTargetNpcId = "station", ShooterType = CombatShooterType.Npc, ShooterNpcId = "enemy", Damage = 21, CurrentRadius = 10 };
        Call(weapons, "ApplyWaveDamage", wave); Assert.That(_system.Station.Health, Is.EqualTo(100));
        wave.CurrentRadius = 30; Call(weapons, "ApplyWaveDamage", wave); Call(weapons, "ApplyWaveDamage", wave);
        Assert.That(_system.Station.Health, Is.EqualTo(79)); Assert.That(wave.StationDamaged, Is.True); Assert.That(_saveRequests, Is.EqualTo(1));
    }
    [Test] public void StationShots_RespectWeaponRangeAndOncePerTickCooldown()
    {
        var weapons = WeaponService();
        var weapon = Asset<WeaponConfig>(); Id(weapon, "weapon");
        Field(weapon, "rangeMin", 100f); Field(weapon, "rangeMax", 100f);
        Field(weapon, "baseDamageMin", 7); Field(weapon, "baseDamageMax", 7);
        Field(weapon, "autoDetectShotType", false); Field(weapon, "shotType", WeaponShotType2A.HeavyProjectile);
        var config = Uninitialized<ConfigService>(); Field(config, "_weaponsById", new Dictionary<string, WeaponConfig> { { "weapon", weapon } });
        Field(weapons, "_configService", config);
        var npc = Enemy(); var runtimeWeapon = npc.Weapons[0]; var target = _combat.FindTarget(npc);
        Assert.That(Call(weapons, "TryCreateProjectile", npc, target, runtimeWeapon, 20, false), Is.EqualTo(false));
        Assert.That(runtimeWeapon.LastShotTick, Is.EqualTo(-1));
        npc.CurrentPosition = target.Position + Vector3.right * 50;
        Assert.That(Call(weapons, "TryCreateProjectile", npc, target, runtimeWeapon, 20, false), Is.EqualTo(true));
        Assert.That(Call(weapons, "TryCreateProjectile", npc, target, runtimeWeapon, 20, false), Is.EqualTo(false));
        Assert.That(Call(weapons, "TryCreateProjectile", npc, target, runtimeWeapon, 21, false), Is.EqualTo(true));
        var shots = (List<GalaxyNpcProjectileRuntimeState>)typeof(SystemNpcCombatService).GetField("_activeProjectiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapons);
        Assert.That(shots.Count, Is.EqualTo(2)); Assert.That(shots[0].TargetType, Is.EqualTo(CombatTargetType.Station)); Assert.That(shots[0].Damage, Is.EqualTo(7));
    }
    [Test] public void UnbuiltStation_IsNotDamagedAndDoesNotProtectCaptureWithoutMilitary()
    {
        _system.Station.Level = 0;
        string before = JsonUtility.ToJson(_system);
        Assert.That(_combat.ApplyDamage("sys", "station", 25, "enemy", "weapon", "Projectile", "shot"), Is.Zero);
        _system.ApplyInfrastructureDamageFromWar(25, false);
        Assert.That(JsonUtility.ToJson(_system), Is.EqualTo(before)); Assert.That(_saveRequests, Is.Zero);
        Assert.That(_system.Station.CanBeAttacked, Is.False);
        Assert.That(StationInfrastructureRules.CanCapture(_system, false), Is.True);
        Assert.That(StationInfrastructureRules.CanCapture(_system, true), Is.False);
    }
    [Test] public void WrongStationIdentityOrTypeCannotBeAttacked()
    {
        Assert.That(_combat.FindTarget(Enemy(SystemNpcBehaviorType.AttackScienceStation)).IsValid, Is.False);
        Assert.That(_combat.ApplyDamage("sys", "different_station", 25, "enemy", "weapon", "Beam", "beam"), Is.Zero);
        _system.Station.Recovery = new StationOperationRuntimeState { OperationId = "recovery", Kind = StationOperationKind.Recovery };
        Assert.That(_combat.FindTarget(Enemy()).IsValid, Is.False);
    }
    [TestCase(StarSystemStatus.Stable, true, AllyBehaviourScenario.EnemyInvasion)]
    [TestCase(StarSystemStatus.Threat, true, AllyBehaviourScenario.EnemyInvasion)]
    [TestCase(StarSystemStatus.Invasion, true, AllyBehaviourScenario.EnemyInvasion)]
    [TestCase(StarSystemStatus.Captured, true, AllyBehaviourScenario.EnemySystemInvasion)]
    [TestCase(StarSystemStatus.Captured, false, AllyBehaviourScenario.EnemySystemInvasion)]
    [TestCase(StarSystemStatus.Stable, false, AllyBehaviourScenario.Normal)]
    public void EnemyScenario_UsesOwnershipOfAttackedSystem(StarSystemStatus status, bool hasEnemies,
        AllyBehaviourScenario expected)
    {
        Type serviceType = typeof(SystemNpcBehaviorService);
        Type contextType = serviceType.GetNestedType("SystemNpcScenarioContext", BindingFlags.NonPublic);
        object context = Activator.CreateInstance(contextType);
        Field(context, "SystemId", "sys");
        Field(context, "SystemStatus", status);
        Field(context, "HasSystemStatus", true);
        Field(context, "HasThreatStatus", status == StarSystemStatus.Threat ||
            status == StarSystemStatus.Invasion || status == StarSystemStatus.Captured);
        Field(context, "HasEnemies", hasEnemies);
        var service = Uninitialized<SystemNpcBehaviorService>();
        var method = serviceType.GetMethod("ResolveBehaviorScenario", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(SystemNpcRuntimeState), contextType }, null);
        Assert.That(method.Invoke(service, new[] { (object)Enemy(), context }), Is.EqualTo(expected));
    }

}
