using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlanetMissionConfig", menuName = "StarFrontier/Configs/PlanetMission")]
public class PlanetMissionConfig : BaseConfig
{
    // [SerializeField] private string planetId;
    [SerializeField] private List<PlanetMissionOfferEntryConfig> possibleMissionOffers = new();

    // public string PlanetId => planetId;
    public List<PlanetMissionOfferEntryConfig> PossibleMissionOffers => possibleMissionOffers;
}