using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>S07-01 configuration, persisted accepted quotes, migration and shared offline budget.</summary>
public sealed class StationConfigAndPersistenceEditModeTests
{
    private readonly List<ScriptableObject> _objects = new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _objects)
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
        _objects.Clear();
    }

    [TestCase(0)]
    [TestCase(7)]
    [TestCase(10)]
    public void NewGame_UsesInitialLevelOfEachConcreteStation(int initialLevel)
    {
        var config = Station(initialLevel);
        StationRuntimeState state = StationLifecycleRules.Create(config);
        Assert.That(state.ConfigId, Is.EqualTo(config.Id));
        Assert.That(state.Level, Is.EqualTo(initialLevel));
        Assert.That(state.IsOperational, Is.EqualTo(initialLevel > 0));
    }

    [Test]
    public void LegacyExistingStation_DefaultsToLevelOne_NotNewGameLevelZero()
    {
        var config = Station(0);
        Assert.That(StationLifecycleRules.Create(config, legacySave: true).Level, Is.EqualTo(1));
        Assert.That(StationLifecycleRules.Create(config).Level, Is.Zero);
    }

    [Test]
    public void OperationAsset_OffersIndependentPaymentAndTimeChoices()
    {
        var config = Station(0);
        var material = Make<ItemConfig>();
        Set(material, "id", "astralite_test");
        var creditsAndMaterial = new StationPaymentOption
        {
            Id = "credits_material", DurationSeconds = 0,
            Costs = new[]
            {
                new StationPaymentCost { Resource = StationPaymentResource.Credits, Amount = 100 },
                new StationPaymentCost { Resource = StationPaymentResource.Astralites, Item = material, Amount = 3 }
            }
        };
        var diamondsWithTime = Diamonds("diamonds_timed", 30);
        Set(config, "constructionPaymentOptions", new[] { creditsAndMaterial, diamondsWithTime });

        Assert.That(config.TryCreateOperation(StationOperationKind.Construction, 0,
            "credits_material", out var instant), Is.True);
        Assert.That(instant.DurationSeconds, Is.Zero);
        Assert.That(instant.PaidCosts.Length, Is.EqualTo(2));
        Assert.That(instant.PaidCosts[1].ItemConfigId, Is.EqualTo("astralite_test"));
        Assert.That(config.TryCreateOperation(StationOperationKind.Construction, 0,
            "diamonds_timed", out var timed), Is.True);
        Assert.That(timed.DurationSeconds, Is.EqualTo(30));
        Assert.That(timed.PaidCosts[0].Resource, Is.EqualTo(StationPaymentResource.Diamonds));
        Assert.That(timed.OperationId, Is.Not.EqualTo(instant.OperationId));
    }

    [Test]
    public void PaymentQuotes_RejectIncompleteMixedOrDuplicateResourceSets()
    {
        var creditOnly = new StationPaymentOption
        {
            Id = "credit_only", Costs = new[]
            { new StationPaymentCost { Resource = StationPaymentResource.Credits, Amount = 10 } }
        };
        var mixed = Diamonds("mixed", 1);
        mixed.Costs = new[]
        {
            new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = 2 },
            new StationPaymentCost { Resource = StationPaymentResource.Credits, Amount = 10 }
        };
        var duplicate = Diamonds("duplicate", 1);
        duplicate.Costs = new[] { duplicate.Costs[0], duplicate.Costs[0] };
        var invalidAmount = Diamonds("invalid_amount", 1);
        invalidAmount.Costs[0].Amount = 0;
        foreach (var option in new[] { creditOnly, mixed, duplicate, invalidAmount })
        {
            Assert.That(option.TryCreateSnapshot(StationOperationKind.Construction, 1, out var snapshot),
                Is.False, option.Id);
            Assert.That(snapshot, Is.Null, option.Id);
        }
    }

    [Test]
    public void ConfigQuotes_RejectUnknownOptionAndImpossibleTransitions()
    {
        var config = Station(0);
        Set(config, "constructionPaymentOptions", new[] { Diamonds("build", 10) });
        Set(config, "upgradeSettings", new[]
        {
            new StationUpgradeSettings { TargetLevel = 3,
                PaymentOptions = new[] { Diamonds("upgrade", 10) } }
        });
        Assert.That(config.TryCreateOperation(StationOperationKind.Construction, 0,
            "unknown", out _), Is.False);
        Assert.That(config.TryCreateOperation(StationOperationKind.Construction, 2,
            "build", out _), Is.False);
        Assert.That(config.TryCreateOperation(StationOperationKind.Upgrade, 0,
            "upgrade", out _), Is.False);
        Assert.That(config.TryCreateOperation(StationOperationKind.Upgrade, 10,
            "upgrade", out _), Is.False);
        Assert.That(config.TryCreateOperation(StationOperationKind.Upgrade, 1,
            "upgrade", out _), Is.False);
        Assert.That(config.TryCreateOperation(StationOperationKind.Upgrade, 2,
            "upgrade", out var valid), Is.True);
        Assert.That(valid.TargetLevel, Is.EqualTo(3));
    }

    [Test]
    public void AcceptedQuote_IsUnchangedByLaterAssetEdits()
    {
        var config = Station(0);
        var option = Diamonds("build", 20);
        Set(config, "constructionPaymentOptions", new[] { option });
        Assert.That(config.TryCreateOperation(StationOperationKind.Construction, 0,
            "build", out var snapshot), Is.True);
        option.Costs[0].Amount = 999;
        option.DurationSeconds = 999;
        option.Id = "renamed";
        Assert.That(snapshot.DurationSeconds, Is.EqualTo(20));
        Assert.That(snapshot.PaidCosts[0].Amount, Is.EqualTo(7));
        Assert.That(snapshot.PaymentOptionId, Is.EqualTo("build"));
    }

    [Test]
    public void GalaxyPartJson_PreservesBothOperationsAndResumesAfterRepelledAttack()
    {
        var system = StationLifecycleRulesEditModeTests.SystemAt(4);
        var operation = StationLifecycleRulesEditModeTests.Operation(StationOperationKind.Upgrade, 5, 100);
        StationLifecycleRules.BeginPaidOperation(system.Station, operation);
        StationLifecycleRules.Advance(system, 25);
        StationLifecycleRules.Destroy(system.Station);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            StationLifecycleRulesEditModeTests.Operation(StationOperationKind.Recovery, 4, 10));
        StationLifecycleRules.Advance(system, 4);
        system.SystemStatus = StarSystemStatus.Invasion;
        StationLifecycleRules.ApplySystemStatus(system.Station, system.SystemStatus);

        var source = new SaveGalaxyPartData();
        source.Galaxy.Systems.Add(system);
        var loaded = JsonUtility.FromJson<SaveGalaxyPartData>(JsonUtility.ToJson(source));
        var restored = loaded.Galaxy.Systems[0];
        StationLifecycleRules.Normalize(restored);
        StationLifecycleRules.Advance(restored, 100);
        Assert.That(restored.Station.Level, Is.EqualTo(4));
        Assert.That(restored.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        Assert.That(restored.Station.Development.OperationId, Is.EqualTo(operation.OperationId));
        Assert.That(restored.Station.Development.PaidCosts[0].Amount, Is.EqualTo(7));
        Assert.That(restored.Station.Recovery.ElapsedSeconds, Is.EqualTo(4));
        Assert.That(restored.Station.Recovery.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
        restored.SystemStatus = StarSystemStatus.Liberated;
        StationLifecycleRules.Advance(restored, 9);
        Assert.That(restored.Station.Recovery, Is.Null);
        Assert.That(restored.Station.Development.ElapsedSeconds, Is.EqualTo(28));
    }

    [Test]
    public void LegacyMigration_LeavesAbsentStationForConfigAwareSessionBinding()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.MissionBattleAndRoutes;
        state.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "legacy_system" });
        Assert.That(SaveMigrationService.Migrate(state), Is.True);
        Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.StationLifecycle));
        Assert.That(state.Galaxy.Systems[0].Station, Is.Null);
        Assert.That(state.Galaxy.Systems[0].StationStateVersion, Is.Zero);
    }

    [Test]
    public void RepeatedMigration_DoesNotResetReachedLevelOrPaidOperation()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.StationLifecycle;
        var system = StationLifecycleRulesEditModeTests.SystemAt(6);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            StationLifecycleRulesEditModeTests.Operation(StationOperationKind.Upgrade, 7, 100));
        StationLifecycleRules.Advance(system, 37);
        state.Galaxy.Systems.Add(system);
        SaveMigrationService.Migrate(state);
        string once = JsonUtility.ToJson(state);
        SaveMigrationService.Migrate(state);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(once));
        Assert.That(system.Station.Level, Is.EqualTo(6));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(37));
    }

    [Test]
    public void JsonRoundTrip_WithOnlyDevelopment_RemovesAbsentRecoveryAndPreservesPaidProgress()
    {
        var system = StationLifecycleRulesEditModeTests.SystemAt(2);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            StationLifecycleRulesEditModeTests.Operation(StationOperationKind.Upgrade, 3, 100000));
        StationLifecycleRules.Advance(system, 43200);
        string operationId = system.Station.Development.OperationId;
        var restored = JsonUtility.FromJson<StarSystemRuntimeState>(JsonUtility.ToJson(system));
        StationLifecycleRules.Normalize(restored);
        var galaxy = new GalaxyRuntimeState { Systems = new List<StarSystemRuntimeState> { restored } };
        Assert.That(StationLifecycleRules.GetValidationErrors(galaxy), Is.Empty);
        Assert.That(restored.Station.Recovery, Is.Null);
        Assert.That(restored.Station.Development.OperationId, Is.EqualTo(operationId));
        Assert.That(restored.Station.Development.DurationSeconds, Is.EqualTo(100000));
        Assert.That(restored.Station.Development.ElapsedSeconds, Is.EqualTo(43200));
    }

    [Test]
    public void EmptyOperationPlaceholders_AreRemovedBeforeSessionValidation()
    {
        var system = StationLifecycleRulesEditModeTests.SystemAt(2);
        system.Station.Development = new StationOperationRuntimeState();
        system.Station.Recovery = new StationOperationRuntimeState();
        Assert.That(StationLifecycleRules.NormalizeSerializedPlaceholders(system), Is.True);
        Assert.That(system.Station.Development, Is.Null);
        Assert.That(system.Station.Recovery, Is.Null);
        var galaxy = new GalaxyRuntimeState { Systems = new List<StarSystemRuntimeState> { system } };
        Assert.That(StationLifecycleRules.GetValidationErrors(galaxy), Is.Empty);
        Assert.That(StationLifecycleRules.NormalizeSerializedPlaceholders(system), Is.False);
    }

    [Test]
    public void BrokenRealOperation_IsNotDiscardedAsAnEmptyPlaceholder()
    {
        var system = StationLifecycleRulesEditModeTests.SystemAt(2);
        var broken = StationLifecycleRulesEditModeTests.Operation(StationOperationKind.Upgrade, 3, 60);
        broken.OperationId = string.Empty;
        system.Station.Development = broken;
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Development, Is.SameAs(broken));
        var galaxy = new GalaxyRuntimeState { Systems = new List<StarSystemRuntimeState> { system } };
        Assert.That(StationLifecycleRules.GetValidationErrors(galaxy), Is.Not.Empty);
    }

    [Test]
    public void JsonRoundTrip_EmptyStationSlot_RemainsEmptyWithoutResettingSchemaMarker()
    {
        var system = new StarSystemRuntimeState
        { SystemId = "empty_station_slot", StationStateVersion = StationLifecycleRules.SchemaVersion };
        var restored = JsonUtility.FromJson<StarSystemRuntimeState>(JsonUtility.ToJson(system));
        StationLifecycleRules.Normalize(restored);
        Assert.That(restored.Station, Is.Null);
        Assert.That(restored.StationStateVersion, Is.EqualTo(StationLifecycleRules.SchemaVersion));
    }

    [Test]
    public void LegacyVersionNineChecksum_ValidatesOriginalJsonAndStillRejectsTampering()
    {
        // Actual old payload: no station schema/state or station offline watermark fields.
        const string original = "{\"Meta\":{\"SaveDataVersion\":9,\"IntegrityChecksum\":\"\"},\"Player\":{\"Credits\":1000}}";
        string checksum;
        using (var sha = SHA256.Create())
            checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(original)))
                .Replace("-", "").ToLowerInvariant();
        string json = original.Replace("\"IntegrityChecksum\":\"\"", "\"IntegrityChecksum\":\"" + checksum + "\"");
        var stage = new SaveIntegrityStage();
        Assert.That(stage.VerifySerializedJson(JsonUtility.FromJson<GameRuntimeState>(json), json),
            Is.EqualTo(SaveIntegrityStatus.Valid));
        string tampered = json.Replace("1000", "9999");
        Assert.That(stage.VerifySerializedJson(JsonUtility.FromJson<GameRuntimeState>(tampered), tampered),
            Is.EqualTo(SaveIntegrityStatus.Invalid));
    }

    [Test]
    public void StationLifecycleLogs_RequireBothChannelAndMasterSwitch()
    {
        var config = Make<DebugLogConfig>();
        Assert.That(config.IsEnabled(DebugLogChannel.StationLifecycle), Is.False);
        Set(config, "logsEnabled", true);
        Set(config, "stationLifecycleLogs", true);
        Assert.That(config.IsEnabled(DebugLogChannel.StationLifecycle), Is.True);
        Set(config, "logsEnabled", false);
        Assert.That(config.IsEnabled(DebugLogChannel.StationLifecycle), Is.False);
        Set(config, "logsEnabled", true);
        Set(config, "stationLifecycleLogs", false);
        Assert.That(config.IsEnabled(DebugLogChannel.StationLifecycle), Is.False);
    }

    [Test]
    public void SharedOfflineWindow_CapsOneElapsedPeriodAtTwelveHours()
    {
        long from = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc).Ticks;
        Assert.That(OfflineProgressWindow.ElapsedSeconds(from, from + TimeSpan.FromHours(30).Ticks),
            Is.EqualTo(12d * 3600d));
        Assert.That(OfflineProgressWindow.ElapsedSeconds(from, from + TimeSpan.FromMinutes(90).Ticks),
            Is.EqualTo(90d * 60d));
    }

    [Test]
    public void InvalidOfflineTimestamps_CannotGrantProgress()
    {
        long now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc).Ticks;
        Assert.That(OfflineProgressWindow.ElapsedSeconds(0, now), Is.Zero);
        Assert.That(OfflineProgressWindow.ElapsedSeconds(now, now), Is.Zero);
        Assert.That(OfflineProgressWindow.ElapsedSeconds(now, now - 1), Is.Zero);
        Assert.That(OfflineProgressWindow.ElapsedSeconds(now, long.MaxValue), Is.Zero);
    }

    private StationConfig Station(int level)
    {
        var config = Make<StationConfig>();
        Set(config, "id", "station_asset_test");
        Set(config, "initialLevel", level);
        Set(config, "isActive", true);
        return config;
    }

    private T Make<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        _objects.Add(instance);
        return instance;
    }

    private static StationPaymentOption Diamonds(string id, float duration)
    {
        return new StationPaymentOption
        {
            Id = id, DurationSeconds = duration,
            Costs = new[] { new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = 7 } }
        };
    }

    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }
}
