/// <summary>
/// Сообщает представлению, что текущая цель изменилась.
///
/// Событие не хранит gameplay-состояние.
/// Источником состояния остаётся TargetingRuntimeState.
/// </summary>
public sealed class TargetChangedEvent2A
{
    /// <summary>Идентификатор цели события.</summary>
    public string TargetId { get; }

    /// <summary>Тип цели, к которой относится событие.</summary>
    public SystemGameplayTargetType TargetType { get; }

    /// <summary>Признак наличия текущей выбранной цели.</summary>
    public bool HasTarget { get; }

    public TargetChangedEvent2A(
        string targetId,
        SystemGameplayTargetType targetType,
        bool hasTarget)
    {
        TargetId =
            targetId ?? string.Empty;

        TargetType =
            targetType;

        HasTarget =
            hasTarget;
    }
}