using UnityEngine;

/// <summary>
/// Создание союзного корабля в системе; сигнал появления для связанных систем и представления.
/// </summary>
public readonly struct SystemAllyCreatedEvent
    {
        /// <summary>Идентификатор союзника в runtime-состоянии.</summary>
        public readonly string RuntimeAllyId;
        /// <summary>Идентификатор конфига союзника.</summary>
        public readonly string AllyConfigId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Позиция объекта в игровом пространстве.</summary>
        public readonly Vector3 Position;

        public SystemAllyCreatedEvent(
            string runtimeAllyId,
            string allyConfigId,
            string systemId,
            Vector3 position)
        {
            RuntimeAllyId = runtimeAllyId;
            AllyConfigId = allyConfigId;
            SystemId = systemId;
            Position = position;
        }
    }