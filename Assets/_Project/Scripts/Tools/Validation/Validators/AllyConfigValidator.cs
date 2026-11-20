using System.Collections.Generic;

public class AllyConfigValidator : IConfigValidator<AllyConfig>
{
    public List<ValidationIssue> Validate(AllyConfig config)
    {
        var issues = NpcLoadoutGroupValidator.Validate(config, config.Level, false, config.HullGroup, config.EquipmentGroup);
        if (config.HasDuplicateBehaviorScenarios())
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "Duplicate behaviour scenarios.", config));
        if (config.LootTable != null)
        {
            foreach (string error in config.LootTable.GetValidationErrors())
                issues.Add(new ValidationIssue(ValidationSeverity.Error, error, config.LootTable));
            if (config.LootTable.GetInitialCargoQuantity() > config.BaseCargoCapacityMin)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Initial cargo exceeds the minimum hull capacity.", config));
        }
        return issues;
    }
}
