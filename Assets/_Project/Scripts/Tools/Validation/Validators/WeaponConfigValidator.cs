using System;
using System.Collections.Generic;

public class WeaponConfigValidator : IConfigValidator<WeaponConfig>
{
    public List<ValidationIssue> Validate(WeaponConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config == null)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "WeaponConfig is null.",
                null));

            return issues;
        }

        if (config.Level <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Level must be > 0.",
                config));
        }

        if (!Enum.IsDefined(typeof(WeaponEquipmentTier), config.EquipmentTier))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "EquipmentTier has invalid value.",
                config));
        }

        if (config.CargoSize <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "CargoSize must be > 0.",
                config));
        }

        if (config.BaseDamageMin <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "BaseDamageMin must be > 0.",
                config));
        }

        if (config.BaseDamageMax < config.BaseDamageMin)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "BaseDamageMax must be >= BaseDamageMin.",
                config));
        }

        if (config.RangeMin <= 0f)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "RangeMin must be > 0.",
                config));
        }

        if (config.RangeMax < config.RangeMin)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "RangeMax must be >= RangeMin.",
                config));
        }

        if (config.EnergyCostMin < 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "EnergyCostMin must be >= 0.",
                config));
        }

        if (config.EnergyCostMax < config.EnergyCostMin)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "EnergyCostMax must be >= EnergyCostMin.",
                config));
        }

        if (config.ProjectileLifetimeMin < 1)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "ProjectileLifetimeMin must be >= 1.",
                config));
        }

        if (config.ProjectileLifetimeMax < config.ProjectileLifetimeMin)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "ProjectileLifetimeMax must be >= ProjectileLifetimeMin.",
                config));
        }

        if (config.WeaponType == WeaponType.Missile && !config.UsesAmmo)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Missile weapon must use ammo.",
                config));
        }

        if (config.UsesAmmo)
        {
            if (config.MaxAmmoChargesMin <= 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "MaxAmmoChargesMin must be > 0 when UsesAmmo is true.",
                    config));
            }

            if (config.MaxAmmoChargesMax < config.MaxAmmoChargesMin)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "MaxAmmoChargesMax must be >= MaxAmmoChargesMin.",
                    config));
            }
        }
        else
        {
            if (config.MaxAmmoChargesMin != 0 || config.MaxAmmoChargesMax != 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "MaxAmmoChargesMin/Max should be 0 when UsesAmmo is false.",
                    config));
            }
        }

        if (config.ProjectilePrefabRef == null)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "ProjectilePrefabRef is not assigned.",
                config));
        }

        return issues;
    }
}