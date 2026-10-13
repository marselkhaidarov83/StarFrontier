/// <summary>
/// Изменение состояния паузы игрового времени.
/// </summary>
public readonly struct GameTimePauseChangedEvent
    {
        /// <summary>Признак приостановки игрового времени.</summary>
        public readonly bool IsPaused;

        public GameTimePauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }