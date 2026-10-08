using UnityEngine;

// Конфиг StationTypeConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "StationTypeConfig", menuName = "StarFrontier/Configs/System/Station Type")]
public class StationTypeConfig : BaseConfig
{
    [Header("Type")]
    [Tooltip("Параметр stationType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StationType stationType;

    [Header("Gameplay Access")]
    [Tooltip("Переключатель hasMarket. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasMarket;
    [Tooltip("Переключатель hasMissions. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasMissions;
    [Tooltip("Переключатель hasRepair. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasRepair;
    [Tooltip("Переключатель hasMedical. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasMedical;
    [Tooltip("Переключатель hasResearch. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasResearch;
    [Tooltip("Переключатель hasRangerBoard. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasRangerBoard;
    [Tooltip("Переключатель hasMilitaryContracts. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool hasMilitaryContracts;

    [Header("Default Content Links")]
    [Tooltip("Параметр defaultMarketProfile. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private MarketProfileConfig defaultMarketProfile;
    [Tooltip("Параметр defaultMissionPool. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ScriptableObject defaultMissionPool;
    [Tooltip("Количество для параметра defaultEncounterProfile. Используется соответствующей системой при генерации или расчёте.")]
    [SerializeField] private ScriptableObject defaultEncounterProfile;

    [Header("Default Visuals")]
    [Tooltip("Спрайт для поля stationSprite. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite stationSprite;
    [Tooltip("Спрайт для поля destroyedSprite. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite destroyedSprite;
    [Tooltip("Спрайт для поля shadowSprite. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite shadowSprite;
    [Tooltip("Визуальный размер объекта на сцене.")]
    [SerializeField] private float visualSize = 180f;
    [Tooltip("Параметр localOffset. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    public StationType StationType => stationType;

    public bool HasMarket => hasMarket;
    public bool HasMissions => hasMissions;
    public bool HasRepair => hasRepair;
    public bool HasMedical => hasMedical;
    public bool HasResearch => hasResearch;
    public bool HasRangerBoard => hasRangerBoard;
    public bool HasMilitaryContracts => hasMilitaryContracts;

    public MarketProfileConfig DefaultMarketProfile => defaultMarketProfile;
    public ScriptableObject DefaultMissionPool => defaultMissionPool;
    public ScriptableObject DefaultEncounterProfile => defaultEncounterProfile;

    public Sprite StationSprite => stationSprite;
    public Sprite DestroyedSprite => destroyedSprite;
    public Sprite ShadowSprite => shadowSprite;
    public float VisualSize => visualSize;
    public Vector2 LocalOffset => localOffset;
}
