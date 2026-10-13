/// <summary>
/// Выдача игроку награды; сигнал обновления связанных игровых систем.
/// </summary>
public readonly struct RewardGrantedEvent
{
    /// <summary>Результат выполненного действия.</summary>
    public readonly RewardGrantResult Result;

    public RewardGrantedEvent(RewardGrantResult result)
    {
        Result = result;
    }
}