using UnityEngine;

public abstract class LocationServicesConfig : BaseConfig
{
    [Header("Услуги")]
    [SerializeField] private bool hasMarket;
    [SerializeField] private bool hasMissions;
    [SerializeField] private bool hasRepair;
    [SerializeField] private bool hasMedical;
    [SerializeField] private bool hasResearch;
    [Tooltip("Узкие исследования.")]
    [SerializeField] private bool hasSpecializedResearch;
    [SerializeField] private bool hasRangerBoard;
    [SerializeField] private bool hasMilitaryContracts;

    [Header("Иллюстрации услуг")]
    [SerializeField] private Sprite marketIllustration;
    [SerializeField] private Sprite missionIllustration;
    [SerializeField] private Sprite refuelIllustration;
    [SerializeField] private Sprite researchIllustration;

    public bool HasMarket => hasMarket;
    public bool HasMissions => hasMissions;
    public bool HasRepair => hasRepair;
    public bool HasMedical => hasMedical;
    public bool HasResearch => hasResearch;
    public bool HasSpecializedResearch => hasSpecializedResearch;
    public bool HasRangerBoard => hasRangerBoard;
    public bool HasMilitaryContracts => hasMilitaryContracts;
    public Sprite MarketIllustration => marketIllustration;
    public Sprite MissionIllustration => missionIllustration;
    public Sprite RefuelIllustration => refuelIllustration;
    public Sprite ResearchIllustration => researchIllustration;
}
