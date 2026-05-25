public readonly struct SystemAllyDestroyedEvent
    {
        public readonly string RuntimeAllyId;
        public readonly string AllyConfigId;
        public readonly string SystemId;

        public SystemAllyDestroyedEvent(
            string runtimeAllyId,
            string allyConfigId,
            string systemId)
        {
            RuntimeAllyId = runtimeAllyId;
            AllyConfigId = allyConfigId;
            SystemId = systemId;
        }
    }