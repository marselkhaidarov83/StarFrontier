using UnityEngine;

    public sealed class ProjectileView : MonoBehaviour
    {
        private int _damage;
        private bool _fromPlayer;

        public void Init(int damage, bool fromPlayer)
        {
            _damage = damage;
            _fromPlayer = fromPlayer;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_fromPlayer)
            {
                HandleFriendlyProjectileHit(other);
                return;
            }

            HandleEnemyProjectileHit(other);
        }

        private void HandleFriendlyProjectileHit(Collider2D other)
        {
            EnemySystemMapEntity enemy = other.GetComponentInParent<EnemySystemMapEntity>();

            if (enemy == null)
                return;

            if (!enemy.IsBound)
                return;

            Debug.Log($"[ProjectileView] Applying damage to enemy: {enemy.RuntimeEnemyId}");

            enemy.ApplyDamage(_damage, true);
            Destroy(gameObject);
        }

        private void HandleEnemyProjectileHit(Collider2D other)
        {
            PlayerCombatEntity player = other.GetComponentInParent<PlayerCombatEntity>();

            if (player != null)
            {
                Debug.Log($"[ProjectileView] Applying damage to player: {_damage}");

                player.ApplyDamage(_damage);
                Destroy(gameObject);
                return;
            }

            AllySystemMapEntity ally = other.GetComponentInParent<AllySystemMapEntity>();

            if (ally == null)
                return;

            if (!ally.IsBound)
                return;

            Debug.Log($"[ProjectileView] Applying damage to ally: {ally.RuntimeAllyId}");

            ally.ApplyDamage(_damage);
            Destroy(gameObject);
        }
    }