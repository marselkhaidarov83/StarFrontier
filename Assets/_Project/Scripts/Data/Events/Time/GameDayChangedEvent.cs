/// <summary>
/// Наступление нового игрового дня; сигнал для симуляции и NPC.
/// </summary>
public readonly struct GameDayChangedEvent
{
    /// <summary>Номер предыдущего игрового дня.</summary>
    public readonly int PreviousDay;
    /// <summary>Номер наступившего игрового дня.</summary>
    public readonly int CurrentDay;

    public GameDayChangedEvent(int previousDay, int currentDay)
    {
        PreviousDay = previousDay;
        CurrentDay = currentDay;
    }
}