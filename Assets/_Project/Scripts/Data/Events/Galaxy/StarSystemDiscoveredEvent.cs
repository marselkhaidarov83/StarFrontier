public readonly struct StarSystemDiscoveredEvent
{
    public readonly string SystemId;

    public StarSystemDiscoveredEvent(string systemId)
    {
        SystemId = systemId;
    }
}