public sealed class
    InteractionAvailabilityChangedEvent2A
{
    public InteractionDescriptor2A Descriptor
    {
        get;
    }

    public bool CanInteract { get; }

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