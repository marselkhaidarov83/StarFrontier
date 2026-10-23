using System.Collections.Generic;

public interface IMissionService
{
    IReadOnlyList<MissionInstanceData> GetAvailableMissions();
    IReadOnlyList<MissionInstanceData> GetActiveMissions();
    IReadOnlyList<MissionInstanceData> GetCompletedMissions();
    IReadOnlyList<MissionInstanceData> GetArchivedMissions();
    void SetAvailableMissions(List<MissionInstanceData> missions);
    bool AcceptMission(string missionRuntimeId);
    bool CompleteMission(string missionRuntimeId);
    bool FailMission(string missionRuntimeId);
    bool CancelMission(string missionRuntimeId);
    bool ExpireMission(string missionRuntimeId);
    void ReevaluateActiveMissions(string reason);
    void ObserveReconTargets();
    MissionInstanceData GetMissionById(string missionRuntimeId);
    void ClearAll();
}
