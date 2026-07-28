public interface IInteractionService2A :
    ITickable
{
    InteractionRuntimeState State { get; }

    bool CanInteract();

    InteractionFailReason2A GetFailReason();

    InteractionExecutionResult2A Execute();

    void RefreshAvailability();
}