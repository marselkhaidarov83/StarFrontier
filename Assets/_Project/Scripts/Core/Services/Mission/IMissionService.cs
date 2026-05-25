using System.Collections.Generic;

    public interface IMissionService
    {
        IReadOnlyList<MissionInstanceData> GetAvailableMissions();
        IReadOnlyList<MissionInstanceData> GetActiveMissions();
        IReadOnlyList<MissionInstanceData> GetCompletedMissions();

        void SetAvailableMissions(List<MissionInstanceData> missions);

        bool AcceptMission(string missionRuntimeId);
        bool CompleteMission(string missionRuntimeId);
        bool FailMission(string missionRuntimeId);

        MissionInstanceData GetMissionById(string missionRuntimeId);
        void ClearAll();

        // void RestoreState(
        //     List<MissionInstanceData> availableMissions,
        //     List<MissionInstanceData> activeMissions,
        //     List<MissionInstanceData> completedMissions);        
    }