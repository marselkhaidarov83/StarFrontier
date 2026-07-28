/// <summary>
/// Описание доступного действия.
///
/// Не содержит GameObject, Transform,
/// Collider или других ссылок на сцену.
/// </summary>
public sealed class InteractionDescriptor2A
{
    public string TargetId { get; }

    public SystemGameplayTargetType TargetType
    {
        get;
    }

    public string ActionId { get; }

    public string ActionDisplayName { get; }

    public string IconKey { get; }

    public InteractionDescriptor2A(
        string targetId,
        SystemGameplayTargetType targetType,
        string actionId,
        string actionDisplayName,
        string iconKey)
    {
        TargetId =
            targetId ?? string.Empty;

        TargetType =
            targetType;

        ActionId =
            actionId ?? string.Empty;

        ActionDisplayName =
            actionDisplayName ?? string.Empty;

        IconKey =
            iconKey ?? string.Empty;
    }
}