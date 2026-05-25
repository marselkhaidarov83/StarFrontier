using UnityEngine;

public readonly struct SystemAllyPositionChangedEvent
    {
        public readonly string RuntimeAllyId;
        public readonly Vector3 Position;

        public SystemAllyPositionChangedEvent(string runtimeAllyId, Vector3 position)
        {
            RuntimeAllyId = runtimeAllyId;
            Position = position;
        }
    }