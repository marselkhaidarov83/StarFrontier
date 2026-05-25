public sealed class SystemEnteredEvent
{
    public string SystemId { get; }

    public SystemEnteredEvent(string systemId)
    {
        SystemId = systemId;
    }
}