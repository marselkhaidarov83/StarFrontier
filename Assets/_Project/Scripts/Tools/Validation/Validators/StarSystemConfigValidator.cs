using System.Collections.Generic;
using UnityEngine;

public class StarSystemConfigValidator : IConfigValidator<StarSystemConfig>
{
    public List<ValidationIssue> Validate(StarSystemConfig config)
    {
        var issues = new List<ValidationIssue>();

        // if (config.NeighborSystemIds == null || config.NeighborSystemIds.Length == 0)
        if (config.LinkedSystems == null || config.LinkedSystems.Length == 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "NeighborSystemIds is empty.",
            config));
        }
        else
        {
            // foreach (var neighborId in config.NeighborSystemIds)
            foreach (var linkedSystem in config.LinkedSystems)
            {
                // if (string.IsNullOrWhiteSpace(neighborId))
                if (string.IsNullOrWhiteSpace(linkedSystem.LinkedSystem.Id))
                {
                    issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "NeighborSystemIds contains an empty value.",
                    config));
                }
            }
        }


        return issues;
    }
}
