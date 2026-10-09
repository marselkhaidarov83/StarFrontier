/// <summary>
/// Продвижение игрового времени на квант; используется расчётами перелётов.
/// </summary>
public readonly struct GameTimeQuantumAdvancedEvent
    {
        /// <summary>Номер наступившего игрового дня.</summary>
        public readonly int CurrentDay;

        public GameTimeQuantumAdvancedEvent(int currentDay)
        {
            CurrentDay = currentDay;
        }
    }