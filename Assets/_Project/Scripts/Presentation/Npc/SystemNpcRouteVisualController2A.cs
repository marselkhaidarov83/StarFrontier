using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SystemNpcRouteVisualController2A : CustomMonoBehaviour
{
    private const float ArrivalDistanceThreshold = 3f;
    private const float SunAvoidanceSafetyMargin = 80f;
    private const int SunAvoidanceArcSegments = 18;
    private const int RoutePlanMaxSteps = 8192;
    private const float DirectionThresholdSqrMagnitude = 0.0001f;


    [Header("View")]
    [SerializeField] private TravelLineView2A npcRouteLineView;
    [SerializeField] private TravelLineView2A playerRouteLineView;

    [Header("Colors")]
    [SerializeField]
    private Color allyBigDotColor =
        new Color(0.345f, 0.733f, 0.965f, 0.9f);

    [SerializeField]
    private Color allySmallDotColor =
        new Color(0.345f, 0.733f, 0.965f, 0.38f);

    [SerializeField]
    private Color enemyAiBigDotColor =
        new Color(1f, 0.25f, 0.18f, 0.9f);

    [SerializeField]
    private Color enemyAiSmallDotColor =
        new Color(1f, 0.25f, 0.18f, 0.38f);

    [SerializeField]
    private Color enemyAncientsBigDotColor =
        new Color(0.75f, 0.45f, 1f, 0.9f);

    [SerializeField]
    private Color enemyAncientsSmallDotColor =
        new Color(0.75f, 0.45f, 1f, 0.38f);

    [SerializeField]
    private Color enemyInfectedBigDotColor =
        new Color(0.35f, 1f, 0.45f, 0.9f);

    [SerializeField]
    private Color enemyInfectedSmallDotColor =
        new Color(0.35f, 1f, 0.45f, 0.38f);

    [SerializeField]
    private Color pirateBigDotColor =
        new Color(1f, 0.55f, 0.12f, 0.9f);

    [SerializeField]
    private Color pirateSmallDotColor =
        new Color(1f, 0.55f, 0.12f, 0.38f);

    [Header("Fallback")]
    [SerializeField] private float fallbackSecondsPerTick = 1f;
    [SerializeField] private float routeRefreshIntervalSeconds = 0.15f;

    private float _nextRouteRefreshTime;

    private int _hideRouteRequestedFrame = -1;

    private readonly TravelRoutePreview2A _preview =
        new TravelRoutePreview2A();

    private readonly List<Vector3> _legacyEnemyRouteWaypoints =
        new List<Vector3>();

    private readonly List<Vector3> _legacyEnemyRoutePath =
        new List<Vector3>();

    private SimpleEventBus _eventBus;
    private ISystemNpcMovementService _npcMovementService;
    private ISystemNpcRuntimeService _npcRuntimeService;
    private IConfigService _configService;
    private ISystemEnemyService _enemyService;
    private IPlayerCombatTargetService _playerCombatTargetService;
    private ISystemShipRouteService2A _shipRouteService;
    private readonly SystemShipRouteResult2A _legacyEnemyRouteBuildResult =
        new SystemShipRouteResult2A();

    private ITargetService2A _targetService;
    private ISystemTravelService _travelService;

    private readonly Dictionary<string, LegacyEnemyPreviewRouteState> _legacyEnemyPreviewRoutes =
        new Dictionary<string, LegacyEnemyPreviewRouteState>();

    private sealed class LegacyEnemyPreviewRouteState
    {
        public readonly List<Vector3> Path = new List<Vector3>();
        public Vector3 Destination;
        public Vector3 LastEnemyPosition;
        public float DistanceTravelled;
    }

    private string _selectedNpcId = string.Empty;
    private bool _selectedTargetIsLegacyEnemy;

    private void Start()
    {
        _debugEnabled = true;
        _debugStop = false;

        ResolveServices();

        LogCustom(
            "[NpcRouteDebug] Start | " +
            "LineView = " + (npcRouteLineView != null) +
            " | SameGameObject = " + (npcRouteLineView != null && npcRouteLineView.gameObject == gameObject) +
            " | EventBus = " + (_eventBus != null) +
            " | NpcMovementService = " + (_npcMovementService != null) +
            " | NpcRuntimeService = " + (_npcRuntimeService != null) +
            " | EnemyService = " + (_enemyService != null) +
            " | PlayerCombatTargetService = " + (_playerCombatTargetService != null) +
            " | ConfigService = " + (_configService != null));

        if (npcRouteLineView != null &&
            npcRouteLineView.gameObject == gameObject)
        {
            LogCustom(
                "[NpcRouteDebug] ERROR | NpcRouteLineView is on same GameObject as controller. " +
                "TravelLineView2A.Hide() disables this object.");
        }

        if (npcRouteLineView != null)
        {
            ApplyNpcRouteLineSettings(null);
            npcRouteLineView.Hide();
        }

        if (_eventBus != null)
        {
            _eventBus.Subscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(
                OnTargetInfoPanelRequested);

            _eventBus.Subscribe<SystemSelectedTargetInfoPanelCloseRequestedEvent2A>(
                OnTargetInfoPanelCloseRequested);

            _eventBus.Subscribe<SystemObjectsPanelRequestedEvent2A>(
                OnObjectsPanelRequested);

            _eventBus.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(
                OnObjectsPanelCloseRequested);

            _eventBus.Subscribe<DestinationSelectedEvent>(
                OnDestinationSelected);

            _eventBus.Subscribe<SystemNpcDestroyedEvent>(
                OnNpcDestroyed);

            _eventBus.Subscribe<SystemEnemyDestroyedEvent>(
                OnEnemyDestroyed);

            LogCustom("[NpcRouteDebug] Subscribed to events.");
        }
        else
        {
            LogCustom("[NpcRouteDebug] EventBus is null. Route visualizer will not receive panel events.");
        }
    }

    private void OnDestroy()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(
            OnTargetInfoPanelRequested);

        _eventBus.Unsubscribe<SystemSelectedTargetInfoPanelCloseRequestedEvent2A>(
            OnTargetInfoPanelCloseRequested);

        _eventBus.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(
            OnObjectsPanelRequested);

        _eventBus.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(
            OnObjectsPanelCloseRequested);

        _eventBus.Unsubscribe<DestinationSelectedEvent>(
            OnDestinationSelected);

        _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(
            OnNpcDestroyed);

        _eventBus.Unsubscribe<SystemEnemyDestroyedEvent>(
            OnEnemyDestroyed);
    }

    private void Update()
    {
        // if (string.IsNullOrWhiteSpace(_selectedNpcId))
        //     return;

        // RefreshRoute();
    }

    private void LateUpdate()
    {
        if (_hideRouteRequestedFrame >= 0)
        {
            if (!string.IsNullOrWhiteSpace(_selectedNpcId))
            {
                _hideRouteRequestedFrame = -1;
            }
            else
            {
                HideRoute();
                _hideRouteRequestedFrame = -1;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_selectedNpcId))
            return;

        if (Time.unscaledTime < _nextRouteRefreshTime)
            return;

        _nextRouteRefreshTime =
            Time.unscaledTime +
            Mathf.Max(0.02f, routeRefreshIntervalSeconds);

        RefreshRoute();
    }

    private void OnTargetInfoPanelRequested(
     SystemSelectedTargetInfoPanelRequestedEvent2A evt)
    {
        _hideRouteRequestedFrame = -1;

        LogCustom(
            "[NpcRouteDebug] PanelRequested | " +
            "TargetId = " + evt.TargetId +
            " | TargetType = " + evt.TargetType);

        if (evt.TargetType != SystemGameplayTargetType.Ally &&
            evt.TargetType != SystemGameplayTargetType.Enemy)
        {
            HideRoute();
            return;
        }

        if (_npcRuntimeService != null &&
            _npcRuntimeService.TryGetNpc(
                evt.TargetId,
                out SystemNpcRuntimeState npc) &&
            npc != null &&
            npc.IsAlive)
        {
            if (!IsNpcRouteTargetAvailableInCurrentSystem(npc))
            {
                StopFollowingUnavailableTarget(
                    evt.TargetId,
                    false,
                    "NpcUnavailableOnPanelRequested");

                return;
            }

            _selectedNpcId = evt.TargetId;
            _selectedTargetIsLegacyEnemy = false;

            _nextRouteRefreshTime = 0f;
            RefreshRoute();
            return;
        }

        if (evt.TargetType == SystemGameplayTargetType.Enemy &&
            _enemyService != null &&
            _enemyService.TryGetEnemy(
                evt.TargetId,
                out SystemEnemyRuntimeState enemy) &&
            enemy != null &&
            enemy.IsAlive)
        {
            if (!IsLegacyEnemyRouteTargetAvailableInCurrentSystem(enemy))
            {
                StopFollowingUnavailableTarget(
                    evt.TargetId,
                    true,
                    "EnemyUnavailableOnPanelRequested");

                return;
            }

            _selectedNpcId = evt.TargetId;
            _selectedTargetIsLegacyEnemy = true;

            _nextRouteRefreshTime = 0f;
            RefreshRoute();
            return;
        }

        StopFollowingUnavailableTarget(
            evt.TargetId,
            evt.TargetType == SystemGameplayTargetType.Enemy,
            "TargetNotFoundOnPanelRequested");
    }

    private void OnTargetInfoPanelCloseRequested(
        SystemSelectedTargetInfoPanelCloseRequestedEvent2A evt)
    {
        HideRoute();
    }

    private void OnObjectsPanelRequested(
        SystemObjectsPanelRequestedEvent2A evt)
    {
        HideRoute();
    }

    private void OnObjectsPanelCloseRequested(
        SystemObjectsPanelCloseRequestedEvent2A evt)
    {
        LogCustom(
            "[NpcRouteDebug] ObjectsPanelCloseRequested | Delay hide to LateUpdate. Frame = " +
            Time.frameCount);

        _hideRouteRequestedFrame = Time.frameCount;
    }

    private void OnDestinationSelected(
    DestinationSelectedEvent evt)
    {
        if (string.IsNullOrWhiteSpace(_selectedNpcId))
            return;

        if (evt.DestinationType == TravelDestinationType.Npc &&
            string.Equals(
                evt.RuntimeNpcId,
                _selectedNpcId,
                System.StringComparison.Ordinal))
        {
            return;
        }

        if (_selectedTargetIsLegacyEnemy &&
            evt.DestinationType == TravelDestinationType.Npc)
        {
            return;
        }

        HideRoute();
    }

    private void OnNpcDestroyed(
    SystemNpcDestroyedEvent evt)
    {
        if (!_selectedTargetIsLegacyEnemy &&
            evt.RuntimeNpcId == _selectedNpcId)
        {
            StopFollowingUnavailableSelectedTarget("NpcDestroyed");
        }
    }

    private void OnEnemyDestroyed(
     SystemEnemyDestroyedEvent evt)
    {
        if (!_selectedTargetIsLegacyEnemy)
            return;

        if (evt.RuntimeEnemyId == _selectedNpcId)
            StopFollowingUnavailableSelectedTarget("EnemyDestroyed");
    }

    private void RefreshRoute()
    {
        if (npcRouteLineView == null)
        {
            LogCustom("[NpcRouteDebug] RefreshRoute failed: npcRouteLineView is null.");
            return;
        }

        if (_selectedTargetIsLegacyEnemy)
        {
            RefreshLegacyEnemyRoute();
            return;
        }

        if (_npcMovementService == null)
        {
            LogCustom("[NpcRouteDebug] RefreshRoute failed: npc movement service is null.");
            return;
        }

        if (_npcRuntimeService == null)
        {
            StopFollowingUnavailableSelectedTarget("NpcRuntimeServiceMissing");
            return;
        }

        if (!_npcRuntimeService.TryGetNpc(
                _selectedNpcId,
                out SystemNpcRuntimeState npc))
        {
            StopFollowingUnavailableSelectedTarget("NpcNotFound");
            return;
        }

        if (npc == null ||
            !npc.IsAlive ||
            npc.IsOnPlanet ||
            !IsNpcRouteTargetAvailableInCurrentSystem(npc))
        {
            StopFollowingUnavailableSelectedTarget("NpcUnavailableOrOnPlanet");
            return;
        }

        ApplyNpcRouteLineSettings(npc);

        bool built =
            _npcMovementService.TryBuildRoutePreview2A(
                _selectedNpcId,
                _preview,
                npcRouteLineView.SmallDotSpacing,
                npcRouteLineView.MaxBigDots,
                npcRouteLineView.MaxSmallDots,
                GetSecondsPerTick());

        if (!built)
        {
            npcRouteLineView.Hide();
            return;
        }

        npcRouteLineView.ShowPreview(_preview);
    }

    private void StopFollowingUnavailableSelectedTarget(string reason)
    {
        StopFollowingUnavailableTarget(
            _selectedNpcId,
            _selectedTargetIsLegacyEnemy,
            reason);
    }

    private void StopFollowingUnavailableTarget(
        string targetId,
        bool isLegacyEnemy,
        string reason)
    {
        LogCustom(
            "[NpcRouteDebug] Stop following unavailable target | " +
            "Reason = " + reason +
            " | TargetId = " + targetId +
            " | LegacyEnemy = " + isLegacyEnemy);

        if (!string.IsNullOrWhiteSpace(targetId))
            _legacyEnemyPreviewRoutes.Remove(targetId);

        ResolveServices();

        if (_travelService != null &&
            _travelService.State != null &&
            _travelService.State.Destination != null &&
            _travelService.State.Destination.Type == TravelDestinationType.Npc &&
            string.Equals(
                _travelService.State.Destination.RuntimeNpcId,
                targetId,
                System.StringComparison.Ordinal))
        {
            _travelService.CancelTravel();
        }

        if (_targetService != null &&
            !string.IsNullOrWhiteSpace(targetId) &&
            _targetService.IsCurrentTarget(targetId))
        {
            _targetService.ClearTarget();
        }

        HideRoute();

        if (_eventBus != null)
            _eventBus.Publish(new SystemSelectedTargetInfoPanelCloseRequestedEvent2A());
    }

    private bool IsNpcRouteTargetAvailableInCurrentSystem(
    SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            !npc.IsAlive ||
            npc.IsOnPlanet)
        {
            return false;
        }

        return IsSameSystemAsPlayer(npc.CurrentSystemId);
    }

    private bool IsLegacyEnemyRouteTargetAvailableInCurrentSystem(
        SystemEnemyRuntimeState enemy)
    {
        if (enemy == null ||
            !enemy.IsAlive)
        {
            return false;
        }

        return IsSameSystemAsPlayer(enemy.SystemId);
    }

    private bool IsSameSystemAsPlayer(string targetSystemId)
    {
        string currentSystemId =
            GetCurrentPlayerSystemId();

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return true;

        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        return string.Equals(
            targetSystemId,
            currentSystemId,
            System.StringComparison.Ordinal);
    }

    private string GetCurrentPlayerSystemId()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return string.Empty;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<ISystemTravelService>(
                out ISystemTravelService travelService) &&
            travelService != null &&
            travelService.State != null &&
            !string.IsNullOrWhiteSpace(travelService.State.CurrentSystemId))
        {
            return travelService.State.CurrentSystemId;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameSessionService>(
                out IGameSessionService gameSessionService) &&
            gameSessionService != null &&
            gameSessionService.State != null &&
            gameSessionService.State.Player != null)
        {
            return gameSessionService.State.Player.CurrentSystemId;
        }

        return string.Empty;
    }

    private void ApplyNpcRouteLineSettings(
        SystemNpcRuntimeState npc)
    {
        if (npcRouteLineView == null)
            return;

        if (playerRouteLineView != null)
        {
            npcRouteLineView.CopyDotSizeSettingsFrom(
                playerRouteLineView);
        }

        GetRouteDotColors(
            npc,
            out Color bigColor,
            out Color smallColor);

        npcRouteLineView.SetDotColors(
            bigColor,
            smallColor);
    }

    private void ApplyLegacyEnemyRouteLineSettings(
        SystemEnemyRuntimeState enemy)
    {
        if (npcRouteLineView == null)
            return;

        if (playerRouteLineView != null)
        {
            npcRouteLineView.CopyDotSizeSettingsFrom(
                playerRouteLineView);
        }

        GetLegacyEnemyRouteDotColors(
            enemy,
            out Color bigColor,
            out Color smallColor);

        npcRouteLineView.SetDotColors(
            bigColor,
            smallColor);
    }

    private void HideRoute()
    {
        _selectedNpcId = string.Empty;
        _selectedTargetIsLegacyEnemy = false;

        if (npcRouteLineView != null)
            npcRouteLineView.Hide();
    }

    private float GetSecondsPerTick()
    {
        if (GameTimeState.SecondsPerDay <= 0f)
            return fallbackSecondsPerTick;

        return GameTimeState.SecondsPerDay;
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _npcMovementService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _enemyService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();

        _playerCombatTargetService =
            Bootstrapper.Instance.ServiceRegistry.Get<IPlayerCombatTargetService>();

        Bootstrapper.Instance.ServiceRegistry.TryGet<ITargetService2A>(
            out _targetService);

        Bootstrapper.Instance.ServiceRegistry.TryGet<ISystemTravelService>(
            out _travelService);

        Bootstrapper.Instance.ServiceRegistry.TryGet<ISystemShipRouteService2A>(
            out _shipRouteService);
    }

    private void GetRouteDotColors(
        SystemNpcRuntimeState npc,
        out Color bigColor,
        out Color smallColor)
    {
        bigColor = allyBigDotColor;
        smallColor = allySmallDotColor;

        if (npc == null)
            return;

        if (npc.IsPirate)
        {
            bigColor = pirateBigDotColor;
            smallColor = pirateSmallDotColor;
            return;
        }

        if (!npc.IsEnemy)
            return;

        WeaponGroupEnemyFaction faction =
            ResolveEnemyFaction(npc);

        switch (faction)
        {
            case WeaponGroupEnemyFaction.Ancients:
                bigColor = enemyAncientsBigDotColor;
                smallColor = enemyAncientsSmallDotColor;
                break;

            case WeaponGroupEnemyFaction.Infected:
                bigColor = enemyInfectedBigDotColor;
                smallColor = enemyInfectedSmallDotColor;
                break;

            case WeaponGroupEnemyFaction.AI:
            default:
                bigColor = enemyAiBigDotColor;
                smallColor = enemyAiSmallDotColor;
                break;
        }
    }

    private void GetLegacyEnemyRouteDotColors(
        SystemEnemyRuntimeState enemy,
        out Color bigColor,
        out Color smallColor)
    {
        bigColor = enemyAiBigDotColor;
        smallColor = enemyAiSmallDotColor;

        if (enemy == null)
            return;

        WeaponGroupEnemyFaction faction =
            ResolveEnemyFaction(enemy);

        switch (faction)
        {
            case WeaponGroupEnemyFaction.Ancients:
                bigColor = enemyAncientsBigDotColor;
                smallColor = enemyAncientsSmallDotColor;
                break;

            case WeaponGroupEnemyFaction.Infected:
                bigColor = enemyInfectedBigDotColor;
                smallColor = enemyInfectedSmallDotColor;
                break;

            case WeaponGroupEnemyFaction.AI:
            default:
                bigColor = enemyAiBigDotColor;
                smallColor = enemyAiSmallDotColor;
                break;
        }
    }

    private WeaponGroupEnemyFaction ResolveEnemyFaction(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return WeaponGroupEnemyFaction.AI;

        if (TryResolveEnemyFactionFromConfig(
                npc,
                out WeaponGroupEnemyFaction faction))
        {
            return faction;
        }

        if (TryResolveEnemyFactionFromConfigId(
                npc.ConfigId,
                out faction))
        {
            return faction;
        }

        return WeaponGroupEnemyFaction.AI;
    }

    private WeaponGroupEnemyFaction ResolveEnemyFaction(
        SystemEnemyRuntimeState enemy)
    {
        if (enemy == null)
            return WeaponGroupEnemyFaction.AI;

        if (TryResolveEnemyFactionFromEnemyConfig(
                enemy.EnemyConfig,
                out WeaponGroupEnemyFaction faction))
        {
            return faction;
        }

        if (TryResolveEnemyFactionFromConfigId(
                enemy.EnemyConfigId,
                out faction))
        {
            return faction;
        }

        return WeaponGroupEnemyFaction.AI;
    }

    private bool TryResolveEnemyFactionFromConfig(
        SystemNpcRuntimeState npc,
        out WeaponGroupEnemyFaction faction)
    {
        faction = WeaponGroupEnemyFaction.None;

        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.ConfigId))
        {
            return false;
        }

        EnemyConfig enemyConfig =
            _configService.GetEnemyConfigById(npc.ConfigId);

        return TryResolveEnemyFactionFromEnemyConfig(
            enemyConfig,
            out faction);
    }

    private bool TryResolveEnemyFactionFromEnemyConfig(
        EnemyConfig enemyConfig,
        out WeaponGroupEnemyFaction faction)
    {
        faction = WeaponGroupEnemyFaction.None;

        if (enemyConfig == null)
            return false;

        IReadOnlyList<WeaponGroupConfig> weaponGroups =
            enemyConfig.WeaponGroups;

        if (weaponGroups == null)
            return false;

        for (int i = 0; i < weaponGroups.Count; i++)
        {
            WeaponGroupConfig weaponGroup =
                weaponGroups[i];

            if (weaponGroup == null)
                continue;

            if (weaponGroup.EnemyFaction == WeaponGroupEnemyFaction.None)
                continue;

            faction = weaponGroup.EnemyFaction;
            return true;
        }

        return false;
    }

    private bool TryResolveEnemyFactionFromConfigId(
        string configId,
        out WeaponGroupEnemyFaction faction)
    {
        faction = WeaponGroupEnemyFaction.None;

        if (string.IsNullOrWhiteSpace(configId))
            return false;

        string normalizedConfigId =
            configId.Trim().ToLowerInvariant();

        if (normalizedConfigId.Contains("ancient"))
        {
            faction = WeaponGroupEnemyFaction.Ancients;
            return true;
        }

        if (normalizedConfigId.Contains("infected"))
        {
            faction = WeaponGroupEnemyFaction.Infected;
            return true;
        }

        if (normalizedConfigId.Contains("ai"))
        {
            faction = WeaponGroupEnemyFaction.AI;
            return true;
        }

        return false;
    }

    private bool TryBuildLegacyEnemyRoutePreview(
    SystemEnemyRuntimeState enemy,
    TravelRoutePreview2A preview)
    {
        if (preview == null)
            return false;

        preview.Clear();

        if (enemy == null || !enemy.IsAlive)
            return false;

        if (!TryFindLegacyEnemyMovementController(
                enemy.RuntimeEnemyId,
                out EnemySystemMovementController movementController))
        {
            LogCustom(
                "[NpcRouteDebug] Legacy enemy route failed: movement controller not found. Enemy=" +
                enemy.RuntimeEnemyId);

            return false;
        }

        float safeSmallDotSpacing =
            npcRouteLineView != null
                ? npcRouteLineView.SmallDotSpacing
                : 20f;

        int safeMaxBigDots =
            npcRouteLineView != null
                ? Mathf.Max(1, npcRouteLineView.MaxBigDots)
                : 160;

        int safeMaxSmallDots =
            npcRouteLineView != null
                ? Mathf.Max(0, npcRouteLineView.MaxSmallDots)
                : 900;

        return movementController.TryBuildRoutePreview2A(
            preview,
            safeSmallDotSpacing,
            safeMaxBigDots,
            safeMaxSmallDots,
            GetSecondsPerTick());
    }

    private bool TryFindLegacyEnemyMovementController(
    string runtimeEnemyId,
    out EnemySystemMovementController movementController)
    {
        movementController = null;

        if (string.IsNullOrWhiteSpace(runtimeEnemyId))
            return false;

#if UNITY_2023_1_OR_NEWER
        EnemySystemMapEntity[] enemyViews =
            Object.FindObjectsByType<EnemySystemMapEntity>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
    EnemySystemMapEntity[] enemyViews =
        Object.FindObjectsOfType<EnemySystemMapEntity>();
#endif

        for (int i = 0; i < enemyViews.Length; i++)
        {
            EnemySystemMapEntity enemyView = enemyViews[i];

            if (enemyView == null ||
                !enemyView.IsBound ||
                enemyView.RuntimeEnemyId != runtimeEnemyId)
            {
                continue;
            }

            movementController =
                enemyView.GetComponent<EnemySystemMovementController>();

            return movementController != null;
        }

        return false;
    }

    private bool TryBuildLegacyEnemyRoutePath(
     SystemEnemyRuntimeState enemy,
     Vector3 destinationPosition,
     List<Vector3> routePath)
    {
        if (enemy == null || routePath == null)
            return false;

        routePath.Clear();

        if (_shipRouteService == null)
        {
            LogCustom("[NpcRouteDebug] Legacy enemy route failed: ship route service is null.");
            return false;
        }

        SystemShipRouteRequest2A request =
            new SystemShipRouteRequest2A
            {
                SystemId = enemy.SystemId,
                StartPosition = enemy.Position,
                DestinationPosition = destinationPosition,
                StartFacingDirection = GetLegacyEnemyFacingDirection(
                    enemy,
                    destinationPosition),
                TargetKind = SystemShipRouteTargetKind2A.Enemy,
                Settings = CreateLegacyEnemyRouteSettings(enemy)
            };

        if (!_shipRouteService.TryBuildRoute(
                request,
                _legacyEnemyRouteBuildResult))
        {
            return false;
        }

        routePath.AddRange(_legacyEnemyRouteBuildResult.Path);

        return routePath.Count > 1;
    }

    private SystemShipRouteSettings2A CreateLegacyEnemyRouteSettings(
SystemEnemyRuntimeState enemy)
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, enemy != null ? enemy.Speed : 0f),
            TurnRadius = enemy != null && enemy.EnemyConfig != null
                ? Mathf.Max(0f, enemy.EnemyConfig.TurnRadius)
                : 0f,
            ArrivalDistanceThreshold = ArrivalDistanceThreshold,
            SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
            SunAvoidanceArcSegments = SunAvoidanceArcSegments,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f,
            MaxRoutePlanSteps = RoutePlanMaxSteps,
            SunAvoidanceTurnRouteReserveMultiplier = 1.5f
        };
    }

    private void BuildLegacyEnemyTravelWaypoints(
        SystemEnemyRuntimeState enemy,
        Vector3 destinationPosition,
        List<Vector3> waypoints)
    {
        waypoints.Clear();

        if (enemy == null)
            return;

        if (_configService == null)
        {
            waypoints.Add(enemy.Position);
            waypoints.Add(destinationPosition);
            return;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(enemy.SystemId);

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            waypoints.Add(enemy.Position);
            waypoints.Add(destinationPosition);
            return;
        }

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                enemy.Position.z);

        float sunRadius =
            Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);

        float avoidanceRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        float turnRadius =
            enemy.EnemyConfig != null
                ? Mathf.Max(0f, enemy.EnemyConfig.TurnRadius)
                : 0f;

        SystemTravelSunAvoidancePath2A.BuildPath(
            waypoints,
            enemy.Position,
            destinationPosition,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments,
            false,
            GetLegacyEnemyFacingDirection(
                enemy,
                destinationPosition),
            turnRadius);
    }

    private Vector2 GetLegacyEnemyFacingDirection(
        SystemEnemyRuntimeState enemy,
        Vector3 destinationPosition)
    {
        if (enemy == null)
            return Vector2.up;

        Vector3 direction =
            destinationPosition - enemy.Position;

        direction.z = 0f;

        if (direction.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return Vector2.up;

        return new Vector2(
            direction.x,
            direction.y).normalized;
    }

    private void AddSmallRoutePreviewDots(
     TravelRoutePreview2A preview,
     List<Vector3> path,
     float intervalStartDistance,
     float intervalEndDistance,
     float passedDistance,
     int tickIndex,
     float smallDotSpacing,
     int maxSmallDots)
    {
        if (preview == null ||
            path == null ||
            path.Count <= 1)
        {
            return;
        }

        if (preview.SmallDotCount >= maxSmallDots)
            return;

        float safeSpacing =
            Mathf.Max(0.01f, smallDotSpacing);

        float firstDotDistance =
            Mathf.Ceil(
                (Mathf.Max(intervalStartDistance, passedDistance) + 0.001f) /
                safeSpacing) *
            safeSpacing;

        for (float dotDistance = firstDotDistance;
             dotDistance < intervalEndDistance - 0.001f;
             dotDistance += safeSpacing)
        {
            if (preview.SmallDotCount >= maxSmallDots)
                return;

            preview.AddSmallDot(
                GetPointOnPathAtDistance(path, dotDistance),
                tickIndex);
        }
    }

    private LegacyEnemyPreviewRouteState GetOrCreateLegacyEnemyPreviewRouteState(
    string runtimeEnemyId)
    {
        if (!_legacyEnemyPreviewRoutes.TryGetValue(
                runtimeEnemyId,
                out LegacyEnemyPreviewRouteState routeState))
        {
            routeState = new LegacyEnemyPreviewRouteState();
            _legacyEnemyPreviewRoutes[runtimeEnemyId] = routeState;
        }

        return routeState;
    }

    private bool ShouldRebuildLegacyEnemyPreviewRoute(
    LegacyEnemyPreviewRouteState routeState,
    SystemEnemyRuntimeState enemy,
    Vector3 destinationPosition)
    {
        if (routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return true;
        }

        float totalPathLength =
            GetPathLength(routeState.Path);

        if (routeState.DistanceTravelled >= totalPathLength - ArrivalDistanceThreshold)
            return true;

        float destinationRefreshDistance =
            Mathf.Max(
                ArrivalDistanceThreshold * 4f,
                Mathf.Max(0.01f, enemy.Speed) * Mathf.Max(0.01f, GetSecondsPerTick()));

        return Vector3.Distance(
            routeState.Destination,
            destinationPosition) > destinationRefreshDistance;
    }

    private void AccumulateLegacyEnemyPreviewDistance(
    LegacyEnemyPreviewRouteState routeState,
    SystemEnemyRuntimeState enemy)
    {
        if (routeState == null ||
            enemy == null)
        {
            return;
        }

        float movedDistance =
            Vector3.Distance(
                routeState.LastEnemyPosition,
                enemy.Position);

        if (movedDistance > 0f)
            routeState.DistanceTravelled += movedDistance;

        routeState.LastEnemyPosition = enemy.Position;
    }

    private void RefreshLegacyEnemyRoute()
    {
        if (npcRouteLineView == null)
            return;

        if (_enemyService == null)
        {
            StopFollowingUnavailableSelectedTarget("EnemyServiceMissing");
            return;
        }

        if (!_enemyService.TryGetEnemy(
                _selectedNpcId,
                out SystemEnemyRuntimeState enemy))
        {
            StopFollowingUnavailableSelectedTarget("EnemyNotFound");
            return;
        }

        if (enemy == null ||
            !enemy.IsAlive ||
            !IsLegacyEnemyRouteTargetAvailableInCurrentSystem(enemy))
        {
            StopFollowingUnavailableSelectedTarget("EnemyUnavailable");
            return;
        }

        if (!TryBuildLegacyEnemyRoutePreview(
                enemy,
                _preview))
        {
            npcRouteLineView.Hide();
            return;
        }

        ApplyLegacyEnemyRouteLineSettings(enemy);
        npcRouteLineView.ShowPreview(_preview);
    }

    private float GetRoutePlanStepDistance(
        float speed)
    {
        int substepsPerTick =
            _configService != null &&
            _configService.ShipMovementConfig != null
                ? _configService.ShipMovementConfig.RouteSubstepsPerTick
                : 10;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            speed / Mathf.Max(1, substepsPerTick));
    }

    private int GetRoutePlanMaxSteps(
        float waypointPathLength,
        float turnRadius,
        float routeStepDistance)
    {
        float expectedLength =
            Mathf.Max(0f, waypointPathLength) +
            Mathf.Max(0f, turnRadius) *
            Mathf.PI *
            2f;

        int steps =
            Mathf.CeilToInt(
                expectedLength /
                Mathf.Max(ArrivalDistanceThreshold, routeStepDistance)) + 64;

        return Mathf.Clamp(
            steps,
            32,
            RoutePlanMaxSteps);
    }

    private float GetIntermediateWaypointArrivalDistanceThreshold(
        IReadOnlyList<Vector3> waypoints,
        float routeStepDistance)
    {
        if (waypoints == null ||
            waypoints.Count <= 2)
        {
            return ArrivalDistanceThreshold;
        }

        return Mathf.Max(
            ArrivalDistanceThreshold,
            routeStepDistance * 2f);
    }

    private float GetRouteStraightExitAngleDegrees()
    {
        if (_configService == null ||
            _configService.ShipMovementConfig == null)
        {
            return 3f;
        }

        return _configService.ShipMovementConfig.RouteStraightExitAngleDegrees;
    }

    private float GetPathLength(
        IReadOnlyList<Vector3> path)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
    }

    private Vector3 GetPointOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count == 0)
        {
            return Vector3.zero;
        }

        if (path.Count == 1)
            return path[0];

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                float t = remainingDistance / segmentDistance;
                return Vector3.Lerp(from, to, t);
            }

            remainingDistance -= segmentDistance;
        }

        return path[path.Count - 1];
    }

    private float GetSunWorldSize(
        SunConfig sun)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetSunWorldSize(sun);
        }

        return sun != null
            ? sun.VisualSize
            : 0f;
    }
}