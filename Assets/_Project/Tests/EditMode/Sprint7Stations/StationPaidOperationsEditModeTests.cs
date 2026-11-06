using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationPaidOperationsEditModeTests
{
    private readonly List<ScriptableObject> _objects = new List<ScriptableObject>();
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private Session _session;
    private StationService _service;
    private StarSystemRuntimeState _system;
    private StationConfig _station;
    private bool _hasEnemies;
    private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _objects.Add(a); return a; }
    private static void Set(object target, string name, object value)
    {
        var type = target.GetType(); FieldInfo f = null;
        while (type != null && f == null) { f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); type = type.BaseType; }
        Assert.That(f, Is.Not.Null, name); f.SetValue(target, value);
    }
    private static StationPaymentCost[] Costs(int amount) => new[] { new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = amount } };
    [SetUp] public void Setup()
    {
        _hasEnemies = false;
        _session = new Session(); _session.State.Player.Diamonds = 100;
        _station = Asset<StationConfig>(); Set(_station, "id", "station"); Set(_station, "initialLevel", 2);
        var type = Asset<StationTypeConfig>(); Set(_station, "stationTypeConfig", type);
        Set(_station, "upgradeSettings", new[] { new StationUpgradeSettings { TargetLevel = 3,
            PaymentOptions = new[] { new StationPaymentOption { Id = "pay", Costs = Costs(10), DurationSeconds = 100 } } } });
        var effects = Asset<StationLevelEffectsConfig>();
        effects.Levels = new[] { new StationLevelEffectsSettings { Level = 2,
            RepairPaymentOptions = new[] { new StationInstantPaymentOption { Id = "pay", Costs = Costs(100) } },
            RecoveryPaymentOptions = new[] { new StationInstantPaymentOption { Id = "pay", Costs = Costs(20) } } },
            new StationLevelEffectsSettings { Level = 3 } };
        Set(_station, "levelEffects", effects);
        var config = Asset<StarSystemConfig>(); Set(config, "id", "sys"); Set(config, "station", _station);
        _system = new StarSystemRuntimeState { SystemId = "sys" }; _session.State.Galaxy.Systems.Add(_system);
        _service = new StationService(_session, _ => config, new SimpleEventBus(), new StationResourcePaymentGateway(_session), hasEnemies: _ => _hasEnemies);
        _service.InitializeSession(true);
    }
    [TearDown] public void Cleanup() { foreach (var asset in _objects) UnityEngine.Object.DestroyImmediate(asset); _objects.Clear(); }
    private bool Start(StationOperationKind kind) => _service.TryStartOperation("sys", kind, "pay", _system.Station.Level, _system.Station.Revision, out _);
    [Test] public void PaidUpgrade_UsesOldLevelUntilCompletionAndChargesOnce()
    {
        Assert.That(Start(StationOperationKind.Upgrade), Is.True);
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90)); Assert.That(_system.Station.Level, Is.EqualTo(2));
        Assert.That(Start(StationOperationKind.Upgrade), Is.False);
        _service.AdvanceTime(99); Assert.That(_system.Station.Level, Is.EqualTo(2));
        _service.AdvanceTime(1); Assert.That(_system.Station.Level, Is.EqualTo(3));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [Test] public void RejectedPayment_DoesNotStartUpgradeOrSpend()
    {
        _session.State.Player.Diamonds = 9;
        Assert.That(Start(StationOperationKind.Upgrade), Is.False);
        Assert.That(_system.Station.Development, Is.Null); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(9));
    }
    [Test] public void PartialRepair_IsInstantAndCostIsProportional()
    {
        _system.ApplyInfrastructureDamageFromWar(135, false);
        Assert.That(Start(StationOperationKind.Repair), Is.True);
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(70)); Assert.That(_system.InfrastructureDamage, Is.Zero);
        Assert.That(_system.Station.Health, Is.EqualTo(450));
        Assert.That(Start(StationOperationKind.Repair), Is.False); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(70));
    }
    [Test] public void Recovery_IsInstantRestoresLevelAndResumesPaidUpgrade()
    {
        Assert.That(Start(StationOperationKind.Upgrade), Is.True); _service.AdvanceTime(25);
        _system.ApplyInfrastructureDamageFromWar(100, true);
        Assert.That(_system.Station.IsDestroyed, Is.True);
        Assert.That(Start(StationOperationKind.Recovery), Is.True);
        Assert.That(_system.Station.IsDestroyed, Is.False); Assert.That(_system.Station.Recovery, Is.Null);
        Assert.That(_system.Station.Level, Is.EqualTo(2)); Assert.That(_system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(70));
        _service.AdvanceTime(75); Assert.That(_system.Station.Level, Is.EqualTo(3));
        Assert.That(Start(StationOperationKind.Recovery), Is.False); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(70));
    }
    [Test] public void StaleRevision_CannotPayAfterNewDamage()
    {
        long revision = _system.Station.Revision; _system.ApplyInfrastructureDamageFromWar(10, false);
        Assert.That(_service.TryStartOperation("sys", StationOperationKind.Upgrade, "pay", 2, revision, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationStateChanged")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
        Assert.That(_service.TryStartOperation("sys", StationOperationKind.Upgrade, "pay", 2,
            _system.Station.Revision, out reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationDamaged")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
        Assert.That(_system.Station.Development, Is.Null);
    }
    [Test] public void CapturedSystem_CannotBeRecoveredBeforeLiberation()
    {
        _system.ApplyInfrastructureDamageFromWar(100, true); _system.MarkCaptured();
        Assert.That(Start(StationOperationKind.Recovery), Is.False); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }
    [Test] public void SaveReload_DoesNotChargeRunningUpgradeAgain()
    {
        Assert.That(Start(StationOperationKind.Upgrade), Is.True); _service.AdvanceTime(25);
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        _service.InitializeSession(false); _service.AdvanceTime(75);
        Assert.That(_session.State.Galaxy.Systems[0].Station.Level, Is.EqualTo(3));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }

    [Test] public void DamagedStation_CannotPayForUpgradeEvenWithOneMissingHealth()
    {
        _system.ApplyInfrastructureDamageFromWar(1, false);
        Assert.That(_service.TryStartOperation("sys", StationOperationKind.Upgrade, "pay", 2,
            _system.Station.Revision, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationDamaged")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
        Assert.That(_system.Station.Development, Is.Null);
    }
    [Test] public void PaidUpgrade_PausesOnDamageAndEnemies_ThenResumesWithoutChargingOrCatchUp()
    {
        Start(StationOperationKind.Upgrade); _service.AdvanceTime(25);
        _system.ApplyInfrastructureDamageFromWar(14, false); _hasEnemies = true;
        _service.AdvanceTime(1000);
        Assert.That(_system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        Assert.That(_system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
        _hasEnemies = false; _service.AdvanceTime(1000);
        Assert.That(_system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.Damaged));
        Assert.That(_system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        StationInfrastructureRules.AutoRepair(_system, new StationLevelEffectsSettings { HealthPerTick = 7 }, false);
        _service.AdvanceTime(10); Assert.That(_system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        StationInfrastructureRules.AutoRepair(_system, new StationLevelEffectsSettings { HealthPerTick = 7 }, false);
        _service.AdvanceTime(74); Assert.That(_system.Station.Development.ElapsedSeconds, Is.EqualTo(99));
        _service.AdvanceTime(1); Assert.That(_system.Station.Level, Is.EqualTo(3));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [Test] public void HealthyStationWithEnemies_CannotAdvanceAlreadyPaidUpgrade()
    {
        Start(StationOperationKind.Upgrade); _hasEnemies = true; _service.AdvanceTime(1000);
        Assert.That(_system.Station.Development.ElapsedSeconds, Is.Zero);
        _hasEnemies = false; _service.AdvanceTime(1);
        Assert.That(_system.Station.Development.ElapsedSeconds, Is.EqualTo(1));
    }
    [Test] public void DamagedPaidUpgrade_RemainsPausedAfterSaveReload()
    {
        Start(StationOperationKind.Upgrade); _service.AdvanceTime(5); _system.ApplyInfrastructureDamageFromWar(1, false);
        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        _service.InitializeSession(false); _service.AdvanceTime(1000);
        var loaded = _session.State.Galaxy.Systems[0];
        Assert.That(loaded.Station.Development.ElapsedSeconds, Is.EqualTo(5));
        Assert.That(loaded.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.Damaged));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [TestCase(StationOperationKind.Construction)]
    [TestCase(StationOperationKind.Upgrade)]
    [TestCase(StationOperationKind.Recovery)]
    [TestCase(StationOperationKind.Repair)]
    public void CapturedSystem_RejectsEveryPaidStationAction(StationOperationKind kind)
    {
        _system.ApplyInfrastructureDamageFromWar(100, true);
        _system.MarkCaptured();
        Assert.That(_service.TryStartOperation("sys", kind, "pay", _system.Station.Level,
            _system.Station.Revision, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("SystemCaptured"));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
        Assert.That(_system.Station.IsDestroyed, Is.True);
        Assert.That(_system.Station.Health, Is.Zero);
    }

}
