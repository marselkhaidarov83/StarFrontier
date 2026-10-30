using System.Collections.Generic;
using UnityEngine;

public class ItemConfigValidator : IConfigValidator<ItemConfig>
{
    public List<ValidationIssue> Validate(ItemConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.ItemCategory == ItemCategory.Currency)
        {
            if (config.BasePrice != 0 || config.CargoSize != 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    "Wallet currency must have zero price and cargo size.", config));
            return issues;
        }

        if (config.BasePrice <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "BasePrice must be > 0.",
            config));
        }

        if (config.CargoSize <= 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "CargoSize must be > 0.",
            config));
        }

        return issues;
    }
}
