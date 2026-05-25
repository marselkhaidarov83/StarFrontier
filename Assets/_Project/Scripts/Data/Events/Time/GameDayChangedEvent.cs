public readonly struct GameDayChangedEvent
{
    public readonly int PreviousDay;
    public readonly int CurrentDay;

    public GameDayChangedEvent(int previousDay, int currentDay)
    {
        PreviousDay = previousDay;
        CurrentDay = currentDay;
    }
}