using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "StarSystemConfig", menuName = "StarFrontier/Configs/System/Star System")]
public class StarSystemConfig : BaseConfig
{
    [Header("System Data")]
    [SerializeField] private SystemEconomyType economyType;
    [SerializeField] [Range(1, 5)] private int dangerLevel = 1;

    [Header("World Structure")]
    [SerializeField] private SunConfig sun;
    [SerializeField] private PlanetConfig[] planetRefs;
    [SerializeField] private StarSystemLink[] linkedSystems;
    [SerializeField] private StationConfig station;

    [Header("Mission Data")]
    [SerializeField] private MissionTag[] missionTags;

    [Header("Map")]
    [SerializeField] private Vector2 mapPosition;

    [Header("Npc")]
    [SerializeField] private SystemPopulationRule systemPopulationRule;
    [SerializeField] private SystemNpcSpawnPointConfig npcSpawnPoints;

    [Header("AtStart")]
    [SerializeField] private bool isStartSystem;
    [SerializeField] private bool isHiddenAtStart;
    [SerializeField] private int recommendedPower;

    [Header("Routes")]
    [SerializeField] private System.Collections.Generic.List<RouteConfig> routes = new();

    public SystemEconomyType EconomyType => economyType;
    public int DangerLevel => dangerLevel;
    public SunConfig Sun => sun;
    public PlanetConfig[] PlanetRefs => planetRefs;
    public PlanetConfig[] PlanetInhabited() { return planetRefs.Where(p => p.IsInhabited).ToArray();}
    public StarSystemLink[] LinkedSystems => linkedSystems;

    public MissionTag[] MissionTags => missionTags;
    public Vector3 MapPosition => mapPosition;
    public SystemPopulationRule SystemPopulationRule => systemPopulationRule;
    public SystemNpcSpawnPointConfig NpcSpawnPoints => npcSpawnPoints;
    public bool IsStartSystem => isStartSystem;
    public bool IsHiddenAtStart => isHiddenAtStart;
    public int RecommendedPower => recommendedPower;
    public System.Collections.Generic.List<RouteConfig> Routes => routes;
    public StationConfig Station => station;
}
