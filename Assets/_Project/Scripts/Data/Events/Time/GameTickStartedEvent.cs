/// <summary>
/// Начало игрового тика; по контракту содержит номер текущего тика.
/// </summary>
public readonly struct GameTickStartedEvent
{
    /// <summary>Номер текущего игрового тика.</summary>
    public readonly int CurrentTick;

    public GameTickStartedEvent(int currentTick)
    {
        CurrentTick = currentTick;
    }
}