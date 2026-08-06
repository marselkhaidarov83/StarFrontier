using UnityEngine;

[CreateAssetMenu(fileName = "StationConfig", menuName = "StarFrontier/Configs/System/Station")]
public class StationConfig : BaseConfig
{
    [Header("Type")]
    [SerializeField] private StationTypeConfig stationTypeConfig;

    [Header("State")]
    [SerializeField] private bool isMainStation = true;
    [SerializeField] private bool isActive = true;
    [SerializeField] private bool isDestroyed;
    [SerializeField] private bool visibleAtStart;

    [Header("Optional Content Overrides")]
    [SerializeField] private MarketProfileConfig marketProfileOverride;
    [SerializeField] private ScriptableObject missionPoolOverride;
    [SerializeField] private ScriptableObject encounterProfileOverride;

    [Header("Optional Visual Overrides")]
    [SerializeField] private Sprite stationSpriteOverride;
    [SerializeField] private Sprite destroyedSpriteOverride;
    [SerializeField] private Sprite shadowSpriteOverride;
    [SerializeField] private bool overrideVisualSize;
    [SerializeField] private float visualSizeOverride = 180f;
    [SerializeField] private bool overrideLocalOffset;
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

    public Vector2 LocalOffset => overrideLocalOffset
        ? localOffsetOverride
        : stationTypeConfig != null
            ? stationTypeConfig.LocalOffset
            : Vector2.zero;

    public Sprite EffectiveIcon => stationTypeConfig != null
        ? stationTypeConfig.Icon
        : Icon;

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