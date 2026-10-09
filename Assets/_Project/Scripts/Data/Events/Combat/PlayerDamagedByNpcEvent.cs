/// <summary>
/// NPC нанёс урон игроку; сообщение о воздействии на корабль игрока.
/// </summary>
public readonly struct PlayerDamagedByNpcEvent
{
    /// <summary>Нанесённый или полученный урон.</summary>
    public readonly int Damage;
    /// <summary>Значение щита после изменения.</summary>
    public readonly int CurrentShield;
    /// <summary>Прочность корпуса после изменения.</summary>
    public readonly int CurrentHull;

    public PlayerDamagedByNpcEvent(
        int damage,
        int currentShield,
        int currentHull)
    {
        Damage = damage;
        CurrentShield = currentShield;
        CurrentHull = currentHull;
    }
}