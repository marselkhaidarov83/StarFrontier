public sealed class CombatEndedEvent
{
    public bool Victory { get; }

        public CombatEndedEvent(bool victory)
        {
            Victory = victory;
        }
}