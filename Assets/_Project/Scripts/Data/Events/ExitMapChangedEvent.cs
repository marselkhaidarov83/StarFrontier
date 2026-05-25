public sealed class ExitMapChangedEvent
{
    public StarSystemLink StarSystemLink;
    public ExitMapChangedEvent(StarSystemLink starSystemLink)
    {
        StarSystemLink = starSystemLink;
    }
}