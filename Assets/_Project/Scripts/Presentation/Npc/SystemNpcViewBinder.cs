using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcViewBinder : CustomMonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private Transform allyRoot;

    [Header("Prefabs")]
    [SerializeField] private SystemNpcView npcViewPrefab;

    private IGameSessionService _gameSessionService;
    private ISystemNpcRuntimeService _runtimeService;
    private IOrbitalMotionService _orbitalMotionService;

    private IConfigService _configService;
    private SimpleEventBus _eventBus;

    private readonly Dictionary<string, SystemNpcView> _viewsByNpcId = new();
    public int VisibleViewCount => _viewsByNpcId.Count;

    private void Awake()
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        if (enemyRoot == null)
            enemyRoot = transform;
        if (allyRoot == null)
            allyRoot = transform;
    }

    private void OnEnable()
    {
        _eventBus.Subscribe<SystemNpcCreatedEvent>(OnNpcCreated);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Subscribe<SystemNpcPositionChangedEvent>(OnNpcPositionChanged);
        _eventBus.Subscribe<SystemNpcTravelStateChangedEvent>(OnSystemNpcTravelStateChangedEvent);
    }

    private void OnDisable()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<SystemNpcCreatedEvent>(OnNpcCreated);
        _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Unsubscribe<SystemNpcPositionChangedEvent>(OnNpcPositionChanged);
        _eventBus.Unsubscribe<SystemNpcTravelStateChangedEvent>(OnSystemNpcTravelStateChangedEvent);
    }

    private void OnSystemNpcTravelStateChangedEvent(SystemNpcTravelStateChangedEvent evt)
    {
        const string source = "TravelStateChanged";

        if (evt.TravelState == SystemNpcTravelState.OnPlanet)
        {
            RemoveView(evt.RuntimeNpcId, source + ":EventOnPlanet");
            return;
        }

        if (evt.Npc != null &&
            (evt.Npc.IsOnPlanet ||
             evt.Npc.TravelState == SystemNpcTravelState.OnPlanet))
        {
            LogNpcViewState("VIEW_SKIPPED_ON_PLANET", evt.Npc, source);
            RemoveView(evt.RuntimeNpcId, source + ":NpcOnPlanet");
            return;
        }

        if (evt.TravelState == SystemNpcTravelState.TravelingToAnotherSystem)
        {
            string currentSystemId = GetCurrentSystemId();

            if (evt.DestinationSystemId != currentSystemId)
            {
                if (evt.Npc != null)
                    LogNpcViewState("VIEW_SKIPPED_OTHER_SYSTEM_TRAVEL", evt.Npc, source);

                RemoveView(evt.RuntimeNpcId, source + ":OtherSystemTravel");
                return;
            }

            CreateViewIfNeeded(evt.Npc, source + ":TravelingToAnotherSystem");
            return;
        }

        CreateViewIfNeeded(evt.Npc, source + ":" + evt.TravelState);
    }

    private void Start()
    {
        RefreshCurrentSystemViews();
    }

    [ContextMenu("Refresh Current System Views")]
    public void RefreshCurrentSystemViews()
    {
        ClearViews();

        string currentSystemId = GetCurrentSystemId();

        if (string.IsNullOrWhiteSpace(currentSystemId))
        {
            Debug.LogWarning("[SystemNpcViewBinder] Current system id is empty.");
            return;
        }

        var npcs = _runtimeService.GetAliveNpcsInSystem(currentSystemId);

        for (int i = 0; i < npcs.Count; i++)
        {
            CreateViewIfNeeded(npcs[i], "RefreshCurrentSystemViews");
        }

        LogCustom($"[SystemNpcViewBinder] Views refreshed. System: {currentSystemId}, Count: {_viewsByNpcId.Count}");
    }

    [ContextMenu("Clear Views")]
    public void ClearViews()
    {
        foreach (var pair in _viewsByNpcId)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        _viewsByNpcId.Clear();
    }

    private void OnNpcCreated(SystemNpcCreatedEvent eventData)
    {
        string currentSystemId = GetCurrentSystemId();

        if (eventData.SystemId != currentSystemId)
            return;

        if (!_runtimeService.TryGetNpc(eventData.RuntimeNpcId, out SystemNpcRuntimeState npc))
            return;

        CreateViewIfNeeded(npc, "NpcCreated");
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent eventData)
    {
        RemoveView(eventData.RuntimeNpcId, "NpcDestroyed");
    }

    private void OnNpcPositionChanged(SystemNpcPositionChangedEvent eventData)
    {
        string currentSystemId = GetCurrentSystemId();

        if (eventData.SystemId != currentSystemId)
        {
            RemoveView(eventData.RuntimeNpcId, "PositionChanged:OtherSystem");
            return;
        }

        if (_viewsByNpcId.ContainsKey(eventData.RuntimeNpcId))
            return;

        if (_runtimeService.TryGetNpc(eventData.RuntimeNpcId, out SystemNpcRuntimeState npc))
            CreateViewIfNeeded(npc, "PositionChanged:MissingView");
    }

    private void CreateViewIfNeeded(SystemNpcRuntimeState npc, string source)
    {
        if (npc == null)
        {
            LogNpcViewState("VIEW_SKIPPED_NULL_NPC", null, source);
            return;
        }

        if (!npc.IsAlive)
        {
            LogNpcViewState("VIEW_SKIPPED_NOT_ALIVE", npc, source);
            return;
        }

        if (npc.IsOnPlanet ||
            npc.TravelState == SystemNpcTravelState.OnPlanet)
        {
            LogNpcViewState("VIEW_SKIPPED_ON_PLANET", npc, source);
            RemoveView(npc.RuntimeNpcId, source + ":CreateBlockedOnPlanet");
            return;
        }

        string currentSystemId = GetCurrentSystemId();

        if (npc.CurrentSystemId != currentSystemId)
        {
            LogNpcViewState("VIEW_SKIPPED_WRONG_SYSTEM", npc, source);
            return;
        }

        if (_viewsByNpcId.ContainsKey(npc.RuntimeNpcId))
            return;

        if (npcViewPrefab == null)
        {
            Debug.LogError("[SystemNpcViewBinder] NPC view prefab is missing.");
            return;
        }

        Sprite sprite = ResolveSprite(npc);
        float worldSize = ResolveWorldSize(npc);

        SystemNpcView view = Instantiate(
            npcViewPrefab,
            npc.CurrentPosition,
            Quaternion.identity,
            npc.NpcType == SystemNpcType.Enemy ? enemyRoot : allyRoot
        );

        view.Bind(
            npc,
            sprite,
            worldSize);

        _viewsByNpcId.Add(npc.RuntimeNpcId, view);

        LogNpcViewState("VIEW_CREATED", npc, source);
    }


    private void RemoveView(string runtimeNpcId, string source = "Unknown")
    {
        if (!_viewsByNpcId.TryGetValue(runtimeNpcId, out SystemNpcView view))
            return;

        if (_runtimeService != null &&
            _runtimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
        {
            LogNpcViewState("VIEW_REMOVED", npc, source);
        }
        else
        {
            Debug.Log(
                "[SystemNpcViewBinder] VIEW_REMOVED" +
                " | Source=" + source +
                " | RuntimeNpcId=" + runtimeNpcId +
                " | CurrentSystemId=" + GetCurrentSystemId() +
                " | RuntimeNpcMissing=True" +
                " | VisibleViewCount=" + _viewsByNpcId.Count);
        }

        if (view != null)
            Destroy(view.gameObject);

        _viewsByNpcId.Remove(runtimeNpcId);
    }

    private void LogNpcViewState(
        string marker,
        SystemNpcRuntimeState npc,
        string source)
    {
        if (npc == null)
        {
            Debug.Log(
                "[SystemNpcViewBinder] " + marker +
                " | Source=" + source +
                " | RuntimeNpcId=null" +
                " | CurrentSystemId=" + GetCurrentSystemId() +
                " | VisibleViewCount=" + _viewsByNpcId.Count);
            return;
        }

        bool hasView =
            !string.IsNullOrWhiteSpace(npc.RuntimeNpcId) &&
            _viewsByNpcId.ContainsKey(npc.RuntimeNpcId);

        string currentPlanetSnapshot =
            BuildPlanetDebugSnapshot(
                "CurrentPlanet",
                npc.CurrentPlanetId,
                npc.CurrentPosition);

        string targetPlanetSnapshot =
            BuildPlanetDebugSnapshot(
                "TargetPlanet",
                npc.TargetPlanetId,
                npc.CurrentPosition);

        string initialRoutePlanetSnapshot =
            BuildPlanetDebugSnapshot(
                "InitialRouteBuildPlanet",
                npc.InitialRouteBuildPlanetId,
                npc.CurrentPosition);

        Debug.Log(
            "[SystemNpcViewBinder] " + marker +
            " | Source=" + source +
            " | RuntimeNpcId=" + npc.RuntimeNpcId +
            " | ConfigId=" + npc.ConfigId +
            " | NpcType=" + npc.NpcType +
            " | CurrentSystemId=" + GetCurrentSystemId() +
            " | NpcSystemId=" + npc.CurrentSystemId +
            " | IsAlive=" + npc.IsAlive +
            " | IsOnPlanet=" + npc.IsOnPlanet +
            " | TravelState=" + npc.TravelState +
            " | Behavior=" + npc.CurrentBehavior +
            " | PrevBehavior=" + npc.PrevBehavior +
            " | CurrentPlanetId=" + (npc.CurrentPlanetId ?? string.Empty) +
            " | TargetPlanetId=" + (npc.TargetPlanetId ?? string.Empty) +
            " | TargetSystemId=" + (npc.TargetSystemId ?? string.Empty) +
            " | Position=" + npc.CurrentPosition.ToString("F3") +
            " | StartPosition=" + npc.StartPosition.ToString("F3") +
            " | TargetPosition=" + npc.TargetPosition.ToString("F3") +
            " | CurrentMovementTargetPosition=" + npc.CurrentMovementTargetPosition.ToString("F3") +
            " | TickMovementTargetPosition=" + npc.TickMovementTargetPosition.ToString("F3") +
            " | TargetSystemExitPoint=" + npc.TargetSystemExitPoint.ToString("F3") +
            " | TargetSystemEntryPoint=" + npc.TargetSystemEntryPoint.ToString("F3") +
            " | IsWaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild +
            " | ReleaseFromPlanetAfterInitialRouteBuild=" + npc.ReleaseFromPlanetAfterInitialRouteBuild +
            " | InitialRouteBuildPlanetId=" + (npc.InitialRouteBuildPlanetId ?? string.Empty) +
            " | TravelProgress01=" + npc.TravelProgress01.ToString("0.000") +
            currentPlanetSnapshot +
            targetPlanetSnapshot +
            initialRoutePlanetSnapshot +
            " | HasView=" + hasView +
            " | VisibleViewCount=" + _viewsByNpcId.Count);
    }

    private string BuildPlanetDebugSnapshot(
        string label,
        string planetId,
        Vector3 npcPosition)
    {
        if (string.IsNullOrWhiteSpace(planetId))
        {
            return
                " | " + label + "Id=" +
                " | " + label + "LivePosition=None" +
                " | " + label + "DistanceToNpc=None";
        }

        if (_configService == null ||
            _orbitalMotionService == null)
        {
            return
                " | " + label + "Id=" + planetId +
                " | " + label + "LivePosition=ServiceMissing" +
                " | " + label + "DistanceToNpc=ServiceMissing";
        }

        PlanetConfig planet =
            _configService.GetPlanetConfigById(planetId);

        if (planet == null ||
            planet.PlanetOrbit == null)
        {
            return
                " | " + label + "Id=" + planetId +
                " | " + label + "LivePosition=ConfigMissing" +
                " | " + label + "DistanceToNpc=ConfigMissing";
        }

        Vector3 planetPosition =
            _orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit);

        planetPosition.z = npcPosition.z;

        return
            " | " + label + "Id=" + planetId +
            " | " + label + "LivePosition=" + planetPosition.ToString("F3") +
            " | " + label + "DistanceToNpc=" +
            Vector3.Distance(npcPosition, planetPosition).ToString("0.###");
    }

    private Sprite ResolveSprite(SystemNpcRuntimeState npc)
    {
        if (npc.NpcType == SystemNpcType.Enemy)
        {
            EnemyConfig enemyConfig = _configService.GetEnemyConfigById(npc.ConfigId);

            if (enemyConfig != null)
                return enemyConfig.CombatSprite;
        }

        if (npc.NpcType == SystemNpcType.Ally)
        {
            AllyConfig allyConfig = _configService.GetAllyConfigById(npc.ConfigId);

            if (allyConfig != null)
                return allyConfig.MapSprite;
        }

        if (npc.NpcType == SystemNpcType.Pirate)
        {
            PirateConfig pirateConfig = _configService.GetPirateConfigById(npc.ConfigId);

            if (pirateConfig != null)
                return pirateConfig.CombatSprite;
        }

        return null;
    }

    private float ResolveWorldSize(SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            _configService == null ||
            _configService.SystemVisualConfig == null)
        {
            return 0f;
        }

        if (npc.NpcType == SystemNpcType.Enemy)
        {
            EnemyConfig enemyConfig =
                _configService.GetEnemyConfigById(npc.ConfigId);

            return _configService
                .SystemVisualConfig
                .GetEnemyWorldSize(enemyConfig);
        }

        if (npc.NpcType == SystemNpcType.Ally)
        {
            AllyConfig allyConfig =
                _configService.GetAllyConfigById(npc.ConfigId);

            return _configService
                .SystemVisualConfig
                .GetAllyWorldSize(allyConfig);
        }

        if (npc.NpcType == SystemNpcType.Pirate)
        {
            PirateConfig pirateConfig =
                _configService.GetPirateConfigById(npc.ConfigId);

            return _configService
                .SystemVisualConfig
                .GetPirateWorldSize(pirateConfig);
        }

        return 0f;
    }

    private string GetCurrentSystemId()
    {
        if (_gameSessionService == null)
            return string.Empty;

        return _gameSessionService.State.Player.CurrentSystemId;
    }
}
