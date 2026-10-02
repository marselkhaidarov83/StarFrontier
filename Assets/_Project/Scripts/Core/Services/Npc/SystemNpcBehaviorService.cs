using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public sealed class SystemNpcBehaviorService : CustomService, ISystemNpcBehaviorService
{
    private sealed class NpcBehaviorSelectAnalytics
    {
        private sealed class Entry
        {
            public SystemNpcBehaviorType Behavior;
            public int Count;
            public double ResolveScenarioMs;
            public double AssignNewMs;
            public double ReassignMs;
            public double ActiveBehaviorMs;
            public double DeferredMs;
        }

        private readonly Dictionary<SystemNpcBehaviorType, Entry> _entries =
            new Dictionary<SystemNpcBehaviorType, Entry>();

        public void AddResolve(
            SystemNpcBehaviorType behavior,
            double elapsedMs)
        {
            Entry entry =
                GetOrCreateEntry(behavior);

            entry.ResolveScenarioMs += Mathf.Max(0f, (float)elapsedMs);
            entry.Count++;
        }

        public void AddAssignNew(
            SystemNpcBehaviorType behavior,
            double elapsedMs)
        {
            GetOrCreateEntry(behavior).AssignNewMs += Mathf.Max(0f, (float)elapsedMs);
        }

        public void AddReassign(
            SystemNpcBehaviorType behavior,
            double elapsedMs)
        {
            GetOrCreateEntry(behavior).ReassignMs += Mathf.Max(0f, (float)elapsedMs);
        }

        public void AddActiveBehavior(
            SystemNpcBehaviorType behavior,
            double elapsedMs)
        {
            GetOrCreateEntry(behavior).ActiveBehaviorMs += Mathf.Max(0f, (float)elapsedMs);
        }

        public void AddDeferred(
            SystemNpcBehaviorType behavior,
            double elapsedMs)
        {
            GetOrCreateEntry(behavior).DeferredMs += Mathf.Max(0f, (float)elapsedMs);
        }

        public string BuildLogFields()
        {
            StringBuilder builder =
                new StringBuilder();

            foreach (KeyValuePair<SystemNpcBehaviorType, Entry> pair in _entries)
            {
                Entry entry =
                    pair.Value;

                if (entry == null)
                    continue;

                double totalMs =
                    entry.ResolveScenarioMs +
                    entry.AssignNewMs +
                    entry.ReassignMs +
                    entry.ActiveBehaviorMs +
                    entry.DeferredMs;

                builder.Append(" | Behavior=");
                builder.Append(entry.Behavior);
                builder.Append(",Count=");
                builder.Append(entry.Count);
                builder.Append(",1_DetermineBehaviorAndTargetMs=");
                builder.Append(totalMs.ToString("F2"));
                builder.Append(",ResolveScenarioMs=");
                builder.Append(entry.ResolveScenarioMs.ToString("F2"));
                builder.Append(",AssignNewMs=");
                builder.Append(entry.AssignNewMs.ToString("F2"));
                builder.Append(",ReassignMs=");
                builder.Append(entry.ReassignMs.ToString("F2"));
                builder.Append(",ActiveBehaviorMs=");
                builder.Append(entry.ActiveBehaviorMs.ToString("F2"));
                builder.Append(",DeferredMs=");
                builder.Append(entry.DeferredMs.ToString("F2"));
            }

            return builder.ToString();
        }

        private Entry GetOrCreateEntry(SystemNpcBehaviorType behavior)
        {
            if (!_entries.TryGetValue(behavior, out Entry entry) ||
                entry == null)
            {
                entry =
                    new Entry
                    {
                        Behavior = behavior
                    };

                _entries[behavior] = entry;
            }

            return entry;
        }
    }

    private struct SystemNpcScenarioContext
    {
        public string SystemId;
        public StarSystemStatus SystemStatus;
        public bool HasSystemStatus;
        public bool HasThreatStatus;
        public bool HasEnemies;
    }

    private const double BehaviorPerfLogThresholdMs = 1.0;
    private const double AssignBehaviorDetailPerfLogThresholdMs = 2.0;
    private const int MinStayDays = 1;
    private const int MaxStayDays = 5;
    private const int PatrolBoundaryPlanetOrdinal = 8;
    private const float PatrolSunSafetyMargin = 80f;
    private const float PatrolSunFallbackPadding = 1f;
    private const int PatrolPointPickAttempts = 20;
    private const float InvalidRoutePointSqrMagnitude = 0.001f;

    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly SimpleEventBus _eventBus;
    private readonly IConfigService _configService;
    private readonly IRouteService _routeService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemSecurityService _systemSecurityService;

    private readonly System.Random _offscreenNpcProcessingRandom = new();
    private readonly HashSet<string> _systemsWithProcessedBehaviorWarmup = new();

    public SystemNpcBehaviorService()
    {
        _debugEnabled = false;
        _debugStop = true;

        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _systemSecurityService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemSecurityService>();

        LogCustom("[NPC-MILITARY-BEHAVIOR] Service debug disabled by default.");
    }

    public void Tick(StarSystemConfig starSystem, int currentTick)
    {
        Tick(starSystem, currentTick, true, false);
    }

    public void Tick(
        StarSystemConfig starSystem,
        int currentTick,
        bool isDetailedSystem)
    {
        Tick(starSystem, currentTick, isDetailedSystem, false);
    }

    public void Tick(
        StarSystemConfig starSystem,
        int currentTick,
        bool isDetailedSystem,
        bool forceInitialWarmup)
    {
        if (starSystem == null || string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        bool isInitialWarmup =
            forceInitialWarmup ||
            !_systemsWithProcessedBehaviorWarmup.Contains(starSystem.Id);

        string behaviorTickPhase =
            isInitialWarmup
                ? "InitialWarmup"
                : "Runtime";

        bool useRuntimeLimits =
            !isDetailedSystem && !isInitialWarmup;

        long totalStartedAt = BeginPerfMeasure();

        long getNpcsStartedAt = BeginPerfMeasure();
        var npcs = _npcRuntimeService.GetAliveNpcsInSystem(starSystem.Id);
        double getNpcsMs = EndPerfMeasureMs(getNpcsStartedAt);

        SystemNpcScenarioContext scenarioContext =
            BuildScenarioContext(
                starSystem.Id,
                npcs);

        NpcBehaviorSelectAnalytics behaviorSelectAnalytics =
            new NpcBehaviorSelectAnalytics();

        long loopStartedAt = BeginPerfMeasure();

        int npcCount = npcs != null ? npcs.Count : 0;
        int processedCount = 0;
        int skippedCount = 0;
        int assignNewCount = 0;
        int deferredAssignNewCount = 0;
        int reassignCount = 0;
        int deferredReassignCount = 0;
        int activeTickCount = 0;
        int deferredActiveBehaviorCount = 0;

        int maxAssignNewPerTick =
            useRuntimeLimits
                ? GetMaxOffscreenAssignNewPerSystemTick()
                : int.MaxValue;

        int maxReassignPerTick =
            useRuntimeLimits
                ? GetMaxOffscreenReassignPerSystemTick()
                : int.MaxValue;

        int maxActiveBehaviorPerTick =
            useRuntimeLimits
                ? GetMaxOffscreenActiveBehaviorPerSystemTick()
                : int.MaxValue;

        int npcStartIndex =
            isDetailedSystem || npcCount <= 1
                ? 0
                : _offscreenNpcProcessingRandom.Next(npcCount);

        double resolveScenarioMs = 0.0;
        double assignNewMs = 0.0;
        double reassignMs = 0.0;
        double activeBehaviorMs = 0.0;

        double maxNpcMs = 0.0;
        string maxNpcId = "";
        string maxNpcStage = "";
        SystemNpcBehaviorType maxNpcBehavior = SystemNpcBehaviorType.None;
        AllyBehaviourScenario maxNpcScenario = default;

        for (int i = 0; i < npcCount; i++)
        {
            int npcIndex = npcStartIndex + i;

            if (npcIndex >= npcCount)
                npcIndex -= npcCount;

            SystemNpcRuntimeState npc = npcs[npcIndex];

            if (npc == null || !npc.IsAlive)
            {
                skippedCount++;
                continue;
            }

            long npcStartedAt = BeginPerfMeasure();

            SystemNpcBehaviorType behaviorBefore = npc.CurrentBehavior;
            AllyBehaviourScenario scenarioBefore = npc.CurrentBehaviorScenario;

            if (!npc.HasActiveBehavior &&
                assignNewCount >= maxAssignNewPerTick)
            {
                deferredAssignNewCount++;
                processedCount++;

                double deferredMs =
                    EndPerfMeasureMs(npcStartedAt);

                behaviorSelectAnalytics.AddDeferred(
                    behaviorBefore,
                    deferredMs);

                TrackMaxNpcBehaviorPerf(
                    npc,
                    "DeferredAssignNew",
                    deferredMs,
                    behaviorBefore,
                    scenarioBefore,
                    ref maxNpcMs,
                    ref maxNpcId,
                    ref maxNpcStage,
                    ref maxNpcBehavior,
                    ref maxNpcScenario);

                continue;
            }

            long resolveScenarioStartedAt = BeginPerfMeasure();

            AllyBehaviourScenario resolvedScenario =
                ResolveBehaviorScenario(
                    npc,
                    scenarioContext);

            double currentResolveScenarioMs =
                EndPerfMeasureMs(resolveScenarioStartedAt);

            resolveScenarioMs += currentResolveScenarioMs;
            processedCount++;

            behaviorSelectAnalytics.AddResolve(
                behaviorBefore,
                currentResolveScenarioMs);

            if (!npc.HasActiveBehavior)
            {
                long assignStartedAt = BeginPerfMeasure();

                AssignBehavior(npc, currentTick, resolvedScenario);

                double currentAssignMs = EndPerfMeasureMs(assignStartedAt);
                assignNewMs += currentAssignMs;
                assignNewCount++;

                behaviorSelectAnalytics.AddAssignNew(
                    npc.CurrentBehavior,
                    currentAssignMs);

                TrackMaxNpcBehaviorPerf(
                    npc,
                    "AssignNew",
                    EndPerfMeasureMs(npcStartedAt),
                    behaviorBefore,
                    scenarioBefore,
                    ref maxNpcMs,
                    ref maxNpcId,
                    ref maxNpcStage,
                    ref maxNpcBehavior,
                    ref maxNpcScenario);

                continue;
            }

            if (ShouldReassignForScenarioChange(npc, resolvedScenario))
            {
                if (reassignCount >= maxReassignPerTick)
                {
                    deferredReassignCount++;

                    double deferredMs =
                        EndPerfMeasureMs(npcStartedAt);

                    behaviorSelectAnalytics.AddDeferred(
                        behaviorBefore,
                        deferredMs);

                    TrackMaxNpcBehaviorPerf(
                        npc,
                        "DeferredReassign",
                        deferredMs,
                        behaviorBefore,
                        scenarioBefore,
                        ref maxNpcMs,
                        ref maxNpcId,
                        ref maxNpcStage,
                        ref maxNpcBehavior,
                        ref maxNpcScenario);

                    continue;
                }

                long reassignStartedAt = BeginPerfMeasure();

                AssignBehavior(npc, currentTick, resolvedScenario);

                double currentReassignMs = EndPerfMeasureMs(reassignStartedAt);
                reassignMs += currentReassignMs;
                reassignCount++;

                behaviorSelectAnalytics.AddReassign(
                    npc.CurrentBehavior,
                    currentReassignMs);

                TrackMaxNpcBehaviorPerf(
                    npc,
                    "Reassign",
                    EndPerfMeasureMs(npcStartedAt),
                    behaviorBefore,
                    scenarioBefore,
                    ref maxNpcMs,
                    ref maxNpcId,
                    ref maxNpcStage,
                    ref maxNpcBehavior,
                    ref maxNpcScenario);

                continue;
            }

            if (activeTickCount >= maxActiveBehaviorPerTick)
            {
                deferredActiveBehaviorCount++;

                double deferredMs =
                    EndPerfMeasureMs(npcStartedAt);

                behaviorSelectAnalytics.AddDeferred(
                    behaviorBefore,
                    deferredMs);

                TrackMaxNpcBehaviorPerf(
                    npc,
                    "DeferredActiveBehavior",
                    deferredMs,
                    behaviorBefore,
                    scenarioBefore,
                    ref maxNpcMs,
                    ref maxNpcId,
                    ref maxNpcStage,
                    ref maxNpcBehavior,
                    ref maxNpcScenario);

                continue;
            }

            long activeBehaviorStartedAt = BeginPerfMeasure();

            TickActiveBehavior(npc, currentTick);

            double currentActiveBehaviorMs =
                EndPerfMeasureMs(activeBehaviorStartedAt);

            activeBehaviorMs += currentActiveBehaviorMs;
            activeTickCount++;

            behaviorSelectAnalytics.AddActiveBehavior(
                behaviorBefore,
                currentActiveBehaviorMs);

            TrackMaxNpcBehaviorPerf(
                npc,
                "TickActiveBehavior",
                EndPerfMeasureMs(npcStartedAt),
                behaviorBefore,
                scenarioBefore,
                ref maxNpcMs,
                ref maxNpcId,
                ref maxNpcStage,
                ref maxNpcBehavior,
                ref maxNpcScenario);
        }

        double loopMs = EndPerfMeasureMs(loopStartedAt);
        double totalMs = EndPerfMeasureMs(totalStartedAt);

        _systemsWithProcessedBehaviorWarmup.Add(starSystem.Id);

        LogNpcBehaviorPerformance(
            totalMs,
            "[SystemNpcBehaviorService] Tick | " +
            "Tick=" + currentTick +
            " | BehaviorTickPhase=" + behaviorTickPhase +
            " | ForceInitialWarmup=" + forceInitialWarmup +
            " | RuntimeLimitsEnabled=" + useRuntimeLimits +
            " | SystemId=" + starSystem.Id +
            " | SystemName=" + starSystem.DisplayName +
            " | ScenarioStatus=" + scenarioContext.SystemStatus +
            " | ScenarioHasEnemies=" + scenarioContext.HasEnemies +
            " | ScenarioHasThreatStatus=" + scenarioContext.HasThreatStatus +
            " | DetailedSystem=" + isDetailedSystem +
            " | Npcs=" + npcCount +
            " | NpcStartIndex=" + npcStartIndex +
            " | Processed=" + processedCount +
            " | Skipped=" + skippedCount +
            " | AssignNew=" + assignNewCount +
            " | DeferredAssignNew=" + deferredAssignNewCount +
            " | AssignNewLimit=" +
            (useRuntimeLimits ? maxAssignNewPerTick.ToString() : "Unlimited") +
            " | Reassign=" + reassignCount +
            " | DeferredReassign=" + deferredReassignCount +
            " | ReassignLimit=" +
            (useRuntimeLimits ? maxReassignPerTick.ToString() : "Unlimited") +
            " | ActiveBehavior=" + activeTickCount +
            " | DeferredActiveBehavior=" + deferredActiveBehaviorCount +
            " | ActiveBehaviorLimit=" +
            (useRuntimeLimits ? maxActiveBehaviorPerTick.ToString() : "Unlimited") +
            " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
            " | ResolveScenarioMs=" + resolveScenarioMs.ToString("F2") +
            " | AssignNewMs=" + assignNewMs.ToString("F2") +
            " | ReassignMs=" + reassignMs.ToString("F2") +
            " | ActiveBehaviorMs=" + activeBehaviorMs.ToString("F2") +
            " | LoopMs=" + loopMs.ToString("F2") +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | MaxNpcMs=" + maxNpcMs.ToString("F2") +
            " | MaxNpc=" + maxNpcId +
            " | MaxNpcStage=" + maxNpcStage +
            " | MaxNpcBehaviorBefore=" + maxNpcBehavior +
            " | MaxNpcScenarioBefore=" + maxNpcScenario);

        LogNpcBehaviorSelectAnalytics(
            currentTick,
            isDetailedSystem,
            behaviorTickPhase,
            starSystem,
            npcCount,
            processedCount,
            skippedCount,
            totalMs,
            behaviorSelectAnalytics);
    }

    private bool ShouldReassignForScenarioChange(
    SystemNpcRuntimeState npc,
    AllyBehaviourScenario resolvedScenario)
    {
        if (npc == null || !npc.IsAlive)
            return false;

        if (npc.CurrentBehaviorScenario == resolvedScenario)
            return false;

        return true;
    }

    private bool ShouldInterruptForThreat(
    SystemNpcRuntimeState npc,
    bool hasEnemiesInSystem)
    {
        if (npc == null || !npc.IsAlive)
            return false;

        if (!hasEnemiesInSystem)
            return false;

        if (!CanNpcReactToThreats(npc))
            return false;

        if (npc.CurrentBehavior == SystemNpcBehaviorType.EngageEnemies ||
            npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return false;

        return !string.IsNullOrWhiteSpace(FindCombatTargetId(npc));
    }

    private bool CanNpcReactToThreats(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.IsEnemy)
            return true;

        if (!npc.IsAlly)
            return false;

        return npc.AllyRole == AllyRole2A.Military ||
               npc.AllyRole == AllyRole2A.Ranger;
    }

    public void AssignBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        AssignBehavior(
            npc,
            currentTick,
            ResolveBehaviorScenario(npc));
    }

    private void AssignBehavior(
        SystemNpcRuntimeState npc,
        int currentTick,
        AllyBehaviourScenario resolvedScenario)
    {
        if (npc == null || !npc.IsAlive)
            return;

        long totalStartedAt = BeginPerfMeasure();

        SystemNpcBehaviorType behaviorBefore = npc.CurrentBehavior;
        SystemNpcBehaviorType prevBehaviorBefore = npc.PrevBehavior;
        SystemNpcTravelState travelStateBefore = npc.TravelState;
        bool wasOnPlanet = npc.IsOnPlanet;
        string currentPlanetBefore = npc.CurrentPlanetId;
        string currentSystemBefore = npc.CurrentSystemId;

        long pickStartedAt = BeginPerfMeasure();

        SystemNpcBehaviorType nextBehavior =
            PickFallbackBehavior(npc, resolvedScenario);

        double pickMs = EndPerfMeasureMs(pickStartedAt);

        npc.CurrentBehaviorScenario = resolvedScenario;

        long applyStartedAt = BeginPerfMeasure();

        ApplyBehavior(npc, nextBehavior, currentTick);

        double applyMs = EndPerfMeasureMs(applyStartedAt);
        double totalMs = EndPerfMeasureMs(totalStartedAt);

        if (totalMs >= AssignBehaviorDetailPerfLogThresholdMs)
        {
            LogNpcBehaviorPerformance(
                totalMs,
                "[SystemNpcBehaviorService] AssignBehavior | " +
                "Tick=" + currentTick +
                " | Npc=" + npc.RuntimeNpcId +
                " | NpcType=" + npc.NpcType +
                " | ConfigId=" + npc.ConfigId +
                " | SystemIdBefore=" + currentSystemBefore +
                " | SystemIdAfter=" + npc.CurrentSystemId +
                " | Scenario=" + resolvedScenario +
                " | BehaviorBefore=" + behaviorBefore +
                " | PrevBehaviorBefore=" + prevBehaviorBefore +
                " | RequestedBehavior=" + nextBehavior +
                " | FinalBehavior=" + npc.CurrentBehavior +
                " | TravelStateBefore=" + travelStateBefore +
                " | TravelStateAfter=" + npc.TravelState +
                " | WasOnPlanet=" + wasOnPlanet +
                " | IsOnPlanet=" + npc.IsOnPlanet +
                " | PlanetBefore=" + currentPlanetBefore +
                " | PlanetAfter=" + npc.CurrentPlanetId +
                " | TargetPlanet=" + npc.TargetPlanetId +
                " | TargetSystem=" + npc.TargetSystemId +
                " | PickMs=" + pickMs.ToString("0.00") +
                " | ApplyMs=" + applyMs.ToString("0.00") +
                " | TotalMs=" + totalMs.ToString("0.00"));
        }
    }

    private void NormalizeInvalidBehaviorTransitionContext(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        bool hasKnownCurrentPlanet =
            npc.IsOnPlanet &&
            !string.IsNullOrWhiteSpace(npc.CurrentPlanetId);

        if (npc.PrevBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel)
            return;

        if (hasKnownCurrentPlanet)
            return;

        LogCustom(
            "[NPC-BEHAVIOR-DEBUG] Invalid previous PlanetToPlanetTravel context fixed. " +
            "Npc=" + npc.RuntimeNpcId +
            ", PrevBehavior=" + npc.PrevBehavior +
            ", CurrentBehavior=" + npc.CurrentBehavior +
            ", TravelState=" + npc.TravelState +
            ", IsOnPlanet=" + npc.IsOnPlanet +
            ", CurrentPlanet=" + npc.CurrentPlanetId);

        npc.PrevBehavior = SystemNpcBehaviorType.None;
    }

    public void CompleteBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        if (npc == null)
            return;

        LogCustom(
            "[NPC-BEHAVIOR-DEBUG] CompleteBehavior before clear. " +
            "Npc=" + npc.RuntimeNpcId +
            ", PrevBehavior=" + npc.PrevBehavior +
            ", CurrentBehavior=" + npc.CurrentBehavior +
            ", TravelState=" + npc.TravelState +
            ", IsOnPlanet=" + npc.IsOnPlanet +
            ", CurrentPlanet=" + npc.CurrentPlanetId +
            ", TargetPlanet=" + npc.TargetPlanetId +
            ", Position=" + npc.CurrentPosition +
            ", TargetPosition=" + npc.TargetPosition +
            ", Tick=" + currentTick);

        ClearBehavior(npc);
        AssignBehavior(npc, currentTick);
    }

    public void ClearBehavior(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        npc.HasActiveBehavior = false;
        npc.PrevBehavior = npc.CurrentBehavior;
        npc.CurrentBehavior = SystemNpcBehaviorType.None;
        npc.BehaviorTargetRuntimeNpcId = null;
        npc.DaysToStayOnPlanet = 0;
        npc.DaysStayedOnPlanet = 0;
    }

    private void TickActiveBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        switch (npc.CurrentBehavior)
        {
            case SystemNpcBehaviorType.StayOnPlanetForDays:
                TickStayOnPlanet(npc, currentTick);
                break;

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                TickAnnihilateOnPlanet(npc);
                break;

            case SystemNpcBehaviorType.EngageEnemies:
                TickEngageEnemies(npc, currentTick);
                break;

            default:
                // Остальные поведения будут завершаться movement service.
                break;
        }
    }

    private void TickStayOnPlanet(SystemNpcRuntimeState npc, int currentTick)
    {
        if (!npc.IsOnPlanet)
            return;

        if (npc.BehaviorEndsTick <= 0)
            return;

        SyncNpcPositionWithCurrentPlanet(
            npc,
            false);

        if (currentTick < npc.BehaviorEndsTick)
            return;

        LogCustom($"StayOnPlanet complete: {npc.RuntimeNpcId}");

        CompleteBehavior(npc, currentTick);
    }

    private void TickAnnihilateOnPlanet(SystemNpcRuntimeState npc)
    {
        if (npc == null)
        {
            LogNpcAnnihilation(
                "[NPC_ANNIHILATION_TICK]" +
                " Result=False" +
                " | Reason=NullNpc");

            return;
        }

        LogNpcAnnihilation(
            "[NPC_ANNIHILATION_TICK]" +
            " RuntimeNpcId=" + npc.RuntimeNpcId +
            " | SystemId=" + npc.CurrentSystemId +
            " | PlanetId=" + npc.CurrentPlanetId +
            " | IsAlive=" + npc.IsAlive +
            " | LifeState=" + npc.LifeState +
            " | IsOnPlanet=" + npc.IsOnPlanet +
            " | CurrentBehavior=" + npc.CurrentBehavior +
            " | HasActiveBehavior=" + npc.HasActiveBehavior);

        if (!npc.IsAlive)
        {
            LogNpcAnnihilation(
                "[NPC_ANNIHILATION_TICK_RESULT]" +
                " Result=False" +
                " | Reason=NpcNotAlive" +
                " | RuntimeNpcId=" + npc.RuntimeNpcId);

            return;
        }

        if (!npc.IsOnPlanet)
        {
            LogNpcAnnihilation(
                "[NPC_ANNIHILATION_TICK_RESULT]" +
                " Result=False" +
                " | Reason=NpcNotOnPlanet" +
                " | RuntimeNpcId=" + npc.RuntimeNpcId +
                " | SystemId=" + npc.CurrentSystemId +
                " | TravelState=" + npc.TravelState);

            return;
        }

        bool annihilated =
            _npcRuntimeService.AnnihilateNpc(npc.RuntimeNpcId);

        LogNpcAnnihilation(
            "[NPC_ANNIHILATION_TICK_RESULT]" +
            " Result=" + annihilated +
            " | RuntimeNpcId=" + npc.RuntimeNpcId +
            " | SystemId=" + npc.CurrentSystemId +
            " | PlanetId=" + npc.CurrentPlanetId +
            " | IsAliveAfter=" + npc.IsAlive +
            " | LifeStateAfter=" + npc.LifeState);
    }

    private void TickEngageEnemies(SystemNpcRuntimeState npc, int currentTick)
    {
        if (npc == null || !npc.IsAlive)
            return;

        string targetId = FindCombatTargetId(npc);

        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
            npc.CurrentTargetRuntimeNpcId = null;
            npc.BehaviorTargetRuntimeNpcId = null;
            npc.CombatState = SystemNpcCombatState.None;
            npc.IsFighting = false;

            if (CanNpcPatrolSystem(npc))
            {
                npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
                SetupPatrolSystem(npc);
                return;
            }

            CompleteBehavior(npc, currentTick);
            return;
        }

        npc.BehaviorTargetRuntimeNpcId = targetId;
        npc.CurrentTargetRuntimeNpcId = targetId;
        npc.TravelState = SystemNpcTravelState.EngagingEnemy;
        npc.CombatState = SystemNpcCombatState.HasTarget;
        npc.IsFighting = true;
    }

    //Определяем, что Npc делает дальше
    private SystemNpcBehaviorType PickFallbackBehavior(
    SystemNpcRuntimeState npc,
    AllyBehaviourScenario resolvedScenario)
    {
        switch (npc.NpcType)
        {
            case SystemNpcType.Enemy:
                return GetRandomBehaviorType4Enemy(npc, resolvedScenario);

            case SystemNpcType.Pirate:
                return GetRandomBehaviorType4Pirate(npc);

            default:
                return GetRandomBehaviorType4Ally(npc, resolvedScenario);
        }
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Enemy(SystemNpcRuntimeState npc)
    {
        return GetRandomBehaviorType4Enemy(
            npc,
            ResolveBehaviorScenario(npc));
    }

    private SystemNpcBehaviorType GetRandomBehaviorType4Enemy(
        SystemNpcRuntimeState npc,
        AllyBehaviourScenario resolvedScenario)
    {
        EnemyConfig enemyConfig =
            _configService.GetEnemyConfigById(npc.ConfigId);

        NpcBehaviourScenarioConfig behaviorScenario =
            GetEnemyBehaviorScenario(enemyConfig, resolvedScenario);

        return PickScenarioBehavior(
            behaviorScenario,
            npc);
    }



    public SystemNpcBehaviorType GetRandomBehaviorType4Pirate(SystemNpcRuntimeState npc)
    {
        PirateGroupSpawnRuleConfig pirateGroupSpawnRuleConfig =
                _configService.GetPirateGroupSpawnRuleConfigById(npc.SpawnRuleId);
        List<SystemNpcBehaviorWeight> weights = pirateGroupSpawnRuleConfig.BehaviorWeights.ToList();

        //Отсекаем невозможные следующие состояния
        switch (npc.PrevBehavior)
        {
            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                weights.Clear();
                break;

            case SystemNpcBehaviorType.EngageEnemies:
            case SystemNpcBehaviorType.PatrolSystem:
            case SystemNpcBehaviorType.TravelToAnotherSystem:
                // weights.Remove(weights.First(x => x.BehaviorType == SystemNpcBehaviorType.AnnihilateOnPlanet));
                weights.Remove(weights.First(x => x.BehaviorType == SystemNpcBehaviorType.StayOnPlanetForDays));
                break;

        }

        if (weights == null || weights.Count == 0)
            throw new InvalidOperationException("Behavior weights are empty.");

        int totalWeight = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
            totalWeight += item.Weight;

        if (totalWeight <= 0)
            throw new InvalidOperationException("Total behavior weight must be greater than zero.");

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
        {
            cumulative += item.Weight;

            if (roll < cumulative)
                return item.BehaviorType;
        }

        return weights[^1].BehaviorType;
    }

    private NpcBehaviourScenarioConfig GetAllyBehaviorScenario(
        AllyConfig allyConfig,
        AllyBehaviourScenario scenario)
    {
        if (allyConfig == null)
            return null;

        NpcBehaviourScenarioConfig behaviorScenario =
            allyConfig.GetBehaviorScenario(scenario);

        if (behaviorScenario != null)
            return behaviorScenario;

        if (scenario != AllyBehaviourScenario.Normal)
            return allyConfig.GetBehaviorScenario(AllyBehaviourScenario.Normal);

        return null;
    }

    private NpcBehaviourScenarioConfig GetEnemyBehaviorScenario(
        EnemyConfig enemyConfig,
        AllyBehaviourScenario scenario)
    {
        if (enemyConfig == null)
            return null;

        NpcBehaviourScenarioConfig behaviorScenario =
            enemyConfig.GetBehaviorScenario(scenario);

        if (behaviorScenario != null)
            return behaviorScenario;

        if (scenario != AllyBehaviourScenario.Normal)
            return enemyConfig.GetBehaviorScenario(AllyBehaviourScenario.Normal);

        return null;
    }

    private SystemNpcScenarioContext BuildScenarioContext(
        string systemId,
        IReadOnlyList<SystemNpcRuntimeState> systemNpcs)
    {
        SystemNpcScenarioContext context =
            new SystemNpcScenarioContext
            {
                SystemId = systemId,
                SystemStatus = StarSystemStatus.Stable,
                HasSystemStatus = false,
                HasThreatStatus = false,
                HasEnemies = false
            };

        if (string.IsNullOrWhiteSpace(systemId))
            return context;

        if (_systemSecurityService != null &&
            _systemSecurityService.TryGetSystemStatus(
                systemId,
                out StarSystemStatus systemStatus))
        {
            context.HasSystemStatus = true;
            context.SystemStatus = systemStatus;
            context.HasThreatStatus =
                IsThreatSystemStatus(systemStatus);
        }

        context.HasEnemies =
            HasEnemiesInSystem(
                systemId,
                systemNpcs);

        return context;
    }

    private AllyBehaviourScenario ResolveBehaviorScenario(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return AllyBehaviourScenario.Normal;

        SystemNpcScenarioContext context =
            BuildScenarioContext(
                npc.CurrentSystemId,
                null);

        return ResolveBehaviorScenario(
            npc,
            context);
    }

    private AllyBehaviourScenario ResolveBehaviorScenario(
        SystemNpcRuntimeState npc,
        SystemNpcScenarioContext context)
    {
        if (npc == null)
            return AllyBehaviourScenario.Normal;

        bool hasScenarioThreat =
            context.HasThreatStatus ||
            context.HasEnemies;

        if (!hasScenarioThreat)
            return AllyBehaviourScenario.Normal;

        if (npc.IsEnemy)
            return AllyBehaviourScenario.EnemySystemInvasion;

        if (npc.IsAlly)
            return AllyBehaviourScenario.EnemyInvasion;

        return AllyBehaviourScenario.Normal;
    }

    private static bool IsThreatSystemStatus(
        StarSystemStatus systemStatus)
    {
        return systemStatus == StarSystemStatus.Captured ||
               systemStatus == StarSystemStatus.Threatened ||
               systemStatus == StarSystemStatus.Invasion;
    }

    private string FormatNpcRoleForDebug(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "NULL";

        if (npc.IsAlly)
            return npc.AllyRole.ToString();

        if (npc.IsEnemy)
            return "Enemy";

        if (npc.IsPirate)
            return "Pirate";

        return npc.NpcType.ToString();
    }

    private SystemNpcBehaviorType PickScenarioBehavior(
        NpcBehaviourScenarioConfig behaviorScenario,
        SystemNpcRuntimeState npc)
    {
        if (behaviorScenario == null)
        {
            LogNpcAnnihilationPick(
                "[NPC_ANNIHILATION_PICK]" +
                " Result=False" +
                " | Reason=NullScenario" +
                " | RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC"));

            return SystemNpcBehaviorType.None;
        }

        IReadOnlyList<SystemNpcBehaviorWeight> behaviorWeights =
            behaviorScenario.BehaviorWeights;

        if (behaviorWeights == null || behaviorWeights.Count == 0)
        {
            LogNpcAnnihilationPick(
                "[NPC_ANNIHILATION_PICK]" +
                " Result=False" +
                " | Reason=EmptyScenarioWeights" +
                " | RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                " | Scenario=" + behaviorScenario.Id);

            return SystemNpcBehaviorType.None;
        }

        List<SystemNpcBehaviorWeight> weights =
            behaviorWeights
                .Where(weight => weight != null && weight.Weight > 0)
                .ToList();

        bool hadAnnihilationBeforeFilters =
            HasBehaviorWeight(
                weights,
                SystemNpcBehaviorType.AnnihilateOnPlanet);

        LogNpcAnnihilationPick(
            "[NPC_ANNIHILATION_PICK_BEFORE_FILTERS]" +
            " RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
            " | ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
            " | NpcType=" + (npc != null ? npc.NpcType.ToString() : "NULL_TYPE") +
            " | RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
            " | SystemId=" + (npc != null ? npc.CurrentSystemId : "NULL_SYSTEM") +
            " | PlanetId=" + (npc != null ? npc.CurrentPlanetId : "NULL_PLANET") +
            " | IsOnPlanet=" + (npc != null && npc.IsOnPlanet) +
            " | PrevBehavior=" + (npc != null ? npc.PrevBehavior.ToString() : "NULL_PREV") +
            " | CurrentBehavior=" + (npc != null ? npc.CurrentBehavior.ToString() : "NULL_CURRENT") +
            " | Scenario=" + behaviorScenario.Id +
            " | HasAnnihilationBeforeFilters=" + hadAnnihilationBeforeFilters +
            " | Weights=" + FormatBehaviorWeightsForDebug(weights));

        bool shouldLog =
            ShouldLogAllyBehaviorPickTrace(npc, behaviorScenario) ||
            IsMilitaryDebugNpc(npc);

        if (shouldLog)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Weights before filters. " +
                "Npc=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                ", ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
                ", RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
                ", PrevBehavior=" + (npc != null ? npc.PrevBehavior.ToString() : "NULL_PREV") +
                ", Scenario=" + behaviorScenario.Id +
                ", Weights=" + FormatBehaviorWeightsForDebug(weights));
        }

        RemoveImpossibleNextBehaviors(weights, npc);
        RemoveRoleForbiddenBehaviors(weights, npc);

        bool hasAnnihilationAfterFilters =
            HasBehaviorWeight(
                weights,
                SystemNpcBehaviorType.AnnihilateOnPlanet);

        LogNpcAnnihilationPick(
            "[NPC_ANNIHILATION_PICK_AFTER_FILTERS]" +
            " RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
            " | ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
            " | NpcType=" + (npc != null ? npc.NpcType.ToString() : "NULL_TYPE") +
            " | RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
            " | SystemId=" + (npc != null ? npc.CurrentSystemId : "NULL_SYSTEM") +
            " | PlanetId=" + (npc != null ? npc.CurrentPlanetId : "NULL_PLANET") +
            " | IsOnPlanet=" + (npc != null && npc.IsOnPlanet) +
            " | Scenario=" + behaviorScenario.Id +
            " | HasAnnihilationBeforeFilters=" + hadAnnihilationBeforeFilters +
            " | HasAnnihilationAfterFilters=" + hasAnnihilationAfterFilters +
            " | Weights=" + FormatBehaviorWeightsForDebug(weights));

        if (shouldLog)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Weights after filters. " +
                "Npc=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                ", ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
                ", RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
                ", CanReactToThreats=" + CanNpcReactToThreats(npc) +
                ", Scenario=" + behaviorScenario.Id +
                ", Weights=" + FormatBehaviorWeightsForDebug(weights));
        }

        if (weights.Count == 0)
        {
            LogNpcAnnihilationPick(
                "[NPC_ANNIHILATION_PICK_RESULT]" +
                " Result=False" +
                " | Reason=NoWeightsAfterFilters" +
                " | RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                " | HadAnnihilationBeforeFilters=" + hadAnnihilationBeforeFilters);

            return SystemNpcBehaviorType.None;
        }

        int totalWeight = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
            totalWeight += Mathf.Max(0, item.Weight);

        if (totalWeight <= 0)
        {
            LogNpcAnnihilationPick(
                "[NPC_ANNIHILATION_PICK_RESULT]" +
                " Result=False" +
                " | Reason=TotalWeightZero" +
                " | RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                " | HadAnnihilationBeforeFilters=" + hadAnnihilationBeforeFilters);

            return SystemNpcBehaviorType.None;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
        {
            cumulative += Mathf.Max(0, item.Weight);

            if (roll < cumulative)
            {
                LogNpcAnnihilationPick(
                    "[NPC_ANNIHILATION_PICK_RESULT]" +
                    " Result=True" +
                    " | RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                    " | Scenario=" + behaviorScenario.Id +
                    " | Roll=" + roll +
                    " | TotalWeight=" + totalWeight +
                    " | PickedBehavior=" + item.BehaviorType +
                    " | HasAnnihilationAfterFilters=" + hasAnnihilationAfterFilters);

                if (shouldLog || item.BehaviorType == SystemNpcBehaviorType.EngageEnemies)
                {
                    LogCustom(
                        "[NPC-BEHAVIOR-PICK] Roll result. " +
                        "Npc=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                        ", ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
                        ", RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
                        ", Scenario=" + behaviorScenario.Id +
                        ", Roll=" + roll +
                        ", TotalWeight=" + totalWeight +
                        ", PickedBehavior=" + item.BehaviorType);
                }

                return item.BehaviorType;
            }
        }

        SystemNpcBehaviorType fallbackBehavior =
            weights[^1].BehaviorType;

        LogNpcAnnihilationPick(
            "[NPC_ANNIHILATION_PICK_RESULT]" +
            " Result=True" +
            " | Reason=FallbackLastWeight" +
            " | RuntimeNpcId=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
            " | Scenario=" + behaviorScenario.Id +
            " | Roll=" + roll +
            " | TotalWeight=" + totalWeight +
            " | PickedBehavior=" + fallbackBehavior +
            " | HasAnnihilationAfterFilters=" + hasAnnihilationAfterFilters);

        return fallbackBehavior;
    }

    private bool HasBehaviorWeight(
        IReadOnlyList<SystemNpcBehaviorWeight> weights,
        SystemNpcBehaviorType behaviorType)
    {
        if (weights == null)
            return false;

        for (int i = 0; i < weights.Count; i++)
        {
            SystemNpcBehaviorWeight weight = weights[i];

            if (weight != null &&
                weight.BehaviorType == behaviorType &&
                weight.Weight > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void LogNpcAnnihilationPick(string message)
    {
        LogNpcAnnihilation(message);
    }

    private void LogNpcAnnihilation(string message)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.NpcAnnihilation))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcAnnihilation,
            message);
    }

    private bool ShouldLogAllyBehaviorPickTrace(
    SystemNpcRuntimeState npc,
    NpcBehaviourScenarioConfig behaviorScenario)
    {
        if (npc == null || !npc.IsAlly)
            return false;

        if (HasEnemiesInSystem(npc.CurrentSystemId))
            return true;

        if (_systemSecurityService != null &&
            _systemSecurityService.TryGetSystemStatus(
                npc.CurrentSystemId,
                out StarSystemStatus systemStatus))
        {
            if (systemStatus == StarSystemStatus.Threatened ||
                systemStatus == StarSystemStatus.Invasion ||
                systemStatus == StarSystemStatus.Captured)
            {
                return true;
            }
        }

        return behaviorScenario != null &&
               !string.IsNullOrWhiteSpace(behaviorScenario.Id) &&
               behaviorScenario.Id.Contains("enemy_invasion");
    }

    private string FormatBehaviorWeightsForDebug(
    IReadOnlyList<SystemNpcBehaviorWeight> weights)
    {
        if (weights == null || weights.Count == 0)
            return "empty";

        return string.Join(
            "; ",
            weights.Select(weight =>
                weight.BehaviorType + "=" + weight.Weight));
    }

    private void RemoveRoleForbiddenBehaviors(
     List<SystemNpcBehaviorWeight> weights,
     SystemNpcRuntimeState npc)
    {
        if (weights == null || npc == null)
            return;

        if (!CanNpcPatrolSystem(npc))
        {
            weights.RemoveAll(weight =>
                weight != null &&
                weight.BehaviorType == SystemNpcBehaviorType.PatrolSystem);
        }
    }

    private bool CanNpcPatrolSystem(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlly)
            return false;

        return npc.AllyRole == AllyRole2A.Military ||
               npc.AllyRole == AllyRole2A.Ranger;
    }

    private void RemoveImpossibleNextBehaviors(
    List<SystemNpcBehaviorWeight> weights,
    SystemNpcRuntimeState npc)
    {
        if (weights == null || npc == null)
            return;

        RemoveTransitionMatrixForbiddenBehaviors(weights, npc);
        RemoveUnavailableNextBehaviors(weights, npc);
    }

    private void RemoveTransitionMatrixForbiddenBehaviors(
        List<SystemNpcBehaviorWeight> weights,
        SystemNpcRuntimeState npc)
    {
        NpcBehaviourTransitionMatrixConfig transitionMatrix =
            _configService != null
                ? _configService.NpcBehaviourTransitionMatrixConfig
                : null;

        if (transitionMatrix == null)
            return;

        weights.RemoveAll(weight =>
            weight == null ||
            !transitionMatrix.IsNextBehaviorAllowed(
                npc.PrevBehavior,
                weight.BehaviorType));
    }

    private void RemoveUnavailableNextBehaviors(
        List<SystemNpcBehaviorWeight> weights,
        SystemNpcRuntimeState npc)
    {
        weights.RemoveAll(weight =>
            weight == null ||
            !CanUseBehaviorInCurrentConditions(
                npc,
                weight.BehaviorType));
    }

    private bool CanUseBehaviorInCurrentConditions(
    SystemNpcRuntimeState npc,
    SystemNpcBehaviorType behaviorType)
    {
        switch (behaviorType)
        {
            case SystemNpcBehaviorType.StayOnPlanetForDays:
                return IsNpcOnKnownPlanet(npc);

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                return IsNpcOnKnownPlanet(npc);

            case SystemNpcBehaviorType.AttackMeteorite:
                return false;

            case SystemNpcBehaviorType.AttackMilitaryStation:
                return HasAliveStation(npc, StationType.Military);

            case SystemNpcBehaviorType.AttackRangerBaseStation:
                return HasAliveStation(npc, StationType.RangerBase);

            case SystemNpcBehaviorType.AttackTradeStation:
                return HasAliveStation(npc, StationType.Trade);

            case SystemNpcBehaviorType.AttackScienceStation:
                return HasAliveStation(npc, StationType.Science);

            case SystemNpcBehaviorType.AttackMedicalStation:
                return HasAliveStation(npc, StationType.Medical);

            case SystemNpcBehaviorType.AttackMilitaryAlly:
                return HasAliveAlly(npc, AllyRole2A.Military);

            case SystemNpcBehaviorType.AttackRangerAlly:
                return HasAliveAlly(npc, AllyRole2A.Ranger);

            case SystemNpcBehaviorType.AttackTraderAlly:
                return HasAliveAlly(npc, AllyRole2A.Trader);

            case SystemNpcBehaviorType.AttackScienceAlly:
                return HasAliveAlly(npc, AllyRole2A.Science);

            case SystemNpcBehaviorType.AttackMedicAlly:
                return HasAliveAlly(npc, AllyRole2A.Medic);

            default:
                return true;
        }
    }

    private bool IsNpcOnKnownPlanet(SystemNpcRuntimeState npc)
    {
        return npc != null &&
               npc.IsOnPlanet &&
               !string.IsNullOrWhiteSpace(npc.CurrentPlanetId);
    }

    private bool HasAliveStation(
        SystemNpcRuntimeState npc,
        StationType stationType)
    {
        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return false;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        StationConfig station = starSystem != null
            ? starSystem.Station
            : null;

        return station != null &&
               station.IsActive &&
               !station.IsDestroyed &&
               station.StationType == stationType;
    }

    private bool HasAliveAlly(
        SystemNpcRuntimeState npc,
        AllyRole2A allyRole)
    {
        if (npc == null ||
            _npcRuntimeService == null ||
            _npcRuntimeService.Npcs == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return false;
        }

        return _npcRuntimeService.Npcs.Any(candidate =>
            candidate != null &&
            candidate.IsAlive &&
            candidate.IsAlly &&
            candidate.CurrentSystemId == npc.CurrentSystemId &&
            candidate.RuntimeNpcId != npc.RuntimeNpcId &&
            candidate.AllyRole == allyRole);
    }

    private void ApplyBehavior(
        SystemNpcRuntimeState npc,
        SystemNpcBehaviorType nextBehavior,
        int currentTick)
    {
        if (npc == null)
            return;

        long totalStartedAt = BeginPerfMeasure();

        SystemNpcBehaviorType previousBehaviorBeforeApply =
            npc.PrevBehavior;

        SystemNpcBehaviorType currentBehaviorBefore =
            npc.CurrentBehavior;

        SystemNpcTravelState travelStateBefore =
            npc.TravelState;

        bool isOnPlanetBefore =
            npc.IsOnPlanet;

        string currentPlanetBefore =
            npc.CurrentPlanetId;

        string targetPlanetBefore =
            npc.TargetPlanetId;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] ApplyBehavior start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", PrevBehavior=" + npc.PrevBehavior +
                ", NextBehavior=" + nextBehavior +
                ", CurrentBehaviorBefore=" + npc.CurrentBehavior +
                ", TravelStateBefore=" + npc.TravelState +
                ", IsOnPlanetBefore=" + npc.IsOnPlanet +
                ", CurrentPlanetBefore=" + npc.CurrentPlanetId +
                ", TargetPlanetBefore=" + npc.TargetPlanetId +
                ", PositionBefore=" + npc.CurrentPosition +
                ", TargetPositionBefore=" + npc.TargetPosition +
                ", Tick=" + currentTick);
        }

        long stateSetupStartedAt = BeginPerfMeasure();

        npc.CurrentBehavior = nextBehavior;
        npc.HasActiveBehavior = true;
        npc.BehaviorStartedTick = currentTick;
        npc.BehaviorEndsTick = 0;
        npc.BehaviorTargetRuntimeNpcId = null;

        double stateSetupMs = EndPerfMeasureMs(stateSetupStartedAt);

        long setupStartedAt = BeginPerfMeasure();

        switch (nextBehavior)
        {
            case SystemNpcBehaviorType.PlanetToPlanetTravel:
                SetupPlanetToPlanetTravel(npc);
                break;

            case SystemNpcBehaviorType.StayOnPlanetForDays:
                SetupStayOnPlanet(npc, currentTick);
                break;

            case SystemNpcBehaviorType.TravelToAnotherSystem:
                SetupTravelToAnotherSystem(npc);
                break;

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                SetupAnnihilateOnPlanet(npc);
                break;

            case SystemNpcBehaviorType.EngageEnemies:
                SetupEngageEnemies(npc);
                break;

            case SystemNpcBehaviorType.PatrolSystem:
                SetupPatrolSystem(npc);
                break;

            default:
                npc.HasActiveBehavior = false;
                break;
        }

        double setupMs = EndPerfMeasureMs(setupStartedAt);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] ApplyBehavior after setup BEFORE event. " +
                "Npc=" + npc.RuntimeNpcId +
                ", PrevBehaviorBeforeApply=" + previousBehaviorBeforeApply +
                ", CurrentBehavior=" + npc.CurrentBehavior +
                ", HasActiveBehavior=" + npc.HasActiveBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", StartPosition=" + npc.StartPosition +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", CurrentMovementTargetPosition=" + npc.CurrentMovementTargetPosition +
                ", TickMovementTargetPosition=" + npc.TickMovementTargetPosition +
                ", BehaviorEndsTick=" + npc.BehaviorEndsTick +
                ", Tick=" + currentTick);
        }

        long behaviorChangedEventStartedAt = BeginPerfMeasure();

        _eventBus.Publish(new SystemNpcBehaviorChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentBehavior));

        double behaviorChangedEventMs =
            EndPerfMeasureMs(behaviorChangedEventStartedAt);

        long travelStateEventStartedAt = BeginPerfMeasure();

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId));

        double travelStateEventMs =
            EndPerfMeasureMs(travelStateEventStartedAt);

        LogCustom(
            $"NPC: {npc.RuntimeNpcId}, Type: {npc.NpcType}, CurrentBehavior: {npc.CurrentBehavior}, TargetPlanet: {npc.TargetPlanetId}");

        double totalMs = EndPerfMeasureMs(totalStartedAt);

        if (totalMs >= AssignBehaviorDetailPerfLogThresholdMs)
        {
            LogNpcBehaviorPerformance(
                totalMs,
                "[SystemNpcBehaviorService] ApplyBehavior | " +
                "Tick=" + currentTick +
                " | Npc=" + npc.RuntimeNpcId +
                " | NpcType=" + npc.NpcType +
                " | ConfigId=" + npc.ConfigId +
                " | RequestedBehavior=" + nextBehavior +
                " | FinalBehavior=" + npc.CurrentBehavior +
                " | BehaviorChangedBySetup=" + (npc.CurrentBehavior != nextBehavior) +
                " | BehaviorBefore=" + currentBehaviorBefore +
                " | PrevBehaviorBeforeApply=" + previousBehaviorBeforeApply +
                " | TravelStateBefore=" + travelStateBefore +
                " | TravelStateAfter=" + npc.TravelState +
                " | IsOnPlanetBefore=" + isOnPlanetBefore +
                " | IsOnPlanetAfter=" + npc.IsOnPlanet +
                " | CurrentPlanetBefore=" + currentPlanetBefore +
                " | CurrentPlanetAfter=" + npc.CurrentPlanetId +
                " | TargetPlanetBefore=" + targetPlanetBefore +
                " | TargetPlanetAfter=" + npc.TargetPlanetId +
                " | TargetSystem=" + npc.TargetSystemId +
                " | HasActiveBehavior=" + npc.HasActiveBehavior +
                " | StateSetupMs=" + stateSetupMs.ToString("0.00") +
                " | SetupMs=" + setupMs.ToString("0.00") +
                " | BehaviorChangedEventMs=" + behaviorChangedEventMs.ToString("0.00") +
                " | TravelStateEventMs=" + travelStateEventMs.ToString("0.00") +
                " | TotalMs=" + totalMs.ToString("0.00"));
        }
    }

    private void SetupPlanetToPlanetTravel(SystemNpcRuntimeState npc)
    {
        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupPlanetToPlanetTravel start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", Position=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", TravelState=" + npc.TravelState);
        }

        SyncNpcPositionWithCurrentPlanet(npc, true);

        string previousPlanetId = npc.CurrentPlanetId;
        bool startedOnPlanet = npc.IsOnPlanet;

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingInsideSystem;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null || starSystem.PlanetRefs == null)
        {
            if (IsMilitaryDebugNpc(npc))
                LogCustom("[NPC-MILITARY-BEHAVIOR] Planet travel fallback: starSystem or planets missing.");

            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        PlanetConfig[] inhabitedPlanets = starSystem.PlanetRefs
            .Where(p => p != null && p.IsInhabited == true)
            .Where(p => p.Id != previousPlanetId)
            .ToArray();

        PlanetConfig randomPlanet = inhabitedPlanets.Length > 0
            ? inhabitedPlanets[UnityEngine.Random.Range(0, inhabitedPlanets.Length)]
            : null;

        if (randomPlanet == null)
        {
            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-BEHAVIOR] Planet travel fallback: no target planet. " +
                    "PreviousPlanet=" + previousPlanetId);
            }

            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        Vector3 planetPosition = _orbitalMotionService != null &&
                                 randomPlanet.PlanetOrbit != null
            ? _orbitalMotionService.GetPlanetCurrentPosition(randomPlanet.PlanetOrbit)
            : Vector3.zero;

        planetPosition.z = npc.CurrentPosition.z;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPlanetId = randomPlanet.Id;
        npc.TargetPosition = planetPosition;
        npc.CurrentMovementTargetPosition = planetPosition;
        npc.TickMovementTargetPosition = planetPosition;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
        npc.TravelProgress01 = 0f;

        npc.IsWaitingForInitialRouteBuild = startedOnPlanet;
        npc.ReleaseFromPlanetAfterInitialRouteBuild = startedOnPlanet;
        npc.InitialRouteBuildPlanetId = startedOnPlanet ? previousPlanetId : null;

        if (!startedOnPlanet)
        {
            npc.IsOnPlanet = false;
            npc.CurrentPlanetId = null;
        }

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupPlanetToPlanetTravel target set. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromPlanet=" + previousPlanetId +
                ", ToPlanet=" + randomPlanet.Id +
                ", StartPosition=" + npc.StartPosition +
                ", PlanetPosition=" + planetPosition +
                ", Distance=" + Vector3.Distance(npc.StartPosition, planetPosition) +
                ", Speed=" + npc.Speed +
                ", WaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild);
        }
    }

    private void SetupStayOnPlanet(SystemNpcRuntimeState npc, int currentTick)
    {
        if (!IsNpcOnKnownPlanet(npc))
        {
            LogCustom(
                "[NPC-BEHAVIOR-DEBUG] StayOnPlanet rejected: NPC is not on known planet. " +
                "Npc=" + npc.RuntimeNpcId +
                ", PrevBehavior=" + npc.PrevBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId);

            npc.PrevBehavior = SystemNpcBehaviorType.None;

            if (CanNpcPatrolSystem(npc))
            {
                npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
                SetupPatrolSystem(npc);
                return;
            }

            ClearBehavior(npc);
            return;
        }

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.OnPlanet;
        npc.IsOnPlanet = true;

        npc.DaysToStayOnPlanet = UnityEngine.Random.Range(MinStayDays, MaxStayDays + 1);
        npc.DaysStayedOnPlanet = 0;

        npc.BehaviorEndsTick = currentTick + npc.DaysToStayOnPlanet;
    }

    private void SetupTravelToAnotherSystem(SystemNpcRuntimeState npc)
    {
        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        bool startedOnPlanet = npc.IsOnPlanet;
        string previousPlanetId = npc.CurrentPlanetId;

        StarSystemConfig currentSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        RouteConfig route = FindUnlockedRouteFromCurrentSystem(currentSystem);

        if (route == null)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        StarSystemConfig targetSystem = route.GetOtherSystem(currentSystem.Id);

        if (targetSystem == null)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        Vector3 exitPoint = route.GetExitPoint(currentSystem.Id);
        Vector3 entryPoint = route.GetEntryPoint(targetSystem.Id);

        if (IsInvalidRoutePoint(currentSystem, exitPoint) ||
            IsInvalidRoutePoint(targetSystem, entryPoint))
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingToAnotherSystem;

        npc.TargetSystemId = targetSystem.Id;
        npc.TargetSystemExitPoint = exitPoint;
        npc.TargetSystemEntryPoint = entryPoint;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = exitPoint;
        npc.CurrentMovementTargetPosition = exitPoint;
        npc.TickMovementTargetPosition = exitPoint;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
        npc.TravelProgress01 = 0f;

        npc.IsWaitingForInitialRouteBuild = startedOnPlanet;
        npc.ReleaseFromPlanetAfterInitialRouteBuild = startedOnPlanet;
        npc.InitialRouteBuildPlanetId = startedOnPlanet ? previousPlanetId : null;

        if (!startedOnPlanet)
        {
            npc.IsOnPlanet = false;
            npc.CurrentPlanetId = null;
        }

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupTravelToAnotherSystem target set without forced facing. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromSystem=" + currentSystem.Id +
                ", ToSystem=" + targetSystem.Id +
                ", StartPosition=" + npc.StartPosition +
                ", ExitPoint=" + npc.TargetSystemExitPoint +
                ", EntryPoint=" + npc.TargetSystemEntryPoint +
                ", DistanceToExit=" + Vector3.Distance(npc.StartPosition, npc.TargetSystemExitPoint) +
                ", Speed=" + npc.Speed +
                ", FacingDirectionKept=" + npc.FacingDirection +
                ", TickMovementDirectionKept=" + npc.TickMovementDirection +
                ", WaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild);
        }
    }

    private RouteConfig FindUnlockedRouteFromCurrentSystem(StarSystemConfig currentSystem)
    {
        if (currentSystem == null || currentSystem.Routes == null)
            return null;

        IRouteService routeService =
            Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();

        List<RouteConfig> availableRoutes = new();

        foreach (RouteConfig route in currentSystem.Routes)
        {
            if (route == null)
                continue;

            StarSystemConfig targetSystem = route.GetOtherSystem(currentSystem.Id);

            if (targetSystem == null)
                continue;

            if (!routeService.HasUnlockedRoute(currentSystem.Id, targetSystem.Id))
                continue;

            availableRoutes.Add(route);
        }

        if (availableRoutes.Count == 0)
            return null;

        return availableRoutes[UnityEngine.Random.Range(0, availableRoutes.Count)];
    }

    private void SetupLinkedSystemTravel(SystemNpcRuntimeState npc)
    {
        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingToAnotherSystem;
        npc.IsOnPlanet = false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        StarSystemLink[] starSystemLinks =
            starSystem != null ? starSystem.LinkedSystems : null;

        StarSystemLink link =
            starSystemLinks != null && starSystemLinks.Length > 0
                ? starSystemLinks[UnityEngine.Random.Range(0, starSystemLinks.Length)]
                : null;

        npc.TargetSystemId = link?.LinkedSystem?.Id;
        npc.TargetSystemExitPoint = link?.ExitPoint ?? Vector3.zero;
        npc.TargetSystemEntryPoint = link?.EntryPoint ?? Vector3.zero;

        if (string.IsNullOrWhiteSpace(npc.TargetSystemId) ||
            IsInvalidRoutePoint(starSystem, npc.TargetSystemExitPoint) ||
            IsInvalidRoutePoint(link?.LinkedSystem, npc.TargetSystemEntryPoint))
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = Vector3.zero;
        npc.TravelProgress01 = 0f;
    }

    private RouteConfig PickUnlockedRouteFromSystem(string currentSystemId)
    {
        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(currentSystemId);

        if (starSystem == null ||
            starSystem.Routes == null ||
            starSystem.Routes.Count == 0)
        {
            return null;
        }

        List<RouteConfig> unlockedRoutes = new();

        foreach (RouteConfig route in starSystem.Routes)
        {
            if (route == null)
                continue;

            StarSystemConfig targetSystem =
                route.GetOtherSystem(currentSystemId);

            if (targetSystem == null ||
                string.IsNullOrWhiteSpace(targetSystem.Id))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(route.Id))
                continue;

            if (!_routeService.IsRouteUnlocked(route.Id))
                continue;

            unlockedRoutes.Add(route);
        }

        if (unlockedRoutes.Count == 0)
            return null;

        return unlockedRoutes[
            UnityEngine.Random.Range(0, unlockedRoutes.Count)];
    }

    private void SetupAnnihilateOnPlanet(SystemNpcRuntimeState npc)
    {
        if (npc == null)
        {
            LogNpcAnnihilation(
                "[NPC_ANNIHILATION_SETUP]" +
                " Result=False" +
                " | Reason=NullNpc");

            return;
        }

        LogNpcAnnihilation(
            "[NPC_ANNIHILATION_SETUP]" +
            " RuntimeNpcId=" + npc.RuntimeNpcId +
            " | SystemId=" + npc.CurrentSystemId +
            " | PlanetId=" + npc.CurrentPlanetId +
            " | IsAlive=" + npc.IsAlive +
            " | LifeState=" + npc.LifeState +
            " | IsOnPlanet=" + npc.IsOnPlanet +
            " | PrevBehavior=" + npc.PrevBehavior +
            " | CurrentBehavior=" + npc.CurrentBehavior +
            " | TravelState=" + npc.TravelState);

        ClearMovementTargets(npc);

        if (!npc.IsOnPlanet)
        {
            LogNpcAnnihilation(
                "[NPC_ANNIHILATION_SETUP_RESULT]" +
                " Result=False" +
                " | Reason=NpcNotOnPlanetFallbackToStay" +
                " | RuntimeNpcId=" + npc.RuntimeNpcId +
                " | SystemId=" + npc.CurrentSystemId +
                " | TravelState=" + npc.TravelState);

            npc.CurrentBehavior = SystemNpcBehaviorType.StayOnPlanetForDays;
            SetupStayOnPlanet(npc, npc.BehaviorStartedTick);
            return;
        }

        npc.TravelState = SystemNpcTravelState.OnPlanet;

        LogNpcAnnihilation(
            "[NPC_ANNIHILATION_SETUP_RESULT]" +
            " Result=True" +
            " | RuntimeNpcId=" + npc.RuntimeNpcId +
            " | SystemId=" + npc.CurrentSystemId +
            " | PlanetId=" + npc.CurrentPlanetId +
            " | TravelState=" + npc.TravelState);
    }

    private void SetupEngageEnemies(SystemNpcRuntimeState npc)
    {
        if (npc == null || !npc.IsAlive)
            return;

        SyncNpcPositionWithCurrentPlanet(npc, true);
        ClearMovementTargets(npc);

        npc.IsOnPlanet = false;

        string targetId = FindCombatTargetId(npc);

        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
            npc.CurrentTargetRuntimeNpcId = null;
            npc.BehaviorTargetRuntimeNpcId = null;
            npc.CombatState = SystemNpcCombatState.None;
            npc.IsFighting = false;

            ClearBehavior(npc);
            return;
        }

        npc.CurrentTargetRuntimeNpcId = targetId;
        npc.BehaviorTargetRuntimeNpcId = targetId;
        npc.TravelState = SystemNpcTravelState.EngagingEnemy;
        npc.CombatState = SystemNpcCombatState.HasTarget;
        npc.IsFighting = true;
    }

    private bool SyncNpcPositionWithCurrentPlanet(
    SystemNpcRuntimeState npc,
    bool publishPositionChanged)
    {
        if (npc == null)
            return false;

        if (!npc.IsOnPlanet)
            return false;

        if (string.IsNullOrWhiteSpace(npc.CurrentPlanetId))
            return false;

        if (_configService == null ||
            _orbitalMotionService == null)
        {
            return false;
        }

        PlanetConfig planetConfig =
            _configService.GetPlanetConfigById(npc.CurrentPlanetId);

        if (planetConfig == null ||
            planetConfig.PlanetOrbit == null)
        {
            return false;
        }

        float previousZ =
            npc.CurrentPosition.z;

        Vector3 planetPosition =
            _orbitalMotionService.GetPlanetCurrentPosition(
                planetConfig.PlanetOrbit);

        planetPosition.z = previousZ;
        npc.CurrentPosition = planetPosition;

        if (publishPositionChanged && _eventBus != null)
        {
            _eventBus.Publish(
                new SystemNpcPositionChangedEvent(
                    npc.RuntimeNpcId,
                    npc.CurrentSystemId,
                    npc.CurrentPosition));
        }

        return true;
    }

    private SystemNpcBehaviorType PickScenarioBehaviorExcluding(
        NpcBehaviourScenarioConfig behaviorScenario,
        SystemNpcRuntimeState npc,
        SystemNpcBehaviorType excludedBehavior)
    {
        if (behaviorScenario == null || behaviorScenario.BehaviorWeights == null)
            return SystemNpcBehaviorType.None;

        List<SystemNpcBehaviorWeight> weights =
            behaviorScenario.BehaviorWeights
                .Where(weight =>
                    weight != null &&
                    weight.Weight > 0 &&
                    weight.BehaviorType != excludedBehavior)
                .ToList();

        RemoveImpossibleNextBehaviors(weights, npc);

        if (weights.Count == 0)
            return SystemNpcBehaviorType.None;

        return PickWeightedBehavior(weights);
    }

    private SystemNpcBehaviorType PickWeightedBehavior(
        List<SystemNpcBehaviorWeight> weights)
    {
        if (weights == null || weights.Count == 0)
            return SystemNpcBehaviorType.None;

        int totalWeight = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
            totalWeight += Mathf.Max(0, item.Weight);

        if (totalWeight <= 0)
            return SystemNpcBehaviorType.None;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
        {
            cumulative += Mathf.Max(0, item.Weight);

            if (roll < cumulative)
                return item.BehaviorType;
        }

        return weights[^1].BehaviorType;
    }

    private void SetupFallbackBehavior(
        SystemNpcRuntimeState npc,
        SystemNpcBehaviorType fallbackBehavior)
    {
        npc.BehaviorTargetRuntimeNpcId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.CombatState = SystemNpcCombatState.None;
        npc.IsFighting = false;

        switch (fallbackBehavior)
        {
            case SystemNpcBehaviorType.PlanetToPlanetTravel:
                SetupPlanetToPlanetTravel(npc);
                break;

            case SystemNpcBehaviorType.StayOnPlanetForDays:
                SetupStayOnPlanet(npc, npc.BehaviorStartedTick);
                break;

            case SystemNpcBehaviorType.TravelToAnotherSystem:
                SetupTravelToAnotherSystem(npc);
                break;

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                SetupAnnihilateOnPlanet(npc);
                break;

            case SystemNpcBehaviorType.PatrolSystem:
                SetupPatrolSystem(npc);
                break;

            default:
                npc.HasActiveBehavior = false;
                break;
        }
    }

    private void SetupPatrolSystem(SystemNpcRuntimeState npc)
    {
        if (!CanNpcPatrolSystem(npc))
        {
            ClearBehavior(npc);
            return;
        }

        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        bool startedOnPlanet = npc.IsOnPlanet;
        string previousPlanetId = npc.CurrentPlanetId;

        ClearMovementTargets(npc);

        Vector3 patrolTargetPosition =
            RandomPatrolPosition(npc);

        if (!IsFinite(patrolTargetPosition))
        {
            ClearBehavior(npc);
            return;
        }

        patrolTargetPosition.z = -2f;

        Vector3 currentPosition =
            npc.CurrentPosition;

        currentPosition.z = -2f;
        npc.CurrentPosition = currentPosition;

        if (Vector3.Distance(npc.CurrentPosition, patrolTargetPosition) <= 0.1f)
        {
            ClearBehavior(npc);
            return;
        }

        npc.TravelState = SystemNpcTravelState.Patrolling;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = patrolTargetPosition;

        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;

        npc.TravelProgress01 = 0f;

        npc.IsWaitingForInitialRouteBuild = startedOnPlanet;
        npc.ReleaseFromPlanetAfterInitialRouteBuild = startedOnPlanet;
        npc.InitialRouteBuildPlanetId = startedOnPlanet ? previousPlanetId : null;

        if (!startedOnPlanet)
        {
            npc.IsOnPlanet = false;
            npc.CurrentPlanetId = null;
        }

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupPatrolSystem route target assigned without forced facing. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", PatrolTarget=" + patrolTargetPosition +
                ", FacingDirectionKept=" + npc.FacingDirection +
                ", TickMovementDirectionKept=" + npc.TickMovementDirection +
                ", WaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild);
        }
    }

    private void ClearMovementTargets(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        npc.TargetSystemId = null;
        npc.TargetSystemExitPoint = Vector3.zero;
        npc.TargetSystemEntryPoint = Vector3.zero;

        npc.TargetPlanetId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.BehaviorTargetRuntimeNpcId = null;

        npc.TargetPosition = Vector3.zero;
        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TickMovementDirection = Vector3.zero;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;

        npc.TravelProgress01 = 0f;
        npc.CombatState = SystemNpcCombatState.None;
        npc.IsFighting = false;
    }

    private bool IsInvalidRoutePoint(
        StarSystemConfig starSystem,
        Vector3 point)
    {
        if (!IsFinite(point))
            return true;

        if (point.sqrMagnitude <= InvalidRoutePointSqrMagnitude)
            return true;

        if (starSystem == null || starSystem.Sun == null)
            return false;

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            point.z);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float safeRadius = sunRadius + PatrolSunSafetyMargin;

        return Vector3.Distance(point, sunCenter) <= safeRadius;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private string FindCombatTargetId(SystemNpcRuntimeState npc)
    {
        if (npc == null || !npc.IsAlive)
            return null;

        float shotDistance = npc.getShotDistance();

        if (shotDistance <= 0f)
            return null;

        if (npc.IsEnemy)
        {
            List<SystemNpcRuntimeState> targets = new();

            foreach (SystemNpcRuntimeState npcRuntimeState in _npcRuntimeService.Npcs)
            {
                if (npcRuntimeState == null)
                    continue;

                if (npcRuntimeState.IsAlive &&
                    npcRuntimeState.IsAlly &&
                    npcRuntimeState.CurrentSystemId == npc.CurrentSystemId &&
                    npcRuntimeState.IsAvailableForCombat() &&
                    Vector3.Distance(npc.CurrentPosition, npcRuntimeState.CurrentPosition) <= shotDistance)
                {
                    targets.Add(npcRuntimeState);
                }
            }

            if (targets.Count == 0)
                return null;

            return targets[UnityEngine.Random.Range(0, targets.Count)].RuntimeNpcId;
        }

        if (!CanNpcReactToThreats(npc))
            return null;

        List<SystemNpcRuntimeState> enemyTargets = new();

        foreach (SystemNpcRuntimeState npcRuntimeState in _npcRuntimeService.Npcs)
        {
            if (npcRuntimeState == null)
                continue;

            if (npcRuntimeState.IsAlive &&
                npcRuntimeState.IsEnemy &&
                npcRuntimeState.CurrentSystemId == npc.CurrentSystemId &&
                npcRuntimeState.IsAvailableForCombat())
            {
                enemyTargets.Add(npcRuntimeState);
            }
        }

        if (enemyTargets.Count == 0)
            return null;

        return enemyTargets[UnityEngine.Random.Range(0, enemyTargets.Count)].RuntimeNpcId;
    }

    private bool HasEnemiesInSystem(string systemId)
    {
        return HasEnemiesInSystem(
            systemId,
            null);
    }

    private bool HasEnemiesInSystem(
        string systemId,
        IReadOnlyList<SystemNpcRuntimeState> systemNpcs)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        if (systemNpcs != null)
        {
            for (int i = 0; i < systemNpcs.Count; i++)
            {
                SystemNpcRuntimeState npc = systemNpcs[i];

                if (npc == null)
                    continue;

                if (npc.IsAlive &&
                    npc.IsEnemy &&
                    npc.CurrentSystemId == systemId)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (SystemNpcRuntimeState npc in _npcRuntimeService.Npcs)
        {
            if (npc == null)
                continue;

            if (npc.IsAlive &&
                npc.IsEnemy &&
                npc.CurrentSystemId == systemId)
            {
                return true;
            }
        }

        return false;
    }

    private float GetSunWorldSize(SunConfig sun)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetSunWorldSize(sun);
        }

        return sun != null
            ? sun.VisualSize
            : 0f;
    }

    private Vector3 RandomOffset(float radius)
    {
        Vector2 random = UnityEngine.Random.insideUnitCircle * radius;
        return new Vector3(random.x, random.y, 0f);
    }

    private Vector3 RandomPatrolPosition(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return Vector3.zero;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null)
            return npc.CurrentPosition;

        if (!TryGetPatrolBounds(
                starSystem,
                out Vector3 patrolCenter,
                out float patrolRadius))
        {
            return npc.CurrentPosition;
        }

        return RandomPatrolOffset(starSystem, patrolCenter, patrolRadius);
    }

    private bool TryGetPatrolBounds(
        StarSystemConfig starSystem,
        out Vector3 patrolCenter,
        out float patrolRadius)
    {
        patrolCenter = Vector3.zero;
        patrolRadius = 0f;

        if (starSystem == null ||
            starSystem.PlanetRefs == null ||
            starSystem.PlanetRefs.Length == 0)
        {
            return false;
        }

        List<PlanetOrbitConfig> orbitConfigs =
            starSystem.PlanetRefs
                .Where(planet => planet != null && planet.PlanetOrbit != null)
                .Select(planet => planet.PlanetOrbit)
                .OrderBy(orbit => orbit.OrbitRadius)
                .ToList();

        if (orbitConfigs.Count == 0)
            return false;

        int boundaryIndex = Mathf.Min(
            PatrolBoundaryPlanetOrdinal - 1,
            orbitConfigs.Count - 1);

        PlanetOrbitConfig boundaryOrbit = orbitConfigs[boundaryIndex];

        patrolCenter = boundaryOrbit.OrbitCenterOffset;
        patrolCenter.z = 0f;
        patrolRadius = Mathf.Max(0f, boundaryOrbit.OrbitRadius);

        return patrolRadius > 0f;
    }

    private Vector3 RandomPatrolOffset(
        StarSystemConfig starSystem,
        Vector3 patrolCenter,
        float patrolRadius)
    {
        if (starSystem == null || starSystem.Sun == null)
            return patrolCenter + RandomOffset(patrolRadius);

        SunConfig sun = starSystem.Sun;
        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            0f);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float safeRadius = sunRadius + PatrolSunSafetyMargin;

        for (int i = 0; i < PatrolPointPickAttempts; i++)
        {
            Vector3 candidate = patrolCenter + RandomOffset(patrolRadius);

            if (Vector3.Distance(candidate, sunCenter) > safeRadius)
                return candidate;
        }

        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.right;

        Vector3 fallback = sunCenter + new Vector3(
            direction.x,
            direction.y,
            0f) * (safeRadius + PatrolSunFallbackPadding);

        if (Vector3.Distance(fallback, patrolCenter) > patrolRadius)
            fallback = patrolCenter +
                       (fallback - patrolCenter).normalized * patrolRadius;

        if (Vector3.Distance(fallback, sunCenter) <= safeRadius)
        {
            Vector3 awayFromSun = patrolCenter - sunCenter;

            if (awayFromSun.sqrMagnitude <= 0.001f)
                awayFromSun = new Vector3(direction.x, direction.y, 0f);

            fallback = patrolCenter + awayFromSun.normalized * patrolRadius;
        }

        return fallback;
    }

    private bool IsMilitaryDebugNpc(SystemNpcRuntimeState npc)
    {
        return npc != null &&
               npc.IsAlly;
        //     &&
        //    (npc.AllyRole == AllyRole2A.Military ||
        //     npc.AllyRole == AllyRole2A.Science);
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Ally(SystemNpcRuntimeState npc)
    {
        return GetRandomBehaviorType4Ally(
            npc,
            ResolveBehaviorScenario(npc));
    }

    private SystemNpcBehaviorType GetRandomBehaviorType4Ally(
        SystemNpcRuntimeState npc,
        AllyBehaviourScenario resolvedScenario)
    {
        AllyConfig allyConfig =
            _configService.GetAllyConfigById(npc.ConfigId);

        NpcBehaviourScenarioConfig behaviorScenario =
            GetAllyBehaviorScenario(allyConfig, resolvedScenario);

        bool shouldLog =
            ShouldLogAllyBehaviorPickTrace(npc, behaviorScenario);

        if (shouldLog)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Ally pick start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", ConfigId=" + npc.ConfigId +
                ", NpcType=" + npc.NpcType +
                ", RuntimeRole=" + npc.AllyRole +
                ", ConfigRole=" + (allyConfig != null ? allyConfig.Role.ToString() : "NULL_CONFIG") +
                ", Level=" + npc.Level +
                ", CurrentSystem=" + npc.CurrentSystemId +
                ", CurrentBehavior=" + npc.CurrentBehavior +
                ", PrevBehavior=" + npc.PrevBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", HasEnemiesInSystem=" + HasEnemiesInSystem(npc.CurrentSystemId) +
                ", ResolvedScenario=" + resolvedScenario +
                ", ScenarioId=" + (behaviorScenario != null ? behaviorScenario.Id : "NULL_SCENARIO") +
                ", ScenarioWeights=" + FormatBehaviorWeightsForDebug(
                    behaviorScenario != null
                        ? behaviorScenario.BehaviorWeights
                        : null));
        }

        SystemNpcBehaviorType pickedBehavior =
            PickScenarioBehavior(
                behaviorScenario,
                npc);

        if (shouldLog || pickedBehavior == SystemNpcBehaviorType.EngageEnemies)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Ally pick result. " +
                "Npc=" + npc.RuntimeNpcId +
                ", ConfigId=" + npc.ConfigId +
                ", RuntimeRole=" + npc.AllyRole +
                ", PickedBehavior=" + pickedBehavior +
                ", ResolvedScenario=" + resolvedScenario +
                ", ScenarioId=" + (behaviorScenario != null ? behaviorScenario.Id : "NULL_SCENARIO"));
        }

        return pickedBehavior;
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private static void TrackMaxNpcBehaviorPerf(
        SystemNpcRuntimeState npc,
        string stage,
        double elapsedMs,
        SystemNpcBehaviorType behaviorBefore,
        AllyBehaviourScenario scenarioBefore,
        ref double maxNpcMs,
        ref string maxNpcId,
        ref string maxNpcStage,
        ref SystemNpcBehaviorType maxNpcBehavior,
        ref AllyBehaviourScenario maxNpcScenario)
    {
        if (elapsedMs <= maxNpcMs)
            return;

        maxNpcMs = elapsedMs;
        maxNpcId = npc != null ? npc.RuntimeNpcId : "";
        maxNpcStage = stage;
        maxNpcBehavior = behaviorBefore;
        maxNpcScenario = scenarioBefore;
    }

    private void LogNpcBehaviorPerformance(double elapsedMs, string message)
    {
        if (elapsedMs < BehaviorPerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.NpcBehavior))
            return;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcBehavior,
            message);
    }

    private int GetMaxOffscreenAssignNewPerSystemTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 15;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .MaxOffscreenAssignNewPerSystemTick;
    }

    private int GetMaxOffscreenReassignPerSystemTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 15;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .MaxOffscreenReassignPerSystemTick;
    }

    private int GetMaxOffscreenActiveBehaviorPerSystemTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 30;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .MaxOffscreenActiveBehaviorPerSystemTick;
    }

    private void LogNpcBehaviorSelectAnalytics(
    int currentTick,
    bool isDetailedSystem,
    string behaviorTickPhase,
    StarSystemConfig starSystem,
    int npcCount,
    int processedCount,
    int skippedCount,
    double totalMs,
    NpcBehaviorSelectAnalytics analytics)
    {
        if (Bootstrapper.Instance == null ||
            analytics == null ||
            starSystem == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.NpcBehavior))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcBehavior,
            "[NPC_BEHAVIOR_SELECT_ANALYTICS]" +
            " | Tick=" + currentTick +
            " | SystemId=" + starSystem.Id +
            " | DetailedSystem=" + isDetailedSystem +
            " | BehaviorTickPhase=" + behaviorTickPhase +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | Npcs=" + npcCount +
            " | Processed=" + processedCount +
            " | Skipped=" + skippedCount +
            analytics.BuildLogFields());
    }
}
