public sealed class CreditsChangedEvent
{
    public readonly int CurrentCredits;

    public CreditsChangedEvent(int currentCredits)
    {
        CurrentCredits = currentCredits;
    }
}