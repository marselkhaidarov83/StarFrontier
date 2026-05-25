using UnityEngine;

    public sealed class SystemMapMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 2f;
        [SerializeField] private float stopDistance = 0.15f;

        public float Speed => speed;
        public float StopDistance => stopDistance;

        public void SetSpeed(float value)
        {
            speed = Mathf.Max(0f, value);
        }

        public void SetStopDistance(float value)
        {
            stopDistance = Mathf.Max(0f, value);
        }

        public bool MoveTowards(Vector3 targetPosition, float deltaTime)
        {
            Vector3 currentPosition = transform.position;
            Vector3 direction = targetPosition - currentPosition;
            direction.z = 0f;

            float distance = direction.magnitude;

            if (distance <= stopDistance)
                return true;

            Vector3 nextPosition = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                speed * deltaTime
            );

            nextPosition.z = currentPosition.z;
            transform.position = nextPosition;

            return false;
        }

        public void MoveDirection(Vector3 direction, float deltaTime)
        {
            direction.z = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.position += direction.normalized * speed * deltaTime;
        }
    }