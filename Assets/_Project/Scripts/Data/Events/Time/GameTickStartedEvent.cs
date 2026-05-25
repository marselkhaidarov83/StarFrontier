public readonly struct GameTickStartedEvent
{
    public readonly int CurrentTick;

    public GameTickStartedEvent(int currentTick)
    {
        CurrentTick = currentTick;
    }
}