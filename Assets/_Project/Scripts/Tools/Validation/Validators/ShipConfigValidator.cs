using System.Collections.Generic;
using UnityEngine;

public class ShipConfigValidator : IConfigValidator<ShipConfig>
{
    public List<ValidationIssue> Validate(ShipConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.BaseHull <= 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseHull must be > 0.", config));

        if (config.BaseShield < 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseShield must be >= 0.", config));

        if (config.BaseEnergy < 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseEnergy must be >= 0.", config));

        if (config.BaseSpeed <= 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseSpeed must be > 0.", config));

        if (config.BaseAcceleration <= 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseAcceleration must be > 0.", config));

        if (config.BaseTurnRate <= 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseTurnRate must be > 0.", config));

        if (config.BaseCargoCapacity < 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "BaseCargoCapacity must be >= 0.", config));

        if (config.WeaponSlotCount < 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "WeaponSlotCount must be >= 0.", config));

        if (config.ModuleSlotCount < 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Error, "ModuleSlotCount must be >= 0.", config));

        if (config.WeaponSlotCount == 0)
        issues.Add(new ValidationIssue(ValidationSeverity.Warning, "WeaponSlotCount is 0.", config));

        return issues;
    }
}