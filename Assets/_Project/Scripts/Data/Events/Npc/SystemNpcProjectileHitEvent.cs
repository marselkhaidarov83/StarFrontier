using UnityEngine;

public readonly struct SystemNpcProjectileHitEvent
    {
        public readonly string ShooterNpcId;
        public readonly string TargetNpcId;
        public readonly int Damage;
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