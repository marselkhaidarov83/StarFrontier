public readonly struct GameTimePauseChangedEvent
    {
        public readonly bool IsPaused;

        public GameTimePauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }