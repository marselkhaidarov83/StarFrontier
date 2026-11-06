public interface IResearchService
{
    ResearchRuntimeState GetSnapshot();
    bool TryEnterStation(string stationSystemId, out string reason);
    void LeaveStation();
    ResearchTreeConfig Tree { get; }
    bool TryStart(string stationSystemId, ResearchUpgrade upgrade, string optionId, long expectedRevision, out string reason);
    void AdvanceTime(double seconds);
    void InitializeSession();
}
