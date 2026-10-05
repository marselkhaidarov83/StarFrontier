public readonly struct SystemSelectedTargetInfoPanelRequestedEvent2A
{
    public readonly string TargetId;
    public readonly SystemGameplayTargetType TargetType;

    public SystemSelectedTargetInfoPanelRequestedEvent2A(
        string targetId,
        SystemGameplayTargetType targetType)
    {
        TargetId = targetId ?? string.Empty;
        TargetType = targetType;
    }
}
