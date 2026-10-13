using UnityEngine;

/// <summary>
/// Запрос показа всплывающего значения полученного урона в бою.
/// </summary>
public readonly struct CombatDamagePopupEvent2A
{
    /// <summary>Тип цели, к которой относится событие.</summary>
    public readonly CombatTargetType TargetType;
    /// <summary>Идентификатор NPC, выбранного целью.</summary>
    public readonly string TargetNpcId;
    /// <summary>Нанесённый или полученный урон.</summary>
    public readonly int Damage;
    /// <summary>Позиция назначенной цели.</summary>
    public readonly Vector3 TargetPosition;
    /// <summary>Позиция источника нанесённого урона.</summary>
    public readonly Vector3 DamageSourcePosition;
    /// <summary>Направление показа визуального индикатора.</summary>
    public readonly Vector3 PopupDirection;

    public CombatDamagePopupEvent2A(
        CombatTargetType targetType,
        string targetNpcId,
        int damage,
        Vector3 targetPosition,
        Vector3 damageSourcePosition,
        Vector3 popupDirection)
    {
        TargetType = targetType;
        TargetNpcId = targetNpcId ?? string.Empty;
        Damage = damage;
        TargetPosition = targetPosition;
        DamageSourcePosition = damageSourcePosition;
        PopupDirection = popupDirection;
    }
}