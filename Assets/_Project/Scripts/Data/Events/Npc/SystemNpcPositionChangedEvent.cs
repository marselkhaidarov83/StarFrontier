using UnityEngine;

public readonly struct SystemNpcPositionChangedEvent
    {
        public readonly string RuntimeNpcId;
        public readonly string SystemId;
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