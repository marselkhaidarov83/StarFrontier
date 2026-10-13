/// <summary>
/// Появление NPC в системе; содержит runtime-ID, тип, ID конфига и систему.
/// </summary>
public readonly struct SystemNpcCreatedEvent
    {
        /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
        public readonly string RuntimeNpcId;
        /// <summary>Тип NPC.</summary>
        public readonly SystemNpcType NpcType;
        /// <summary>Идентификатор связанного игрового конфига.</summary>
        public readonly string ConfigId;
        /// <summary>Идентификатор звёздной системы.</summary>
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