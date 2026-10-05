using System.Collections.Generic;

public sealed class GalaxyNpcCombatService : CustomService, IGalaxyNpcCombatService
{
    private const double PerfLogThresholdMs = 1.0;

    private int _prevTick = int.MinValue;

    private readonly IGalaxyNpcSimulationScheduleService _simulationScheduleService;
    private readonly ISystemNpcCombatService _systemNpcCombatService;

    public double LastCurrentSystemMs { get; private set; }
    public double LastOffscreenMs { get; private set; }
    public double LastTotalMs { get; private set; }

    public GalaxyNpcCombatService()
    {
        _debugStop = true;

        _simulationScheduleService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcSimulationScheduleService>();

        _systemNpcCombatService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcCombatService>();
    }

    public void TickQuant(int quantTick)
    {
        TickCombatSystems(quantTick);
    }

    public void Tick(float deltaTime, int quantTick)
    {
        _systemNpcCombatService.TickProjectiles(deltaTime);

        TickCombatSystems(quantTick);
    }

    private void TickCombatSystems(int quantTick)
    {
        LastCurrentSystemMs = 0d;
        LastOffscreenMs = 0d;
        LastTotalMs = 0d;

        if (_prevTick == quantTick)
            return;

        long totalStartedAt =
            BeginPerfMeasure();

        double currentSystemMs = 0d;
        double offscreenMs = 0d;

        int currentSystems = 0;
        int scheduledOffscreenSystemsCount = 0;
        int offscreenSystems = 0;

        StarSystemConfig currentSystem =
            _simulationScheduleService.GetCurrentSystem();

        if (currentSystem != null)
        {
            long currentStartedAt =
                BeginPerfMeasure();

            _systemNpcCombatService.Tick(
                currentSystem,
                quantTick);

            currentSystemMs =
                EndPerfMeasureMs(currentStartedAt);

            currentSystems = 1;
        }

        string currentSystemId =
            currentSystem != null
                ? currentSystem.Id
                : string.Empty;

        IReadOnlyList<StarSystemConfig> offscreenSystemsToProcess =
            _simulationScheduleService.GetScheduledOffscreenSystems(quantTick);

        scheduledOffscreenSystemsCount =
            offscreenSystemsToProcess != null
                ? offscreenSystemsToProcess.Count
                : 0;

        if (offscreenSystemsToProcess != null)
        {
            for (int i = 0; i < offscreenSystemsToProcess.Count; i++)
            {
                StarSystemConfig offscreenSystem =
                    offscreenSystemsToProcess[i];

                if (offscreenSystem == null ||
                    string.IsNullOrWhiteSpace(offscreenSystem.Id) ||
                    offscreenSystem.Id == currentSystemId)
                {
                    continue;
                }

                long offscreenStartedAt =
                    BeginPerfMeasure();

                _systemNpcCombatService.Tick(
                    offscreenSystem,
                    quantTick);

                offscreenMs +=
                    EndPerfMeasureMs(offscreenStartedAt);

                offscreenSystems++;
            }
        }

        double totalMs =
            EndPerfMeasureMs(totalStartedAt);

        LastCurrentSystemMs = currentSystemMs;
        LastOffscreenMs = offscreenMs;
        LastTotalMs = totalMs;

        _prevTick = quantTick;

        LogNpcCombatSplitAnalytics(
            quantTick,
            totalMs,
            currentSystemMs,
            offscreenMs,
            currentSystems,
            scheduledOffscreenSystemsCount,
            offscreenSystems,
            currentSystemId);
    }

    private void LogNpcCombatSplitAnalytics(
        int quantTick,
        double totalMs,
        double currentSystemMs,
        double offscreenMs,
        int currentSystems,
        int scheduledOffscreenSystemsCount,
        int offscreenSystems,
        string currentSystemId)
    {
        if (totalMs < PerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[NPC_COMBAT_SPLIT]" +
            " Tick=" + quantTick +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | CurrentSystemMs=" + currentSystemMs.ToString("F2") +
            " | OffscreenMs=" + offscreenMs.ToString("F2") +
            " | CurrentSystems=" + currentSystems +
            " | ScheduledOffscreenSystems=" + scheduledOffscreenSystemsCount +
            " | OffscreenSystems=" + offscreenSystems +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" +
            _simulationScheduleService.PrioritySourceSystemId);
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
}