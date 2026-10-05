using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcBehaviorService : CustomService, IGalaxyNpcBehaviorService
{
    public double LastCurrentSystemMs { get; private set; }
    public double LastOffscreenMs { get; private set; }
    public double LastTotalMs { get; private set; }

    private const double GalaxyBehaviorPerfLogThresholdMs = 1.0;
    private const double SystemBehaviorPerfLogThresholdMs = 1.0;

    //Параметр, чтобы по 1 тику логика запускалась только 1 раз
    private int _prevTick = 0;

    private readonly IGalaxyNpcSimulationScheduleService _simulationScheduleService;
    private readonly ISystemNpcBehaviorService _systemNpcBehaviorService;

    public GalaxyNpcBehaviorService()
    {
        _debugStop = true;

        _simulationScheduleService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcSimulationScheduleService>();

        _systemNpcBehaviorService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();
    }

    public void Tick(int quantTick)
    {
        LastCurrentSystemMs = 0d;
        LastOffscreenMs = 0d;
        LastTotalMs = 0d;

        if (_prevTick == quantTick)
            return;

        if (!_simulationScheduleService.IsInitialWarmupComplete)
        {
            LogNpcBehaviorPerformance(
                1.0,
                "[GalaxyNpcBehaviorService] Runtime skipped. " +
                "InitialWarmup is not complete. " +
                "Tick=" + quantTick +
                " | CurrentSystemId=" +
                _simulationScheduleService.CurrentSystemId +
                " | PrioritySourceSystemId=" +
                _simulationScheduleService.PrioritySourceSystemId);

            _prevTick = quantTick;
            return;
        }

        long totalStartedAt = BeginPerfMeasure();

        StarSystemConfig currentSystem =
            _simulationScheduleService.GetCurrentSystem();

        IReadOnlyList<StarSystemConfig> offscreenSystems =
            _simulationScheduleService.GetScheduledOffscreenSystems(quantTick);

        int processedSystems = 0;
        int detailedSystems = 0;
        int offscreenProcessedSystems = 0;

        double systemsTotalMs = 0.0;
        double currentSystemMs = 0.0;
        double offscreenSystemsMs = 0.0;
        double maxSystemMs = 0.0;
        string maxSystemId = "";
        string maxSystemName = "";

        if (currentSystem != null)
        {
            currentSystemMs =
                TickSystem(
                    currentSystem,
                    quantTick,
                    true,
                    false,
                    ref maxSystemMs,
                    ref maxSystemId,
                    ref maxSystemName);

            systemsTotalMs += currentSystemMs;
            processedSystems++;
            detailedSystems++;
        }

        string currentSystemId =
            currentSystem != null
                ? currentSystem.Id
                : string.Empty;

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

                double systemMs =
                    TickSystem(
                        offscreenSystem,
                        quantTick,
                        false,
                        false,
                        ref maxSystemMs,
                        ref maxSystemId,
                        ref maxSystemName);

                systemsTotalMs += systemMs;
                offscreenSystemsMs += systemMs;
                processedSystems++;
                offscreenProcessedSystems++;
            }
        }

        double totalMs = EndPerfMeasureMs(totalStartedAt);

        LastCurrentSystemMs = currentSystemMs;
        LastOffscreenMs = offscreenSystemsMs;
        LastTotalMs = totalMs;

        LogNpcBehaviorSplitAnalytics(
            quantTick,
            totalMs,
            currentSystemMs,
            offscreenSystemsMs,
            systemsTotalMs,
            detailedSystems,
            offscreenSystems != null ? offscreenSystems.Count : 0,
            offscreenProcessedSystems,
            processedSystems,
            currentSystemId,
            maxSystemId,
            maxSystemName,
            maxSystemMs);

        LogNpcBehaviorPerformance(
            totalMs,
            "[GalaxyNpcBehaviorService] Tick | " +
            "Tick=" + quantTick +
            " | GalaxyBehaviorPhase=Runtime" +
            " | DetailedSystems=" + detailedSystems +
            " | ScheduledOffscreenSystems=" +
            (offscreenSystems != null ? offscreenSystems.Count : 0) +
            " | OffscreenProcessedSystems=" + offscreenProcessedSystems +
            " | ProcessedSystems=" + processedSystems +
            " | CurrentSystemMs=" + currentSystemMs.ToString("0.00") +
            " | OffscreenMs=" + offscreenSystemsMs.ToString("0.00") +
            " | SystemsTotalMs=" + systemsTotalMs.ToString("0.00") +
            " | TotalMs=" + totalMs.ToString("0.00") +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" +
            _simulationScheduleService.PrioritySourceSystemId +
            " | MaxSystemId=" + maxSystemId +
            " | MaxSystemName=" + maxSystemName +
            " | MaxSystemMs=" + maxSystemMs.ToString("0.00"));

        _prevTick = quantTick;
    }

    private void LogNpcBehaviorSplitAnalytics(
        int quantTick,
        double totalMs,
        double currentSystemMs,
        double offscreenSystemsMs,
        double systemsTotalMs,
        int detailedSystems,
        int scheduledOffscreenSystems,
        int offscreenProcessedSystems,
        int processedSystems,
        string currentSystemId,
        string maxSystemId,
        string maxSystemName,
        double maxSystemMs)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        if (totalMs < GalaxyBehaviorPerfLogThresholdMs)
            return;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[NPC_BEHAVIOR_SPLIT]" +
            " Tick=" + quantTick +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | CurrentSystemMs=" + currentSystemMs.ToString("F2") +
            " | OffscreenMs=" + offscreenSystemsMs.ToString("F2") +
            " | SystemsTotalMs=" + systemsTotalMs.ToString("F2") +
            " | DetailedSystems=" + detailedSystems +
            " | ScheduledOffscreenSystems=" + scheduledOffscreenSystems +
            " | OffscreenProcessedSystems=" + offscreenProcessedSystems +
            " | ProcessedSystems=" + processedSystems +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" + _simulationScheduleService.PrioritySourceSystemId +
            " | MaxSystemId=" + maxSystemId +
            " | MaxSystemName=" + maxSystemName +
            " | MaxSystemMs=" + maxSystemMs.ToString("F2"));
    }

    private double TickSystem(
        StarSystemConfig starSystem,
        int quantTick,
        bool isDetailedSystem,
        bool isInitialWarmup,
        ref double maxSystemMs,
        ref string maxSystemId,
        ref string maxSystemName)
    {
        long systemStartedAt = BeginPerfMeasure();

        _systemNpcBehaviorService.Tick(
            starSystem,
            quantTick,
            isDetailedSystem,
            isInitialWarmup);

        double systemMs = EndPerfMeasureMs(systemStartedAt);

        if (systemMs > maxSystemMs)
        {
            maxSystemMs = systemMs;
            maxSystemId = starSystem.Id;
            maxSystemName = starSystem.DisplayName;
        }

        if (systemMs >= SystemBehaviorPerfLogThresholdMs)
        {
            LogNpcBehaviorPerformance(
                systemMs,
                "[GalaxyNpcBehaviorService] SystemTick | " +
                "Tick=" + quantTick +
                " | GalaxyBehaviorPhase=" +
                (isInitialWarmup ? "InitialWarmup" : "Runtime") +
                " | SystemId=" + starSystem.Id +
                " | SystemName=" + starSystem.DisplayName +
                " | DetailedSystem=" + isDetailedSystem +
                " | Ms=" + systemMs.ToString("0.00"));
        }

        return systemMs;
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

    private void LogNpcBehaviorPerformance(double elapsedMs, string message)
    {
        if (elapsedMs < GalaxyBehaviorPerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.NpcBehavior))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcBehavior,
            message);
    }
}