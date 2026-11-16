using UnityEngine;

// Конфиг описывает планету и связывает её с общим типом и орбитой.
[CreateAssetMenu(fileName = "PlanetConfig", menuName = "StarFrontier/Configs/System/Planet")]
public class PlanetConfig : BaseConfig
{
    [Header("Base Info")]
    [Tooltip("Общий конфиг типа, населённости, рынка, миссий и графики планеты.")]
    [SerializeField] private PlanetTypeConfig planetTypeConfig;

    [Header("Relations")]
    [Tooltip("Орбита планеты в системе.")]
    [SerializeField] private PlanetOrbitConfig planetOrbit;

    [Header("Visuals")]
    [Tooltip("Визуальный размер объекта на сцене.")]
    [SerializeField][Min(0f)] private float visualSize = 96f;

    public PlanetTypeConfig PlanetTypeConfig => planetTypeConfig;
    public PlanetType PlanetType => planetTypeConfig != null ? planetTypeConfig.PlanetType : default;
    public bool IsInhabited => planetTypeConfig != null && planetTypeConfig.IsInhabited;
    public PlanetOrbitConfig PlanetOrbit => planetOrbit;
    public MarketProfileConfig MarketProfile => planetTypeConfig != null ? planetTypeConfig.MarketProfile : null;
    public PlanetMissionConfig PlanetMissionConfig => planetTypeConfig != null ? planetTypeConfig.PlanetMissionConfig : null;
    public override Sprite Icon => planetTypeConfig != null ? planetTypeConfig.Icon : base.Icon;
    public float VisualSize => visualSize;
}
