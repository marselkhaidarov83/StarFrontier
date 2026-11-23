using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

// Конфиг описывает звёздную систему: звезду, планеты, маршруты, станцию и население.
[CreateAssetMenu(fileName = "StarSystemConfig", menuName = "StarFrontier/Configs/System/Star System")]
public class StarSystemConfig : BaseConfig
{
    [Header("World Structure")]
    [Tooltip("Конфиг звезды в системе.")]
    [SerializeField] private SunConfig sun;
    [Tooltip("Список планет системы.")]
    [SerializeField] private PlanetConfig[] planetRefs;
    [Tooltip("Станция системы.")]
    [SerializeField] private StationConfig station;

    [Header("Map")]
    [Tooltip("Позиция объекта на карте галактики или сектора.")]
    [SerializeField] private Vector2 mapPosition;

    [Header("Npc")]
    [Tooltip("Правила союзной и вражеской популяции. Группы вторжения назначаются отдельным набором в профиле.")]
    [SerializeField] private PopulationConfig systemPopulationRule;

    [Header("Спавн врагов")]
    [FormerlySerializedAs("npcSpawnPoints")]
    [Tooltip("Конфиг базовых точек спавна врагов и радиуса разброса в этой системе.")]
    [SerializeField] private SystemEnemySpawnPointConfig enemySpawnPoints;

    [Header("AtStart")]
    [Tooltip("Отмечает стартовую систему игрока.")]
    [SerializeField] private bool isStartSystem;

    [Header("Routes")]
    [Tooltip("Маршруты системы. Каждый маршрут прикреплён к обеим своим системам; открытость хранится в RouteRuntimeState.")]
    [SerializeField] private System.Collections.Generic.List<RouteConfig> routes = new();

    public SunConfig Sun => sun;
    public PlanetConfig[] PlanetRefs => planetRefs;
    public PlanetConfig[] PlanetInhabited() { return planetRefs.Where(p => p.IsInhabited).ToArray(); }

    public Vector3 MapPosition => mapPosition;
    public PopulationConfig Population => systemPopulationRule;
    public SystemEnemySpawnPointConfig EnemySpawnPoints => enemySpawnPoints;
    public bool IsStartSystem => isStartSystem;
    public System.Collections.Generic.List<RouteConfig> Routes => routes;
    public StationConfig Station => station;
    public InvasionSpawnRuleSetConfig InvasionSpawnRuleSet =>
        systemPopulationRule != null ? systemPopulationRule.InvasionSpawnRuleSet : null;
}
