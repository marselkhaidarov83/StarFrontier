using System.Collections.Generic;
using UnityEngine;

public class ModuleConfigValidator : IConfigValidator<ModuleConfig>
{
    public List<ValidationIssue> Validate(ModuleConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.EnergyCost < 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "EnergyCost must be >= 0.",
            config));
        }

        if (config.ActivationType == ModuleActivationType.Active)
        {
            if (config.Cooldown <= 0)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Cooldown must be > 0 for active modules.",
                config));
            }

            if (config.ActiveEffectType == ModuleActiveEffectType.None)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Active modules must have a valid ActiveEffectType.",
                config));
            }
        }

        if (config.ActivationType == ModuleActivationType.Passive)
        {
            if (config.StatModifiers == null || config.StatModifiers.Length == 0)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "Passive module has no stat modifiers.",
                config));
            }
        }

        return issues;
    }
}