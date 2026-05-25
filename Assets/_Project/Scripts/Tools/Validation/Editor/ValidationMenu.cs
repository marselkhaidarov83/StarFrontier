#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ValidationMenu
{
    [MenuItem("SpaceWorld/Validation/Validate All Configs")]
    public static void ValidateAllConfigs()
    {
        var issues = new List<ValidationIssue>();

        var allConfigs = new List<BaseConfig>();
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<ShipConfig>());
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<WeaponConfig>());
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<ModuleConfig>());
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<ItemConfig>());
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<StarSystemConfig>());
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<MissionTemplateConfig>());
        allConfigs.AddRange(ValidationRunner.LoadAllConfigs<EnemyConfig>());

        var starSystemConfigs = new List<StarSystemConfig>();
        starSystemConfigs.AddRange(ValidationRunner.LoadAllConfigs<StarSystemConfig>());

        var baseValidator = new BaseGameConfigValidator();
        var idValidator = new ConfigIdUniquenessValidator();

        foreach (var config in allConfigs)
            issues.AddRange(baseValidator.Validate(config));
        issues.AddRange(idValidator.Validate(allConfigs));

        var shipValidator = new ShipConfigValidator();
        foreach (var config in ValidationRunner.LoadAllConfigs<ShipConfig>())
        issues.AddRange(shipValidator.Validate(config));

        var starSystemValidator = new StarSystemConfigValidator();
        foreach (var config in starSystemConfigs)
            issues.AddRange(starSystemValidator.Validate(config));

        var starSystemConnectivityValidator = new StarSystemConnectivityValidator();
        issues.AddRange(starSystemConnectivityValidator.Validate(
            starSystemConfigs,
            "system_helios_01",
            true));

        Debug.Log($"Validation finished. Issues found: {issues.Count}");

        foreach (var issue in issues)
        {
            if (issue.Severity == ValidationSeverity.Error)
            Debug.LogError(issue.ToString(), issue.SourceObject);
            else
            Debug.LogWarning(issue.ToString(), issue.SourceObject);
        }
    }
}
#endif