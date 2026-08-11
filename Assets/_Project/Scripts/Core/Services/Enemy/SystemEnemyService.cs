using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SystemEnemyService : CustomService, ISystemEnemyService
{
    private const float DefaultAttackCooldownSeconds = 2.5f;
    private const float DefaultAttackRange = 12f;
    private const int DefaultAttackDamage = 8;

    private readonly List<SystemEnemyRuntimeState> _enemies = new();

    private readonly SimpleEventBus _eventBus;
    private readonly ISystemEncounterService _encounterService;

    private IPlayerCombatTargetService _playerTargetService;

    public IReadOnlyList<SystemEnemyRuntimeState> Enemies => _enemies;

    public SystemEnemyService()
    {
        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _encounterService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
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
            WasKilledByPlayer = false,

            CurrentTargetId = "player",
            HasTarget = true,

            AttackCooldownSeconds = DefaultAttackCooldownSeconds,
            AttackTimerSeconds = UnityEngine.Random.Range(
                0.25f,
                DefaultAttackCooldownSeconds),

            BaseAttackDamage = Mathf.Max(
                DefaultAttackDamage,
                enemyConfig.DangerTier * DefaultAttackDamage),

            AttackRange = DefaultAttackRange
        };

        _enemies.Add(enemy);

        _eventBus.Publish(new SystemEnemyCreatedEvent(
            enemy.RuntimeEnemyId,
            enemy.EnemyConfigId,
            enemy.SystemId,
            enemy.Position));

        return enemy;
    }

    public bool TryGetEnemy(
        string runtimeEnemyId,
        out SystemEnemyRuntimeState enemy)
    {
        enemy = _enemies.FirstOrDefault(
            x => x.RuntimeEnemyId == runtimeEnemyId);

        return enemy != null;
    }

    public IReadOnlyList<SystemEnemyRuntimeState> GetAliveEnemiesInSystem(
        string systemId)
    {
        return _enemies
            .Where(x => x.SystemId == systemId && x.IsAlive)
            .ToList();
    }

    public void UpdateEnemyPosition(
        string runtimeEnemyId,
        Vector3 position)
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
            return;

        if (!enemy.IsAlive)
            return;

        int remainingDamage = damage;

        if (enemy.CurrentShield > 0)
        {
            int shieldDamage =
                Mathf.Min(enemy.CurrentShield, remainingDamage);

            enemy.CurrentShield -= shieldDamage;
            remainingDamage -= shieldDamage;
        }

        if (remainingDamage > 0)
            enemy.CurrentHull -= remainingDamage;

        if (enemy.CurrentHull < 0)
            enemy.CurrentHull = 0;

        _eventBus.Publish(new SystemEnemyDamagedEvent(
            enemy.RuntimeEnemyId,
            damage,
            enemy.CurrentHull,
            enemy.CurrentShield));

        _eventBus.Publish(new CombatDamageEvent2A(
            enemy.RuntimeEnemyId,
            false,
            damage,
            enemy.CurrentShield,
            enemy.CurrentHull));

        if (enemy.CurrentHull <= 0)
            DestroyEnemy(enemy, fromPlayer);
    }

    public void TickSystemMapCombat(float deltaTime)
    {
        if (!_encounterService.HasActiveEncounter)
            return;

        if (!ResolvePlayerTargetService())
            return;

        ActiveSystemEncounter encounter =
            _encounterService.Current;

        if (encounter == null)
            return;

        if (!_playerTargetService.IsPlayerAvailableInSystem(encounter.SystemId))
            return;

        Vector3 playerPosition =
            _playerTargetService.GetPlayerPosition();

        for (int i = 0; i < _enemies.Count; i++)
        {
            SystemEnemyRuntimeState enemy =
                _enemies[i];

            if (enemy == null)
                continue;

            if (!enemy.IsAlive)
                continue;

            if (enemy.SystemId != encounter.SystemId)
                continue;

            enemy.HasTarget = true;
            enemy.CurrentTargetId = "player";
            enemy.TickAttackTimer(deltaTime);

            float distance =
                Vector3.Distance(enemy.Position, playerPosition);

            if (!enemy.CanAttack(distance))
                continue;

            string projectileId =
                Guid.NewGuid().ToString("N");

            _eventBus.Publish(new CombatProjectileCreatedEvent2A(
                projectileId,
                enemy.SystemId,
                enemy.RuntimeEnemyId,
                "player",
                enemy.EnemyConfigId,
                enemy.Position,
                playerPosition));

            _playerTargetService.ApplyDamage(enemy.BaseAttackDamage);

            _eventBus.Publish(new CombatProjectileImpactEvent2A(
                projectileId,
                enemy.SystemId,
                "player",
                enemy.BaseAttackDamage,
                playerPosition,
                true));

            enemy.ResetAttackTimer();
        }
    }

    public void ClearSystemEnemies(string systemId)
    {
        _enemies.RemoveAll(x => x.SystemId == systemId);
    }

    public void ClearAll()
    {
        _enemies.Clear();
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

    private void DestroyEnemy(
        SystemEnemyRuntimeState enemy,
        bool killedByPlayer)
    {
        if (!enemy.IsAlive)
            return;

        enemy.IsAlive = false;
        enemy.WasKilledByPlayer = killedByPlayer;
        enemy.CurrentHull = 0;
        enemy.HasTarget = false;

        _encounterService.RegisterEnemyDestroyed(killedByPlayer);

        _eventBus.Publish(new SystemEnemyDestroyedEvent(
            enemy.RuntimeEnemyId,
            enemy.EnemyConfigId,
            enemy.SystemId,
            killedByPlayer));
    }

    private bool ResolvePlayerTargetService()
    {
        if (_playerTargetService != null)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
            return false;

        return Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _playerTargetService);
    }
}