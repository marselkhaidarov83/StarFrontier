using System.Collections.Generic;
using UnityEngine;

public class EnemyConfigValidator : IConfigValidator<EnemyConfig>
{
    public List<ValidationIssue> Validate(EnemyConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.BaseHull <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "BaseHull must be > 0.",
            config));
        }

        if (config.BaseShield < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "BaseShield must be >= 0.",
            config));
        }

        if (config.BaseEnergy < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "BaseEnergy must be >= 0.",
            config));
        }

        if (config.BaseSpeed <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "BaseSpeed must be > 0.",
            config));
        }

        if (config.WeaponConfig == null)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "WeaponConfig is not assigned.",
            config));
        }

        if (config.CreditReward < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "CreditReward must be >= 0.",
            config));
        }

        if (config.XpReward < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "XpReward must be >= 0.",
            config));
        }

        if (config.DangerTier < 1)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "DangerTier must be >= 1.",
            config));
        }

        if (config.DangerTier > 3)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "DangerTier is higher than current MVP expectation (3).",
            config));
        }

        return issues;
    }
}