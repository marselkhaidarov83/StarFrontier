using System.Collections.Generic;
using UnityEngine;

    public sealed class SystemEncounterSaveService : CustomService, ISystemEncounterSaveService
    {
        private readonly ISystemEncounterService _encounterService;
        private readonly ISystemEnemyService _enemyService;
        private readonly ISystemAllyService _allyService;
        private readonly IConfigService _configService;

        public SystemEncounterSaveService()
        {
            _debugStop = true;
            _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
            _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
            _allyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemAllyService>();
            _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        }

        public SystemEncounterSaveData Capture()
        {
            var current = _encounterService.Current;

            if (current == null)
            {
                return new SystemEncounterSaveData
                {
                    HasEncounter = false
                };
            }

            var saveData = new SystemEncounterSaveData
            {
                HasEncounter = true,

                EncounterId = current.EncounterId,
                SystemId = current.SystemId,

                State = current.State,
                DefeatReason = current.DefeatReason,

                EnemiesAlive = current.EnemiesAlive,
                AlliesAlive = current.AlliesAlive,

                PlayerKills = current.PlayerKills,
                PlayerDestroyed = current.PlayerDestroyed,

                PlayerIsOnPlanet = current.PlayerIsOnPlanet,
                DaysPlayerStayedOnPlanetDuringEncounter = current.DaysPlayerStayedOnPlanetDuringEncounter
            };

            foreach (var enemy in _enemyService.Enemies)
            {
                saveData.Enemies.Add(new SystemEnemySaveData
                {
                    RuntimeEnemyId = enemy.RuntimeEnemyId,
                    EnemyConfigId = enemy.EnemyConfigId,
                    SystemId = enemy.SystemId,
                    Position = enemy.Position,
                    CurrentHull = enemy.CurrentHull,
                    CurrentShield = enemy.CurrentShield,
                    CurrentEnergy = enemy.CurrentEnergy,
                    IsAlive = enemy.IsAlive,
                    WasKilledByPlayer = enemy.WasKilledByPlayer
                });
            }

            foreach (var ally in _allyService.Allies)
            {
                saveData.Allies.Add(new SystemAllySaveData
                {
                    RuntimeAllyId = ally.RuntimeAllyId,
                    AllyConfigId = ally.AllyConfigId,
                    SystemId = ally.SystemId,
                    Position = ally.Position,
                    CurrentHull = ally.CurrentHull,
                    CurrentShield = ally.CurrentShield,
                    CurrentEnergy = ally.CurrentEnergy,
                    IsAlive = ally.IsAlive
                });
            }

            Debug.Log("[SystemEncounterSaveService] Captured encounter save data.");

            return saveData;
        }

        public void Restore(SystemEncounterSaveData saveData)
        {
            _encounterService.ClearEncounter();
            _enemyService.ClearAll();
            _allyService.ClearAll();

            if (saveData == null || !saveData.HasEncounter)
            {
                LogCustom("No encounter to restore.");
                return;
            }

            var encounter = new ActiveSystemEncounter
            {
                EncounterId = saveData.EncounterId,
                SystemId = saveData.SystemId,

                State = saveData.State,
                DefeatReason = saveData.DefeatReason,

                EnemiesAlive = saveData.EnemiesAlive,
                AlliesAlive = saveData.AlliesAlive,

                PlayerKills = saveData.PlayerKills,
                PlayerDestroyed = saveData.PlayerDestroyed,

                PlayerIsOnPlanet = saveData.PlayerIsOnPlanet,
                DaysPlayerStayedOnPlanetDuringEncounter = saveData.DaysPlayerStayedOnPlanetDuringEncounter
            };

            _encounterService.RestoreEncounter(encounter);

            var restoredEnemies = new List<SystemEnemyRuntimeState>();

            foreach (var enemySave in saveData.Enemies)
            {
                EnemyConfig enemyConfig = _configService.GetEnemyConfigById(enemySave.EnemyConfigId);

                if (enemyConfig == null)
                {
                    Debug.LogWarning($"[SystemEncounterSaveService] EnemyConfig not found: {enemySave.EnemyConfigId}");
                    continue;
                }

                restoredEnemies.Add(new SystemEnemyRuntimeState
                {
                    RuntimeEnemyId = enemySave.RuntimeEnemyId,
                    EnemyConfigId = enemySave.EnemyConfigId,
                    EnemyConfig = enemyConfig,
                    SystemId = enemySave.SystemId,
                    Position = enemySave.Position,
                    CurrentHull = enemySave.CurrentHull,
                    CurrentShield = enemySave.CurrentShield,
                    CurrentEnergy = enemySave.CurrentEnergy,
                    IsAlive = enemySave.IsAlive,
                    WasKilledByPlayer = enemySave.WasKilledByPlayer
                });
            }

            var restoredAllies = new List<SystemAllyRuntimeState>();

            foreach (var allySave in saveData.Allies)
            {
                AllyConfig allyConfig = _configService.GetAllyConfigById(allySave.AllyConfigId);

                if (allyConfig == null)
                {
                    Debug.LogWarning($"[SystemEncounterSaveService] AllyConfig not found: {allySave.AllyConfigId}");
                    continue;
                }

                restoredAllies.Add(new SystemAllyRuntimeState
                {
                    RuntimeAllyId = allySave.RuntimeAllyId,
                    AllyConfigId = allySave.AllyConfigId,
                    AllyConfig = allyConfig,
                    SystemId = allySave.SystemId,
                    Position = allySave.Position,
                    CurrentHull = allySave.CurrentHull,
                    CurrentShield = allySave.CurrentShield,
                    CurrentEnergy = allySave.CurrentEnergy,
                    IsAlive = allySave.IsAlive
                });
            }

            _enemyService.RestoreEnemies(restoredEnemies);
            _allyService.RestoreAllies(restoredAllies);

            Debug.Log("[SystemEncounterSaveService] Restored encounter save data.");
        }
    }