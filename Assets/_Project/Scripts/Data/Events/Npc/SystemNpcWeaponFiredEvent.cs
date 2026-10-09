using UnityEngine;

/// <summary>
/// NPC произвёл выстрел из оружия; сигнал для боевой/визуальной подсистемы.
/// </summary>
public readonly struct SystemNpcWeaponFiredEvent
    {
        /// <summary>Идентификатор NPC, выполнившего выстрел.</summary>
        public readonly string ShooterNpcId;
        /// <summary>Идентификатор NPC, выбранного целью.</summary>
        public readonly string TargetNpcId;
        /// <summary>Идентификатор конфига оружия.</summary>
        public readonly string WeaponConfigId;
        /// <summary>Начальная позиция воздействия или снаряда.</summary>
        public readonly Vector3 StartPosition;
        /// <summary>Позиция назначенной цели.</summary>
        public readonly Vector3 TargetPosition;
        /// <summary>Нанесённый или полученный урон.</summary>
        public readonly int Damage;

        public SystemNpcWeaponFiredEvent(
            string shooterNpcId,
            string targetNpcId,
            string weaponConfigId,
            Vector3 startPosition,
            Vector3 targetPosition,
            int damage)
        {
            ShooterNpcId = shooterNpcId;
            TargetNpcId = targetNpcId;
            WeaponConfigId = weaponConfigId;
            StartPosition = startPosition;
            TargetPosition = targetPosition;
            Damage = damage;
        }
    }