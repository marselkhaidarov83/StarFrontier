public readonly struct DiamondsChangedEvent
{
    public readonly int CurrentDiamonds;
    public DiamondsChangedEvent(int currentDiamonds) => CurrentDiamonds = currentDiamonds;
}
