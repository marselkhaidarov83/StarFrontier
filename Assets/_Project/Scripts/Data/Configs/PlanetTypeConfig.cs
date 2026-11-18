using UnityEngine;

[CreateAssetMenu(fileName = "PlanetTypeConfig", menuName = "StarFrontier/Configs/System/Planet Type")]
public sealed class PlanetTypeConfig : LocationServicesConfig
{
    [SerializeField] private PlanetType planetType;
    [SerializeField] private bool isInhabited;
    [SerializeField] private MarketProfileConfig marketProfile;
    [SerializeField] private MissionPoolConfig planetMissionConfig;

    public PlanetType PlanetType => planetType;
    public bool IsInhabited => isInhabited;
    public MarketProfileConfig MarketProfile => marketProfile;
    public MissionPoolConfig MissionPool => planetMissionConfig;
}
