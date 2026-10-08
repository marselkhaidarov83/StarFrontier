using UnityEngine;

// Конфиг описывает планету: тип, орбиту, рынок, миссии, спрайты и связанный контент.
[CreateAssetMenu(fileName = "PlanetConfig", menuName = "StarFrontier/Configs/System/Planet")]
public class PlanetConfig : BaseConfig
{
    [Header("Base Info")]
    [Tooltip("Тип планеты. Используется для логики и отображения планеты.")]
    [SerializeField] private PlanetType planetType;
    [Tooltip("Признак населённости планеты.")]
    [SerializeField] private bool isInhabited;

    [Header("Relations")]
    [Tooltip("Орбита планеты в системе.")]
    [SerializeField] private PlanetOrbitConfig planetOrbit;

    [Header("Content Links")]
    [Tooltip("Набор миссий планеты.")]
    [SerializeField] private ScriptableObject missionPool;
    [Tooltip("Профиль столкновений, связанный с планетой или станцией.")]
    [SerializeField] private ScriptableObject encounterProfile;

    [Header("Market")]
    [Tooltip("Профиль рынка. Используется экраном рынка и расчётом цен.")]
    [SerializeField] public MarketProfileConfig marketProfile;

    [Header("Mission")]
    [Tooltip("Набор планетарных миссий.")]
    [SerializeField] private PlanetMissionConfig planetMissionConfig;

    [Header("Visuals")]
    [Tooltip("Спрайт планеты.")]
    [SerializeField] private Sprite planetSprite;
    [Tooltip("Фоновый спрайт планеты или связанного вида.")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("Визуальный размер объекта на сцене.")]
    [SerializeField][Min(0f)] private float visualSize = 96f;

    public PlanetType PlanetType => planetType;
    public bool IsInhabited => isInhabited;
    public PlanetOrbitConfig PlanetOrbit => planetOrbit;
    public MarketProfileConfig MarketProfile => marketProfile;
    public PlanetMissionConfig PlanetMissionConfig => planetMissionConfig;
    public ScriptableObject MissionPool => missionPool;
    public ScriptableObject EncounterProfile => encounterProfile;
    public Sprite PlanetSprite => planetSprite;
    public Sprite BackgroundSprite => backgroundSprite;
    public float VisualSize => visualSize;
}
