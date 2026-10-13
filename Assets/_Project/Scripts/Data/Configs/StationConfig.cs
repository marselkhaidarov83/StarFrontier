using UnityEngine;

// Конфиг StationConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "StationConfig", menuName = "StarFrontier/Configs/System/Station")]
public class StationConfig : BaseConfig
{
    [Header("Type")]
    [Tooltip("Параметр stationTypeConfig. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StationTypeConfig stationTypeConfig;

    [Header("State")]
    [Tooltip("Переключатель isMainStation. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool isMainStation = true;
    [Tooltip("Переключатель isActive. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool isActive = true;
    [Tooltip("Переключатель isDestroyed. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool isDestroyed;
    [Tooltip("Параметр visibleAtStart. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private bool visibleAtStart;

    [Header("Optional Content Overrides")]
    [Tooltip("Параметр marketProfileOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private MarketProfileConfig marketProfileOverride;
    [Tooltip("Параметр missionPoolOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ScriptableObject missionPoolOverride;
    [Tooltip("Количество для параметра encounterProfileOverride. Используется соответствующей системой при генерации или расчёте.")]
    [SerializeField] private ScriptableObject encounterProfileOverride;

    [Header("Optional Visual Overrides")]
    [Tooltip("Спрайт для поля stationSpriteOverride. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite stationSpriteOverride;
    [Tooltip("Спрайт для поля destroyedSpriteOverride. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite destroyedSpriteOverride;
    [Tooltip("Спрайт для поля shadowSpriteOverride. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite shadowSpriteOverride;
    [Tooltip("Параметр overrideVisualSize. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private bool overrideVisualSize;
    [Tooltip("Параметр visualSizeOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float visualSizeOverride = 180f;

    [Header("Map Placement")]
    [Tooltip("Параметр stationOrbit. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private PlanetOrbitConfig stationOrbit;
    [Tooltip("Параметр orbitAngleDegrees. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float orbitAngleDegrees;
    [Tooltip("Параметр overrideLocalOffset. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private bool overrideLocalOffset;
    [Tooltip("Параметр localOffsetOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector2 localOffsetOverride = Vector2.zero;

    public StationTypeConfig StationTypeConfig => stationTypeConfig;

    public StationType StationType => stationTypeConfig != null
        ? stationTypeConfig.StationType
        : default;

    public bool IsMainStation => isMainStation;
    public bool IsActive => isActive;
    public bool IsDestroyed => isDestroyed;
    public bool VisibleAtStart => visibleAtStart;

    public bool HasMarket => stationTypeConfig != null && stationTypeConfig.HasMarket;
    public bool HasMissions => stationTypeConfig != null && stationTypeConfig.HasMissions;
    public bool HasRepair => stationTypeConfig != null && stationTypeConfig.HasRepair;
    public bool HasMedical => stationTypeConfig != null && stationTypeConfig.HasMedical;
    public bool HasResearch => stationTypeConfig != null && stationTypeConfig.HasResearch;
    public bool HasRangerBoard => stationTypeConfig != null && stationTypeConfig.HasRangerBoard;
    public bool HasMilitaryContracts => stationTypeConfig != null && stationTypeConfig.HasMilitaryContracts;

    public MarketProfileConfig MarketProfile => marketProfileOverride != null
        ? marketProfileOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DefaultMarketProfile
            : null;

    public ScriptableObject MissionPool => missionPoolOverride != null
        ? missionPoolOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DefaultMissionPool
            : null;

    public ScriptableObject EncounterProfile => encounterProfileOverride != null
        ? encounterProfileOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DefaultEncounterProfile
            : null;

    public Sprite StationSprite => stationSpriteOverride != null
        ? stationSpriteOverride
        : stationTypeConfig != null
            ? stationTypeConfig.StationSprite
            : null;

    public Sprite DestroyedSprite => destroyedSpriteOverride != null
        ? destroyedSpriteOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DestroyedSprite
            : null;

    public Sprite ShadowSprite => shadowSpriteOverride != null
        ? shadowSpriteOverride
        : stationTypeConfig != null
            ? stationTypeConfig.ShadowSprite
            : null;

    public float VisualSize => overrideVisualSize
        ? visualSizeOverride
        : stationTypeConfig != null
            ? stationTypeConfig.VisualSize
            : 180f;

    public PlanetOrbitConfig StationOrbit => stationOrbit;
    public float OrbitAngleDegrees => orbitAngleDegrees;

    public Vector2 LocalOffset => stationOrbit != null
        ? CalculateOrbitLocalOffset(stationOrbit, orbitAngleDegrees)
        : overrideLocalOffset
        ? localOffsetOverride
        : stationTypeConfig != null
            ? stationTypeConfig.LocalOffset
            : Vector2.zero;

    public Sprite EffectiveIcon => stationTypeConfig != null
        ? stationTypeConfig.Icon
        : Icon;

    private static Vector2 CalculateOrbitLocalOffset(PlanetOrbitConfig orbitConfig, float angleDegrees)
    {
        if (orbitConfig == null)
        {
            return Vector2.zero;
        }

        float radians = angleDegrees * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, orbitConfig.OrbitRadius);

        return new Vector2(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stationTypeConfig == null)
        {
            Debug.LogWarning($"StationConfig '{name}' has no StationTypeConfig assigned.", this);
        }
    }
#endif
}
