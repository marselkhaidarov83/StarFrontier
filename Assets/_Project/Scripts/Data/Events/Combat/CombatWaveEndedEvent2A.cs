/// <summary>
/// Завершение визуальной волны оружия; передаёт идентификатор волны для удаления эффекта.
/// </summary>
public readonly struct CombatWaveEndedEvent2A
{
    /// <summary>Идентификатор волнового эффекта.</summary>
    public readonly string WaveId;

    public CombatWaveEndedEvent2A(string waveId)
    {
        WaveId = waveId;
    }
}