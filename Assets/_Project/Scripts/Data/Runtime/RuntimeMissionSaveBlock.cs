using System;
using System.Collections.Generic;

[Serializable]
public class RuntimeMissionSaveBlock
{
    public Dictionary<string, PlanetOfferedMissionData> OffersByPlanet = new();
    public List<PlanetOfferedMissionData> OffersByPlanet_List = new();

    public List<MissionInstanceData> AvailableMissions = new();
    public List<MissionInstanceData> ActiveMissions = new();
    public List<MissionInstanceData> CompletedMissions = new();

    public List<string> RewardGrantedMissionIds = new();
}