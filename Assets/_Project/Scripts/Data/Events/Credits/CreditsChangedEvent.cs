/// <summary>
/// Изменение количества кредитов игрока; сигнал обновления экономики и UI.
/// </summary>
public sealed class CreditsChangedEvent
{
    /// <summary>Текущее количество кредитов.</summary>
    public readonly int CurrentCredits;

    public CreditsChangedEvent(int currentCredits)
    {
        CurrentCredits = currentCredits;
    }
}