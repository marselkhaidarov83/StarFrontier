public sealed class InteractionExecutionEvent2A
{
    public InteractionExecutionResult2A Result
    {
        get;
    }

    public InteractionExecutionEvent2A(
        InteractionExecutionResult2A result)
    {
        Result = result;
    }
}