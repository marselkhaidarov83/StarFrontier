using System.Collections.Generic;

public class StarSystemConfigValidator : IConfigValidator<StarSystemConfig>
{
    public List<ValidationIssue> Validate(StarSystemConfig config)
    {
        var issues = new List<ValidationIssue>();
        if (config == null) return issues;
        if (config.Routes == null || config.Routes.Count == 0)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "System has no routes.", config));
            return issues;
        }
        var ids = new HashSet<string>();
        foreach (RouteConfig route in config.Routes)
        {
            StarSystemConfig other = route?.GetOtherSystem(config.Id);
            if (route == null || string.IsNullOrWhiteSpace(route.Id) || other == null ||
                string.IsNullOrWhiteSpace(other.Id) || other.Id == config.Id)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "System contains an invalid route or route endpoint.", config));
            else if (!ids.Add(route.Id))
                issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Duplicate route: {route.Id}.", config));
        }


        return issues;
    }
}
