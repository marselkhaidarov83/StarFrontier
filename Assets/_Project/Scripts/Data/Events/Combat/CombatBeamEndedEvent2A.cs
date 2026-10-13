/// <summary>
/// Завершение визуального лучевого выстрела; прекращение соответствующего эффекта.
/// </summary>
public readonly struct CombatBeamEndedEvent2A
{
    /// <summary>Идентификатор активного лучевого эффекта.</summary>
    public readonly string BeamId;

    public CombatBeamEndedEvent2A(string beamId)
    {
        BeamId = beamId;
    }
}