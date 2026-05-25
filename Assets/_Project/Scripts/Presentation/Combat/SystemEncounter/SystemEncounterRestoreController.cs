using UnityEngine;

public sealed class SystemEncounterRestoreController : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private SystemEncounterRuntimeRoot runtimeRoot;

    [Header("View Prefabs")]
    [SerializeField] private EnemySystemMapEntity enemyViewPrefab;
    [SerializeField] private AllySystemMapEntity allyViewPrefab;

    private ISystemEnemyService _enemyService;
    private ISystemAllyService _allyService;

    private void Awake()
    {
        _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        _allyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemAllyService>();
    }

    [ContextMenu("Restore Views From Runtime")]
    public void RestoreViewsFromRuntime()
    {
        if (runtimeRoot == null)
        {
            Debug.LogError("[SystemEncounterRestoreController] RuntimeRoot missing.");
            return;
        }

        ClearViews();

        foreach (var enemy in _enemyService.Enemies)
        {
            if (!enemy.IsAlive)
                continue;

            EnemySystemMapEntity enemyView = Instantiate(
                enemyViewPrefab,
                enemy.Position,
                Quaternion.identity,
                runtimeRoot.EnemiesRoot
            );

            enemyView.Bind(enemy);
        }

        foreach (var ally in _allyService.Allies)
        {
            if (!ally.IsAlive)
                continue;

            AllySystemMapEntity allyView = Instantiate(
                allyViewPrefab,
                ally.Position,
                Quaternion.identity,
                runtimeRoot.AlliesRoot
            );

            allyView.Bind(ally);
        }

        Debug.Log("[SystemEncounterRestoreController] Views restored from runtime.");
    }

    public void ClearViews()
    {
        ClearChildren(runtimeRoot.EnemiesRoot);
        ClearChildren(runtimeRoot.AlliesRoot);
        ClearChildren(runtimeRoot.ProjectilesRoot);
        ClearChildren(runtimeRoot.VfxRoot);
    }

    private void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}