using UnityEngine;

[CreateAssetMenu(fileName = "StationTypeConfig", menuName = "StarFrontier/Configs/System/Station Type")]
public class StationTypeConfig : BaseConfig
{
    [Header("Type")]
    [SerializeField] private StationType stationType;

    [Header("Gameplay Access")]
    [SerializeField] private bool hasMarket;
    [SerializeField] private bool hasMissions;
    [SerializeField] private bool hasRepair;
    [SerializeField] private bool hasMedical;
    [SerializeField] private bool hasResearch;
    [SerializeField] private bool hasRangerBoard;
    [SerializeField] private bool hasMilitaryContracts;

    [Header("Default Content Links")]
    [SerializeField] private MarketProfileConfig defaultMarketProfile;
    [SerializeField] private ScriptableObject defaultMissionPool;
    [SerializeField] private ScriptableObject defaultEncounterProfile;

    [Header("Default Visuals")]
    [SerializeField] private Sprite stationSprite;
    [SerializeField] private Sprite destroyedSprite;
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private float visualSize = 180f;
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