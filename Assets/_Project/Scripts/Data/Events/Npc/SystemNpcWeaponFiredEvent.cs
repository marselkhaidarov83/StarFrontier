using UnityEngine;

public readonly struct SystemNpcWeaponFiredEvent
    {
        public readonly string ShooterNpcId;
        public readonly string TargetNpcId;
        public readonly string WeaponConfigId;
        public readonly Vector3 StartPosition;
        public readonly Vector3 TargetPosition;
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