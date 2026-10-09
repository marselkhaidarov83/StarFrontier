/// <summary>
/// Изменение доступности взаимодействия с выбранным игровым объектом.
/// </summary>
public sealed class
    InteractionAvailabilityChangedEvent2A
{
    /// <summary>Описатель выбранного игрового объекта.</summary>
    public InteractionDescriptor2A Descriptor
    {
        get;
    }

    /// <summary>Признак доступности взаимодействия.</summary>
    public bool CanInteract { get; }

    /// <summary>Причина неуспешного выполнения действия.</summary>
    public InteractionFailReason2A FailReason
    {
        get;
    }

    public InteractionAvailabilityChangedEvent2A(
        InteractionDescriptor2A descriptor,
        bool canInteract,
        InteractionFailReason2A failReason)
    {
        Descriptor = descriptor;
        CanInteract = canInteract;
        FailReason = failReason;
    }
}