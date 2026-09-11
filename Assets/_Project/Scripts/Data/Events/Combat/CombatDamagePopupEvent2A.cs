using UnityEngine;

public readonly struct CombatDamagePopupEvent2A
{
    public readonly CombatTargetType TargetType;
    public readonly string TargetNpcId;
    public readonly int Damage;
    public readonly Vector3 TargetPosition;
    public readonly Vector3 DamageSourcePosition;
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