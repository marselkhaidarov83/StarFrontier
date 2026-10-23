using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public sealed class PlayerAnalyticsIntegrationTests
{
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Cost : IRefuelCostProvider { public int GetCostPerFuelUnit() => 10; }
    private sealed class Availability : IRefuelAvailabilityProvider { public bool IsRefuelAvailable() => true; }
    private sealed class Preferences : IPlayerAnalyticsPreferences
    {
        public string InstallationId => "33333333-3333-4333-8333-333333333333";
        public bool CollectionEnabled { get; set; } = true;
    }
    private sealed class Transport : IPlayerAnalyticsTransport
    {
        public readonly List<PlayerAnalyticsRecord> Events = new List<PlayerAnalyticsRecord>();
        public bool Start(PlayerAnalyticsOptions options, string key) => true;
        public void Report(PlayerAnalyticsRecord record) { Events.Add(record); }
        public void SetEnabled(bool enabled) { }
        public void Pause(bool paused) { }
        public void Flush() { }
    }
    private GameObject _root;
    private Bootstrapper _previous;
    private Session _session;
    private Transport _transport;
    private PlayerAnalyticsService _analytics;
    private SimpleEventBus _bus;

    [SetUp] public void SetUp()
    {
        _previous = Bootstrapper.Instance;
        _root = new GameObject("AnalyticsIntegrationFixture");
        _root.SetActive(false);
        var bootstrap = _root.AddComponent<Bootstrapper>();
        bootstrap.ServiceRegistry = new ServiceRegistry();
        Bootstrapper.Instance = bootstrap;
        _session = new Session();
        _session.State.Player.Credits = 1000;
        _session.State.Player.PlayerShipState.ActiveShipId = "ship_01";
        _session.State.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData
        { ShipId = "ship_01", FuelCapacity = 100, CurrentFuel = 50 });
        _transport = new Transport(); _bus = new SimpleEventBus();
        _analytics = new PlayerAnalyticsService(new PlayerAnalyticsOptions
        { IsTest = true, TestApiKey = "11111111-1111-4111-8111-111111111111", BalanceVersion = "test" },
            _transport, new Preferences(), () => PlayerAnalyticsUnityFactory.Capture(_session));
        bootstrap.ServiceRegistry.Register<IPlayerAnalytics>(_analytics);
    }

    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_root);
        Bootstrapper.Instance = _previous;
    }

    [Test] public void ActualRefuel_RecordsOneResultAndActualMoneyAndFuel()
    {
        var service = new RefuelService(_session, _bus, new Cost(), new Availability());
        Assert.That(service.Refuel(10).IsSuccess, Is.True);
        var e = _transport.Events.Single(x => x.action == "sf_refuel");
        Assert.That(e.credits_before, Is.EqualTo(1000)); Assert.That(e.credits_after, Is.EqualTo(900));
        Assert.That(e.fuel_before, Is.EqualTo(50)); Assert.That(e.fuel_after, Is.EqualTo(60));
    }
    [Test] public void StandaloneRefuel_SavesOnceAfterFuelAndCreditsAreUpdated()
    {
        var order = new List<string>();
        _bus.Subscribe<FuelChangedEvent>(_ => order.Add("fuel"));
        _bus.Subscribe<CreditsChangedEvent>(_ => order.Add("credits"));
        _bus.Subscribe<SaveNeedEvent>(_ =>
        {
            Assert.That(_session.State.Player.Credits, Is.EqualTo(900));
            Assert.That(_session.State.Player.GetActiveShip().CurrentFuel, Is.EqualTo(60));
            order.Add("save");
        });
        var service = new RefuelService(_session, _bus, new Cost(), new Availability());
        Assert.That(service.Refuel(10).IsSuccess, Is.True);
        Assert.That(order, Is.EqualTo(new[] { "fuel", "credits", "save" }));
        Assert.That(_transport.Events.Count(x => x.action == "sf_refuel"), Is.EqualTo(1));
    }

    [Test] public void RejectedRefuel_DoesNotRequestSave()
    {
        int saves = 0;
        _session.State.Player.Credits = 1;
        _bus.Subscribe<SaveNeedEvent>(_ => saves++);
        var service = new RefuelService(_session, _bus, new Cost(), new Availability());
        Assert.That(service.Refuel(10).IsSuccess, Is.False);
        Assert.That(saves, Is.Zero);
    }

    [Test] public void RefuelToFull_DoesNotDoubleRecordNestedRefuel()
    {
        var service = new RefuelService(_session, _bus, new Cost(), new Availability());
        Assert.That(service.RefuelToFull().IsSuccess, Is.True);
        Assert.That(_transport.Events.Count(x => x.action == "sf_refuel"), Is.EqualTo(1));
    }
    [Test] public void AvailabilityPolling_DoesNotCreatePlayerAttempts()
    {
        var service = new RefuelService(_session, _bus, new Cost(), new Availability());
        for (int i = 0; i < 100; i++) service.CanRefuel(10);
        Assert.That(_transport.Events.Count, Is.EqualTo(1), "Only session start is expected.");
    }
    [Test] public void RefuelRefusal_UsesActualReasonAndLeavesBalancesIntact()
    {
        _session.State.Player.Credits = 1;
        var service = new RefuelService(_session, _bus, new Cost(), new Availability());
        Assert.That(service.Refuel(10).IsSuccess, Is.False);
        var e = _transport.Events.Single(x => x.action == "sf_refuel");
        Assert.That(e.reason, Is.EqualTo("NotEnoughCredits")); Assert.That(e.credits_delta, Is.Zero);
        Assert.That(e.fuel_after, Is.EqualTo(50));
    }
    [Test] public void TravelBridge_UsesSnapshotBeforeFuelSpendAndUnsubscribes()
    {
        using (var bridge = new PlayerAnalyticsEventBridge(_bus, _analytics))
        {
            _bus.Publish(new TravelStartedEvent("system_01", "system_02"));
            _session.State.Player.GetActiveShip().CurrentFuel -= 7;
            _bus.Publish(new TravelFinishedEvent("system_01", "system_02", true, 7, TravelFailReason.None));
        }
        _bus.Publish(new TravelFinishedEvent("system_01", "system_02", true, 7, TravelFailReason.None));
        var e = _transport.Events.Single(x => x.action == "sf_travel_result");
        Assert.That(e.fuel_before, Is.EqualTo(50)); Assert.That(e.fuel_after, Is.EqualTo(43));
    }
    [Test] public void JsonPayload_ContainsNoPlayerNameOrMessages()
    {
        _session.State.Player.PlayerName = "private@example.com";
        _analytics.Track(PlayerAnalyticsAction.Interaction, false, "user@example.com", contentId: "https://example.com");
        string json = JsonUtility.ToJson(_transport.Events.Last());
        Assert.That(json, Does.Not.Contain("private@example.com"));
        Assert.That(json, Does.Not.Contain("user@example.com")); Assert.That(json, Does.Not.Contain("https://"));
        Assert.That(json, Does.Contain("invalid_code"));
    }
    [Test] public void NativeSdkConfiguration_DisablesUnneededCollectorsAndBoundsQueue()
    {
        var c = AppMetricaPlayerAnalyticsTransport.CreateConfig(new PlayerAnalyticsOptions
        { MaxQueuedReports = 20000, DispatchPeriodSeconds = 0 }, "11111111-1111-4111-8111-111111111111");
        Assert.That(c.AdvIdentifiersTracking, Is.False); Assert.That(c.LocationTracking, Is.False);
        Assert.That(c.CrashReporting, Is.False); Assert.That(c.NativeCrashReporting, Is.False);
        Assert.That(c.RevenueAutoTrackingEnabled, Is.False); Assert.That(c.AppOpenTrackingEnabled, Is.False);
        Assert.That(c.SessionsAutoTrackingEnabled, Is.False);
        Assert.That(c.MaxReportsInDatabaseCount, Is.EqualTo(10000));
        Assert.That(c.DispatchPeriodSeconds, Is.EqualTo(10));
    }
    [Test] public void ActualDebugAsset_TestRouteIsIndependentOfLogMasterSwitch()
    {
        var config = ScriptableObject.CreateInstance<DebugLogConfig>();
        try
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("testPlayerAnalytics").boolValue = true;
            serialized.FindProperty("logsEnabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(config.TestPlayerAnalytics, Is.True);
            Assert.That(PlayerAnalyticsOptions.UseTest(false, false, false, config.TestPlayerAnalytics), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
}
