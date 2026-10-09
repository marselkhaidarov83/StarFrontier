using UnityEngine;

/// <summary>
/// Создание вражеского корабля в текущей системе.
/// </summary>
public readonly struct SystemEnemyCreatedEvent
    {
        /// <summary>Идентификатор врага в runtime-состоянии.</summary>
        public readonly string RuntimeEnemyId;
        /// <summary>Идентификатор конфига врага.</summary>
        public readonly string EnemyConfigId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Позиция объекта в игровом пространстве.</summary>
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