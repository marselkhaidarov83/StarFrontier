public interface IStationInfrastructureService
{
    void InitializeSession();
    float GetRangerAttackProbability(string systemId, out bool configured);
    double GetScienceRate();
    bool CanTravel(SystemNpcRuntimeState npc, string targetSystemId);
    void AdvanceTime(double seconds);
    void CompleteTick(int tick);
    void AdvanceOfflineRepair(double seconds);
    double NextRepairBoundary(double maximumSeconds);
    void RelocateStationMilitary();
}
