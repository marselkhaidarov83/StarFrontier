using UnityEngine;

public enum MissionCreditRewardMode { Range = 0, Fixed = 1, FuelDistance = 2 }

public abstract class MissionTemplateConfig : BaseConfig
{
    [Header("Mission Core")]
    [SerializeField] private string titlePattern;
    [SerializeField] [TextArea] private string descriptionPattern;
    [SerializeField] private MissionOutcomeTexts outcomeTexts;

    [Header("Lifecycle")]
    [Tooltip("Срок в игровых днях после принятия. 0 — без срока. Миссия истекает при достижении дня принятия + срока.")]
    [SerializeField] [Min(0)] private int durationDays;
    public int DurationDays => durationDays;

    [Header("Rewards")]
    [SerializeField] private MissionCreditRewardMode creditRewardMode;
    public MissionCreditRewardMode CreditRewardMode => creditRewardMode;
    [SerializeField] private int creditRewardMin;
    [SerializeField] private int creditRewardMax;
    [SerializeField] private int xpRewardMin;
    [SerializeField] private int xpRewardMax;
    [SerializeField] private int rewardCredits;

    [Header("Target Rules")]
    [SerializeField] private PlanetConfig sourcePlanet;

    public abstract MissionType MissionType { get; }
    public string TitlePattern => titlePattern;
    public string DescriptionPattern => descriptionPattern;
    public MissionOutcomeTexts OutcomeTexts => outcomeTexts;
    public int CreditRewardMin => creditRewardMin;
    public int CreditRewardMax => creditRewardMax;
    public int XpRewardMin => xpRewardMin;
    public int XpRewardMax => xpRewardMax;
    public int RewardCredits => rewardCredits;
    public PlanetConfig SourcePlanet => sourcePlanet;
}
