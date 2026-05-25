public sealed class CurrentSystemEnteredEvent
{
    public string SystemId { get; }

    public CurrentSystemEnteredEvent(string systemId)
    {
        SystemId = systemId;
    }
}