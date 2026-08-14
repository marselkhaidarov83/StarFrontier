using System.Collections.Generic;
using UnityEngine;

public class AllyConfigValidator : IConfigValidator<AllyConfig>
{
    public List<ValidationIssue> Validate(AllyConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.BaseHull <= 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseHull must be > 0.", config));
        if (config.BaseShield < 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseShield must be >= 0.", config));
        if (config.BaseEnergy < 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseEnergy must be >= 0.", config));
        if (config.BaseEnergyRegen < 0f)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseEnergyRegen must be >= 0.", config));
        if (config.BaseSpeed <= 0f)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseSpeed must be > 0.", config));
        if (config.BaseAccelerationMin <= 0f)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseAccelerationMin must be > 0.", config));
        if (config.BaseAccelerationMax < config.BaseAccelerationMin)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseAccelerationMax must be >= BaseAccelerationMin.", config));
        if (config.BaseTurnRateMin <= 0f)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseTurnRateMin must be > 0.", config));
        if (config.BaseTurnRateMax < config.BaseTurnRateMin)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseTurnRateMax must be >= BaseTurnRateMin.", config));
        if (config.BaseCargoCapacityMin < 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseCargoCapacityMin must be >= 0.", config));
        if (config.BaseCargoCapacityMax < config.BaseCargoCapacityMin)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseCargoCapacityMax must be >= BaseCargoCapacityMin.", config));
        if (config.WeaponSlotCount < 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "WeaponSlotCount must be >= 0.", config));
        if (config.ModuleSlotCount < 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "ModuleSlotCount must be >= 0.", config));
        if (config.WeaponSlotCount == 0)
            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "WeaponSlotCount is 0.", config));
        if (config.CombatSprite == null)
            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "CombatSprite is not assigned.", config));

        return issues;
    }
}
