using UnityEngine;

/// <summary>
/// Изменение позиции союзного корабля в звёздной системе.
/// </summary>
public readonly struct SystemAllyPositionChangedEvent
    {
        /// <summary>Идентификатор союзника в runtime-состоянии.</summary>
        public readonly string RuntimeAllyId;
        /// <summary>Позиция объекта в игровом пространстве.</summary>
        public readonly Vector3 Position;

        public SystemAllyPositionChangedEvent(string runtimeAllyId, Vector3 position)
        {
            RuntimeAllyId = runtimeAllyId;
            Position = position;
        }
    }