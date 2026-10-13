/// <summary>
/// Запрос открытия панели информации о выбранной цели.
/// </summary>
public readonly struct SystemSelectedTargetInfoPanelRequestedEvent2A
{
    /// <summary>Идентификатор цели события.</summary>
    public readonly string TargetId;
    /// <summary>Тип цели, к которой относится событие.</summary>
    public readonly SystemGameplayTargetType TargetType;

    public SystemSelectedTargetInfoPanelRequestedEvent2A(
        string targetId,
        SystemGameplayTargetType targetType)
    {
        TargetId = targetId ?? string.Empty;
        TargetType = targetType;
    }
}
