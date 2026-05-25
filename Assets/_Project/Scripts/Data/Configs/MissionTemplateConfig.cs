using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MissionTemplateConfig", menuName = "StarFrontier/Configs/Mission Template")]
public class MissionTemplateConfig : BaseConfig
{
    [Header("Mission Core")]
    [SerializeField] private MissionType missionType;
    [SerializeField] private string titlePattern;
    [SerializeField] [TextArea] private string descriptionPattern;
    [SerializeField] private MissionDifficultyBand difficultyBand;

    [Header("Rewards")]
    [SerializeField] private int creditRewardMin;
    [SerializeField] private int creditRewardMax;
    [SerializeField] private int xpRewardMin;
    [SerializeField] private int xpRewardMax;
    [SerializeField] private int rewardCredits;
    [SerializeField] private ItemConfig[] rewardItems;

    [Header("Generation Constraints")]
    [SerializeField] [Range(1, 5)] private int minDangerLevel = 1;
    [SerializeField] [Range(1, 5)] private int maxDangerLevel = 5;
    [SerializeField] private MissionTag[] requiredMissionTags;
    [SerializeField] private int minCargoCapacity;

    [Header("Target Rules")]
    [SerializeField] private PlanetConfig sourcePlanet;
    [SerializeField] private PlanetConfig targetPlanet;
    [SerializeField] private StarSystemConfig targetSystem;
    [SerializeField] private EnemyConfig targetEnemy;
    [SerializeField] private int targetEnemyCountMin;
    [SerializeField] private int targetEnemyCountMax;
    [SerializeField] private PirateGroupSpawnRuleConfig pirateGroupSpawnRuleConfig;

    public MissionType MissionType => missionType;
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
    public PlanetConfig TargetPlanet => targetPlanet;
    public StarSystemConfig TargetSystem => targetSystem;
    public EnemyConfig TargetEnemy => targetEnemy;
    public int TargetEnemyCountMin => targetEnemyCountMin;
    public int TargetEnemyCountMax => targetEnemyCountMax;
    public PirateGroupSpawnRuleConfig PirateGroupSpawnRuleConfig => pirateGroupSpawnRuleConfig;
}