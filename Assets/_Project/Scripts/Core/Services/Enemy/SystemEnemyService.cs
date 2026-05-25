using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

    public sealed class SystemEnemyService : CustomService, ISystemEnemyService
    {
        private readonly List<SystemEnemyRuntimeState> _enemies = new();

        private readonly SimpleEventBus _eventBus;
        private readonly ISystemEncounterService _encounterService;

        public IReadOnlyList<SystemEnemyRuntimeState> Enemies => _enemies;

        public SystemEnemyService()
        {
            _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        }

        public SystemEnemyRuntimeState CreateEnemy(
            EnemyConfig enemyConfig,
            string systemId,
            Vector3 position)
        {
            if (enemyConfig == null)
                throw new ArgumentNullException(nameof(enemyConfig));

            if (string.IsNullOrWhiteSpace(enemyConfig.Id))
                throw new ArgumentException("EnemyConfig.Id is empty.", nameof(enemyConfig));

            if (string.IsNullOrWhiteSpace(systemId))
                throw new ArgumentException("SystemId is empty.", nameof(systemId));

            var enemy = new SystemEnemyRuntimeState
            {
                RuntimeEnemyId = Guid.NewGuid().ToString("N"),

                EnemyConfigId = enemyConfig.Id,
                EnemyConfig = enemyConfig,

                SystemId = systemId,
                Position = position,

                CurrentHull = enemyConfig.BaseHull,
                CurrentShield = enemyConfig.BaseShield,
                CurrentEnergy = enemyConfig.BaseEnergy,

                IsAlive = true,
                WasKilledByPlayer = false
            };

            _enemies.Add(enemy);

            _eventBus.Publish(new SystemEnemyCreatedEvent(
                enemy.RuntimeEnemyId,
                enemy.EnemyConfigId,
                enemy.SystemId,
                enemy.Position));

            return enemy;
        }

        public bool TryGetEnemy(string runtimeEnemyId, out SystemEnemyRuntimeState enemy)
        {
            enemy = _enemies.FirstOrDefault(x => x.RuntimeEnemyId == runtimeEnemyId);
            return enemy != null;
        }

        public IReadOnlyList<SystemEnemyRuntimeState> GetAliveEnemiesInSystem(string systemId)
        {
            return _enemies
                .Where(x => x.SystemId == systemId && x.IsAlive)
                .ToList();
        }

        public void UpdateEnemyPosition(string runtimeEnemyId, Vector3 position)
        {
            if (!TryGetEnemy(runtimeEnemyId, out var enemy))
                return;

            if (!enemy.IsAlive)
                return;

            enemy.Position = position;

            _eventBus.Publish(new SystemEnemyPositionChangedEvent(
                enemy.RuntimeEnemyId,
                enemy.Position));
        }

        public void ApplyDamage(
            string runtimeEnemyId,
            int damage,
            bool fromPlayer)
        {
            if (damage <= 0)
                return;

            if (!TryGetEnemy(runtimeEnemyId, out var enemy))
            {
                if (IsDebug())
                    Debug.Log("[SystemEnemyService] ApplyDamage.runtimeEnemyId = " + runtimeEnemyId);
                return;
            }

            if (!enemy.IsAlive)
            {
                if (IsDebug())
                    Debug.Log("[SystemEnemyService] ApplyDamage.IsAlive = false");
                return;                
            }

            int remainingDamage = damage;

            if (enemy.CurrentShield > 0)
            {
                int shieldDamage = Mathf.Min(enemy.CurrentShield, remainingDamage);
                enemy.CurrentShield -= shieldDamage;
                remainingDamage -= shieldDamage;
            }

            if (remainingDamage > 0)
                enemy.CurrentHull -= remainingDamage;

            _eventBus.Publish(new SystemEnemyDamagedEvent(
                enemy.RuntimeEnemyId,
                damage,
                enemy.CurrentHull,
                enemy.CurrentShield));

            if (enemy.CurrentHull <= 0)
                DestroyEnemy(enemy, fromPlayer);

            Debug.Log(
                $"[SystemEnemyService] Damage: {damage}, Enemy: {enemy.EnemyConfigId}, " +
                $"Hull: {enemy.CurrentHull}, Shield: {enemy.CurrentShield}"
            );       
        }

        public void ClearSystemEnemies(string systemId)
        {
            _enemies.RemoveAll(x => x.SystemId == systemId);
        }

        public void ClearAll()
        {
            _enemies.Clear();
        }

        private void DestroyEnemy(SystemEnemyRuntimeState enemy, bool killedByPlayer)
        {
            if (!enemy.IsAlive)
                return;

            enemy.IsAlive = false;
            enemy.WasKilledByPlayer = killedByPlayer;
            enemy.CurrentHull = 0;

            _encounterService.RegisterEnemyDestroyed(killedByPlayer);

            _eventBus.Publish(new SystemEnemyDestroyedEvent(
                enemy.RuntimeEnemyId,
                enemy.EnemyConfigId,
                enemy.SystemId,
                killedByPlayer));
        }

    public void RestoreEnemy(SystemEnemyRuntimeState enemy)
    {
        if (enemy == null)
            return;

        _enemies.RemoveAll(x => x.RuntimeEnemyId == enemy.RuntimeEnemyId);
        _enemies.Add(enemy);

        _eventBus.Publish(new SystemEnemyCreatedEvent(
            enemy.RuntimeEnemyId,
            enemy.EnemyConfigId,
            enemy.SystemId,
            enemy.Position));
    }

    public void RestoreEnemies(IEnumerable<SystemEnemyRuntimeState> enemies)
    {
        if (enemies == null)
            return;

        foreach (var enemy in enemies)
            RestoreEnemy(enemy);
    }        
    }