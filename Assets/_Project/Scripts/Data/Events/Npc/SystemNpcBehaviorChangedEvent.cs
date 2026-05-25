public readonly struct SystemNpcBehaviorChangedEvent
{
    public readonly string RuntimeNpcId;
    public readonly SystemNpcBehaviorType BehaviorType;

    public SystemNpcBehaviorChangedEvent(
        string runtimeNpcId,
        SystemNpcBehaviorType behaviorType)
    {
        RuntimeNpcId = runtimeNpcId;
        BehaviorType = behaviorType;
    }
}