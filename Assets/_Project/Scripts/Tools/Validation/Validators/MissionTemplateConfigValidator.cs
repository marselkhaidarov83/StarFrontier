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

        if (config.CreditRewardMode == MissionCreditRewardMode.Fixed && config.RewardCredits < 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "Fixed reward must be nonnegative.", config));
        if (config is DeliveryMissionTemplateConfig delivery)
        {
            if (delivery.CargoOptions == null || delivery.CargoOptions.Count == 0 || delivery.MinimumQuantity <= 0 || delivery.MaximumQuantity < delivery.MinimumQuantity)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Delivery requires a cargo pool and valid quantity limits.", config));
            if (config.CreditRewardMode != MissionCreditRewardMode.FuelDistance || delivery.FuelRewardDeflator <= 0 ||
                float.IsNaN(delivery.FuelRewardDeflator) || float.IsInfinity(delivery.FuelRewardDeflator))
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Delivery requires FuelDistance reward mode and positive fuel deflator.", config));
            if (string.IsNullOrWhiteSpace(config.DescriptionPattern) || !config.DescriptionPattern.Contains("{cargoVolume}") || !config.DescriptionPattern.Contains("{amount}"))
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Delivery text must include {amount} and {cargoVolume}.", config));
        }
        if (config is ReconMissionTemplateConfig recon &&
            (recon.DistanceWeights == null || recon.DistanceWeights.Count == 0 || recon.ScanDistance <= 0 ||
             float.IsNaN(recon.ScanDistance) || float.IsInfinity(recon.ScanDistance) ||
             recon.MinimumEnemyCount <= 0 || recon.MaximumEnemyCount < recon.MinimumEnemyCount ||
             recon.EnemyReservePercent < 0 || recon.EnemyReservePercent > 100))
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "Recon requires valid distance, enemy count and reserve settings.", config));
        if (config is ReconMissionTemplateConfig &&
            (string.IsNullOrWhiteSpace(config.DescriptionPattern) ||
             !config.DescriptionPattern.Contains("{enemyCount}") || !config.DescriptionPattern.Contains("{scanDistance}")))
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "Recon text must include {enemyCount} and {scanDistance}.", config));
        return issues;
    }
}
