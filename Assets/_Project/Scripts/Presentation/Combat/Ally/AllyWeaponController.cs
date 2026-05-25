using UnityEngine;

    public sealed class AllyWeaponController : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private ProjectileView projectilePrefab;

        [Header("Fire")]
        [SerializeField] private float fireInterval = 2f;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float projectileLifetime = 8f;
        [SerializeField] private int damage = 8;
        [SerializeField] private float spawnOffset = 0.5f;
        [SerializeField] private float attackRange = 6f;

        private float _timer;

        private AllySystemMapEntity _allyEntity;
        private ISystemEnemyService _enemyService;

        private void Awake()
        {
            _allyEntity = GetComponent<AllySystemMapEntity>();
            _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        }

        private void Update()
        {
            if (_allyEntity == null || !_allyEntity.IsBound)
                return;

            _timer -= Time.deltaTime;

            if (_timer <= 0f)
            {
                TryFireAtNearestEnemy();
                _timer = fireInterval;
            }
        }

        private void TryFireAtNearestEnemy()
        {
            EnemySystemMapEntity targetView = FindNearestEnemyView();

            if (targetView == null)
                return;

            FireAtTarget(targetView.transform);
        }

        private EnemySystemMapEntity FindNearestEnemyView()
        {
            EnemySystemMapEntity[] enemies = FindObjectsOfType<EnemySystemMapEntity>();

            EnemySystemMapEntity best = null;
            float bestDistance = float.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsBound)
                    continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                if (distance > attackRange)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }

            return best;
        }

        private void FireAtTarget(Transform target)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning("[AllyWeaponController] Projectile prefab is missing.");
                return;
            }

            Vector3 startPosition = transform.position;
            startPosition.z = 0f;

            Vector3 directionToTarget = target.position - startPosition;
            directionToTarget.z = 0f;

            if (directionToTarget.sqrMagnitude <= 0.0001f)
                return;

            startPosition += directionToTarget.normalized * spawnOffset;

            ProjectileView projectile = Instantiate(
                projectilePrefab,
                startPosition,
                Quaternion.identity
            );

            ProjectileMover mover = projectile.GetComponent<ProjectileMover>();

            if (mover == null)
            {
                Debug.LogError("[AllyWeaponController] ProjectileMover is missing on projectile prefab.");
                Destroy(projectile.gameObject);
                return;
            }

            mover.InitToTarget(
                target,
                projectileSpeed,
                projectileLifetime
            );

            projectile.Init(damage, true);
        }
    }