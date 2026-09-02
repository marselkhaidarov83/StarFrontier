public readonly struct SystemNpcFollowModeChangedEvent2A
{
    public readonly int ModeIndex;
    public readonly int ModeNumber;
    public readonly float FollowDistance;

    public SystemNpcFollowModeChangedEvent2A(
        int modeIndex,
        float followDistance)
    {
        ModeIndex = modeIndex;
        ModeNumber = modeIndex + 1;
        FollowDistance = followDistance;
    }
}