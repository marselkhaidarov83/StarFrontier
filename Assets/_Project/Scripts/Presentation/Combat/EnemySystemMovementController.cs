using UnityEngine;

[RequireComponent(typeof(EnemySystemMapEntity))]
public sealed class EnemySystemMovementController : MonoBehaviour
{
    private EnemySystemMapEntity _enemyView;
    private ISystemEnemyMovementService _enemyMovementService;

    private void Awake()
    {
        _enemyView = GetComponent<EnemySystemMapEntity>();
        ResolveServices();
    }

    public void ApplyRuntimeConfig(SystemEnemyRuntimeState runtimeEnemy)
    {
    }

    public void SetMovementMode(EnemyMovementMode mode)
    {
    }

    public void SetDestination(Vector3 destination)
    {
    }

    public void SetFollowTarget(Transform target)
    {
    }

    public bool TryBuildRoutePreview2A(
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick)
    {
        ResolveServices();

        if (_enemyView == null ||
            !_enemyView.IsBound ||
            _enemyMovementService == null)
        {
            return false;
        }

        return _enemyMovementService.TryBuildRoutePreview2A(
            _enemyView.RuntimeEnemyId,
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            secondsPerTick);
    }

    private void ResolveServices()
    {
        if (_enemyMovementService != null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(out _enemyMovementService);
    }
}