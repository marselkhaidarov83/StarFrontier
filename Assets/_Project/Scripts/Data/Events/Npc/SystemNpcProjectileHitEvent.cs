using UnityEngine;

/// <summary>
/// Факт попадания снаряда NPC в боевой объект.
/// </summary>
public readonly struct SystemNpcProjectileHitEvent
    {
        /// <summary>Идентификатор NPC, выполнившего выстрел.</summary>
        public readonly string ShooterNpcId;
        /// <summary>Идентификатор NPC, выбранного целью.</summary>
        public readonly string TargetNpcId;
        /// <summary>Нанесённый или полученный урон.</summary>
        public readonly int Damage;
        /// <summary>Позиция попадания или завершения полёта снаряда.</summary>
        public readonly Vector3 HitPosition;

        public SystemNpcProjectileHitEvent(
            string shooterNpcId,
            string targetNpcId,
            int damage,
            Vector3 hitPosition)
        {
            ShooterNpcId = shooterNpcId;
            TargetNpcId = targetNpcId;
            Damage = damage;
            HitPosition = hitPosition;
        }
    }