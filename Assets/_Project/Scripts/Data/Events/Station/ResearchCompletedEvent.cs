public sealed class ResearchCompletedEvent
{
    public readonly ResearchUpgrade Upgrade;
    public readonly int Level;
    public ResearchCompletedEvent(ResearchUpgrade upgrade, int level) { Upgrade = upgrade; Level = level; }
}
