using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcMovementService : CustomService, IGalaxyNpcMovementService
{
    public double LastCurrentSystemMs { get; private set; }
    public double LastOffscreenMs { get; private set; }
    public double LastTotalMs { get; private set; }

    private const double PerfLogThresholdMs = 4.0;
    private int _lastOffscreenMovementQuantTick = int.MinValue;

    private readonly IGalaxyNpcSimulationScheduleService _simulationScheduleService;
    private readonly ISystemNpcMovementService _systemNpcMovementService;
    private readonly ISystemNpcOffscreenSimulationService _offscreenSimulationService;

    public GalaxyNpcMovementService()
    {
        _debugStop = true;

        _simulationScheduleService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyNpcSimulationScheduleService>();

        _systemNpcMovementService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementService>();

        _offscreenSimulationService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcOffscreenSimulationService>();
    }

    public void Tick(float deltaTime, int quantTick)
    {
        long totalStartedAt = BeginPerfMeasure();

        LastCurrentSystemMs = 0d;
        LastOffscreenMs = 0d;
        LastTotalMs = 0d;

        double detailedMs = 0.0;
        double offscreenMs = 0.0;
        int detailedSystems = 0;
        int offscreenSystems = 0;
        int scheduledOffscreenSystemsCount = 0;
        bool offscreenSkippedSameTick = false;

        StarSystemConfig currentSystem =
            _simulationScheduleService.GetCurrentSystem();

        if (currentSystem != null)
        {
            long detailedStartedAt = BeginPerfMeasure();

            _systemNpcMovementService.Tick(
                currentSystem,
                deltaTime,
                quantTick);

            detailedMs = EndPerfMeasureMs(detailedStartedAt);
            detailedSystems = 1;
        }

        string currentSystemId =
            currentSystem != null
                ? currentSystem.Id
                : string.Empty;

        if (_lastOffscreenMovementQuantTick == quantTick)
        {
            offscreenSkippedSameTick = true;
        }
        else
        {
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

                    long offscreenStartedAt = BeginPerfMeasure();

                    _offscreenSimulationService.Tick(
                        offscreenSystem,
                        quantTick,
                        false);

                    offscreenMs += EndPerfMeasureMs(offscreenStartedAt);
                    offscreenSystems++;
                }
            }

            _lastOffscreenMovementQuantTick = quantTick;
        }

        double totalMs = EndPerfMeasureMs(totalStartedAt);

        LastCurrentSystemMs = detailedMs;
        LastOffscreenMs = offscreenMs;
        LastTotalMs = totalMs;

        LogNpcMovementSplitAnalytics(
            quantTick,
            deltaTime,
            totalMs,
            detailedMs,
            offscreenMs,
            detailedSystems,
            scheduledOffscreenSystemsCount,
            offscreenSystems,
            offscreenSkippedSameTick,
            currentSystemId);

        LogNpcMovementPerf(
            totalMs,
            "[GalaxyNpcMovementService] Tick" +
            " | DetailedSystems=" + detailedSystems +
            " | ScheduledOffscreenSystems=" + scheduledOffscreenSystemsCount +
            " | OffscreenSystems=" + offscreenSystems +
            " | OffscreenSkippedSameTick=" + offscreenSkippedSameTick +
            " | DetailedMs=" + detailedMs.ToString("F2") +
            " | OffscreenMs=" + offscreenMs.ToString("F2") +
            " | DeltaTime=" + deltaTime.ToString("F4") +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" +
            _simulationScheduleService.PrioritySourceSystemId);
    }

    private void LogNpcMovementSplitAnalytics(
        int quantTick,
        float deltaTime,
        double totalMs,
        double detailedMs,
        double offscreenMs,
        int detailedSystems,
        int scheduledOffscreenSystemsCount,
        int offscreenSystems,
        bool offscreenSkippedSameTick,
        string currentSystemId)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        float frameMs =
            deltaTime > 0f
                ? deltaTime * 1000f
                : 0f;

        double otherFrameMs =
            Math.Max(
                0d,
                frameMs - detailedMs - offscreenMs);

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[NPC_MOVEMENT_SPLIT]" +
            " UnityFrame=" + Time.frameCount +
            " | Tick=" + quantTick +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | CurrentSystemMs=" + detailedMs.ToString("F2") +
            " | OffscreenMs=" + offscreenMs.ToString("F2") +
            " | OtherFrameMs=" + otherFrameMs.ToString("F2") +
            " | GalaxyNpcMovementMs=" + totalMs.ToString("F2") +
            " | DetailedSystems=" + detailedSystems +
            " | ScheduledOffscreenSystems=" + scheduledOffscreenSystemsCount +
            " | OffscreenSystems=" + offscreenSystems +
            " | OffscreenSkippedSameTick=" + offscreenSkippedSameTick +
            " | CurrentSystemId=" + currentSystemId +
            " | PrioritySourceSystemId=" + _simulationScheduleService.PrioritySourceSystemId);
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

    private void LogNpcMovementPerf(double elapsedMs, string message)
    {
        if (elapsedMs < PerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.NpcMovement))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovement,
            message +
            " | Ms=" +
            elapsedMs.ToString("F2"));
    }
}