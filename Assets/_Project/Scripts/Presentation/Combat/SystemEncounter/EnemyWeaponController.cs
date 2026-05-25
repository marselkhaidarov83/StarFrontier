using UnityEngine;

    public sealed class EnemyWeaponController : MonoBehaviour
    {
        [SerializeField] private Transform parentTransform;

        [Header("Projectile")]
        [SerializeField] private ProjectileView projectilePrefab;

        [Header("Fire")]
        [SerializeField] private float fireInterval = 2f;
        [SerializeField] private float projectileSpeed = 8f;
        [SerializeField] private float projectileLifetime = 8f;
        [SerializeField] private int damage = 5;
        [SerializeField] private float spawnOffset = 0.5f;

        private float _timer;
        private Transform _player;
        private EnemySystemMapEntity _entity;

        private void Awake()
        {
            _entity = GetComponent<EnemySystemMapEntity>();

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                _player = playerObject.transform;
            else
                Debug.LogError("[EnemyWeaponController] Player object with tag 'Player' not found.");
        }

        private void Update()
        {
            if (_entity == null || !_entity.IsBound)
                return;

            if (_player == null)
                return;

            _timer -= Time.deltaTime;

            if (_timer <= 0f)
            {
                FireAtPlayer();
                _timer = fireInterval;
            }
        }

        private void FireAtPlayer()
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning("[EnemyWeaponController] Projectile prefab is missing.");
                return;
            }

            Vector3 startPosition = transform.position;
            startPosition.z = 0f;

            Vector3 directionToPlayer = _player.position - startPosition;
            directionToPlayer.z = 0f;

            if (directionToPlayer.sqrMagnitude <= 0.0001f)
                return;

            startPosition += directionToPlayer.normalized * spawnOffset;

            ProjectileView projectile = Instantiate(
                projectilePrefab,
                startPosition,
                Quaternion.identity
            );

            ProjectileMover mover = projectile.GetComponent<ProjectileMover>();

            if (mover == null)
            {
                Debug.LogError("[EnemyWeaponController] ProjectileMover is missing on projectile prefab.");
                Destroy(projectile.gameObject);
                return;
            }

            mover.InitToTarget(
                _player,
                projectileSpeed,
                projectileLifetime
            );

            projectile.Init(damage, false);
        }
    }