/// <summary>
/// Результат одной попытки взаимодействия.
/// </summary>
public sealed class InteractionExecutionResult2A
{
    public bool Success { get; }

    public InteractionFailReason2A FailReason
    {
        get;
    }

    public InteractionDescriptor2A Descriptor
    {
        get;
    }

    public string Message { get; }

    private InteractionExecutionResult2A(
        bool success,
        InteractionFailReason2A failReason,
        InteractionDescriptor2A descriptor,
        string message)
    {
        Success = success;
        FailReason = failReason;
        Descriptor = descriptor;
        Message = message ?? string.Empty;
    }

    public static InteractionExecutionResult2A
        Completed(
            InteractionDescriptor2A descriptor,
            string message)
    {
        return new InteractionExecutionResult2A(
            true,
            InteractionFailReason2A.None,
            descriptor,
            message);
    }

    public static InteractionExecutionResult2A
        Failed(
            InteractionFailReason2A failReason,
            InteractionDescriptor2A descriptor,
            string message)
    {
        return new InteractionExecutionResult2A(
            false,
            failReason,
            descriptor,
            message);
    }
}