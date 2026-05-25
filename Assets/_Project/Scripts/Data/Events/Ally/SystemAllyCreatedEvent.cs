using UnityEngine;

public readonly struct SystemAllyCreatedEvent
    {
        public readonly string RuntimeAllyId;
        public readonly string AllyConfigId;
        public readonly string SystemId;
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