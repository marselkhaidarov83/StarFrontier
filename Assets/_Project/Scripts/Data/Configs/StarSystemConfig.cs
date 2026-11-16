using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

// Конфиг описывает звёздную систему: звезду, планеты, соседние системы, станцию и население.
[CreateAssetMenu(fileName = "StarSystemConfig", menuName = "StarFrontier/Configs/System/Star System")]
public class StarSystemConfig : BaseConfig
{
    [Header("World Structure")]
    [Tooltip("Конфиг звезды в системе.")]
    [SerializeField] private SunConfig sun;
    [Tooltip("Список планет системы.")]
    [SerializeField] private PlanetConfig[] planetRefs;
    [Tooltip("Связи с соседними системами.")]
    [SerializeField] private StarSystemLink[] linkedSystems;
    [Tooltip("Станция системы.")]
    [SerializeField] private StationConfig station;

    [Header("Map")]
    [Tooltip("Позиция объекта на карте галактики или сектора.")]
    [SerializeField] private Vector2 mapPosition;

    [Header("Npc")]
    [Tooltip("Правила союзной популяции. Группы вторжения назначаются отдельным набором в профиле.")]
    [SerializeField] private SystemPopulationRule systemPopulationRule;

    [Header("Спавн врагов")]
    [FormerlySerializedAs("npcSpawnPoints")]
    [Tooltip("Конфиг базовых точек спавна врагов и радиуса разброса в этой системе.")]
    [SerializeField] private SystemEnemySpawnPointConfig enemySpawnPoints;
    [Tooltip("Набор правил спавна врагов для вражеской системы. Выбор конкретного правила зависит от фракции-владельца системы.")]
    [SerializeField] private EnemySpawnRuleSetConfig enemySpawnRuleSet;

    [Header("AtStart")]
    [Tooltip("Отмечает стартовую систему игрока.")]
    [SerializeField] private bool isStartSystem;

    [Header("Routes")]
    [Tooltip("Список routes. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private System.Collections.Generic.List<RouteConfig> routes = new();

    public SunConfig Sun => sun;
    public PlanetConfig[] PlanetRefs => planetRefs;
    public PlanetConfig[] PlanetInhabited() { return planetRefs.Where(p => p.IsInhabited).ToArray(); }
    public StarSystemLink[] LinkedSystems => linkedSystems;

    public Vector3 MapPosition => mapPosition;
    public SystemPopulationRule SystemPopulationRule => systemPopulationRule;
    public SystemEnemySpawnPointConfig EnemySpawnPoints => enemySpawnPoints;
    public bool IsStartSystem => isStartSystem;
    public System.Collections.Generic.List<RouteConfig> Routes => routes;
    public StationConfig Station => station;
    public EnemySpawnRuleSetConfig EnemySpawnRuleSet => enemySpawnRuleSet;
    public InvasionSpawnRuleSetConfig InvasionSpawnRuleSet =>
        systemPopulationRule != null ? systemPopulationRule.InvasionSpawnRuleSet : null;
}
