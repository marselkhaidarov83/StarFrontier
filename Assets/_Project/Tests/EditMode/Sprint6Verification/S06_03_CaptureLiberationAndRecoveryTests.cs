using NUnit.Framework;

/// <summary>2A-S06-03: capture, liberation, re-capture and infrastructure damage hooks.</summary>
public sealed class S06_03_CaptureLiberationAndRecoveryTests
{
    [Test]
    public void Capture_DegradesSystemAndDamagesInfrastructure_WithoutNegativeValues()
    {
        var system = new StarSystemRuntimeState
        {
            SystemId = "system_a", SystemStatus = StarSystemStatus.Invasion,
            Stability = 20, DevelopmentLevel = 1
        };

        system.MarkCaptured();

        Assert.That(system.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(system.Stability, Is.Zero);
        Assert.That(system.DevelopmentLevel, Is.Zero);
        Assert.That(system.InfrastructureDamage, Is.EqualTo(35));
        Assert.That(system.HasDamagedInfrastructure(), Is.True);
        Assert.That(system.IsSecured(0), Is.False);
    }

    [Test]
    public void DestroyedInfrastructure_CapsAt100AndCannotBeHealedByNegativeDamage()
    {
        var system = new StarSystemRuntimeState();
        system.ApplyInfrastructureDamageFromWar(80, false);
        system.ApplyInfrastructureDamageFromWar(50, false);
        system.ApplyInfrastructureDamageFromWar(-1000, false);

        Assert.That(system.InfrastructureDamage, Is.EqualTo(100));
        Assert.That(system.HasDestroyedInfrastructure(), Is.True);
    }

    [Test]
    public void PlayerLiberation_OpensRecoveryHook_AndRecaptureClearsPendingHook()
    {
        var system = new StarSystemRuntimeState
        {
            SystemId = "system_a", SystemStatus = StarSystemStatus.Captured,
            Stability = 0, DevelopmentLevel = 1
        };

        system.MarkLiberatedByPlayer();
        system.MarkRecoveryHookPending(20, "player_liberation");
        Assert.That(system.SystemStatus, Is.EqualTo(StarSystemStatus.Liberated));
        Assert.That(system.Stability, Is.GreaterThanOrEqualTo(10));
        Assert.That(system.HasPendingRecoveryHook, Is.True);
        Assert.That(system.IsSecured(0), Is.False);

        system.MarkCaptured();
        Assert.That(system.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(system.HasPendingRecoveryHook, Is.False);
        Assert.That(system.RecoveryHookCreatedAtTick, Is.Zero);
        Assert.That(system.RecoveryHookReason, Is.Empty);
    }

    [Test]
    public void ElapsedOfflineInvasion_DoesNotCaptureOrEstablishOwnershipWithoutCombatState()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "enemy_origin", SystemStatus = StarSystemStatus.Captured,
            Stability = 50, DevelopmentLevel = 1
        });
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "target", SystemStatus = StarSystemStatus.Invasion,
            Stability = 80, DevelopmentLevel = 3
        });
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "remaining_safe", SystemStatus = StarSystemStatus.Stable,
            Stability = 80, DevelopmentLevel = 3
        });
        galaxy.Invasions.Add(new InvasionState
        {
            InvasionId = "inv_ai_1", FactionId = "ai",
            SourceSystemId = "enemy_origin", TargetSystemId = "target",
            LifecycleState = InvasionLifecycleState.Active,
            ResolveAtTick = 5, StartedAtTick = 1
        });

        galaxy.ProcessOfflineWarCatchUp(5);
        Assert.That(galaxy.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
        Assert.That(galaxy.Systems[1].SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
        Assert.That(galaxy.EnemyFactions, Is.Empty,
            "Elapsed offline time must not establish enemy ownership without restored combat state.");
        Assert.That(galaxy.Invasions[0].LastUpdatedTick, Is.EqualTo(5));
        Assert.That(galaxy.Invasions[0].NextUpdateTick, Is.EqualTo(6));

        galaxy.ProcessOfflineWarCatchUp(5);
        Assert.That(galaxy.Invasions, Has.Count.EqualTo(1));
        Assert.That(galaxy.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
        Assert.That(galaxy.Systems[1].SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
        Assert.That(galaxy.EnemyFactions, Is.Empty);
        Assert.That(galaxy.Invasions[0].LastUpdatedTick, Is.EqualTo(5));
        Assert.That(galaxy.Invasions[0].NextUpdateTick, Is.EqualTo(6));
    }

    [Test]
    public void AllSystemsCaptured_OfflineRepairPreservesRecoveryPath()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "one", SystemStatus = StarSystemStatus.Captured
        });
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "two", SystemStatus = StarSystemStatus.Captured
        });

        Assert.That(galaxy.RepairHopelessCollapse(), Is.True);
        Assert.That(galaxy.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(galaxy.RepairHopelessCollapse(), Is.False);
    }
}
