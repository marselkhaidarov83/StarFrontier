using UnityEngine;

    public sealed class ProjectileMover : MonoBehaviour
    {
        [SerializeField] private float hitDistance = 0.2f;

        private Transform _target;
        private Vector3 _direction;
        private float _speed;
        private float _lifetime;
        private bool _hasTarget;

        public void InitToTarget(Transform target, float speed, float lifetime)
        {
            _target = target;
            _speed = Mathf.Max(0.01f, speed);
            _lifetime = Mathf.Max(0.1f, lifetime);
            _hasTarget = target != null;
        }

        public void InitDirection(Vector3 direction, float speed, float lifetime)
        {
            _direction = direction.normalized;
            _speed = Mathf.Max(0.01f, speed);
            _lifetime = Mathf.Max(0.1f, lifetime);
            _hasTarget = false;
        }

        private void Update()
        {
            _lifetime -= Time.deltaTime;

            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_hasTarget)
                MoveToTarget();
            else
                MoveByDirection();
        }

        private void MoveToTarget()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targetPosition = _target.position;
            targetPosition.z = transform.position.z;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                _speed * Time.deltaTime
            );

            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance <= hitDistance)
            {
                // физический trigger может не успеть сработать,
                // поэтому collision обработает ProjectileView через collider,
                // а если не обработает — projectile хотя бы дошел до цели.
            }
        }

        private void MoveByDirection()
        {
            if (_direction.sqrMagnitude <= 0.0001f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += _direction * _speed * Time.deltaTime;
        }        
    }