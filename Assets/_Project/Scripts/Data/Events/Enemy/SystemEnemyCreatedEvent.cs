using UnityEngine;

public readonly struct SystemEnemyCreatedEvent
    {
        public readonly string RuntimeEnemyId;
        public readonly string EnemyConfigId;
        public readonly string SystemId;
        public readonly Vector3 Position;

        public SystemEnemyCreatedEvent(
            string runtimeEnemyId,
            string enemyConfigId,
            string systemId,
            Vector3 position)
        {
            RuntimeEnemyId = runtimeEnemyId;
            EnemyConfigId = enemyConfigId;
            SystemId = systemId;
            Position = position;
        }
    }