using UnityEngine;

public sealed class SystemNpcProjectileVisual : MonoBehaviour
{
    [SerializeField] private float defaultSpeed = 12f;
    [SerializeField] private float maxLifetime = 3f;

    private Vector3 _targetPosition;
    private float _speed;
    private float _lifeTimer;
    private bool _isInitialized;

    public void Init(Vector3 startPosition, Vector3 targetPosition, float speed)
    {
        transform.position = startPosition;

        _targetPosition = targetPosition;
        _targetPosition.z = 0f;

        _speed = speed > 0f ? speed : defaultSpeed;
        _lifeTimer = 0f;
        _isInitialized = true;
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        bool destroyedByLifetime = false;
        bool destroyedByArrival = false;

        try
        {
            if (!_isInitialized)
                return;

            _lifeTimer += Time.deltaTime;

            if (_lifeTimer >= maxLifetime)
            {
                destroyedByLifetime = true;
                Destroy(gameObject);
                return;
            }

            Vector3 current = transform.position;
            current.z = 0f;

            transform.position = Vector3.MoveTowards(
                current,
                _targetPosition,
                _speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPosition) <= 0.1f)
            {
                destroyedByArrival = true;
                Destroy(gameObject);
            }
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            VisualUpdateAggregateLog.Record(
                "SystemNpcProjectileVisual.Update",
                elapsedMs,
                "Name=" + gameObject.name +
                " | Initialized=" + _isInitialized +
                " | LifeTimer=" + _lifeTimer.ToString("F3") +
                " | MaxLifetime=" + maxLifetime.ToString("F3") +
                " | DestroyedByLifetime=" + destroyedByLifetime +
                " | DestroyedByArrival=" + destroyedByArrival);
        }
    }
}