using UnityEngine;

public readonly struct SystemEnemyPositionChangedEvent
    {
        public readonly string RuntimeEnemyId;
        public readonly Vector3 Position;

        public SystemEnemyPositionChangedEvent(string runtimeEnemyId, Vector3 position)
        {
            RuntimeEnemyId = runtimeEnemyId;
            Position = position;
        }
    }