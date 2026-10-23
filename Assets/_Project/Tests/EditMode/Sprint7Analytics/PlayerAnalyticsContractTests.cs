using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class PlayerAnalyticsContractTests
{
    private const string TestKey = "11111111-1111-4111-8111-111111111111";
    private const string ProductionKey = "22222222-2222-4222-8222-222222222222";
    private sealed class Preferences : IPlayerAnalyticsPreferences
    {
        public string InstallationId { get; set; } = "33333333-3333-4333-8333-333333333333";
        public bool CollectionEnabled { get; set; } = true;
    }
    private sealed class Transport : IPlayerAnalyticsTransport
    {
        public readonly List<PlayerAnalyticsRecord> Events = new List<PlayerAnalyticsRecord>();
        public string Key;
        public int Starts, Flushes;
        public bool Enabled;
        public bool ThrowReport, ThrowStart, ThrowSettings, ThrowFlush;
        public bool Start(PlayerAnalyticsOptions options, string key)
        { Starts++; if (ThrowStart) throw new InvalidOperationException(); Key = key; return true; }
        public void Report(PlayerAnalyticsRecord record)
        { if (ThrowReport) throw new InvalidOperationException(); Events.Add(record); }
        public void SetEnabled(bool enabled) { if (ThrowSettings) throw new InvalidOperationException(); Enabled = enabled; }
        public void Pause(bool paused) { if (ThrowSettings) throw new InvalidOperationException(); }
        public void Flush() { if (ThrowFlush) throw new InvalidOperationException(); Flushes++; }
    }
    private static PlayerAnalyticsOptions Options(bool test = true) => new PlayerAnalyticsOptions
    {
        TestApiKey = TestKey, ProductionApiKey = ProductionKey, IsTest = test,
        AppVersion = "0.2.7", BuildNumber = "17", BalanceVersion = "balance-01", Platform = "Android"
    };
    private static PlayerAnalyticsService Create(Transport transport, Preferences prefs = null,
        PlayerAnalyticsOptions options = null, Func<PlayerAnalyticsSnapshot> capture = null, Func<DateTime> clock = null)
        => new PlayerAnalyticsService(options ?? Options(), transport, prefs ?? new Preferences(),
            capture ?? (() => new PlayerAnalyticsSnapshot()), clock);

    [Test] public void DebugRouting_IsTestForEverySafetySignal()
    {
        for (int mask = 0; mask < 16; mask++)
            Assert.That(PlayerAnalyticsOptions.UseTest((mask & 1) != 0, (mask & 2) != 0,
                (mask & 4) != 0, (mask & 8) != 0), Is.EqualTo(mask != 0));
    }
    [Test] public void TestGame_UsesOnlyTestKey()
    {
        var transport = new Transport(); Create(transport);
        Assert.That(transport.Key, Is.EqualTo(TestKey));
        Assert.That(transport.Events.Single().environment, Is.EqualTo("test"));
    }
    [Test] public void NormalRelease_UsesOnlyProductionKey()
    {
        var transport = new Transport(); Create(transport, options: Options(false));
        Assert.That(transport.Key, Is.EqualTo(ProductionKey));
        Assert.That(transport.Events.Single().environment, Is.EqualTo("production"));
    }
    [Test] public void MissingTestKey_DoesNotFallBackToProduction()
    {
        var transport = new Transport(); var options = Options(); options.TestApiKey = "";
        var service = Create(transport, options: options);
        Assert.That(service.CollectionEnabled, Is.False);
        Assert.That(transport.Starts, Is.Zero);
    }
    [Test] public void DisabledPreference_DoesNotActivateSdkOrRecordEvents()
    {
        var transport = new Transport(); var service = Create(transport, new Preferences { CollectionEnabled = false });
        service.Track(PlayerAnalyticsAction.BuyItem, true); service.Flush();
        Assert.That(transport.Starts, Is.Zero); Assert.That(transport.Events, Is.Empty); Assert.That(transport.Flushes, Is.Zero);
    }
    [Test] public void ConfigMasterSwitch_CannotBeOverriddenByPreference()
    {
        var options = Options(); options.Enabled = false; var transport = new Transport();
        var service = Create(transport, options: options); service.SetCollectionEnabled(true);
        Assert.That(service.CollectionEnabled, Is.False); Assert.That(transport.Starts, Is.Zero);
    }
    [Test] public void OptOut_StopsRecordsAndFlushAndPersistsPreference()
    {
        var transport = new Transport(); var prefs = new Preferences(); var service = Create(transport, prefs);
        service.SetCollectionEnabled(false); service.Track(PlayerAnalyticsAction.BuyItem, true); service.Flush();
        Assert.That(prefs.CollectionEnabled, Is.False); Assert.That(transport.Enabled, Is.False);
        Assert.That(transport.Events.Count, Is.EqualTo(1)); Assert.That(transport.Flushes, Is.Zero);
    }
    [Test] public void Reenable_StartsNewSessionWithoutChangingInstallation()
    {
        var transport = new Transport(); var service = Create(transport);
        var first = transport.Events[0]; service.SetCollectionEnabled(false); service.SetCollectionEnabled(true);
        var second = transport.Events[1];
        Assert.That(second.session_id, Is.Not.EqualTo(first.session_id));
        Assert.That(second.installation_id, Is.EqualTo(first.installation_id));
        Assert.That(transport.Starts, Is.EqualTo(1));
    }
    [Test] public void Relaunch_NewSessionStableInstallation()
    {
        var prefs = new Preferences(); var a = new Transport(); var b = new Transport(); Create(a, prefs); Create(b, prefs);
        Assert.That(a.Events[0].installation_id, Is.EqualTo(b.Events[0].installation_id));
        Assert.That(a.Events[0].session_id, Is.Not.EqualTo(b.Events[0].session_id));
    }
    [Test] public void ShortBackground_DoesNotSplitSession()
    {
        DateTime time = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var t = new Transport(); var s = Create(t, clock: () => time); s.OnApplicationPause(true);
        time = time.AddSeconds(10); s.OnApplicationPause(false); s.Track(PlayerAnalyticsAction.NewGame, true);
        Assert.That(t.Events.Last().session_id, Is.EqualTo(t.Events.First().session_id));
        Assert.That(t.Events.Count(e => e.action == "sf_session_started"), Is.EqualTo(1));
    }
    [Test] public void LongBackground_StartsNewSession()
    {
        DateTime time = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var t = new Transport(); var s = Create(t, clock: () => time); s.OnApplicationPause(true);
        time = time.AddMinutes(31); s.OnApplicationPause(false);
        Assert.That(t.Events.Last().session_id, Is.Not.EqualTo(t.Events.First().session_id));
    }
    [Test] public void NewGameAndLoad_DoNotResetAnalyticsSession()
    {
        var t = new Transport(); var s = Create(t); s.Track(PlayerAnalyticsAction.NewGame, true); s.Track(PlayerAnalyticsAction.ContinueGame, true);
        Assert.That(t.Events.Select(e => e.session_id).Distinct().Count(), Is.EqualTo(1));
    }
    [Test] public void Context_IsCapturedAtOccurrenceAndIncludesActualEconomyDelta()
    {
        int credits = 100, fuel = 20;
        var t = new Transport(); var s = Create(t, capture: () => new PlayerAnalyticsSnapshot
        { HasGame = true, Credits = credits, Diamonds = 3, Fuel = fuel, PlayerLevel = 2, SectorCount = 4, SystemId = "system_01", PlanetId = "planet_01" });
        var before = s.Capture(); credits = 65; fuel = 25;
        s.Track(PlayerAnalyticsAction.Refuel, true, before: before, quantity: 5, cost: 35);
        credits = 999; fuel = 999;
        var e = t.Events.Last();
        Assert.That(e.credits_before, Is.EqualTo(100)); Assert.That(e.credits_after, Is.EqualTo(65));
        Assert.That(e.credits_delta, Is.EqualTo(-35)); Assert.That(e.fuel_before, Is.EqualTo(20)); Assert.That(e.fuel_after, Is.EqualTo(25));
        Assert.That(e.progression_sector_count, Is.EqualTo(4)); Assert.That(e.cost, Is.EqualTo(35));
    }
    [Test] public void FailedAttempt_PreservesObservedReason()
    {
        var t = new Transport(); var s = Create(t); s.Track(PlayerAnalyticsAction.BuyItem, false, "NotEnoughCredits");
        Assert.That(t.Events.Last().reason, Is.EqualTo("NotEnoughCredits")); Assert.That(t.Events.Last().result, Is.EqualTo("failure"));
    }
    [Test] public void ClosedPayload_DoesNotExposeNamesMessagesOrDeviceIds()
    {
        var fields = typeof(PlayerAnalyticsRecord).GetFields().Select(f => f.Name).ToArray();
        foreach (string forbidden in new[] { "PlayerName", "PlayerId", "device_id", "advertising_id", "message", "email", "save" })
            Assert.That(fields, Does.Not.Contain(forbidden));
        Assert.That(fields.Length, Is.EqualTo(28), "Schema expansion requires explicit review.");
    }
    [Test] public void FreeTextEmailUrlAndOversizedParameters_AreRejected()
    {
        foreach (string input in new[] { "user@example.com", "https://example.com", "Pilot Name", "ошибка игрока", new string('a', 97) })
            Assert.That(PlayerAnalyticsService.SafeCode(input), Is.EqualTo("invalid_code"));
    }
    [Test] public void UnknownAction_IsNotSent()
    {
        var t = new Transport(); var s = Create(t); s.Track((PlayerAnalyticsAction)999, true);
        Assert.That(t.Events.Count, Is.EqualTo(1)); Assert.That(s.RejectedEvents, Is.EqualTo(1));
    }
    [Test] public void InvalidInstallationId_IsNotSent()
    {
        var t = new Transport(); var s = Create(t, new Preferences { InstallationId = "player" });
        Assert.That(t.Events, Is.Empty); Assert.That(s.RejectedEvents, Is.EqualTo(1));
    }
    [Test] public void EachActualAttempt_HasUniqueEventId()
    {
        var t = new Transport(); var s = Create(t);
        for (int i = 0; i < 100; i++) s.Track(PlayerAnalyticsAction.BuyItem, false, "NotEnoughCredits");
        Assert.That(t.Events.Select(e => e.event_id).Distinct().Count(), Is.EqualTo(101));
    }
    [Test] public void OperationCompletion_IsRecordedOnce()
    {
        var t = new Transport(); var s = Create(t);
        using (var op = new PlayerAnalyticsOperation(s, PlayerAnalyticsAction.Repair, null, 0))
        { op.Complete(true); op.Complete(true); }
        Assert.That(t.Events.Count(e => e.action == "sf_repair"), Is.EqualTo(1));
    }
    [Test] public void InterruptedOperation_IsFailureAndDoesNotInventSuccess()
    {
        var t = new Transport(); var s = Create(t);
        using (new PlayerAnalyticsOperation(s, PlayerAnalyticsAction.MissionAccept, "mission_01", 0)) { }
        Assert.That(t.Events.Last().reason, Is.EqualTo("UnexpectedException"));
        Assert.That(t.Events.Last().result, Is.EqualTo("failure"));
    }
    [Test] public void SdkReportFailure_DoesNotThrowOrRetryAtApplicationLayer()
    {
        var t = new Transport(); var s = Create(t); t.ThrowReport = true;
        Assert.DoesNotThrow(() => s.Track(PlayerAnalyticsAction.BuyItem, true));
        Assert.That(s.RejectedEvents, Is.EqualTo(1)); Assert.That(s.AcceptedEvents, Is.EqualTo(1));
    }
    [Test] public void SdkActivationFailure_DoesNotBreakGameStartup()
    {
        var t = new Transport { ThrowStart = true }; PlayerAnalyticsService s = null;
        Assert.DoesNotThrow(() => s = Create(t)); Assert.That(s.CollectionEnabled, Is.False);
    }
    [Test] public void SdkSettingsAndFlushFailures_DoNotEscape()
    {
        var t = new Transport(); var s = Create(t); t.ThrowSettings = t.ThrowFlush = true;
        Assert.DoesNotThrow(() => s.OnApplicationPause(true)); Assert.DoesNotThrow(() => s.Flush());
        Assert.DoesNotThrow(() => s.SetCollectionEnabled(false));
    }
    [Test] public void ContextFailure_DoesNotStopEventPipeline()
    {
        var t = new Transport(); var s = Create(t, capture: () => throw new InvalidOperationException());
        Assert.DoesNotThrow(() => s.Track(PlayerAnalyticsAction.Travel, false, "UnknownContext"));
        Assert.That(t.Events.Count, Is.EqualTo(2)); Assert.That(t.Events.Last().has_game, Is.False);
    }
    [Test] public void Dictionary_CoversEveryDefinedActionWithUniqueNames()
    {
        var actions = Enum.GetValues(typeof(PlayerAnalyticsAction)).Cast<PlayerAnalyticsAction>().ToArray();
        Assert.That(actions.Select(PlayerAnalyticsService.EventName).Distinct().Count(), Is.EqualTo(actions.Length));
        Assert.That(actions.Select(PlayerAnalyticsService.EventName), Is.All.StartsWith("sf_"));
    }
    [Test] public void EnvironmentAndBuildContext_AreFrozenUntilRestart()
    {
        var options = Options(); var t = new Transport(); var s = Create(t, options: options);
        options.IsTest = false; options.BalanceVersion = "balance-new"; options.AppVersion = "99";
        s.Track(PlayerAnalyticsAction.NewGame, true);
        Assert.That(t.Events.Last().environment, Is.EqualTo("test"));
        Assert.That(t.Events.Last().balance_version, Is.EqualTo("balance-01"));
        Assert.That(t.Events.Last().app_version, Is.EqualTo("0.2.7"));
    }
}
