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

/// <summary>Режим, явно выбранный игроком кнопкой Игра/Пауза.</summary>
public readonly struct GameTimePauseModeSelectedEvent
{
    public readonly bool IsPaused;

    public GameTimePauseModeSelectedEvent(bool isPaused)
    {
        IsPaused = isPaused;
    }
}
