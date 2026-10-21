using System.Collections.Generic;

public interface IGalaxyNpcSimulationScheduleService
{
    // Cached per Unity frame so all services process the same selection.
    // quantTick is game time for diagnostics, not the scheduling cadence.
    IReadOnlyList<StarSystemConfig> GetScheduledOffscreenSystems(int quantTick);
    IReadOnlyList<StarSystemConfig> GetInitialWarmupOffscreenSystems();

    StarSystemConfig GetCurrentSystem();

    string CurrentSystemId { get; }
    string PrioritySourceSystemId { get; }

    bool IsInitialWarmupComplete { get; }

    void CompleteInitialWarmup();
}