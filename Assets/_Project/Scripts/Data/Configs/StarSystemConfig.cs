using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "StarSystemConfig", menuName = "StarFrontier/Configs/Star System")]
public class StarSystemConfig : BaseConfig
{
    [Header("System Data")]
    [SerializeField] private SystemEconomyType economyType;
    [SerializeField][Range(1, 5)] private int dangerLevel = 1;

    [Header("World Structure")]
    [SerializeField] private SunConfig sun;
    [SerializeField] private PlanetConfig[] planetRefs;
    [SerializeField] private StarSystemLink[] linkedSystems;

    [Header("Mission Data")]
    [SerializeField] private MissionTag[] missionTags;

    [Header("Map")]
    [SerializeField] private Vector3 mapPosition;

    [Header("Npc")]
    [SerializeField] private SystemPopulationConfig systemPopulation;

    public string SectorId;
    public bool DiscoveredAtStart;
    public bool UnlockedAtStart;
    public bool CapturedAtStart;

    public int StartThreatLevel;

    public SystemEconomyType EconomyType => economyType;
    public int DangerLevel => dangerLevel;
    public SunConfig Sun => sun;
    public PlanetConfig[] PlanetRefs => planetRefs;
    public PlanetConfig[] PlanetInhabited() { return planetRefs.Where(p => p.IsInhabited).ToArray(); }
    public StarSystemLink[] LinkedSystems => linkedSystems;

    public MissionTag[] MissionTags => missionTags;
    public Vector3 MapPosition => mapPosition;
    public SystemPopulationConfig SystemPopulation => systemPopulation;
}