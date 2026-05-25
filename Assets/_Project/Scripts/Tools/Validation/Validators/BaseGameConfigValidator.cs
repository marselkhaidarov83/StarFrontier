using System.Collections.Generic;

public class BaseGameConfigValidator : IConfigValidator<BaseConfig>
{
    public List<ValidationIssue> Validate(BaseConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config == null)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "Config is null.",
            null));
            return issues;
        }

        if (string.IsNullOrWhiteSpace(config.Id))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "Id is empty.",
            config));
        }

        if (string.IsNullOrWhiteSpace(config.DisplayName))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "DisplayName is empty.",
            config));
        }

        if (string.IsNullOrWhiteSpace(config.Description))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "Description is empty.",
            config));
        }

        if (config.Icon == null)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "Icon is not assigned.",
            config));
        }

        return issues;
    }
}