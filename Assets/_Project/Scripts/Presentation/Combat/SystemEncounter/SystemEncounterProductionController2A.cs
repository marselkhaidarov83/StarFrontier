using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SystemEncounterProductionController2A : MonoBehaviour
{
    [Header("Start")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Enemy Rules")]
    [SerializeField] private EnemyGroupSpawnRuleConfig[] enemyGroupSpawnRules =
        new EnemyGroupSpawnRuleConfig[0];

    [SerializeField] [Range(1, 10)] private int encounterGalaxyLevel = 1;

    [SerializeField] private EnemySystemMapEntity enemyPrefab;

    [Header("Ally Rules")]
    [SerializeField] private bool spawnAllies = true;

    [SerializeField] private AllySpawnRuleConfig[] allySpawnRules =
        new AllySpawnRuleConfig[0];

    [SerializeField] private AllySystemMapEntity allyPrefab;

    [Header("Roots")]
    [SerializeField] private Transform enemiesRoot;
    [SerializeField] private Transform alliesRoot;

    [Header("Placement")]
    [SerializeField] private float enemySpawnSpacing = 1.2f;
    [SerializeField] private float allySpawnSpacing = 1.2f;

    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ISystemEncounterService _encounterService;
    private ISystemEnemyService _enemyService;
    private ISystemAllyService _allyService;

    private void Awake()
    {
        ResolveServices();
        AutoBindRoots();
    }

    private void Start()
    {
        if (!spawnOnStart)
            return;

        TryStartEncounterInCurrentSystem("scene_start");
    }

    [ContextMenu("S04-02 Start Encounter In Current System")]
    public void StartEncounterInCurrentSystemFromMenu()
    {
        TryStartEncounterInCurrentSystem("context_menu");
    }

    public bool TryStartEncounterInCurrentSystem(string reason)
    {
        if (!ResolveServices())
            return false;

        if (_gameSessionService.State == null)
        {
            Debug.LogWarning("[S04-02] Cannot start encounter: game state is missing.");
            return false;
        }

        PlayerState player =
            _gameSessionService.State.Player;

        if (player == null)
        {
            Debug.LogWarning("[S04-02] Cannot start encounter: player state is missing.");
            return false;
        }

        string systemId =
            player.CurrentSystemId;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        EnemyGroupSpawnRuleConfig enemySpawnRule =
            SelectEnemySpawnRule();

        ShipRuntimeData activeShip =
            player.GetActiveShip();

        bool canStart =
            SystemEncounterProductionRules2A.CanStart(
                systemId,
                player,
                activeShip,
                _encounterService.HasActiveEncounter,
                starSystem,
                enemySpawnRule,
                out SystemEncounterStartFailReason2A failReason);

        if (!canStart)
        {
            Debug.Log("[S04-02] Encounter not started. Reason: " + failReason);
            return false;
        }

        List<EnemySpawnRequest2A> enemySpawnPlan =
            BuildEnemySpawnPlan(enemySpawnRule);

        if (enemySpawnPlan.Count <= 0)
        {
            Debug.LogWarning("[S04-02] Encounter not started: enemy spawn plan is empty.");
            return false;
        }

        List<AllySpawnRequest2A> allySpawnPlan =
            BuildAllySpawnPlan();

        string encounterId =
            "encounter_" + systemId + "_" + DateTime.UtcNow.Ticks;

        _encounterService.StartEncounter(
            encounterId,
            systemId,
            enemySpawnPlan.Count,
            allySpawnPlan.Count);

        SpawnEnemies(
            enemySpawnPlan,
            systemId,
            player.SystemMapShipPosition);

        SpawnAllies(
            allySpawnPlan,
            systemId,
            player.SystemMapShipPosition);

        Debug.Log(
            "[S04-02] Production encounter started. " +
            "Reason: " + reason + ", " +
            "EncounterId: " + encounterId + ", " +
            "SystemId: " + systemId + ", " +
            "Enemies: " + enemySpawnPlan.Count + ", " +
            "Allies: " + allySpawnPlan.Count);

        return true;
    }

    private bool ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            Debug.LogWarning("[S04-02] Bootstrapper or ServiceRegistry is missing.");
            return false;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        _gameSessionService ??=
            registry.Get<IGameSessionService>();

        _configService ??=
            registry.Get<IConfigService>();

        _encounterService ??=
            registry.Get<ISystemEncounterService>();

        _enemyService ??=
            registry.Get<ISystemEnemyService>();

        _allyService ??=
            registry.Get<ISystemAllyService>();

        return _gameSessionService != null
            && _configService != null
            && _encounterService != null
            && _enemyService != null
            && _allyService != null;
    }

    private void AutoBindRoots()
    {
        if (enemiesRoot != null && alliesRoot != null)
            return;

        SystemSceneRoot2A sceneRoot =
            UnityEngine.Object.FindFirstObjectByType<SystemSceneRoot2A>();

        if (sceneRoot == null)
            return;

        if (enemiesRoot == null)
            enemiesRoot = sceneRoot.EnemiesRoot;

        if (alliesRoot == null)
            alliesRoot = sceneRoot.AlliesRoot;
    }

    private EnemyGroupSpawnRuleConfig SelectEnemySpawnRule()
    {
        if (enemyGroupSpawnRules == null)
            return null;

        int currentGalaxyLevel =
            Mathf.Clamp(encounterGalaxyLevel, 1, 10);

        for (int i = 0; i < enemyGroupSpawnRules.Length; i++)
        {
            EnemyGroupSpawnRuleConfig rule =
                enemyGroupSpawnRules[i];

            if (rule == null)
                continue;

            if (rule.HasValidEnemiesForGalaxyLevel(currentGalaxyLevel))
                return rule;
        }

        return null;
    }

    private List<EnemySpawnRequest2A> BuildEnemySpawnPlan(
        EnemyGroupSpawnRuleConfig spawnRule)
    {
        List<EnemySpawnRequest2A> result =
            new List<EnemySpawnRequest2A>();

        if (spawnRule == null)
            return result;

        int currentGalaxyLevel =
            Mathf.Clamp(encounterGalaxyLevel, 1, 10);

        EnemyGroupSpawnLevelEntryConfig levelEntry =
            spawnRule.GetEntryForGalaxyLevel(currentGalaxyLevel);

        if (levelEntry == null)
            return result;

        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            levelEntry.Enemies;

        if (enemies == null)
            return result;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGroupEntryConfig entry =
                enemies[i];

            if (entry == null)
                continue;

            if (!entry.IsValid())
                continue;

            int countToSpawn =
                UnityEngine.Random.Range(
                    entry.MinCount,
                    entry.MaxCount + 1);

            for (int c = 0; c < countToSpawn; c++)
            {
                result.Add(
                    new EnemySpawnRequest2A(
                        entry.EnemyConfig,
                        new Vector3(6f, 0f, 0f)));
            }
        }

        return result;
    }

    private List<AllySpawnRequest2A> BuildAllySpawnPlan()
    {
        List<AllySpawnRequest2A> result =
            new List<AllySpawnRequest2A>();

        if (!spawnAllies)
            return result;

        if (allyPrefab == null)
            return result;

        if (allySpawnRules == null)
            return result;

        for (int r = 0; r < allySpawnRules.Length; r++)
        {
            AllySpawnRuleConfig rule =
                allySpawnRules[r];

            if (rule == null)
                continue;

            if (!rule.HasValidAllies())
                continue;

            IReadOnlyList<AllyGroupEntryConfig> allies =
                rule.Allies;

            if (allies == null)
                continue;

            for (int i = 0; i < allies.Count; i++)
            {
                AllyGroupEntryConfig entry =
                    allies[i];

                if (entry == null)
                    continue;

                if (!entry.IsValid())
                    continue;

                int countToSpawn =
                    UnityEngine.Random.Range(
                        entry.MinCount,
                        entry.MaxCount + 1);

                for (int c = 0; c < countToSpawn; c++)
                {
                    result.Add(
                        new AllySpawnRequest2A(
                            entry.AllyConfig));
                }
            }
        }

        return result;
    }

    private void SpawnEnemies(
        List<EnemySpawnRequest2A> enemySpawnPlan,
        string systemId,
        Vector3 playerPosition)
    {
        if (enemySpawnPlan == null || enemySpawnPlan.Count <= 0)
            return;

        for (int i = 0; i < enemySpawnPlan.Count; i++)
        {
            EnemySpawnRequest2A request =
                enemySpawnPlan[i];

            if (request.EnemyConfig == null)
                continue;

            Vector3 position =
                BuildEnemyPosition(
                    playerPosition,
                    request.StartPosition,
                    i);

            SystemEnemyRuntimeState runtimeEnemy =
                _enemyService.CreateEnemy(
                    request.EnemyConfig,
                    systemId,
                    position);

            SpawnEnemyView(
                runtimeEnemy,
                position);
        }
    }

    private void SpawnAllies(
        List<AllySpawnRequest2A> allySpawnPlan,
        string systemId,
        Vector3 playerPosition)
    {
        if (allySpawnPlan == null || allySpawnPlan.Count <= 0)
            return;

        for (int i = 0; i < allySpawnPlan.Count; i++)
        {
            AllySpawnRequest2A request =
                allySpawnPlan[i];

            if (request.AllyConfig == null)
                continue;

            Vector3 position =
                BuildAllyPosition(
                    playerPosition,
                    i);

            SystemAllyRuntimeState runtimeAlly =
                _allyService.CreateAlly(
                    request.AllyConfig,
                    systemId,
                    position);

            SpawnAllyView(
                runtimeAlly,
                position);
        }
    }

    private void SpawnEnemyView(
        SystemEnemyRuntimeState runtimeEnemy,
        Vector3 position)
    {
        if (runtimeEnemy == null)
            return;

        if (enemyPrefab == null)
        {
            Debug.LogError("[S04-02] Enemy prefab is missing.");
            return;
        }

        Transform root =
            enemiesRoot != null ? enemiesRoot : transform;

        EnemySystemMapEntity view =
            Instantiate(
                enemyPrefab,
                position,
                Quaternion.identity,
                root);

        view.Bind(runtimeEnemy);
    }

    private void SpawnAllyView(
        SystemAllyRuntimeState runtimeAlly,
        Vector3 position)
    {
        if (runtimeAlly == null)
            return;

        if (allyPrefab == null)
        {
            Debug.LogError("[S04-02] Ally prefab is missing.");
            return;
        }

        Transform root =
            alliesRoot != null ? alliesRoot : transform;

        AllySystemMapEntity view =
            Instantiate(
                allyPrefab,
                position,
                Quaternion.identity,
                root);

        view.Bind(runtimeAlly);
    }

    private Vector3 BuildEnemyPosition(
        Vector3 playerPosition,
        Vector3 ruleOffset,
        int index)
    {
        Vector3 position =
            playerPosition + ruleOffset;

        position.x += index * enemySpawnSpacing;
        position.y += (index % 2 == 0 ? 1f : -1f) * enemySpawnSpacing;
        position.z = 0f;

        return position;
    }

    private Vector3 BuildAllyPosition(
        Vector3 playerPosition,
        int index)
    {
        Vector3 position =
            playerPosition;

        position.x -= 2f + index * allySpawnSpacing;
        position.y -= 1f;
        position.z = 0f;

        return position;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (enemyGroupSpawnRules == null)
            enemyGroupSpawnRules = new EnemyGroupSpawnRuleConfig[0];

        if (allySpawnRules == null)
            allySpawnRules = new AllySpawnRuleConfig[0];

        enemySpawnSpacing =
            Mathf.Max(0.1f, enemySpawnSpacing);

        allySpawnSpacing =
            Mathf.Max(0.1f, allySpawnSpacing);
    }
#endif

    private readonly struct EnemySpawnRequest2A
    {
        public EnemySpawnRequest2A(
            EnemyConfig enemyConfig,
            Vector3 startPosition)
        {
            EnemyConfig = enemyConfig;
            StartPosition = startPosition;
        }

        public EnemyConfig EnemyConfig { get; }

        public Vector3 StartPosition { get; }
    }

    private readonly struct AllySpawnRequest2A
    {
        public AllySpawnRequest2A(
            AllyConfig allyConfig)
        {
            AllyConfig = allyConfig;
        }

        public AllyConfig AllyConfig { get; }
    }
}
