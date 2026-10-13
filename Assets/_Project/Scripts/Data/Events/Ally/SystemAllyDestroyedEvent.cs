/// <summary>
/// Уничтожение союзного корабля; сигнал очистки объекта и реакции остальных систем.
/// </summary>
public readonly struct SystemAllyDestroyedEvent
    {
        /// <summary>Идентификатор союзника в runtime-состоянии.</summary>
        public readonly string RuntimeAllyId;
        /// <summary>Идентификатор конфига союзника.</summary>
        public readonly string AllyConfigId;
        /// <summary>Идентификатор звёздной системы.</summary>
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