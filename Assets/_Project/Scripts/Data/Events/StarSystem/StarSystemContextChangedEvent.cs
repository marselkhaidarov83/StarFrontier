public readonly struct StarSystemContextChangedEvent
{
    public readonly string SystemId;
    public readonly int DevelopmentLevel;
    public readonly int DangerLevel;
    public readonly int Stability;
    public readonly bool IsDiscovered;
    public readonly bool IsVisited;

    public StarSystemContextChangedEvent(
        string systemId,
        int developmentLevel,
        int dangerLevel,
        int stability,
        bool isDiscovered,
        bool isVisited)
    {
        SystemId = systemId;
        DevelopmentLevel = developmentLevel;
        DangerLevel = dangerLevel;
        Stability = stability;
        IsDiscovered = isDiscovered;
        IsVisited = isVisited;
    }
}