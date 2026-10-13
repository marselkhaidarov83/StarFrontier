using UnityEngine;

/// <summary>
/// Изменение координат NPC в системе.
/// </summary>
public readonly struct SystemNpcPositionChangedEvent
    {
        /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
        public readonly string RuntimeNpcId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Позиция объекта в игровом пространстве.</summary>
        public readonly Vector3 Position;

        public SystemNpcPositionChangedEvent(
            string runtimeNpcId,
            string systemId,
            Vector3 position)
        {
            RuntimeNpcId = runtimeNpcId;
            SystemId = systemId;
            Position = position;
        }
    }