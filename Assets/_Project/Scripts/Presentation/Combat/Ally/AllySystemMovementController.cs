using UnityEngine;

    [RequireComponent(typeof(SystemMapMover))]
    [RequireComponent(typeof(AllySystemMapEntity))]
    public sealed class AllySystemMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float patrolRadius = 2f;
        [SerializeField] private float pointReachedDistance = 0.2f;

        private SystemMapMover _mover;
        private AllySystemMapEntity _allyView;

        private Vector3 _spawnPosition;
        private Vector3 _currentDestination;
        private bool _hasDestination;

        private void Awake()
        {
            _mover = GetComponent<SystemMapMover>();
            _allyView = GetComponent<AllySystemMapEntity>();
        }

        private void Start()
        {
            _spawnPosition = transform.position;
            PickNewPatrolPoint();
        }

        private void Update()
        {
            if (_allyView == null || !_allyView.IsBound)
                return;

            TickPatrol();
        }

        public void ApplyRuntimeConfig(SystemAllyRuntimeState runtimeAlly)
        {
            if (runtimeAlly == null || runtimeAlly.AllyConfig == null)
                return;

            _mover.SetSpeed(runtimeAlly.AllyConfig.BaseSpeed);
            _mover.SetStopDistance(pointReachedDistance);
        }

        private void TickPatrol()
        {
            if (!_hasDestination)
                PickNewPatrolPoint();

            bool reached = _mover.MoveTowards(_currentDestination, Time.deltaTime);

            if (reached)
                PickNewPatrolPoint();
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
    }