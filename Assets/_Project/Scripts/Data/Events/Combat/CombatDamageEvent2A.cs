using UnityEngine;

/// <summary>
/// Результат нанесения урона в боевом контуре: цель, величина урона и состояние щита/корпуса.
/// </summary>
public struct CombatDamageEvent2A
{
    /// <summary>Идентификатор цели события.</summary>
    public string TargetId { get; }
    /// <summary>Признак, что цель урона — корабль игрока.</summary>
    public bool IsPlayerTarget { get; }
    /// <summary>Нанесённый или полученный урон.</summary>
    public int Damage { get; }
    /// <summary>Значение щита после изменения.</summary>
    public int CurrentShield { get; }
    /// <summary>Прочность корпуса после изменения.</summary>
    public int CurrentHull { get; }

    public CombatDamageEvent2A(
        string targetId,
        bool isPlayerTarget,
        int damage,
        int currentShield,
        int currentHull)
    {
        TargetId = targetId;
        IsPlayerTarget = isPlayerTarget;
        Damage = damage;
        CurrentShield = currentShield;
        CurrentHull = currentHull;
    }
}
