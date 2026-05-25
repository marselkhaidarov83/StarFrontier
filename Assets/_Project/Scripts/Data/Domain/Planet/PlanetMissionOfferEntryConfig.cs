using UnityEngine;

[CreateAssetMenu(fileName = "PlanetMissionOfferEntryConfig", menuName = "StarFrontier/Configs/PlanetMissionOfferEntry")]
public class PlanetMissionOfferEntryConfig : BaseConfig
{
    [SerializeField] private MissionTemplateConfig missionTemplateConfig;
        // public string MissionTemplateId;
    [SerializeField] private int weight = 1;
    [SerializeField] private bool isEnabled = true;

    public MissionTemplateConfig MissionTemplateConfig => missionTemplateConfig;
    public int Weight => weight;
    public bool IsEnabled => isEnabled;
}