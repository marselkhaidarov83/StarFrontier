using System.Collections.Generic;

public interface IRewardService
{
    bool TryGrantMissionReward(MissionInstanceData mission, PlayerProfileData profile);
    List<string> ExportGrantedMissionIds();
    void ImportGrantedMissionIds(List<string> ids);
    string GetLastRewardMessage();
    void Clear();
}