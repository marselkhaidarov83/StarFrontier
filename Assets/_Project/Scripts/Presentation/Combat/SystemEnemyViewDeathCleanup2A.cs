using UnityEngine;

public sealed class SystemEnemyViewDeathCleanup2A : MonoBehaviour
{
    [SerializeField] private EnemySystemMapEntity enemyView;

    private SimpleEventBus _eventBus;
    private string _runtimeEnemyId;

    private void Awake()
    {
        if (enemyView == null)
            enemyView = GetComponent<EnemySystemMapEntity>();

        ResolveRuntimeId();

        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null)
        {
            _eventBus =
                Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

            _eventBus.Subscribe<SystemEnemyDestroyedEvent>(OnEnemyDestroyed);
        }
    }

    private void OnDestroy()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<SystemEnemyDestroyedEvent>(OnEnemyDestroyed);
    }

    private void OnEnemyDestroyed(SystemEnemyDestroyedEvent evt)
    {
        ResolveRuntimeId();

        if (string.IsNullOrWhiteSpace(_runtimeEnemyId))
            return;

        if (evt.RuntimeEnemyId != _runtimeEnemyId)
            return;

        Destroy(gameObject);
    }

    private void ResolveRuntimeId()
    {
        if (!string.IsNullOrWhiteSpace(_runtimeEnemyId))
            return;

        if (enemyView == null)
            return;

        _runtimeEnemyId = enemyView.RuntimeEnemyId;
    }
}