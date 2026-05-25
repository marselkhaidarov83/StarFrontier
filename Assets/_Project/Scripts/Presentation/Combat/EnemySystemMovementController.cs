using UnityEngine;

    [RequireComponent(typeof(SystemMapMover))]
    [RequireComponent(typeof(EnemySystemMapEntity))]
    public sealed class EnemySystemMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private EnemyMovementMode movementMode = EnemyMovementMode.PatrolAroundSpawn;
        [SerializeField] private float patrolRadius = 2f;
        [SerializeField] private float pointReachedDistance = 0.2f;

        [Header("Debug")]
        [SerializeField] private Transform debugFollowTarget;

        private SystemMapMover _mover;
        private EnemySystemMapEntity _enemyView;

        private Vector3 _spawnPosition;
        private Vector3 _currentDestination;

        private bool _hasDestination;

        private void Awake()
        {
            _mover = GetComponent<SystemMapMover>();
            _enemyView = GetComponent<EnemySystemMapEntity>();
        }

        private void Start()
        {
            _spawnPosition = transform.position;
            PickNewPatrolPoint();
        }

        private void Update()
        {
            if (_enemyView == null || !_enemyView.IsBound)
                return;

            switch (movementMode)
            {
                case EnemyMovementMode.None:
                    break;

                case EnemyMovementMode.MoveToPoint:
                    TickMoveToPoint();
                    break;

                case EnemyMovementMode.PatrolAroundSpawn:
                    TickPatrolAroundSpawn();
                    break;

                case EnemyMovementMode.FollowTarget:
                    TickFollowTarget();
                    break;
            }
        }

        public void ApplyRuntimeConfig(SystemEnemyRuntimeState runtimeEnemy)
        {
            if (runtimeEnemy == null || runtimeEnemy.EnemyConfig == null)
                return;

            _mover.SetSpeed(runtimeEnemy.EnemyConfig.BaseSpeed);
            _mover.SetStopDistance(pointReachedDistance);
        }

        public void SetMovementMode(EnemyMovementMode mode)
        {
            movementMode = mode;
        }

        public void SetDestination(Vector3 destination)
        {
            _currentDestination = destination;
            _hasDestination = true;
            movementMode = EnemyMovementMode.MoveToPoint;
        }

        public void SetFollowTarget(Transform target)
        {
            debugFollowTarget = target;
            movementMode = EnemyMovementMode.FollowTarget;
        }

        private void TickMoveToPoint()
        {
            if (!_hasDestination)
                return;

            bool reached = _mover.MoveTowards(_currentDestination, Time.deltaTime);

            if (reached)
                _hasDestination = false;
        }

        private void TickPatrolAroundSpawn()
        {
            if (!_hasDestination)
                PickNewPatrolPoint();

            bool reached = _mover.MoveTowards(_currentDestination, Time.deltaTime);

            if (reached)
                PickNewPatrolPoint();
        }

        private void TickFollowTarget()
        {
            if (debugFollowTarget == null)
                return;

            _mover.MoveTowards(debugFollowTarget.position, Time.deltaTime);
        }

        private void PickNewPatrolPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;

            _currentDestination = _spawnPosition + new Vector3(
                randomCircle.x,
                randomCircle.y,
                0f
            );

            _hasDestination = true;
        }

        [ContextMenu("Debug Mode: None")]
        private void DebugModeNone()
        {
            SetMovementMode(EnemyMovementMode.None);
        }

        [ContextMenu("Debug Mode: Patrol")]
        private void DebugModePatrol()
        {
            SetMovementMode(EnemyMovementMode.PatrolAroundSpawn);
            PickNewPatrolPoint();
        }

        [ContextMenu("Debug Move Random Point")]
        private void DebugMoveRandomPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * 5f;
            SetDestination(transform.position + new Vector3(randomCircle.x, randomCircle.y, 0f));
        }
    }