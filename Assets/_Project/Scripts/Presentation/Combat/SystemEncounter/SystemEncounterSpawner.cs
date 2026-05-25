using UnityEngine;

public sealed class SystemEncounterSpawner : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private SystemEncounterRuntimeRoot runtimeRoot;

    [Header("Enemy Configs")]
    [SerializeField] private EnemyConfig chaserConfig;
    [SerializeField] private EnemyConfig kiterConfig;
    [SerializeField] private EnemyConfig tankConfig;

    [Header("Ally Configs")]
    [SerializeField] private AllyConfig allyPatrolConfig;

    [Header("View Prefabs")]
    [SerializeField] private EnemySystemMapEntity enemyViewPrefab;
    [SerializeField] private AllySystemMapEntity allyViewPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private string debugSystemId = "system_debug";
    [SerializeField] private float spawnRadius = 200f;

    private ISystemEncounterService _encounterService;
    private ISystemEnemyService _enemyService;
    private ISystemAllyService _allyService;

    private void Awake()
    {
        _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        _allyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemAllyService>();
    }

    public void SpawnDebugEncounter(Vector3 centerPosition)
    {
        if (runtimeRoot == null)
        {
            Debug.LogError("[SystemEncounterSpawner] RuntimeRoot is missing.");
            return;
        }

        ClearRuntimeObjects();

        _enemyService.ClearSystemEnemies(debugSystemId);
        _allyService.ClearSystemAllies(debugSystemId);

        _encounterService.StartEncounter(
            "encounter_debug_system_pirates",
            debugSystemId,
            enemyCount: 3,
            allyCount: 2
        );

        SpawnEnemy(chaserConfig, centerPosition + new Vector3(spawnRadius, 0f, 0f));
        SpawnEnemy(kiterConfig, centerPosition + new Vector3(-spawnRadius, 1.5f, 0f));
        SpawnEnemy(tankConfig, centerPosition + new Vector3(0f, -spawnRadius, 0f));

        SpawnAlly(allyPatrolConfig, centerPosition + new Vector3(spawnRadius, 2.5f, 0f));
        SpawnAlly(allyPatrolConfig, centerPosition + new Vector3(spawnRadius, -2.5f, 0f));

        Debug.Log("[SystemEncounterSpawner] Debug encounter spawned with enemies and allies.");
    }

    public void ClearRuntimeObjects()
    {
        if (runtimeRoot == null)
            return;

        ClearChildren(runtimeRoot.EnemiesRoot);
        ClearChildren(runtimeRoot.AlliesRoot);
        ClearChildren(runtimeRoot.ProjectilesRoot);
        ClearChildren(runtimeRoot.VfxRoot);
    }

    private void SpawnEnemy(EnemyConfig enemyConfig, Vector3 position)
    {
        if (enemyConfig == null)
        {
            Debug.LogError("[SystemEncounterSpawner] EnemyConfig is missing.");
            return;
        }

        if (enemyViewPrefab == null)
        {
            Debug.LogError("[SystemEncounterSpawner] Enemy view prefab is missing.");
            return;
        }

        SystemEnemyRuntimeState runtimeEnemy = _enemyService.CreateEnemy(
            enemyConfig,
            debugSystemId,
            position
        );

        EnemySystemMapEntity view = Instantiate(
            enemyViewPrefab,
            position,
            Quaternion.identity,
            runtimeRoot.EnemiesRoot
        );

        view.Bind(runtimeEnemy);
    }

    private void SpawnAlly(AllyConfig allyConfig, Vector3 position)
    {
        if (allyConfig == null)
        {
            Debug.LogError("[SystemEncounterSpawner] AllyConfig is missing.");
            return;
        }

        if (allyViewPrefab == null)
        {
            Debug.LogError("[SystemEncounterSpawner] Ally view prefab is missing.");
            return;
        }

        SystemAllyRuntimeState runtimeAlly = _allyService.CreateAlly(
            allyConfig,
            debugSystemId,
            position
        );

        AllySystemMapEntity view = Instantiate(
            allyViewPrefab,
            position,
            Quaternion.identity,
            runtimeRoot.AlliesRoot
        );

        view.Bind(runtimeAlly);
    }

    private void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }
}