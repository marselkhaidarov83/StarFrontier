/// <summary>
/// Результат попытки выполнения действия взаимодействия; обновление статуса в UI.
/// </summary>
public sealed class InteractionExecutionEvent2A
{
    /// <summary>Результат выполненного действия.</summary>
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