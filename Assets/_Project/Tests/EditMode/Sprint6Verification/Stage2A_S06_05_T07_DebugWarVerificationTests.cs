using System.IO;
using NUnit.Framework;

public sealed class Stage2A_S06_05_T07_DebugWarVerificationTests
{
    [Test]
    public void T07_DebugController_HasForcedInvasionCommand()
    {
        string debugControllerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Force Invasion"));

        Assert.That(
            debugControllerText,
            Does.Contain("DebugForceInvasion"));

        Assert.That(
            debugControllerText,
            Does.Contain("TryStartInvasion"));

        Assert.That(
            debugControllerText,
            Does.Contain("TryPickRandomInvasionCandidate"));

        Assert.That(
            debugControllerText,
            Does.Contain("Force invasion blocked by InvasionService validation"));
    }

    [Test]
    public void T07_DebugController_HasSystemStatusSwitchCommands()
    {
        string debugControllerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Status/Apply Inspector Status"));

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Status/Set Stable"));

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Status/Set Threat"));

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Status/Set Invasion"));

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Status/Set Captured"));

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Status/Set Liberated Recovery"));

        Assert.That(
            debugControllerText,
            Does.Contain("DebugApplySystemStatus"));

        Assert.That(
            debugControllerText,
            Does.Contain("CaptureSystem"));

        Assert.That(
            debugControllerText,
            Does.Contain("LiberateSystemByPlayer"));

        Assert.That(
            debugControllerText,
            Does.Contain("MarkRecoveryHookPending"));
    }

    [Test]
    public void T07_DebugController_HasOfflineWarStepCommand()
    {
        string debugControllerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(
            debugControllerText,
            Does.Contain("STAR FRONTIER/War/Offline/Run War Offline Step"));

        Assert.That(
            debugControllerText,
            Does.Contain("DebugRunWarOfflineStep"));

        Assert.That(
            debugControllerText,
            Does.Contain("ProcessOfflineWarCatchUp"));

        Assert.That(
            debugControllerText,
            Does.Contain("debugOfflineWarTargetTickOffset"));

        Assert.That(
            debugControllerText,
            Does.Contain("debugOfflineWarUseCurrentTickAsBase"));

        Assert.That(
            debugControllerText,
            Does.Contain("ActiveInvasionsBefore"));

        Assert.That(
            debugControllerText,
            Does.Contain("ActiveInvasionsAfter"));
    }

    [Test]
    public void T07_OfflineWarCatchUp_ResolvesActiveInvasionAndCapturesTarget()
    {
        GameRuntimeState state =
            CreateWarRuntimeState();

        state.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_ai_01",
                FactionId = "ai",
                SourceSystemId = "system_ai_origin",
                TargetSystemId = "system_target",
                Level = 1,
                LifecycleState = InvasionLifecycleState.Active,
                StartedAtTick = 1,
                LastUpdatedTick = 1,
                NextUpdateTick = 2,
                ResolveAtTick = 3,
                CleanupAfterTick = 0
            });

        int changedCount =
            state.Galaxy.ProcessOfflineWarCatchUp(3);

        InvasionState invasion =
            state.Galaxy.Invasions[0];

        StarSystemRuntimeState targetSystem =
            FindSystem(
                state,
                "system_target");

        Assert.That(changedCount, Is.GreaterThan(0));
        Assert.That(invasion.LifecycleState, Is.EqualTo(InvasionLifecycleState.Resolved));
        Assert.That(invasion.CleanupAfterTick, Is.EqualTo(4));
        Assert.That(targetSystem.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
    }

    [Test]
    public void T07_OfflineWarCatchUp_PreventsHopelessCollapse()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_last",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 50,
                DevelopmentLevel = 2
            });

        state.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_last_01",
                FactionId = "infected",
                SourceSystemId = "system_infected_origin",
                TargetSystemId = "system_last",
                Level = 1,
                LifecycleState = InvasionLifecycleState.Active,
                StartedAtTick = 1,
                LastUpdatedTick = 1,
                NextUpdateTick = 2,
                ResolveAtTick = 3,
                CleanupAfterTick = 0
            });

        int changedCount =
            state.Galaxy.ProcessOfflineWarCatchUp(3);

        StarSystemRuntimeState targetSystem =
            FindSystem(
                state,
                "system_last");

        Assert.That(changedCount, Is.GreaterThan(0));
        Assert.That(targetSystem.SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
    }

    [Test]
    public void T07_OfflineWarCatchUp_DegradesThreatIntoResolvedWarState()
    {
        GameRuntimeState state =
            CreateWarRuntimeState();

        StarSystemRuntimeState targetSystem =
            FindSystem(
                state,
                "system_target");

        targetSystem.SystemStatus =
            StarSystemStatus.Threat;

        int changedCount =
            state.Galaxy.ProcessOfflineWarCatchUp(3);

        bool hasAllowedResolvedStatus =
            targetSystem.SystemStatus == StarSystemStatus.Invasion ||
            targetSystem.SystemStatus == StarSystemStatus.Captured ||
            targetSystem.SystemStatus == StarSystemStatus.RecoveryReady;

        Assert.That(changedCount, Is.GreaterThan(0));
        Assert.That(
            hasAllowedResolvedStatus,
            Is.True,
            "Threat offline catch-up must move the system into Invasion, Captured or RecoveryReady.");
    }

    [Test]
    public void T07_SystemStatusTransitions_PreserveRecoveryHookContract()
    {
        StarSystemRuntimeState system =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Captured,
                Stability = 0,
                DevelopmentLevel = 1
            };

        system.MarkLiberatedByPlayer();
        system.MarkRecoveryHookPending(
            12,
            "debug_liberation");

        Assert.That(system.SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(system.HasPendingRecoveryHook, Is.True);
        Assert.That(system.RecoveryHookCreatedAtTick, Is.EqualTo(12));
        Assert.That(system.RecoveryHookReason, Is.EqualTo("debug_liberation"));

        system.ClearRecoveryHook();

        Assert.That(system.HasPendingRecoveryHook, Is.False);
        Assert.That(system.RecoveryHookCreatedAtTick, Is.Zero);
        Assert.That(system.RecoveryHookReason, Is.EqualTo(string.Empty));
    }

    [Test]
    public void T07_DebugWarLogs_UseDebugLogConfigPath()
    {
        string debugControllerText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(
            debugControllerText,
            Does.Contain("LogWarDebug"));

        Assert.That(
            debugControllerText,
            Does.Contain("IsDebugLogEnabled(warDebugLogChannel)"));

        Assert.That(
            debugControllerText,
            Does.Contain("Bootstrapper.Instance.LogDebug"));

        Assert.That(
            debugControllerText,
            Does.Not.Contain("Debug.Log(\"[DebugController2A][War]"));

        Assert.That(
            debugControllerText,
            Does.Not.Contain("Debug.LogWarning(\"[DebugController2A][War]"));
    }

    private static GameRuntimeState CreateWarRuntimeState()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_ai_origin",
                SystemStatus = StarSystemStatus.Captured,
                Stability = 40,
                DevelopmentLevel = 2
            });

        state.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 50,
                DevelopmentLevel = 2
            });

        state.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_safe",
                SystemStatus = StarSystemStatus.Stable,
                Stability = 80,
                DevelopmentLevel = 3
            });

        return state;
    }

    private static StarSystemRuntimeState FindSystem(
        GameRuntimeState state,
        string systemId)
    {
        for (int i = 0; i < state.Galaxy.Systems.Count; i++)
        {
            StarSystemRuntimeState system =
                state.Galaxy.Systems[i];

            if (system == null)
                continue;

            if (system.SystemId == systemId)
                return system;
        }

        Assert.Fail("System was not found: " + systemId);
        return null;
    }

    private static string ReadProjectFile(
        string path)
    {
        Assert.That(
            File.Exists(path),
            Is.True,
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}