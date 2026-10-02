using System.Collections.Generic;

public interface IGalaxyNpcSimulationScheduleService
{
    IReadOnlyList<StarSystemConfig> GetScheduledOffscreenSystems(int quantTick);
    IReadOnlyList<StarSystemConfig> GetInitialWarmupOffscreenSystems();

    StarSystemConfig GetCurrentSystem();

    string CurrentSystemId { get; }
    string PrioritySourceSystemId { get; }

    bool IsInitialWarmupComplete { get; }

    void CompleteInitialWarmup();
}