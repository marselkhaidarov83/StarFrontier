public sealed class StarSystemEnteredEvent
{
    public string SystemId { get; }

    public StarSystemEnteredEvent(string systemId)
    {
        SystemId = systemId;
    }
}