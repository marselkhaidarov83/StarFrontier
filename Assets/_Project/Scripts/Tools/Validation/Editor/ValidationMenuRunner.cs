#if UNITY_EDITOR
using System.Collections.Generic;

    public static class ValidationMenuRunner
    {
        public static List<ValidationIssue> RunAll()
        {
            var issues =
                new List<ValidationIssue>();

            var shipConfigs =
                ValidationRunner
                .LoadAllConfigs<ShipConfig>();

            var weaponConfigs =
                ValidationRunner
                .LoadAllConfigs<WeaponConfig>();

            var moduleConfigs =
                ValidationRunner
                .LoadAllConfigs<ModuleConfig>();

            var itemConfigs =
                ValidationRunner
                .LoadAllConfigs<ItemConfig>();

            var starSystemConfigs =
                ValidationRunner
                .LoadAllConfigs<StarSystemConfig>();

            var missionConfigs =
                ValidationRunner
                .LoadAllConfigs<MissionTemplateConfig>();

            var enemyConfigs =
                ValidationRunner
                .LoadAllConfigs<EnemyConfig>();

            var allConfigs =
                new List<BaseConfig>();

            allConfigs.AddRange(shipConfigs);
            allConfigs.AddRange(weaponConfigs);
            allConfigs.AddRange(moduleConfigs);
            allConfigs.AddRange(itemConfigs);
            allConfigs.AddRange(starSystemConfigs);
            allConfigs.AddRange(missionConfigs);
            allConfigs.AddRange(enemyConfigs);

            var baseValidator =
                new BaseGameConfigValidator();

            var idValidator =
                new ConfigIdUniquenessValidator();

            foreach (var config in allConfigs)
                issues.AddRange(
                    baseValidator.Validate(config));

            issues.AddRange(
                idValidator.Validate(allConfigs));

            var shipValidator =
                new ShipConfigValidator();

            foreach (var config in shipConfigs)
                issues.AddRange(
                    shipValidator.Validate(config));

            var weaponValidator =
                new WeaponConfigValidator();

            foreach (var config in weaponConfigs)
                issues.AddRange(
                    weaponValidator.Validate(config));

            var moduleValidator =
                new ModuleConfigValidator();

            foreach (var config in moduleConfigs)
                issues.AddRange(
                    moduleValidator.Validate(config));

            var itemValidator =
                new ItemConfigValidator();

            foreach (var config in itemConfigs)
                issues.AddRange(
                    itemValidator.Validate(config));

            var starValidator =
                new StarSystemConfigValidator();

            foreach (var config in starSystemConfigs)
                issues.AddRange(
                    starValidator.Validate(config));

            var connectivityValidator =
                new StarSystemConnectivityValidator();

            issues.AddRange(
                connectivityValidator.Validate(
                    starSystemConfigs,
                    "system_helios_01",
                    true));

            var missionValidator =
                new MissionTemplateConfigValidator();

            foreach (var config in missionConfigs)
                issues.AddRange(
                    missionValidator.Validate(config));

            var enemyValidator =
                new EnemyConfigValidator();

            foreach (var config in enemyConfigs)
                issues.AddRange(
                    enemyValidator.Validate(config));

            return issues;
        }
    }
#endif