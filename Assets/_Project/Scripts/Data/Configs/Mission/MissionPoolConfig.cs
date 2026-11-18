using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MissionPoolConfig", menuName = "StarFrontier/Configs/Mission/MissionPool")]
public class MissionPoolConfig : BaseConfig
{
    [SerializeField] private List<PlanetMissionOfferEntryConfig> possibleMissionOffers = new();

    public List<PlanetMissionOfferEntryConfig> PossibleMissionOffers => possibleMissionOffers;
}
