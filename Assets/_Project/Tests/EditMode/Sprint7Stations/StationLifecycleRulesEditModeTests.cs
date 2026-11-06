using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>S07-01: pure station lifecycle, independent of an open scene and wallet.</summary>
public sealed class StationLifecycleRulesEditModeTests
{
    [Test]
    public void LevelZero_IsNotOperationalOrDockable_UntilConstructionFinishes()
    {
        var system = SystemAt(0);
        Assert.That(system.Station.IsOperational, Is.False);
        Assert.That(system.Station.CanDock, Is.False);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Construction, 1, 10)), Is.True);
        StationLifecycleRules.Advance(system, 9);
        Assert.That(system.Station.Level, Is.Zero);
        Assert.That(system.Station.CanDock, Is.False);
        StationLifecycleRules.Advance(system, 1);
        Assert.That(system.Station.Level, Is.EqualTo(1));
        Assert.That(system.Station.IsOperational, Is.True);
        Assert.That(system.Station.CanDock, Is.True);
    }

    [Test]
    public void BeginPaidOperation_OwnsPaymentSnapshot_AssetQuoteCannotRepriceIt()
    {
        var system = SystemAt(2);
        var quote = Operation(StationOperationKind.Upgrade, 3, 60);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station, quote), Is.True);
        quote.PaidCosts[0].Amount = 999;
        quote.DurationSeconds = 999;
        quote.PaymentOptionId = "changed_asset_variant";
        Assert.That(system.Station.Development.PaidCosts[0].Amount, Is.EqualTo(7));
        Assert.That(system.Station.Development.DurationSeconds, Is.EqualTo(60));
        Assert.That(system.Station.Development.PaymentOptionId, Is.EqualTo("diamonds_timed"));
    }

    [Test]
    public void Upgrade_KeepsPreviousLevelUntilCompletion_AndDoesNotCompleteTwice()
    {
        var system = SystemAt(4);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Upgrade, 5, 20));
        StationLifecycleRules.Advance(system, 19);
        Assert.That(system.Station.Level, Is.EqualTo(4));
        Assert.That(system.Station.IsOperational, Is.True);
        StationLifecycleRules.Advance(system, 1);
        Assert.That(system.Station.Level, Is.EqualTo(5));
        Assert.That(system.Station.Development, Is.Null);
        StationLifecycleRules.Advance(system, 100);
        Assert.That(system.Station.Level, Is.EqualTo(5));
    }

    [TestCase(StationOperationKind.Construction, 0)]
    [TestCase(StationOperationKind.Upgrade, 3)]
    public void InstantDevelopment_CompletesWithZeroAdvance(StationOperationKind kind, int level)
    {
        var system = SystemAt(level);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(kind, level + 1, 0)), Is.True);
        StationLifecycleRules.Advance(system, 0);
        Assert.That(system.Station.Level, Is.EqualTo(level + 1));
        Assert.That(system.Station.Development, Is.Null);
    }

    [Test]
    public void InvalidOrDuplicateBegin_DoesNotReplacePaidDevelopment()
    {
        var system = SystemAt(2);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Upgrade, 5, 30)), Is.False);
        Assert.That(system.Station.Development, Is.Null);
        var first = Operation(StationOperationKind.Upgrade, 3, 30);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station, first), Is.True);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Upgrade, 3, 0)), Is.False);
        Assert.That(system.Station.Development.OperationId, Is.EqualTo(first.OperationId));
        Assert.That(system.Station.Development.DurationSeconds, Is.EqualTo(30));
    }

    [Test]
    public void OrdinaryInfrastructureDamage_PausesUpgradeWithoutDisablingStation()
    {
        var system = SystemAt(3);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Upgrade, 4, 20));
        system.ApplyInfrastructureDamageFromWar(15, false);
        StationLifecycleRules.Advance(system, 10);
        Assert.That(system.InfrastructureDamageState, Is.EqualTo(SystemInfrastructureDamageState.Damaged));
        Assert.That(system.Station.IsOperational, Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(3));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.Zero);
        Assert.That(system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.Damaged));
    }

    [TestCase(StationOperationKind.Construction, 0)]
    [TestCase(StationOperationKind.Upgrade, 4)]
    public void Destruction_PreservesReachedLevelAndPaidDevelopment(StationOperationKind kind, int level)
    {
        var system = SystemAt(level);
        var operation = Operation(kind, level + 1, 100);
        StationLifecycleRules.BeginPaidOperation(system.Station, operation);
        StationLifecycleRules.Advance(system, 25);
        StationLifecycleRules.Destroy(system.Station);
        StationLifecycleRules.Advance(system, 1000);
        Assert.That(system.Station.IsDestroyed, Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(level));
        Assert.That(system.Station.IsOperational, Is.False);
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        Assert.That(system.Station.Development.OperationId, Is.EqualTo(operation.OperationId));
        Assert.That(system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.Destroyed));
    }

    [TestCase(StationOperationKind.Construction, 0)]
    [TestCase(StationOperationKind.Upgrade, 4)]
    public void Recovery_UsesOwnProgress_ThenSpendsOnlyRemainingTimeOnDevelopment(
        StationOperationKind kind, int level)
    {
        var system = SystemAt(level);
        StationLifecycleRules.BeginPaidOperation(system.Station, Operation(kind, level + 1, 100));
        StationLifecycleRules.Advance(system, 25);
        StationLifecycleRules.Destroy(system.Station);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Recovery, level, 10)), Is.True);
        StationLifecycleRules.Advance(system, 4);
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.EqualTo(4));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        Assert.That(system.Station.CanDock, Is.False);
        StationLifecycleRules.Advance(system, 9);
        Assert.That(system.Station.Recovery, Is.Null);
        Assert.That(system.Station.IsDestroyed, Is.False);
        Assert.That(system.InfrastructureDamage, Is.Zero);
        Assert.That(system.InfrastructureDamageState, Is.EqualTo(SystemInfrastructureDamageState.Intact));
        Assert.That(system.Station.Level, Is.EqualTo(level));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(28));
        StationLifecycleRules.Advance(system, 72);
        Assert.That(system.Station.Level, Is.EqualTo(level + 1));
        Assert.That(system.Station.Development, Is.Null);
    }

    [Test]
    public void Recovery_PausesOnInvasion_AutomaticallyResumesAfterRepelledAttack()
    {
        var system = RecoveringSystem();
        StationLifecycleRules.Advance(system, 4);
        system.SystemStatus = StarSystemStatus.Invasion;
        StationLifecycleRules.Advance(system, 100);
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.EqualTo(4));
        Assert.That(system.Station.Recovery.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
        system.SystemStatus = StarSystemStatus.Liberated;
        StationLifecycleRules.Advance(system, 3);
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.EqualTo(7));
        Assert.That(system.Station.Recovery.PauseReason, Is.EqualTo(StationOperationPauseReason.None));
    }

    [Test]
    public void ThreatForecast_IsNotAnActualAttack_DoesNotPauseRecovery()
    {
        var system = RecoveringSystem();
        system.SystemStatus = StarSystemStatus.Threat;
        StationLifecycleRules.Advance(system, 3);
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.EqualTo(3));
        Assert.That(system.Station.Recovery.PauseReason, Is.EqualTo(StationOperationPauseReason.None));
    }

    [Test]
    public void Capture_DiscardsOnlyRecovery_NextRecoveryStartsFromZero()
    {
        var system = SystemAt(5);
        var upgrade = Operation(StationOperationKind.Upgrade, 6, 100);
        StationLifecycleRules.BeginPaidOperation(system.Station, upgrade);
        StationLifecycleRules.Advance(system, 20);
        StationLifecycleRules.Destroy(system.Station);
        var firstRecovery = Operation(StationOperationKind.Recovery, 5, 10);
        StationLifecycleRules.BeginPaidOperation(system.Station, firstRecovery);
        StationLifecycleRules.Advance(system, 6);
        StationLifecycleRules.ApplySystemStatus(system.Station, StarSystemStatus.Captured);
        Assert.That(system.Station.Recovery, Is.Null);
        Assert.That(system.Station.IsDestroyed, Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(5));
        Assert.That(system.Station.Development.OperationId, Is.EqualTo(upgrade.OperationId));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(20));
        var secondRecovery = Operation(StationOperationKind.Recovery, 5, 10);
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station, secondRecovery), Is.True);
        Assert.That(system.Station.Recovery.OperationId, Is.Not.EqualTo(firstRecovery.OperationId));
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.Zero);
    }

    [Test]
    public void RecoveringStation_IsNotAttackable_AdditionalDestructionCannotEraseRecovery()
    {
        var system = RecoveringSystem();
        StationLifecycleRules.Advance(system, 3);
        string id = system.Station.Recovery.OperationId;
        Assert.That(system.Station.CanBeAttacked, Is.False);
        StationLifecycleRules.Destroy(system.Station);
        Assert.That(system.Station.Recovery.OperationId, Is.EqualTo(id));
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.EqualTo(3));
        StationLifecycleRules.Advance(system, 7);
        Assert.That(system.Station.IsDestroyed, Is.False);
        Assert.That(system.Station.CanBeAttacked, Is.True);
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidElapsedTime_DoesNotAdvanceOrCompleteOperation(double delta)
    {
        var system = SystemAt(2);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Upgrade, 3, 10));
        StationLifecycleRules.Advance(system, delta);
        Assert.That(system.Station.Level, Is.EqualTo(2));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.Zero);
    }

    [Test]
    public void Normalize_ClampsFiniteBounds_AndRepeatedNormalizationIsIdempotent()
    {
        var system = SystemAt(4);
        system.Station.Level = -5;
        system.Station.Development = Operation(StationOperationKind.Construction, 1, 20);
        system.Station.Development.ElapsedSeconds = 50;
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Level, Is.Zero);
        Assert.That(system.Station.Development.DurationSeconds, Is.EqualTo(20));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(20));
        string normalized = JsonUtility.ToJson(system);
        Assert.That(StationLifecycleRules.Normalize(system), Is.False);
        Assert.That(JsonUtility.ToJson(system), Is.EqualTo(normalized));
    }

    [Test]
    public void CorruptDuration_IsRejectedInsteadOfTurningIntoAFreeOrNeverEndingOperation()
    {
        var system = SystemAt(2);
        system.Station.Development = Operation(StationOperationKind.Upgrade, 3, double.NaN);
        var galaxy = new GalaxyRuntimeState { Systems = new List<StarSystemRuntimeState> { system } };
        StationLifecycleRules.Normalize(system);
        Assert.That(StationLifecycleRules.GetValidationErrors(galaxy).Any(), Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(2));
    }

    [Test]
    public void Validation_DetectsDuplicateOwners_WithoutMutatingSavedDevelopment()
    {
        var first = SystemAt(2);
        first.Station.Development = Operation(StationOperationKind.Upgrade, 3, 10);
        var second = SystemAt(4);
        var galaxy = new GalaxyRuntimeState { Systems = new List<StarSystemRuntimeState> { first, second } };
        string before = JsonUtility.ToJson(galaxy);
        Assert.That(StationLifecycleRules.GetValidationErrors(galaxy)
            .Any(message => message.Contains("Duplicate")), Is.True);
        Assert.That(JsonUtility.ToJson(galaxy), Is.EqualTo(before));
    }

    [Test]
    public void Validation_RejectsRecoveryForIntactStation_AndNonConsecutiveUpgrade()
    {
        var system = SystemAt(2);
        system.Station.Recovery = Operation(StationOperationKind.Recovery, 2, 10);
        system.Station.Development = Operation(StationOperationKind.Upgrade, 8, 10);
        var galaxy = new GalaxyRuntimeState { Systems = new List<StarSystemRuntimeState> { system } };
        string[] errors = StationLifecycleRules.GetValidationErrors(galaxy).ToArray();
        Assert.That(errors.Any(message => message.Contains("recovery")), Is.True);
        Assert.That(errors.Any(message => message.Contains("development")), Is.True);
    }

    [Test]
    public void StateCopy_SeparatesBothOperationsAndTheirPaidCosts()
    {
        var system = RecoveringSystem();
        system.Station.Development = Operation(StationOperationKind.Upgrade, 3, 100);
        StationRuntimeState copy = system.Station.Copy();
        copy.Recovery.ElapsedSeconds = 8;
        copy.Recovery.PaidCosts[0].Amount = 80;
        copy.Development.ElapsedSeconds = 90;
        copy.Development.PaidCosts[0].Amount = 90;
        Assert.That(system.Station.Recovery.ElapsedSeconds, Is.Zero);
        Assert.That(system.Station.Recovery.PaidCosts[0].Amount, Is.EqualTo(7));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.Zero);
        Assert.That(system.Station.Development.PaidCosts[0].Amount, Is.EqualTo(7));
    }

    internal static StarSystemRuntimeState SystemAt(int level)
    {
        return new StarSystemRuntimeState
        {
            SystemId = "system_station_test", StationStateVersion = StationLifecycleRules.SchemaVersion,
            SystemStatus = StarSystemStatus.Stable,
            Station = new StationRuntimeState
            { ConfigId = "station_test", StationType = StationType.Science, Level = level }
        };
    }

    internal static StationOperationRuntimeState Operation(StationOperationKind kind, int target, double seconds)
    {
        return new StationOperationRuntimeState
        {
            OperationId = Guid.NewGuid().ToString("N"), Kind = kind, TargetLevel = target,
            PaymentOptionId = "diamonds_timed", DurationSeconds = seconds,
            PaidCosts = new[] { new StationPaymentCostSnapshot
                { Resource = StationPaymentResource.Diamonds, Amount = 7 } }
        };
    }

    private static StarSystemRuntimeState RecoveringSystem()
    {
        var system = SystemAt(2);
        StationLifecycleRules.Destroy(system.Station);
        StationLifecycleRules.BeginPaidOperation(system.Station,
            Operation(StationOperationKind.Recovery, 2, 10));
        return system;
    }

    [Test] public void InstantUpgrade_CannotFinishWhileEnemiesArePresent()
    {
        var system = SystemAt(2);
        StationLifecycleRules.BeginPaidOperation(system.Station, Operation(StationOperationKind.Upgrade, 3, 0));
        StationLifecycleRules.Advance(system, 0, hasEnemies: true);
        Assert.That(system.Station.Level, Is.EqualTo(2));
        Assert.That(system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
        StationLifecycleRules.Advance(system, 0, hasEnemies: false);
        Assert.That(system.Station.Level, Is.EqualTo(3)); Assert.That(system.Station.Development, Is.Null);
    }

    [Test] public void EmptySerializerOperations_DoNotIncrementStationRevision()
    {
        var system = SystemAt(2); StationLifecycleRules.Normalize(system);
        long revision = system.Station.Revision;
        system.Station.Development = new StationOperationRuntimeState();
        system.Station.Recovery = new StationOperationRuntimeState();
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Development, Is.Null); Assert.That(system.Station.Recovery, Is.Null);
        Assert.That(system.Station.Revision, Is.EqualTo(revision));
        Assert.That(StationLifecycleRules.Normalize(system), Is.False);
    }
    [Test] public void RealClockNormalization_StillIncrementsRevisionOnlyOnce()
    {
        var system = SystemAt(2); StationLifecycleRules.Normalize(system);
        system.Station.Development = Operation(StationOperationKind.Upgrade, 3, 60);
        system.Station.Development.ElapsedSeconds = 70;
        long revision = system.Station.Revision;
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(60));
        Assert.That(system.Station.Revision, Is.GreaterThan(revision));
        revision = system.Station.Revision; StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Revision, Is.EqualTo(revision));
    }
}
