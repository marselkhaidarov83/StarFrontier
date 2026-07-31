/// <summary>
/// Сообщает представлению, что текущая цель изменилась.
///
/// Событие не хранит gameplay-состояние.
/// Источником состояния остаётся TargetingRuntimeState.
/// </summary>
public sealed class TargetChangedEvent2A
{
    public string TargetId { get; }

    public SystemGameplayTargetType TargetType { get; }

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