using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcWarmupService :
    CustomService,
    IGalaxyNpcWarmupService
{
    private readonly IGalaxyNpcSimulationScheduleService _simulationScheduleService;
    private readonly ISystemNpcBehaviorService _systemNpcBehaviorService;
    private readonly ISystemNpcOffscreenSimulationService _offscreenSimulationService;
    private readonly IGameTimeService _gameTimeService;
    private readonly ISystemNpcMovementService _systemNpcMovementService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;

    public GalaxyNpcWarmupService()
    {
        _debugStop = true;

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        _simulationScheduleService =
            registry.Get<IGalaxyNpcSimulationScheduleService>();

        _systemNpcBehaviorService =
            registry.Get<ISystemNpcBehaviorService>();

        _systemNpcMovementService =
            registry.Get<ISystemNpcMovementService>();

        registry.TryGet(out _offscreenSimulationService);
        registry.TryGet(out _gameTimeService);
        registry.TryGet(out _npcRuntimeService);
    }

    public void RunInitialWarmup(string reason)
    {
        if (_simulationScheduleService == null ||
            _systemNpcBehaviorService == null)
        {
            AppLog.Error(
                "[GalaxyNpcWarmupService] Cannot run warmup: " +
                "required services are missing.");

            return;
        }

        int quantTick = GetCurrentQuantTick();
        long startedAt = BeginPerfMeasure();

        StarSystemConfig currentSystem =
            _simulationScheduleService.GetCurrentSystem();

        IReadOnlyList<StarSystemConfig> offscreenSystems =
            _simulationScheduleService.GetInitialWarmupOffscreenSystems();

        int detailedSystems = 0;
        int detailedMovementWarmupSystems = 0;
        int offscreenSystemsProcessed = 0;

        string currentSystemId =
            currentSystem != null
                ? currentSystem.Id
                : string.Empty;

        WarmupQueueSummary queueSummary =
            new WarmupQueueSummary();

        if (currentSystem != null)
        {
            _systemNpcBehaviorService.Tick(
                currentSystem,
                quantTick,
                true,
                true);

            _systemNpcMovementService?.RunInitialWarmupRoutes(
                currentSystem,
                quantTick);

            detailedSystems++;
            detailedMovementWarmupSystems++;

            AddQueueSummary(
                queueSummary,
                currentSystem,
                quantTick,
                true);
        }

        if (offscreenSystems != null)
        {
            for (int i = 0; i < offscreenSystems.Count; i++)
            {
                StarSystemConfig offscreenSystem = offscreenSystems[i];

                if (offscreenSystem == null ||
                    string.IsNullOrWhiteSpace(offscreenSystem.Id) ||
                    offscreenSystem.Id == currentSystemId)
                {
                    continue;
                }

                _systemNpcBehaviorService.Tick(
                    offscreenSystem,
                    quantTick,
                    false,
                    true);

                _offscreenSimulationService?.Tick(
                    offscreenSystem,
                    quantTick,
                    true);

                offscreenSystemsProcessed++;

                AddQueueSummary(
                    queueSummary,
                    offscreenSystem,
                    quantTick,
                    false);
            }
        }

        _simulationScheduleService.CompleteInitialWarmup();

        double totalMs = EndPerfMeasureMs(startedAt);

        LogNpcWarmupPerformance(
            totalMs,
            "[GalaxyNpcWarmupService] InitialWarmup completed. " +
            "Reason=" + reason +
            " | Tick=" + quantTick +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" +
            _simulationScheduleService.PrioritySourceSystemId +
            " | DetailedSystems=" + detailedSystems +
            " | DetailedMovementWarmupSystems=" + detailedMovementWarmupSystems +
            " | OffscreenSystems=" + offscreenSystemsProcessed +
            " | TotalMs=" + totalMs.ToString("0.00"));

        LogWarmupSummary(
            reason,
            quantTick,
            currentSystemId,
            detailedSystems,
            detailedMovementWarmupSystems,
            offscreenSystems != null ? offscreenSystems.Count : 0,
            offscreenSystemsProcessed,
            queueSummary,
            totalMs);
    }

    public IEnumerator RunInitialWarmupRoutine(
        string reason,
        float progressFrom01,
        float progressTo01)
    {
        if (_simulationScheduleService == null ||
            _systemNpcBehaviorService == null)
        {
            AppLog.Error(
                "[GalaxyNpcWarmupService] Cannot run warmup: " +
                "required services are missing.");

            yield break;
        }

        int quantTick = GetCurrentQuantTick();
        long startedAt = BeginPerfMeasure();

        StarSystemConfig currentSystem =
            _simulationScheduleService.GetCurrentSystem();

        IReadOnlyList<StarSystemConfig> offscreenSystems =
            _simulationScheduleService.GetInitialWarmupOffscreenSystems();

        int detailedSystems = 0;
        int detailedMovementWarmupSystems = 0;
        int offscreenSystemsProcessed = 0;

        string currentSystemId =
            currentSystem != null
                ? currentSystem.Id
                : string.Empty;

        WarmupQueueSummary queueSummary =
            new WarmupQueueSummary();

        LoadingSceneContext.SetProgress(
            string.Empty,
            progressFrom01);

        yield return null;

        if (currentSystem != null)
        {
            _systemNpcBehaviorService.Tick(
                currentSystem,
                quantTick,
                true,
                true);

            LoadingSceneContext.SetProgress(
                string.Empty,
                Mathf.Lerp(progressFrom01, progressTo01, 0.25f));

            yield return null;

            _systemNpcMovementService?.RunInitialWarmupRoutes(
                currentSystem,
                quantTick);

            detailedSystems++;
            detailedMovementWarmupSystems++;

            AddQueueSummary(
                queueSummary,
                currentSystem,
                quantTick,
                true);

            LoadingSceneContext.SetProgress(
                string.Empty,
                Mathf.Lerp(progressFrom01, progressTo01, 0.50f));

            yield return null;
        }

        if (offscreenSystems != null)
        {
            for (int i = 0; i < offscreenSystems.Count; i++)
            {
                StarSystemConfig offscreenSystem = offscreenSystems[i];

                if (offscreenSystem == null ||
                    string.IsNullOrWhiteSpace(offscreenSystem.Id) ||
                    offscreenSystem.Id == currentSystemId)
                {
                    continue;
                }

                _systemNpcBehaviorService.Tick(
                    offscreenSystem,
                    quantTick,
                    false,
                    true);

                yield return null;

                _offscreenSimulationService?.Tick(
                    offscreenSystem,
                    quantTick,
                    true);

                offscreenSystemsProcessed++;

                AddQueueSummary(
                    queueSummary,
                    offscreenSystem,
                    quantTick,
                    false);

                float systemsProgress =
                    offscreenSystems.Count <= 0
                        ? 1f
                        : (float)(i + 1) / offscreenSystems.Count;

                LoadingSceneContext.SetProgress(
                    string.Empty,
                    Mathf.Lerp(
                        Mathf.Lerp(progressFrom01, progressTo01, 0.50f),
                        progressTo01,
                        systemsProgress));

                yield return null;
            }
        }

        _simulationScheduleService.CompleteInitialWarmup();

        LoadingSceneContext.SetProgress(
            string.Empty,
            progressTo01);

        double totalMs = EndPerfMeasureMs(startedAt);

        LogNpcWarmupPerformance(
            totalMs,
            "[GalaxyNpcWarmupService] InitialWarmup completed. " +
            "Reason=" + reason +
            " | Tick=" + quantTick +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" +
            _simulationScheduleService.PrioritySourceSystemId +
            " | DetailedSystems=" + detailedSystems +
            " | DetailedMovementWarmupSystems=" + detailedMovementWarmupSystems +
            " | OffscreenSystems=" + offscreenSystemsProcessed +
            " | TotalMs=" + totalMs.ToString("0.00"));

        LogWarmupSummary(
            reason,
            quantTick,
            currentSystemId,
            detailedSystems,
            detailedMovementWarmupSystems,
            offscreenSystems != null ? offscreenSystems.Count : 0,
            offscreenSystemsProcessed,
            queueSummary,
            totalMs);
    }

    private void AddQueueSummary(
        WarmupQueueSummary summary,
        StarSystemConfig system,
        int quantTick,
        bool isCurrentSystem)
    {
        if (summary == null ||
            system == null ||
            string.IsNullOrWhiteSpace(system.Id) ||
            _npcRuntimeService == null)
        {
            return;
        }

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.GetAliveNpcsInSystem(system.Id);

        int npcCount =
            npcs != null
                ? npcs.Count
                : 0;

        summary.SystemsChecked++;
        summary.NpcsChecked += npcCount;

        if (isCurrentSystem)
            summary.CurrentSystemsChecked++;
        else
            summary.OffscreenSystemsChecked++;

        if (npcs == null)
            return;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null || !npc.IsAlive)
                continue;

            if (!npc.HasActiveBehavior)
                continue;

            summary.ActiveBehaviorNpcs++;

            if (!IsMovementBehavior(npc.CurrentBehavior))
                continue;

            summary.ActiveMovementNpcs++;

            if (isCurrentSystem)
            {
                summary.CurrentSystemMovementNpcs++;

                if (npc.IsWaitingForInitialRouteBuild)
                    summary.CurrentSystemMovementWaitingInitialRouteBuild++;

                if (npc.TickMovementDirectionTick != quantTick &&
                    !npc.TickMovementArrived)
                {
                    summary.CurrentSystemMovementWithoutTickDirection++;
                }

                continue;
            }

            summary.OffscreenMovementNpcs++;

            if (npc.TravelEndTick <= 0)
            {
                summary.OffscreenUninitializedMovementQueues++;
                continue;
            }

            if (quantTick >= npc.TravelEndTick)
                summary.OffscreenCompletedMovementQueuesStillPending++;
        }
    }

    private static bool IsMovementBehavior(SystemNpcBehaviorType behavior)
    {
        return behavior == SystemNpcBehaviorType.PlanetToPlanetTravel ||
               behavior == SystemNpcBehaviorType.TravelToAnotherSystem ||
               behavior == SystemNpcBehaviorType.PatrolSystem;
    }

    private void LogWarmupSummary(
        string reason,
        int quantTick,
        string currentSystemId,
        int detailedSystems,
        int detailedMovementWarmupSystems,
        int expectedOffscreenSystems,
        int processedOffscreenSystems,
        WarmupQueueSummary queueSummary,
        double totalMs)
    {
        if (queueSummary == null)
            queueSummary = new WarmupQueueSummary();

        bool allExpectedSystemsProcessed =
            detailedSystems == 1 &&
            expectedOffscreenSystems == processedOffscreenSystems;

        bool priorityRebuiltForCurrentSystem =
            _simulationScheduleService != null &&
            _simulationScheduleService.PrioritySourceSystemId == currentSystemId;

        bool initialWarmupComplete =
            _simulationScheduleService != null &&
            _simulationScheduleService.IsInitialWarmupComplete;

        bool noDueMovementQueues =
            queueSummary.OffscreenCompletedMovementQueuesStillPending == 0 &&
            queueSummary.OffscreenUninitializedMovementQueues == 0 &&
            queueSummary.CurrentSystemMovementWaitingInitialRouteBuild == 0;

        LogNpcWarmupPerformance(
            totalMs,
            "[GALAXY_NPC_WARMUP_SUMMARY]" +
            " Reason=" + reason +
            " | Tick=" + quantTick +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" +
            (_simulationScheduleService != null
                ? _simulationScheduleService.PrioritySourceSystemId
                : string.Empty) +
            " | PriorityRebuiltForCurrentSystem=" + priorityRebuiltForCurrentSystem +
            " | InitialWarmupComplete=" + initialWarmupComplete +
            " | DetailedSystems=" + detailedSystems +
            " | DetailedMovementWarmupSystems=" + detailedMovementWarmupSystems +
            " | ExpectedOffscreenSystems=" + expectedOffscreenSystems +
            " | ProcessedOffscreenSystems=" + processedOffscreenSystems +
            " | AllExpectedSystemsProcessed=" + allExpectedSystemsProcessed +
            " | SystemsCheckedAfterWarmup=" + queueSummary.SystemsChecked +
            " | CurrentSystemsCheckedAfterWarmup=" + queueSummary.CurrentSystemsChecked +
            " | OffscreenSystemsCheckedAfterWarmup=" + queueSummary.OffscreenSystemsChecked +
            " | NpcsCheckedAfterWarmup=" + queueSummary.NpcsChecked +
            " | ActiveBehaviorNpcsAfterWarmup=" + queueSummary.ActiveBehaviorNpcs +
            " | ActiveMovementNpcsAfterWarmup=" + queueSummary.ActiveMovementNpcs +
            " | CurrentSystemMovementNpcs=" + queueSummary.CurrentSystemMovementNpcs +
            " | CurrentSystemMovementWaitingInitialRouteBuild=" +
            queueSummary.CurrentSystemMovementWaitingInitialRouteBuild +
            " | CurrentSystemMovementWithoutTickDirection=" +
            queueSummary.CurrentSystemMovementWithoutTickDirection +
            " | OffscreenMovementNpcs=" + queueSummary.OffscreenMovementNpcs +
            " | OffscreenCompletedMovementQueuesStillPending=" +
            queueSummary.OffscreenCompletedMovementQueuesStillPending +
            " | OffscreenUninitializedMovementQueues=" +
            queueSummary.OffscreenUninitializedMovementQueues +
            " | CompletedMovementQueuesStillPending=" +
            queueSummary.OffscreenCompletedMovementQueuesStillPending +
            " | UninitializedMovementQueues=" +
            queueSummary.OffscreenUninitializedMovementQueues +
            " | NoDueMovementQueues=" + noDueMovementQueues +
            " | TotalMs=" + totalMs.ToString("0.00"));
    }

    private int GetCurrentQuantTick()
    {
        if (_gameTimeService == null)
            return 1;

        return Mathf.Max(
            1,
            _gameTimeService.CurrentQuantTick);
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks *
               1000.0 /
               System.Diagnostics.Stopwatch.Frequency;
    }

    private void LogNpcWarmupPerformance(
        double elapsedMs,
        string message)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            message);
    }

    private sealed class WarmupQueueSummary
    {
        public int SystemsChecked;
        public int CurrentSystemsChecked;
        public int OffscreenSystemsChecked;
        public int NpcsChecked;
        public int ActiveBehaviorNpcs;
        public int ActiveMovementNpcs;

        public int CurrentSystemMovementNpcs;
        public int CurrentSystemMovementWaitingInitialRouteBuild;
        public int CurrentSystemMovementWithoutTickDirection;

        public int OffscreenMovementNpcs;
        public int OffscreenCompletedMovementQueuesStillPending;
        public int OffscreenUninitializedMovementQueues;
    }
}