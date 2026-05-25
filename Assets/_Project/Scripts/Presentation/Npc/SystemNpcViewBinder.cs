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
    // private ISystemTravelService _systemTravelService;
    private IConfigService _configService;
    private SimpleEventBus _eventBus;

    private readonly Dictionary<string, SystemNpcView> _viewsByNpcId = new();

    private void Awake()
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        // _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
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
        if (evt.TravelState.Equals(SystemNpcTravelState.TravelingToAnotherSystem))
        {
            if (evt.DestinationSystemId != GetCurrentSystemId())
            {
                RemoveView(evt.RuntimeNpcId);
                return;        
            }
            else
            {
                CreateViewIfNeeded(evt.Npc);
                return;
            }            
        }
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
            CreateViewIfNeeded(npcs[i]);
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

        CreateViewIfNeeded(npc);
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent eventData)
    {
        RemoveView(eventData.RuntimeNpcId);
    }

    private void OnNpcPositionChanged(SystemNpcPositionChangedEvent eventData)
    {
        string currentSystemId = GetCurrentSystemId();

        if (eventData.SystemId != currentSystemId)
        {
            RemoveView(eventData.RuntimeNpcId);
            return;
        }

        if (_viewsByNpcId.ContainsKey(eventData.RuntimeNpcId))
            return;

        if (_runtimeService.TryGetNpc(eventData.RuntimeNpcId, out SystemNpcRuntimeState npc))
            CreateViewIfNeeded(npc);
    }

    private void CreateViewIfNeeded(SystemNpcRuntimeState npc)
    {
        if (npc == null || !npc.IsAlive)
            return;

        string currentSystemId = GetCurrentSystemId();

        if (npc.CurrentSystemId != currentSystemId)
            return;

        if (_viewsByNpcId.ContainsKey(npc.RuntimeNpcId))
            return;

        if (npcViewPrefab == null)
        {
            Debug.LogError("[SystemNpcViewBinder] NPC view prefab is missing.");
            return;
        }

        Sprite sprite = ResolveSprite(npc);

        SystemNpcView view = Instantiate(
            npcViewPrefab,
            npc.CurrentPosition,
            Quaternion.identity,
            npc.NpcType == SystemNpcType.Enemy ? enemyRoot : allyRoot
        );
        view.Bind(npc, sprite);

        _viewsByNpcId.Add(npc.RuntimeNpcId, view);
    }

    private void RemoveView(string runtimeNpcId)
    {
        if (!_viewsByNpcId.TryGetValue(runtimeNpcId, out SystemNpcView view))
            return;

        if (view != null)
            Destroy(view.gameObject);

        _viewsByNpcId.Remove(runtimeNpcId);
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

    private string GetCurrentSystemId()
    {
        if (_gameSessionService == null)
            return string.Empty;

        return _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;
    }
}