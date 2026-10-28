using UnityEngine;

[CreateAssetMenu(fileName = "mission_gameplay", menuName = "StarFrontier/Configs/Mission/Gameplay")]
public sealed class MissionGameplayConfig : BaseConfig
{
    [Header("Кредиты за личное убийство при победе")]
    [Min(0)] public int DefenseKillRewardDeflator = 100;
    [Min(0)] public int LiberationKillRewardDeflator = 100;

    [Header("Тексты защиты")]
    [SerializeField] private string defenseTitlePattern;
    [SerializeField] [TextArea] private string defenseDescriptionPattern;
    [SerializeField] private MissionOutcomeTexts defenseOutcomeTexts;

    [Header("Тексты освобождения")]
    [SerializeField] private string liberationTitlePattern;
    [SerializeField] [TextArea] private string liberationDescriptionPattern;
    [SerializeField] private MissionOutcomeTexts liberationOutcomeTexts;

    public string DefenseTitlePattern => defenseTitlePattern;
    public string DefenseDescriptionPattern => defenseDescriptionPattern;
    public MissionOutcomeTexts DefenseOutcomeTexts => defenseOutcomeTexts;
    public string LiberationTitlePattern => liberationTitlePattern;
    public string LiberationDescriptionPattern => liberationDescriptionPattern;
    public MissionOutcomeTexts LiberationOutcomeTexts => liberationOutcomeTexts;
}
