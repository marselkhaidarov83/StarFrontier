public readonly struct SystemNpcCreatedEvent
    {
        public readonly string RuntimeNpcId;
        public readonly SystemNpcType NpcType;
        public readonly string ConfigId;
        public readonly string SystemId;

        public SystemNpcCreatedEvent(
            string runtimeNpcId,
            SystemNpcType npcType,
            string configId,
            string systemId)
        {
            RuntimeNpcId = runtimeNpcId;
            NpcType = npcType;
            ConfigId = configId;
            SystemId = systemId;
        }
    }