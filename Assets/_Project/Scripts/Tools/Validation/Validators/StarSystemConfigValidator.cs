using System.Collections.Generic;
using UnityEngine;

public class StarSystemConfigValidator : IConfigValidator<StarSystemConfig>
{
    public List<ValidationIssue> Validate(StarSystemConfig config)
    {
        var issues = new List<ValidationIssue>();

        if (config.DangerLevel < 1 || config.DangerLevel > 5)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "DangerLevel must be between 1 and 5.",
            config));
        }

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

        if (config.MissionTags == null || config.MissionTags.Length == 0)
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Warning,
            "MissionTags is empty.",
            config));
        }

        return issues;
    }
}