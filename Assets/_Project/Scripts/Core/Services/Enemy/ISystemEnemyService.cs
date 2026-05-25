using System.Collections.Generic;
using UnityEngine;

    public interface ISystemEnemyService
    {
        IReadOnlyList<SystemEnemyRuntimeState> Enemies { get; }

        SystemEnemyRuntimeState CreateEnemy(
            EnemyConfig enemyConfig,
            string systemId,
            Vector3 position);

        void RestoreEnemy(SystemEnemyRuntimeState enemy);
        void RestoreEnemies(IEnumerable<SystemEnemyRuntimeState> enemies);

        bool TryGetEnemy(string runtimeEnemyId, out SystemEnemyRuntimeState enemy);

        IReadOnlyList<SystemEnemyRuntimeState> GetAliveEnemiesInSystem(string systemId);

        void UpdateEnemyPosition(string runtimeEnemyId, Vector3 position);

        void ApplyDamage(
            string runtimeEnemyId,
            int damage,
            bool fromPlayer);

        void ClearSystemEnemies(string systemId);
        void ClearAll();
    }
