using System.Linq;
using UnityEngine;

// Конфиг описывает звёздную систему: экономику, опасность, звезду, планеты, соседние системы, станцию и население.
[CreateAssetMenu(fileName = "StarSystemConfig", menuName = "StarFrontier/Configs/System/Star System")]
public class StarSystemConfig : BaseConfig
{
    [Header("System Data")]
    [Tooltip("Тип экономики системы.")]
    [SerializeField] private SystemEconomyType economyType;
    [Tooltip("Параметр dangerLevel. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] [Range(1, 5)] private int dangerLevel = 1;

    [Header("World Structure")]
    [Tooltip("Конфиг звезды в системе.")]
    [SerializeField] private SunConfig sun;
    [Tooltip("Список планет системы.")]
    [SerializeField] private PlanetConfig[] planetRefs;
    [Tooltip("Связи с соседними системами.")]
    [SerializeField] private StarSystemLink[] linkedSystems;
    [Tooltip("Станция системы.")]
    [SerializeField] private StationConfig station;

    [Header("Mission Data")]
    [Tooltip("Метки миссий, доступных в системе.")]
    [SerializeField] private MissionTag[] missionTags;

    [Header("Map")]
    [Tooltip("Позиция объекта на карте галактики или сектора.")]
    [SerializeField] private Vector2 mapPosition;

    [Header("Npc")]
    [Tooltip("Правило населения системы союзниками и врагами.")]
    [SerializeField] private SystemPopulationRule systemPopulationRule;
    [Tooltip("Точки появления неигровых кораблей в системе.")]
    [SerializeField] private SystemNpcSpawnPointConfig npcSpawnPoints;

    [Header("AtStart")]
    [Tooltip("Отмечает стартовую систему игрока.")]
    [SerializeField] private bool isStartSystem;
    [Tooltip("Скрывает систему в начале игры.")]
    [SerializeField] private bool isHiddenAtStart;
    [Tooltip("Рекомендуемая сила игрока для системы.")]
    [SerializeField] private int recommendedPower;

    [Header("Routes")]
    [Tooltip("Список routes. Используется связанными игровыми системами этого конфига.")]
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
