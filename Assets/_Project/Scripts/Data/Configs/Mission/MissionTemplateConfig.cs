using System;
using UnityEngine;

public enum MissionCreditRewardMode { Range = 0, Fixed = 1, FuelDistance = 2 }

[CreateAssetMenu(fileName = "MissionTemplateConfig", menuName = "StarFrontier/Configs/Mission/Mission Template")]
public class MissionTemplateConfig : BaseConfig
{
    [Header("Mission Core")]
    [SerializeField] [HideInInspector] private MissionType missionType;
    [SerializeField] private string titlePattern;
    [SerializeField] [TextArea] private string descriptionPattern;
    [SerializeField] private MissionDifficultyBand difficultyBand;

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
    [SerializeField] private ItemConfig[] rewardItems;

    [Header("Generation Constraints")]
    [SerializeField] [Range(1, 5)] private int minDangerLevel;
    [SerializeField] [Range(1, 5)] private int maxDangerLevel;
    [SerializeField] private MissionTag[] requiredMissionTags;
    [SerializeField] private int minCargoCapacity;

    [Header("Target Rules")]
    [SerializeField] private PlanetConfig sourcePlanet;
    [SerializeField] private EnemyConfig targetEnemy;
    [SerializeField] private int targetEnemyCountMin;
    [SerializeField] private int targetEnemyCountMax;
    [SerializeField] private PirateGroupSpawnRuleConfig pirateGroupSpawnRuleConfig;

    public virtual MissionType MissionType => missionType;
    public string TitlePattern => titlePattern;
    public string DescriptionPattern => descriptionPattern;
    public MissionDifficultyBand DifficultyBand => difficultyBand;
    public int CreditRewardMin => creditRewardMin;
    public int CreditRewardMax => creditRewardMax;
    public int XpRewardMin => xpRewardMin;
    public int XpRewardMax => xpRewardMax;
    public int RewardCredits => rewardCredits;
    public ItemConfig[] RewardItems => rewardItems;
    public int MinDangerLevel => minDangerLevel;
    public int MaxDangerLevel => maxDangerLevel;
    public MissionTag[] RequiredMissionTags => requiredMissionTags;
    public int MinCargoCapacity => minCargoCapacity;
    public PlanetConfig SourcePlanet => sourcePlanet;
    public EnemyConfig TargetEnemy => targetEnemy;
    public int TargetEnemyCountMin => targetEnemyCountMin;
    public int TargetEnemyCountMax => targetEnemyCountMax;
    public PirateGroupSpawnRuleConfig PirateGroupSpawnRuleConfig => pirateGroupSpawnRuleConfig;
}