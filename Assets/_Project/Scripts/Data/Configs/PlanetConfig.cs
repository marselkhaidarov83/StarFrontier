using UnityEngine;

[CreateAssetMenu(fileName = "PlanetConfig", menuName = "StarFrontier/Configs/Planet")]
public class PlanetConfig : BaseConfig
{
    [Header("Base Info")]
    [SerializeField] private PlanetType planetType;
    [SerializeField] private bool isInhabited;

    [Header("Relations")]
    [SerializeField] private string systemId;
    [SerializeField] private PlanetOrbitConfig planetOrbit;

    [Header("Content Links")]
    [SerializeField] private ScriptableObject missionPool;
    [SerializeField] private ScriptableObject encounterProfile;

    [Header("Market")]
    [SerializeField] public MarketProfileConfig marketProfile;

    [Header("Mission")]
    [SerializeField] private PlanetMissionConfig planetMissionConfig;

    [Header("Visuals")]
    [SerializeField] private Sprite planetSprite;
    [SerializeField] private Sprite backgroundSprite;

    public PlanetType PlanetType => planetType;
    public bool IsInhabited => isInhabited;
    public string SystemId => systemId;
    public PlanetOrbitConfig PlanetOrbit => planetOrbit;
    public MarketProfileConfig MarketProfile => marketProfile;
    public PlanetMissionConfig PlanetMissionConfig => planetMissionConfig;
    public ScriptableObject MissionPool => missionPool;
    public ScriptableObject EncounterProfile => encounterProfile;
    public Sprite PlanetSprite => planetSprite;
    public Sprite BackgroundSprite => backgroundSprite;
}