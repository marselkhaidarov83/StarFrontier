/// <summary>
/// Нанесение урона NPC; сигнал обновления его состояния.
/// </summary>
public readonly struct SystemNpcDamagedEvent
{
    /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
    public readonly string RuntimeNpcId;
    /// <summary>Нанесённый или полученный урон.</summary>
    public readonly int Damage;
    /// <summary>Прочность корпуса после изменения.</summary>
    public readonly int CurrentHull;
    /// <summary>Значение щита после изменения.</summary>
    public readonly int CurrentShield;

    public SystemNpcDamagedEvent(
        string runtimeNpcId,
        int damage,
        int currentHull,
        int currentShield)
    {
        RuntimeNpcId = runtimeNpcId;
        Damage = damage;
        CurrentHull = currentHull;
        CurrentShield = currentShield;
    }
}