using System.Collections.Generic;
using UnityEngine;

public class MissionTemplateConfigValidator : IConfigValidator<MissionTemplateConfig>
{
    public List<ValidationIssue> Validate(MissionTemplateConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(config.TitlePattern))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "TitlePattern is empty.",
            config));
        }

        if (string.IsNullOrWhiteSpace(config.DescriptionPattern))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "DescriptionPattern is empty.",
            config));
        }

        if (config.CreditRewardMin < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "CreditRewardMin must be >= 0.",
            config));
        }

        if (config.CreditRewardMax < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "CreditRewardMax must be >= 0.",
            config));
        }

        if (config.XpRewardMin < 0)
        {
        issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "XpRewardMin must be >= 0.",
            config));
        }

        if (config.XpRewardMax < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "XpRewardMax must be >= 0.",
            config));
        }

        if (config.CreditRewardMin > config.CreditRewardMax)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "CreditRewardMin must be <= CreditRewardMax.",
            config));
        }

        if (config.XpRewardMin > config.XpRewardMax)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "XpRewardMin must be <= XpRewardMax.",
            config));
        }

        if (config.MinDangerLevel > config.MaxDangerLevel)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "MinDangerLevel must be <= MaxDangerLevel.",
            config));
        }

        if (config.RequiredMissionTags == null || config.RequiredMissionTags.Length == 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "RequiredMissionTags is empty.",
            config));
        }

        if (config.MissionType == MissionType.Elimination)
        {
            if (config.TargetEnemyCountMin <= 0)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Elimination mission must have TargetEnemyCountMin > 0.",
                config));
            }

            if (config.TargetEnemyCountMax < config.TargetEnemyCountMin)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "TargetEnemyCountMax must be >= TargetEnemyCountMin.",
                config));
            }
        }

        return issues;
    }
}