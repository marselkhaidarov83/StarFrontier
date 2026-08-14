using System;
using UnityEngine;

[Serializable]
public sealed class SystemEnemyRuntimeState
{
    public string RuntimeEnemyId;

    public string EnemyConfigId;
    public EnemyConfig EnemyConfig;

    public string SystemId;
    public Vector3 Position;

    public int CurrentHull;
    public int CurrentShield;
    public int CurrentEnergy;

    public int Speed;

    public bool IsAlive;
    public bool WasKilledByPlayer;

    public string CurrentTargetId;
    public bool HasTarget;
    public float AttackCooldownSeconds;
    public float AttackTimerSeconds;
    public int BaseAttackDamage;
    public float AttackRange;

    public bool CanAttack(float distanceToPlayer)
    {
        return IsAlive &&
               HasTarget &&
               AttackTimerSeconds <= 0f &&
               distanceToPlayer <= AttackRange &&
               BaseAttackDamage > 0;
    }

    public void ResetAttackTimer()
    {
        AttackTimerSeconds = Mathf.Max(0.1f, AttackCooldownSeconds);
    }

    public void TickAttackTimer(float deltaTime)
    {
        if (AttackTimerSeconds <= 0f)
            return;

        AttackTimerSeconds -= Mathf.Max(0f, deltaTime);

        if (AttackTimerSeconds < 0f)
            AttackTimerSeconds = 0f;
    }
}
