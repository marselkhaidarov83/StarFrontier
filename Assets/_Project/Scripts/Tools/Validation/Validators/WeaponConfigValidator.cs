using System.Collections.Generic;

public class WeaponConfigValidator : IConfigValidator<WeaponConfig>
{
    public List<ValidationIssue> Validate(WeaponConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.BaseDamage <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "BaseDamage must be > 0.",
            config));
        }

        if (config.Range <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "Range must be > 0.",
            config));
        }

        if (config.Cooldown <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "Cooldown must be > 0.",
            config));
        }

        if (config.EnergyCost < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "EnergyCost must be >= 0.",
            config));
        }

        if (!config.IsHitscan && config.ProjectileSpeed <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "ProjectileSpeed must be > 0 when IsHitscan is false.",
            config));
        }

        if (config.IsHitscan && config.ProjectileSpeed > 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "ProjectileSpeed is set but IsHitscan is true. This value will likely be ignored.",
            config));
        }

        return issues;
    }
}