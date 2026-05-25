using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConfigIdUniquenessValidator
{
    public List<ValidationIssue> Validate(IEnumerable<BaseConfig> configs)
    {
        var issues = new List<ValidationIssue>();
        var configList = configs.Where(c => c != null).ToList();

        foreach (var config in configList)
        {
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Id is empty.",
                config));
            }
        }

        var duplicateGroups = configList
        .Where(c => !string.IsNullOrWhiteSpace(c.Id))
        .GroupBy(c => c.Id)
        .Where(g => g.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var config in group)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"Duplicate Id detected: {group.Key}",
                config));
            }
        }

        return issues;
    }
}