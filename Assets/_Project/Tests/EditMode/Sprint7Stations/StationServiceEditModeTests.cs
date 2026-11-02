using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>S07-01 service integration through injected dependencies, without Bootstrapper or scenes.</summary>
public sealed class StationServiceEditModeTests
{
    private readonly List<ScriptableObject> _objects = new List<ScriptableObject>();
    private readonly Dictionary<string, StarSystemConfig> _configs = new Dictionary<string, StarSystemConfig>();
    private FakeSession _session;
    private SimpleEventBus _events;

    [SetUp]
    public void SetUp()
    {
        _configs.Clear();
        _session = new FakeSession();
        _session.StartNewSession(new GameRuntimeState());
        _events = new SimpleEventBus();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _objects)
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
        _objects.Clear();
    }

    [Test]
    public void MissingPaymentGateway_RejectsConfiguredOperation_WithoutChangingWorld()
    {
        var system = AddSystem("sys", 2, 10);
        var service = Service();
        service.InitializeSession(true);
        string before = JsonUtility.ToJson(_session.State);
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("PaymentUnavailable"));
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(before));
    }

    [Test]
    public void PaymentRejected_DoesNotCreateOperationOrPublishSaveRequest()
    {
        var system = AddSystem("sys", 2, 10);
        var payments = new PaymentStub { Accept = false };
        var service = Service(payments);
        service.InitializeSession(true);
        int saveRequests = 0;
        _events.Subscribe<SaveNeedEvent>(_ => saveRequests++);
        string before = JsonUtility.ToJson(_session.State);
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("InsufficientResources"));
        Assert.That(payments.Calls, Is.EqualTo(1));
        Assert.That(saveRequests, Is.Zero);
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(before));
    }

    [Test]
    public void InstantOperation_IsCompletedByStart_WithoutWaitingForTick()
    {
        var system = AddSystem("sys", 2, 0);
        var payments = new PaymentStub();
        var service = Service(payments);
        service.InitializeSession(true);
        int saveRequests = 0;
        _events.Subscribe<SaveNeedEvent>(_ => saveRequests++);
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(3));
        Assert.That(system.Station.Development, Is.Null);
        Assert.That(payments.Calls, Is.EqualTo(1));
        Assert.That(saveRequests, Is.EqualTo(1));
    }

    [Test]
    public void DuplicateTimedStart_CannotPayTwiceOrReplaceRunningOperation()
    {
        var system = AddSystem("sys", 2, 30);
        var payments = new PaymentStub();
        var service = Service(payments);
        service.InitializeSession(true);
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.True);
        string id = system.Station.Development.OperationId;
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.False);
        Assert.That(payments.Calls, Is.EqualTo(1));
        Assert.That(system.Station.Development.OperationId, Is.EqualTo(id));
    }

    [Test]
    public void DuplicateInstantClick_WithStaleLevelAndRevision_CannotBuyNextLevel()
    {
        var system = AddSystem("sys", 2, 0);
        var payments = new PaymentStub();
        var service = Service(payments);
        service.InitializeSession(true);
        int quotedLevel = system.Station.Level;
        long quotedRevision = system.Station.Revision;
        Assert.That(service.TryStartOperation("sys", StationOperationKind.Upgrade, "pay",
            quotedLevel, quotedRevision, out _), Is.True);
        Assert.That(service.TryStartOperation("sys", StationOperationKind.Upgrade, "pay",
            quotedLevel, quotedRevision, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationStateChanged"));
        Assert.That(payments.Calls, Is.EqualTo(1));
        Assert.That(system.Station.Level, Is.EqualTo(3));
    }

    [Test]
    public void RemovedAndReplacedConfig_PreservesAcceptedQuoteAndContinuesPaidProgress()
    {
        var system = AddSystem("sys", 2, 20);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.True);
        service.AdvanceTime(5);
        string acceptedId = system.Station.Development.OperationId;
        _configs.Remove("sys");
        service.ReconcileSystem("sys");
        Assert.That(system.Station.ConfigMissing, Is.True);
        service.AdvanceTime(5);
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(10));

        var replacement = Make<StationConfig>();
        Set(replacement, "id", "replacement_station");
        Set(replacement, "initialLevel", 9);
        Set(replacement, "stationTypeConfig", Make<StationTypeConfig>());
        var replacementSystem = Make<StarSystemConfig>();
        Set(replacementSystem, "id", "sys");
        Set(replacementSystem, "station", replacement);
        _configs["sys"] = replacementSystem;
        service.ReconcileSystem("sys");
        Assert.That(system.Station.ConfigMissing, Is.True);
        Assert.That(system.Station.ConfigId, Is.EqualTo("station_sys"));
        Assert.That(system.Station.StationType, Is.EqualTo(StationType.Science));
        Assert.That(system.Station.Development.OperationId, Is.EqualTo(acceptedId));
        Assert.That(system.Station.Development.DurationSeconds, Is.EqualTo(20));
        service.AdvanceTime(10);
        Assert.That(system.Station.Level, Is.EqualTo(3));
        Assert.That(system.Station.Development, Is.Null);
    }

    [Test]
    public void SessionBinding_DistinguishesNewGameFromLegacyExistingStation()
    {
        var system = AddSystem("sys", 0, 10);
        Service().InitializeSession(true);
        Assert.That(system.Station.Level, Is.Zero);

        system.Station = null;
        system.StationStateVersion = 0;
        Service().InitializeSession(false);
        Assert.That(system.Station.Level, Is.EqualTo(1));
        Assert.That(system.StationStateVersion, Is.EqualTo(StationLifecycleRules.SchemaVersion));
    }

    [Test]
    public void LegacyMissingConfig_CanBindWhenConfigReturns()
    {
        var system = AddSystem("sys", 0, 10);
        StarSystemConfig config = _configs["sys"];
        StationConfig originalStation = config.Station;
        Set(config, "station", null);
        var service = Service();
        service.InitializeSession(false);
        Assert.That(system.Station, Is.Null);
        Assert.That(system.StationStateVersion, Is.Zero);
        Set(config, "station", originalStation);
        service.InitializeSession(false);
        Assert.That(system.Station.Level, Is.EqualTo(1));
        Assert.That(system.Station.ConfigMissing, Is.False);
    }

    [Test]
    public void ExistingLevelZeroAndPaidConstruction_AreNotReboundFromEditedAsset()
    {
        var system = AddSystem("sys", 0, 10);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        Assert.That(Start(service, system, StationOperationKind.Construction, "pay", out _), Is.True);
        service.AdvanceTime(3);
        string operationId = system.Station.Development.OperationId;
        Set(_configs["sys"].Station, "initialLevel", 9);
        service.InitializeSession(false);
        Assert.That(system.Station.Level, Is.Zero);
        Assert.That(system.Station.Development.OperationId, Is.EqualTo(operationId));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(3));
    }

    [Test]
    public void ObserverAndQuerySnapshots_CannotMutateLiveStateOrNextPreviousSnapshot()
    {
        var system = AddSystem("sys", 2, 20);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        double previousElapsed = -1;
        int previousPrice = -1;
        _events.Subscribe<StationStateChangedEvent>(change =>
        {
            if (change.Previous?.Development != null)
            {
                previousElapsed = change.Previous.Development.ElapsedSeconds;
                previousPrice = change.Previous.Development.PaidCosts[0].Amount;
                change.Previous.Development.PaidCosts[0].Amount = 999;
            }
            change.Current.Level = 10;
            if (change.Current.Development != null)
            {
                change.Current.Development.ElapsedSeconds = 999;
                change.Current.Development.PaidCosts[0].Amount = 999;
            }
        });
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.True);
        Assert.That(service.TryGetStation("sys", out var query), Is.True);
        query.Level = 8;
        query.Development.PaidCosts[0].Amount = 888;
        service.AdvanceTime(3);
        Assert.That(system.Station.Level, Is.EqualTo(2));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(3));
        Assert.That(system.Station.Development.PaidCosts[0].Amount, Is.EqualTo(7));
        Assert.That(previousElapsed, Is.Zero);
        Assert.That(previousPrice, Is.EqualTo(7));
    }

    [Test]
    public void Offline_UsesSameCappedWindowForAllStations_AndDoesNotReplayAfterLoad()
    {
        var first = AddSystem("first", 2, 100000);
        var second = AddSystem("second", 4, 100000);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        Assert.That(Start(service, first, StationOperationKind.Upgrade, "pay", out _), Is.True);
        Assert.That(Start(service, second, StationOperationKind.Upgrade, "pay", out _), Is.True);
        long from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        long now = from + TimeSpan.FromHours(30).Ticks;
        _session.State.Meta.LastSaveUtc = from;
        service.ProcessOffline(_session.State, now);
        Assert.That(first.Station.Development.ElapsedSeconds, Is.EqualTo(43200));
        Assert.That(second.Station.Development.ElapsedSeconds, Is.EqualTo(43200));
        Assert.That(_session.State.Meta.LastSaveUtc, Is.EqualTo(from));
        service.ProcessOffline(_session.State, now);
        Assert.That(first.Station.Development.ElapsedSeconds, Is.EqualTo(43200));

        _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        var restored = Service();
        restored.InitializeSession(false);
        restored.ProcessOffline(_session.State, now);
        Assert.That(_session.State.Galaxy.Systems[0].Station.Development.ElapsedSeconds, Is.EqualTo(43200));
        Assert.That(_session.State.Galaxy.Systems[1].Station.Development.ElapsedSeconds, Is.EqualTo(43200));
    }

    [Test]
    public void ActiveInvasion_PreventsOfflineRecoveryProgress()
    {
        var system = AddSystem("sys", 2, 100000);
        Set(_configs["sys"].Station, "isDestroyed", true);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        Assert.That(Start(service, system, StationOperationKind.Recovery, "pay", out _), Is.True);
        service.AdvanceTime(5);
        system.MarkInvasion();
        long from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        _session.State.Meta.LastSaveUtc = from;
        service.ProcessOffline(_session.State, from + TimeSpan.FromHours(1).Ticks);
        Assert.That(system.Station.IsDestroyed, Is.True);
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.EqualTo(5));
        Assert.That(system.Station.Recovery.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
    }

    [Test]
    public void ThrowingObserver_DoesNotPreventNextObserverReceivingInstantCompletion()
    {
        var system = AddSystem("sys", 2, 0);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        int deliveredLevel = -1;
        int deliveredCount = 0;
        _events.Subscribe<StationStateChangedEvent>(_ => throw new InvalidOperationException("observer failure"));
        _events.Subscribe<StationStateChangedEvent>(change =>
        {
            deliveredLevel = change.Current.Level;
            deliveredCount++;
        });
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.True);
        Assert.That(deliveredCount, Is.EqualTo(1));
        Assert.That(deliveredLevel, Is.EqualTo(3));
        Assert.That(system.Station.Level, Is.EqualTo(3));
        Assert.That(system.Station.Development, Is.Null);
    }

    [Test]
    public void MutatingObserver_CannotCorruptSnapshotReceivedBySecondObserver()
    {
        var system = AddSystem("sys", 2, 20);
        var service = Service(new PaymentStub());
        service.InitializeSession(true);
        int observedLevel = -1;
        int observedPrice = -1;
        double observedElapsed = -1;
        _events.Subscribe<StationStateChangedEvent>(change =>
        {
            var detached = change.Current;
            detached.Level = 10;
            detached.Development.ElapsedSeconds = 999;
            detached.Development.PaidCosts[0].Amount = 999;
        });
        _events.Subscribe<StationStateChangedEvent>(change =>
        {
            var detached = change.Current;
            observedLevel = detached.Level;
            observedPrice = detached.Development.PaidCosts[0].Amount;
            observedElapsed = detached.Development.ElapsedSeconds;
        });
        Assert.That(Start(service, system, StationOperationKind.Upgrade, "pay", out _), Is.True);
        Assert.That(observedLevel, Is.EqualTo(2));
        Assert.That(observedPrice, Is.EqualTo(7));
        Assert.That(observedElapsed, Is.Zero);
        Assert.That(system.Station.Level, Is.EqualTo(2));
        Assert.That(system.Station.Development.PaidCosts[0].Amount, Is.EqualTo(7));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.Zero);
    }

    private bool Start(StationService service, StarSystemRuntimeState system,
        StationOperationKind kind, string option, out string reason)
    {
        return service.TryStartOperation(system.SystemId, kind, option,
            system.Station.Level, system.Station.Revision, out reason);
    }

    private StationService Service(IStationPaymentGateway payments = null)
    {
        return new StationService(_session,
            id => _configs.TryGetValue(id, out var config) ? config : null, _events, payments);
    }

    private StarSystemRuntimeState AddSystem(string id, int initialLevel, float duration)
    {
        var type = Make<StationTypeConfig>();
        Set(type, "stationType", StationType.Science);
        var station = Make<StationConfig>();
        Set(station, "id", "station_" + id);
        Set(station, "stationTypeConfig", type);
        Set(station, "initialLevel", initialLevel);
        Set(station, "isActive", true);
        var option = new StationPaymentOption
        {
            Id = "pay", DurationSeconds = duration,
            Costs = new[] { new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = 7 } }
        };
        Set(station, "constructionPaymentOptions", new[] { option });
        Set(station, "recoveryPaymentOptions", new[] { option });
        var upgrades = new List<StationUpgradeSettings>();
        for (int level = 2; level <= 10; level++)
            upgrades.Add(new StationUpgradeSettings { TargetLevel = level, PaymentOptions = new[] { option } });
        Set(station, "upgradeSettings", upgrades.ToArray());
        var config = Make<StarSystemConfig>();
        Set(config, "id", id);
        Set(config, "station", station);
        _configs[id] = config;
        var system = new StarSystemRuntimeState { SystemId = id, SystemStatus = StarSystemStatus.Stable };
        _session.State.Galaxy.Systems.Add(system);
        return system;
    }

    private T Make<T>() where T : ScriptableObject
    {
        var result = ScriptableObject.CreateInstance<T>();
        _objects.Add(result);
        return result;
    }

    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private sealed class FakeSession : IGameSessionService
    {
        public GameRuntimeState State { get; private set; }
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }

    private sealed class PaymentStub : IStationPaymentGateway
    {
        public int Calls;
        public bool Accept = true;
        public bool TryPay(string systemId, StationOperationRuntimeState quote, out string reason)
        {
            Calls++;
            reason = Accept ? string.Empty : "InsufficientResources";
            return Accept;
        }
    }
}
