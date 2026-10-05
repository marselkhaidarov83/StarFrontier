using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcOffscreenSimulationService :
    CustomService,
    ISystemNpcOffscreenSimulationService
{
    private sealed class OffscreenMovementTickStats
    {
        public int MovementLimit;
        public int Scanned;
        public int DeferredByMovementLimit;
        public double CompletionBudgetReserveMs;
        public int DeferredCompletedTravelsByProjectedTimeBudget;
        public double CompletionBudgetUsedMs;
        public double CompletionBudgetMaxMs;
        public int DeferredCompletedTravelsByTimeBudget;
        public int Processed;
        public int MovementBehaviors;
        public int InitializedTravels;
        public int CompletedTravels;
        public int PublishedEvents;
        public double InitializeMs;
        public double CompleteMs;
        public double CompleteBodyMs;
        public double CompletePlanetTravelMs;
        public double CompleteSystemTravelMs;
        public double CompletePatrolMs;
        public double CompletePublishMs;
        public double CompletePublishPositionChangedMs;
        public double CompletePublishTravelStateChangedMs;
        public double CompleteCurrentSystemClearMs;
        public double CompleteBehaviorServiceMs;
        public double PublishMs;
        public double MaxNpcMs;
        public string MaxNpcId = string.Empty;
        public SystemNpcBehaviorType MaxNpcBehavior;
        public SystemNpcTravelState MaxNpcTravelState;
        public double BehaviorCompleteMs;
        public int DeferredCompletedTravels;
        public int CompletionLimit;
        public bool RuntimeLimitsEnabled;
        public int LoopStartIndex;

        public double MaxCompletionMs;
        public string MaxCompletionNpcId = string.Empty;
        public SystemNpcBehaviorType MaxCompletionBehaviorBefore;
        public SystemNpcTravelState MaxCompletionTravelStateBefore;
        public SystemNpcBehaviorType MaxCompletionBehaviorAfter;
        public SystemNpcTravelState MaxCompletionTravelStateAfter;
        public string MaxCompletionSystemBefore = string.Empty;
        public string MaxCompletionSystemAfter = string.Empty;
        public string MaxCompletionTargetSystemId = string.Empty;
        public string MaxCompletionTargetPlanetId = string.Empty;
        public double MaxCompletionBodyMs;
        public double MaxCompletionPublishMs;
        public double MaxCompletionBehaviorServiceMs;
        public double MaxCompletionCurrentSystemClearMs;
        public double MaxCompletionPublishPositionChangedMs;
        public double MaxCompletionPublishTravelStateChangedMs;
    }

    private const double PerfLogThresholdMs = 2.0;
    private const int PerfLogMinNpcCount = 100;
    private readonly IConfigService _configService;
    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly ISystemNpcBehaviorService _behaviorService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly SimpleEventBus _eventBus;
    private readonly Dictionary<string, int> _completionCursors = new();
    private readonly Dictionary<string, int> _movementCursors = new();

    public SystemNpcOffscreenSimulationService()
    {
        _debugStop = true;

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _runtimeService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();

        _behaviorService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();

        _orbitalMotionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public void Tick(
        StarSystemConfig starSystem,
        int currentTick,
        bool forceUnlimitedCompletion)
    {
        if (starSystem == null || string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        long totalStartedAt = BeginPerfMeasure();

        long getNpcsStartedAt = BeginPerfMeasure();

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _runtimeService.GetAliveNpcsInSystem(starSystem.Id);

        double getNpcsMs = EndPerfMeasureMs(getNpcsStartedAt);

        int npcCount = npcs != null ? npcs.Count : 0;

        if (npcs == null || npcs.Count == 0)
            return;

        long activeCombatStartedAt = BeginPerfMeasure();
        bool hasActiveCombat = HasActiveCombat(npcs);
        double activeCombatMs = EndPerfMeasureMs(activeCombatStartedAt);

        if (hasActiveCombat)
        {
            double totalMs = EndPerfMeasureMs(totalStartedAt);

            LogOffscreenMovementDetailIfNeeded(
                totalMs,
                starSystem,
                currentTick,
                npcCount,
                getNpcsMs,
                activeCombatMs,
                true,
                null,
                0d);

            if (ShouldLogNpcMovementPerf(totalMs, npcCount))
            {
                LogNpcMovementPerf(
                    totalMs,
                    npcCount,
                    "[SystemNpcOffscreenSimulationService] Skipped active combat" +
                    " | SystemId=" + starSystem.Id +
                    " | System=" + starSystem.DisplayName +
                    " | Npcs=" + npcCount +
                    " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
                    " | HasActiveCombatMs=" + activeCombatMs.ToString("F2"));
            }

            return;
        }

        bool runtimeLimitsEnabled = !forceUnlimitedCompletion;

        int movementLimit =
            runtimeLimitsEnabled
                ? GetMaxOffscreenMovementNpcsPerSystemTick()
                : int.MaxValue;

        int completionLimit =
            runtimeLimitsEnabled
                ? GetMaxOffscreenCompletedTravelsPerSystemTick()
                : int.MaxValue;

        double completionBudgetMaxMs =
            runtimeLimitsEnabled
                ? GetMaxOffscreenCompletedTravelMsPerSystemTick()
                : 0d;

        int startIndex =
            runtimeLimitsEnabled
                ? GetMovementCursor(starSystem.Id, npcs.Count)
                : 0;

        int scanLimit =
            runtimeLimitsEnabled
                ? Mathf.Min(npcs.Count, movementLimit)
                : npcs.Count;

        OffscreenMovementTickStats stats =
            new OffscreenMovementTickStats
            {
                MovementLimit = movementLimit,
                CompletionLimit = completionLimit,
                CompletionBudgetMaxMs = completionBudgetMaxMs,
                CompletionBudgetReserveMs = GetOffscreenCompletedTravelBudgetReserveMs(),
                RuntimeLimitsEnabled = runtimeLimitsEnabled,
                LoopStartIndex = startIndex
            };

        long loopStartedAt = BeginPerfMeasure();

        for (int offset = 0; offset < scanLimit; offset++)
        {
            int npcIndex = startIndex + offset;

            if (npcIndex >= npcs.Count)
                npcIndex -= npcs.Count;

            stats.Scanned++;

            SystemNpcRuntimeState npc = npcs[npcIndex];

            if (npc == null || !npc.IsAlive)
                continue;

            stats.Processed++;

            if (IsAnalyticMovementBehavior(npc.CurrentBehavior))
                stats.MovementBehaviors++;

            long npcStartedAt = BeginPerfMeasure();

            TickNpcOffscreen(
                npc,
                currentTick,
                stats);

            double npcMs = EndPerfMeasureMs(npcStartedAt);

            if (npcMs > stats.MaxNpcMs)
            {
                stats.MaxNpcMs = npcMs;
                stats.MaxNpcId = npc.RuntimeNpcId;
                stats.MaxNpcBehavior = npc.CurrentBehavior;
                stats.MaxNpcTravelState = npc.TravelState;
            }
        }

        if (runtimeLimitsEnabled && scanLimit < npcs.Count)
        {
            stats.DeferredByMovementLimit =
                npcs.Count - scanLimit;
        }

        if (runtimeLimitsEnabled)
        {
            SaveMovementCursor(
                starSystem.Id,
                startIndex,
                stats.Scanned,
                npcs.Count);
        }

        double loopMs = EndPerfMeasureMs(loopStartedAt);
        double totalTickMs = EndPerfMeasureMs(totalStartedAt);

        LogOffscreenMovementDetailIfNeeded(
            totalTickMs,
            starSystem,
            currentTick,
            npcCount,
            getNpcsMs,
            activeCombatMs,
            false,
            stats,
            loopMs);

        if (ShouldLogNpcMovementPerf(totalTickMs, npcCount))
        {
            LogNpcMovementPerf(
                totalTickMs,
                npcCount,
                "[SystemNpcOffscreenSimulationService] Tick" +
                " | SystemId=" + starSystem.Id +
                " | System=" + starSystem.DisplayName +
                " | Npcs=" + npcCount +
                " | MovementLimit=" +
                (stats.RuntimeLimitsEnabled ? stats.MovementLimit.ToString() : "Unlimited") +
                " | Scanned=" + stats.Scanned +
                " | Processed=" + stats.Processed +
                " | DeferredByMovementLimit=" + stats.DeferredByMovementLimit +
                " | MovementBehaviors=" + stats.MovementBehaviors +
                " | InitializedTravels=" + stats.InitializedTravels +
                " | CompletedTravels=" + stats.CompletedTravels +
                " | DeferredCompletedTravels=" + stats.DeferredCompletedTravels +
                " | CompletionLimit=" +
                (stats.RuntimeLimitsEnabled ? stats.CompletionLimit.ToString() : "Unlimited") +
                " | CompletionBudgetMaxMs=" +
                (stats.RuntimeLimitsEnabled ? stats.CompletionBudgetMaxMs.ToString("F2") : "Unlimited") +
                " | CompletionBudgetUsedMs=" + stats.CompletionBudgetUsedMs.ToString("F2") +
                " | DeferredCompletedTravelsByTimeBudget=" + stats.DeferredCompletedTravelsByTimeBudget +
                " | RuntimeLimitsEnabled=" + stats.RuntimeLimitsEnabled +
                " | CompletionBudgetReserveMs=" + stats.CompletionBudgetReserveMs.ToString("F2") +
                " | DeferredCompletedTravelsByProjectedTimeBudget=" + stats.DeferredCompletedTravelsByProjectedTimeBudget +
                " | LoopStartIndex=" + stats.LoopStartIndex +
                " | PublishedEvents=" + stats.PublishedEvents +
                " | Tick=" + currentTick +
                " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
                " | HasActiveCombatMs=" + activeCombatMs.ToString("F2") +
                " | LoopMs=" + loopMs.ToString("F2") +
                " | InitializeMs=" + stats.InitializeMs.ToString("F2") +
                " | CompleteMs=" + stats.CompleteMs.ToString("F2") +
                " | PublishMs=" + stats.PublishMs.ToString("F2") +
                " | BehaviorCompleteMs=" + stats.BehaviorCompleteMs.ToString("F2") +
                " | MaxNpcMs=" + stats.MaxNpcMs.ToString("F2") +
                " | MaxNpc=" + stats.MaxNpcId +
                " | MaxNpcBehavior=" + stats.MaxNpcBehavior +
                " | MaxNpcTravelState=" + stats.MaxNpcTravelState);
        }
    }

    private void LogOffscreenMovementDetailIfNeeded(
        double totalMs,
        StarSystemConfig starSystem,
        int currentTick,
        int npcCount,
        double getNpcsMs,
        double activeCombatMs,
        bool skippedByActiveCombat,
        OffscreenMovementTickStats stats,
        double loopMs)
    {
        if (totalMs < PerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        string systemId =
            starSystem != null
                ? starSystem.Id
                : string.Empty;

        string systemName =
            starSystem != null
                ? starSystem.DisplayName
                : string.Empty;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[OFFSCREEN_NPC_MOVEMENT_DETAIL]" +
            " Tick=" + currentTick +
            " | SystemId=" + systemId +
            " | System=" + systemName +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | Npcs=" + npcCount +
            " | SkippedByActiveCombat=" + skippedByActiveCombat +
            " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
            " | HasActiveCombatMs=" + activeCombatMs.ToString("F2") +
            " | LoopMs=" + loopMs.ToString("F2") +
            " | RuntimeLimitsEnabled=" + (stats != null && stats.RuntimeLimitsEnabled) +
            " | MovementLimit=" +
            (stats != null
                ? (stats.RuntimeLimitsEnabled ? stats.MovementLimit.ToString() : "Unlimited")
                : "0") +
            " | Scanned=" + (stats != null ? stats.Scanned : 0) +
            " | Processed=" + (stats != null ? stats.Processed : 0) +
            " | DeferredByMovementLimit=" + (stats != null ? stats.DeferredByMovementLimit : 0) +
            " | MovementBehaviors=" + (stats != null ? stats.MovementBehaviors : 0) +
            " | InitializedTravels=" + (stats != null ? stats.InitializedTravels : 0) +
            " | CompletedTravels=" + (stats != null ? stats.CompletedTravels : 0) +
            " | DeferredCompletedTravels=" + (stats != null ? stats.DeferredCompletedTravels : 0) +
            " | CompletionLimit=" +
            (stats != null
                ? (stats.RuntimeLimitsEnabled ? stats.CompletionLimit.ToString() : "Unlimited")
                : "0") +
            " | CompletionBudgetMaxMs=" +
            (stats != null
                ? (stats.RuntimeLimitsEnabled ? stats.CompletionBudgetMaxMs.ToString("F2") : "Unlimited")
                : "0.00") +
            " | CompletionBudgetUsedMs=" + (stats != null ? stats.CompletionBudgetUsedMs.ToString("F2") : "0.00") +
            " | DeferredCompletedTravelsByTimeBudget=" + (stats != null ? stats.DeferredCompletedTravelsByTimeBudget : 0) +
            " | CompletionBudgetReserveMs=" + (stats != null ? stats.CompletionBudgetReserveMs.ToString("F2") : "0.00") +
            " | DeferredCompletedTravelsByProjectedTimeBudget=" + (stats != null ? stats.DeferredCompletedTravelsByProjectedTimeBudget : 0) +
            " | LoopStartIndex=" + (stats != null ? stats.LoopStartIndex : 0) +
            " | PublishedEvents=" + (stats != null ? stats.PublishedEvents : 0) +
            " | InitializeMs=" + (stats != null ? stats.InitializeMs.ToString("F2") : "0.00") +
            " | CompleteMs=" + (stats != null ? stats.CompleteMs.ToString("F2") : "0.00") +
            " | CompleteBodyMs=" + (stats != null ? stats.CompleteBodyMs.ToString("F2") : "0.00") +
            " | CompletePlanetTravelMs=" + (stats != null ? stats.CompletePlanetTravelMs.ToString("F2") : "0.00") +
            " | CompleteSystemTravelMs=" + (stats != null ? stats.CompleteSystemTravelMs.ToString("F2") : "0.00") +
            " | CompletePatrolMs=" + (stats != null ? stats.CompletePatrolMs.ToString("F2") : "0.00") +
            " | PublishMs=" + (stats != null ? stats.PublishMs.ToString("F2") : "0.00") +
            " | CompletePublishMs=" + (stats != null ? stats.CompletePublishMs.ToString("F2") : "0.00") +
            " | CompletePublishPositionChangedMs=" + (stats != null ? stats.CompletePublishPositionChangedMs.ToString("F2") : "0.00") +
            " | CompletePublishTravelStateChangedMs=" + (stats != null ? stats.CompletePublishTravelStateChangedMs.ToString("F2") : "0.00") +
            " | CompleteCurrentSystemClearMs=" + (stats != null ? stats.CompleteCurrentSystemClearMs.ToString("F2") : "0.00") +
            " | BehaviorCompleteMs=" + (stats != null ? stats.BehaviorCompleteMs.ToString("F2") : "0.00") +
            " | CompleteBehaviorServiceMs=" + (stats != null ? stats.CompleteBehaviorServiceMs.ToString("F2") : "0.00") +
            " | MaxNpcMs=" + (stats != null ? stats.MaxNpcMs.ToString("F2") : "0.00") +
            " | MaxNpc=" + (stats != null ? stats.MaxNpcId : string.Empty) +
            " | MaxNpcBehavior=" + (stats != null ? stats.MaxNpcBehavior.ToString() : string.Empty) +
            " | MaxNpcTravelState=" + (stats != null ? stats.MaxNpcTravelState.ToString() : string.Empty) +
            " | MaxCompletionMs=" + (stats != null ? stats.MaxCompletionMs.ToString("F2") : "0.00") +
            " | MaxCompletionNpc=" + (stats != null ? stats.MaxCompletionNpcId : string.Empty) +
            " | MaxCompletionBehaviorBefore=" + (stats != null ? stats.MaxCompletionBehaviorBefore.ToString() : string.Empty) +
            " | MaxCompletionTravelStateBefore=" + (stats != null ? stats.MaxCompletionTravelStateBefore.ToString() : string.Empty) +
            " | MaxCompletionBehaviorAfter=" + (stats != null ? stats.MaxCompletionBehaviorAfter.ToString() : string.Empty) +
            " | MaxCompletionTravelStateAfter=" + (stats != null ? stats.MaxCompletionTravelStateAfter.ToString() : string.Empty) +
            " | MaxCompletionSystemBefore=" + (stats != null ? stats.MaxCompletionSystemBefore : string.Empty) +
            " | MaxCompletionSystemAfter=" + (stats != null ? stats.MaxCompletionSystemAfter : string.Empty) +
            " | MaxCompletionTargetSystemId=" + (stats != null ? stats.MaxCompletionTargetSystemId : string.Empty) +
            " | MaxCompletionTargetPlanetId=" + (stats != null ? stats.MaxCompletionTargetPlanetId : string.Empty) +
            " | MaxCompletionBodyMs=" + (stats != null ? stats.MaxCompletionBodyMs.ToString("F2") : "0.00") +
            " | MaxCompletionPublishMs=" + (stats != null ? stats.MaxCompletionPublishMs.ToString("F2") : "0.00") +
            " | MaxCompletionPublishPositionChangedMs=" + (stats != null ? stats.MaxCompletionPublishPositionChangedMs.ToString("F2") : "0.00") +
            " | MaxCompletionPublishTravelStateChangedMs=" + (stats != null ? stats.MaxCompletionPublishTravelStateChangedMs.ToString("F2") : "0.00") +
            " | MaxCompletionCurrentSystemClearMs=" + (stats != null ? stats.MaxCompletionCurrentSystemClearMs.ToString("F2") : "0.00") +
            " | MaxCompletionBehaviorServiceMs=" + (stats != null ? stats.MaxCompletionBehaviorServiceMs.ToString("F2") : "0.00"));
    }

    private int GetMaxOffscreenMovementNpcsPerSystemTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 35;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .MaxOffscreenMovementNpcsPerSystemTick;
    }


    private void TickNpcOffscreen(
     SystemNpcRuntimeState npc,
     int currentTick,
     OffscreenMovementTickStats stats)
    {
        switch (npc.CurrentBehavior)
        {
            case SystemNpcBehaviorType.PlanetToPlanetTravel:
            case SystemNpcBehaviorType.TravelToAnotherSystem:
            case SystemNpcBehaviorType.PatrolSystem:
                TickAnalyticTravel(npc, currentTick, stats);
                break;

            case SystemNpcBehaviorType.StayOnPlanetForDays:
            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                // Эти поведения уже завершаются через SystemNpcBehaviorService.
                break;
        }
    }

    private void TickAnalyticTravel(
        SystemNpcRuntimeState npc,
        int currentTick,
        OffscreenMovementTickStats stats)
    {
        if (!npc.HasActiveBehavior)
            return;

        if (npc.TravelEndTick <= 0)
        {
            long initializeStartedAt = BeginPerfMeasure();

            InitializeAnalyticTravel(npc, currentTick, stats);

            stats.InitializeMs += EndPerfMeasureMs(initializeStartedAt);
            return;
        }

        if (currentTick < npc.TravelEndTick)
            return;

        if (stats.RuntimeLimitsEnabled &&
            stats.CompletedTravels >= stats.CompletionLimit)
        {
            stats.DeferredCompletedTravels++;
            return;
        }

        if (IsOffscreenCompletionTimeBudgetExceeded(stats))
        {
            stats.DeferredCompletedTravels++;
            stats.DeferredCompletedTravelsByTimeBudget++;
            return;
        }

        if (WouldOffscreenCompletionExceedProjectedTimeBudget(stats))
        {
            stats.DeferredCompletedTravels++;
            stats.DeferredCompletedTravelsByProjectedTimeBudget++;
            return;
        }

        SystemNpcBehaviorType behaviorBefore = npc.CurrentBehavior;
        SystemNpcTravelState travelStateBefore = npc.TravelState;
        string systemBefore = npc.CurrentSystemId ?? string.Empty;
        string targetSystemId = npc.TargetSystemId ?? string.Empty;
        string targetPlanetId = npc.TargetPlanetId ?? string.Empty;

        long completeStartedAt = BeginPerfMeasure();

        CompleteAnalyticTravel(npc, currentTick, stats);

        double completeMs =
            EndPerfMeasureMs(completeStartedAt);

        stats.CompleteMs += completeMs;
        stats.CompletionBudgetUsedMs += completeMs;

        if (completeMs > stats.MaxCompletionMs)
        {
            stats.MaxCompletionMs = completeMs;
            stats.MaxCompletionNpcId = npc.RuntimeNpcId ?? string.Empty;
            stats.MaxCompletionBehaviorBefore = behaviorBefore;
            stats.MaxCompletionTravelStateBefore = travelStateBefore;
            stats.MaxCompletionBehaviorAfter = npc.CurrentBehavior;
            stats.MaxCompletionTravelStateAfter = npc.TravelState;
            stats.MaxCompletionSystemBefore = systemBefore;
            stats.MaxCompletionSystemAfter = npc.CurrentSystemId ?? string.Empty;
            stats.MaxCompletionTargetSystemId = targetSystemId;
            stats.MaxCompletionTargetPlanetId = targetPlanetId;
            stats.MaxCompletionBodyMs = stats.CompleteBodyMs;
            stats.MaxCompletionPublishMs = stats.CompletePublishMs;
            stats.MaxCompletionBehaviorServiceMs = stats.CompleteBehaviorServiceMs;
            stats.MaxCompletionCurrentSystemClearMs = stats.CompleteCurrentSystemClearMs;
            stats.MaxCompletionPublishPositionChangedMs = stats.CompletePublishPositionChangedMs;
            stats.MaxCompletionPublishTravelStateChangedMs = stats.CompletePublishTravelStateChangedMs;
        }
    }

    private bool WouldOffscreenCompletionExceedProjectedTimeBudget(
    OffscreenMovementTickStats stats)
    {
        if (stats == null)
            return false;

        if (!stats.RuntimeLimitsEnabled)
            return false;

        if (stats.CompletionBudgetMaxMs <= 0d)
            return false;

        if (stats.CompletedTravels <= 0)
            return false;

        double reserveMs =
            stats.CompletionBudgetReserveMs > 0d
                ? stats.CompletionBudgetReserveMs
                : GetAverageOffscreenCompletedTravelMs(stats);

        return stats.CompletionBudgetUsedMs + reserveMs > stats.CompletionBudgetMaxMs;
    }

    private double GetAverageOffscreenCompletedTravelMs(
        OffscreenMovementTickStats stats)
    {
        if (stats == null ||
            stats.CompletedTravels <= 0)
        {
            return 0d;
        }

        return stats.CompletionBudgetUsedMs / stats.CompletedTravels;
    }

    private float GetOffscreenCompletedTravelBudgetReserveMs()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 1f;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .OffscreenCompletedTravelBudgetReserveMs;
    }

    private bool IsOffscreenCompletionTimeBudgetExceeded(
    OffscreenMovementTickStats stats)
    {
        if (stats == null)
            return false;

        if (!stats.RuntimeLimitsEnabled)
            return false;

        if (stats.CompletionBudgetMaxMs <= 0d)
            return false;

        return stats.CompletionBudgetUsedMs >= stats.CompletionBudgetMaxMs;
    }

    private float GetMaxOffscreenCompletedTravelMsPerSystemTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 0f;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .MaxOffscreenCompletedTravelMsPerSystemTick;
    }

    private void InitializeAnalyticTravel(
        SystemNpcRuntimeState npc,
        int currentTick,
        OffscreenMovementTickStats stats)
    {
        Vector3 targetPosition = ResolveAnalyticTargetPosition(npc);

        if (!IsFinite(targetPosition))
            return;

        float distance =
            Vector3.Distance(npc.CurrentPosition, targetPosition);

        float speed =
            Mathf.Max(0.01f, npc.Speed);

        float speedMultiplier =
            Mathf.Max(0.01f, GetSpeedMultiplier());

        float slowdown =
            Mathf.Max(0.01f, GetOffscreenNpcTravelSlowdown());

        float effectiveDistancePerTick =
            speed * speedMultiplier;

        int durationTicks =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    distance / effectiveDistancePerTick * slowdown));

        npc.TravelStartTick = currentTick;
        npc.TravelEndTick = currentTick + durationTicks;
        npc.TravelProgress01 = 0f;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = targetPosition;
        npc.CurrentMovementTargetPosition = targetPosition;
        npc.TickMovementTargetPosition = targetPosition;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;

        stats.InitializedTravels++;
    }

    private float GetOffscreenNpcTravelSlowdown()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 1f;

        return config.OffscreenNpcTravelSlowdown;
    }

    private Vector3 ResolveAnalyticTargetPosition(SystemNpcRuntimeState npc)
    {
        switch (npc.CurrentBehavior)
        {
            case SystemNpcBehaviorType.PlanetToPlanetTravel:
                return ResolvePlanetPosition(npc.TargetPlanetId, npc.CurrentPosition.z);

            case SystemNpcBehaviorType.TravelToAnotherSystem:
                return npc.TargetSystemExitPoint;

            case SystemNpcBehaviorType.PatrolSystem:
                return npc.TargetPosition;

            default:
                return Vector3.zero;
        }
    }

    private void CompleteAnalyticTravel(
        SystemNpcRuntimeState npc,
        int currentTick,
        OffscreenMovementTickStats stats)
    {
        long bodyStartedAt = BeginPerfMeasure();

        switch (npc.TravelState)
        {
            case SystemNpcTravelState.TravelingInsideSystem:
                {
                    long travelStartedAt = BeginPerfMeasure();

                    CompletePlanetTravel(npc);

                    stats.CompletePlanetTravelMs += EndPerfMeasureMs(travelStartedAt);
                    break;
                }

            case SystemNpcTravelState.TravelingToAnotherSystem:
                {
                    long travelStartedAt = BeginPerfMeasure();

                    CompleteSystemTravel(npc);

                    stats.CompleteSystemTravelMs += EndPerfMeasureMs(travelStartedAt);
                    break;
                }

            case SystemNpcTravelState.Patrolling:
                {
                    long travelStartedAt = BeginPerfMeasure();

                    CompletePatrol(npc);

                    stats.CompletePatrolMs += EndPerfMeasureMs(travelStartedAt);
                    break;
                }

            default:
                return;
        }

        stats.CompleteBodyMs += EndPerfMeasureMs(bodyStartedAt);
        stats.CompletedTravels++;

        long publishStartedAt = BeginPerfMeasure();

        PublishNpcChanged(npc, stats);

        double publishMs =
            EndPerfMeasureMs(publishStartedAt);

        stats.PublishMs += publishMs;
        stats.CompletePublishMs += publishMs;

        if (npc.CurrentSystemId == GetCurrentSystemId())
        {
            long clearStartedAt = BeginPerfMeasure();

            ClearCompletedBehaviorWithoutReassign(npc);

            stats.CompleteCurrentSystemClearMs += EndPerfMeasureMs(clearStartedAt);
            return;
        }

        long behaviorCompleteStartedAt = BeginPerfMeasure();

        _behaviorService.CompleteBehavior(npc, currentTick);

        double behaviorCompleteMs =
            EndPerfMeasureMs(behaviorCompleteStartedAt);

        stats.BehaviorCompleteMs += behaviorCompleteMs;
        stats.CompleteBehaviorServiceMs += behaviorCompleteMs;
    }

    private void CompletePlanetTravel(SystemNpcRuntimeState npc)
    {
        Vector3 planetPosition =
            ResolvePlanetPosition(npc.TargetPlanetId, npc.CurrentPosition.z);

        npc.CurrentPosition = planetPosition;
        npc.StartPosition = planetPosition;
        npc.CurrentPlanetId = npc.TargetPlanetId;
        npc.TargetPlanetId = null;
        npc.IsOnPlanet = true;
        npc.TravelState = SystemNpcTravelState.OnPlanet;
        npc.TravelProgress01 = 1f;
    }

    private void CompleteSystemTravel(SystemNpcRuntimeState npc)
    {
        string targetSystemId = npc.TargetSystemId;
        Vector3 entryPoint = npc.TargetSystemEntryPoint;

        if (!string.IsNullOrWhiteSpace(targetSystemId))
        {
            npc.CurrentSystemId = targetSystemId;
            npc.CurrentPosition = entryPoint;
            npc.StartPosition = entryPoint;
        }

        npc.TargetSystemId = null;
        npc.TargetSystemExitPoint = Vector3.zero;
        npc.TargetSystemEntryPoint = Vector3.zero;

        npc.CurrentPlanetId = null;
        npc.TargetPlanetId = null;
        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;
        npc.TravelProgress01 = 1f;

        ApplyFacingToSun(npc);
    }

    private void CompletePatrol(SystemNpcRuntimeState npc)
    {
        Vector3 targetPosition = npc.TargetPosition;

        npc.CurrentPosition = targetPosition;
        npc.StartPosition = targetPosition;
        npc.TargetPosition = Vector3.zero;
        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;
        npc.TravelProgress01 = 1f;
    }

    private void ClearCompletedBehaviorWithoutReassign(SystemNpcRuntimeState npc)
    {
        npc.HasActiveBehavior = false;
        npc.PrevBehavior = npc.CurrentBehavior;
        npc.CurrentBehavior = SystemNpcBehaviorType.None;
        npc.BehaviorStartedTick = 0;
        npc.BehaviorEndsTick = 0;
        npc.TravelStartTick = 0;
        npc.TravelEndTick = 0;
        npc.BehaviorTargetRuntimeNpcId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.IsFighting = false;
        npc.CombatState = SystemNpcCombatState.None;
    }

    private bool HasActiveCombat(IReadOnlyList<SystemNpcRuntimeState> npcs)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null || !npc.IsAlive)
                continue;

            if (npc.CurrentBehavior == SystemNpcBehaviorType.EngageEnemies)
                return true;

            if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
                return true;

            if (npc.IsFighting)
                return true;

            if (npc.CombatState != SystemNpcCombatState.None)
                return true;

            if (!string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
                return true;
        }

        return false;
    }

    private Vector3 ResolvePlanetPosition(
        string planetId,
        float z)
    {
        PlanetConfig planet =
            _configService.GetPlanetConfigById(planetId);

        if (planet == null || planet.PlanetOrbit == null)
            return Vector3.zero;

        Vector3 position =
            _orbitalMotionService != null
                ? _orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit)
                : Vector3.zero;

        position.z = z;
        return position;
    }

    private void ApplyFacingToSun(SystemNpcRuntimeState npc)
    {
        Vector3 direction = Vector3.zero - npc.CurrentPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.up;

        direction.Normalize();

        npc.FacingDirection = direction;
        npc.TickMovementDirection = direction;
        npc.TargetPosition = npc.CurrentPosition + direction;
        npc.CurrentMovementTargetPosition = npc.TargetPosition;
        npc.TickMovementTargetPosition = npc.TargetPosition;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
    }

    private float GetSpeedMultiplier()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 1f;

        return config.SpeedMultiplier;
    }

    private string GetCurrentSystemId()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return string.Empty;
        }

        return _gameSessionService.State.Player.CurrentSystemId ?? string.Empty;
    }

    private void PublishNpcChanged(
        SystemNpcRuntimeState npc,
        OffscreenMovementTickStats stats)
    {
        long positionPublishStartedAt = BeginPerfMeasure();

        _eventBus.Publish(
            new SystemNpcPositionChangedEvent(
                npc.RuntimeNpcId,
                npc.CurrentSystemId,
                npc.CurrentPosition));

        stats.CompletePublishPositionChangedMs += EndPerfMeasureMs(positionPublishStartedAt);

        long travelStatePublishStartedAt = BeginPerfMeasure();

        _eventBus.Publish(
            new SystemNpcTravelStateChangedEvent(
                npc.RuntimeNpcId,
                npc,
                npc.TravelState,
                npc.CurrentSystemId));

        stats.CompletePublishTravelStateChangedMs += EndPerfMeasureMs(travelStatePublishStartedAt);

        stats.PublishedEvents += 2;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !(float.IsNaN(value.x) ||
                 float.IsNaN(value.y) ||
                 float.IsNaN(value.z) ||
                 float.IsInfinity(value.x) ||
                 float.IsInfinity(value.y) ||
                 float.IsInfinity(value.z));
    }

    private static bool IsAnalyticMovementBehavior(
    SystemNpcBehaviorType behavior)
    {
        return behavior == SystemNpcBehaviorType.PlanetToPlanetTravel ||
               behavior == SystemNpcBehaviorType.TravelToAnotherSystem ||
               behavior == SystemNpcBehaviorType.PatrolSystem;
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private bool ShouldLogNpcMovementPerf(double elapsedMs, int npcCount)
    {
        if (elapsedMs < PerfLogThresholdMs)
            return false;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.NpcMovement))
        {
            return false;
        }

        return true;
    }

    private void LogNpcMovementPerf(double elapsedMs, int npcCount, string message)
    {
        if (!ShouldLogNpcMovementPerf(elapsedMs, npcCount))
            return;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovement,
            message + " | Ms=" + elapsedMs.ToString("F2"));
    }

    private int GetMaxOffscreenCompletedTravelsPerSystemTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig == null)
        {
            return 25;
        }

        return Bootstrapper
            .Instance
            .OffscreenNpcSimulationScheduleConfig
            .MaxOffscreenCompletedTravelsPerSystemTick;
    }

    private int GetMovementCursor(
    string systemId,
    int npcCount)
    {
        if (npcCount <= 0 || string.IsNullOrWhiteSpace(systemId))
            return 0;

        if (!_movementCursors.TryGetValue(systemId, out int cursor))
            return 0;

        return Mathf.Clamp(cursor, 0, npcCount - 1);
    }

    private void SaveMovementCursor(
        string systemId,
        int startIndex,
        int scanned,
        int npcCount)
    {
        if (npcCount <= 0 || string.IsNullOrWhiteSpace(systemId))
            return;

        int step =
            Mathf.Max(1, scanned);

        int nextCursor =
            startIndex + step;

        while (nextCursor >= npcCount)
            nextCursor -= npcCount;

        _movementCursors[systemId] = nextCursor;
    }
}