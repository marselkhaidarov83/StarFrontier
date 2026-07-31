public interface IInteractionHandler2A
{
    bool CanHandle(
        SystemGameplayTargetType targetType);

    InteractionDescriptor2A CreateDescriptor(
        string targetId,
        SystemGameplayTargetType targetType);

    InteractionFailReason2A GetFailReason(
        InteractionDescriptor2A descriptor);

    InteractionExecutionResult2A Execute(
        InteractionDescriptor2A descriptor);
}