using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemNpcView : CustomMonoBehaviour, IPointerClickHandler
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;
    private const bool NpcMilitaryViewDebugLogEnabled = false;

    [Header("View")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SystemNpcRuntimeState systemNpcRuntimeState;

    [Header("Runtime")]
    [SerializeField] private string runtimeNpcId;
    [SerializeField] private SystemNpcType npcType;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateOffscreenViewCount;
    private static int _aggregateDestroyedMissingCount;
    private static int _aggregateDestroyedDeadCount;
    private static int _aggregateDestroyedOffscreenCount;
    private static double _aggregateTotalMs;
    private static double _aggregateBoundCheckMs;
    private static double _aggregateTryGetNpcMs;
    private static double _aggregateCurrentSystemCheckMs;
    private static double _aggregateAssignStateMs;
    private static double _aggregateAliveCheckMs;
    private static double _aggregateApplyPositionMs;
    private static double _aggregateApplyDirectionMs;
    private static double _aggregateDestroyMs;
    private static double _aggregateMaxMs;
    private static string _aggregateMaxNpcId = string.Empty;
    private static string _aggregateMaxNpcName = string.Empty;
    private static string _aggregateMaxNpcSystemId = string.Empty;
    private static string _aggregateMaxCurrentSystemId = string.Empty;

    private SimpleEventBus _simpleEventBus;
    private IGameSessionService _gameSessionService;
    private ISystemNpcRuntimeService _runtimeService;
    private IPlayerAttackService _playerAttackService;
    private ISystemTravelService _systemTravelService;

    private Quaternion _initialRootRotation;
    private Quaternion _initialSpriteLocalRotation;
    private Quaternion _lastSpriteRotation;

    private bool _isInitialized;

    public bool IsBound => !string.IsNullOrWhiteSpace(runtimeNpcId);
    public string RuntimeNpcId => runtimeNpcId;
    public float WorldSize { get; private set; }

    private void Initialize()
    {
        if (_isInitialized)
            return;

        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _playerAttackService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerAttackService>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _initialRootRotation = transform.rotation;

        if (spriteRenderer != null)
        {
            _initialSpriteLocalRotation = spriteRenderer.transform.localRotation;
            _lastSpriteRotation = _initialSpriteLocalRotation;
        }

        _simpleEventBus.Subscribe<SystemNpcBehaviorChangedEvent>(
            OnSystemNpcBehaviorChangedEvent);

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        _simpleEventBus?.Unsubscribe<SystemNpcBehaviorChangedEvent>(
            OnSystemNpcBehaviorChangedEvent);
    }

    private void OnSystemNpcBehaviorChangedEvent(SystemNpcBehaviorChangedEvent evt)
    {
        if (evt.RuntimeNpcId != runtimeNpcId)
            return;

        SystemNpcRuntimeState npc = null;

        if (_runtimeService != null)
            _runtimeService.TryGetNpc(runtimeNpcId, out npc);

        bool isMilitary =
            npc != null &&
            npc.IsAlly &&
            npc.AllyRole == AllyRole2A.Military;

        bool wasActive =
            gameObject.activeSelf;

        bool shouldBeActive =
            evt.BehaviorType != SystemNpcBehaviorType.StayOnPlanetForDays &&
            evt.BehaviorType != SystemNpcBehaviorType.AnnihilateOnPlanet;

        if (NpcMilitaryViewDebugLogEnabled && isMilitary)
        {
            LogCustom(
                "[NPC-MILITARY-VIEW] BehaviorChanged received. " +
                "Npc=" + runtimeNpcId +
                ", EventBehavior=" + evt.BehaviorType +
                ", WasActive=" + wasActive +
                ", ShouldBeActive=" + shouldBeActive +
                ", RuntimeBehavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", RuntimePosition=" + npc.CurrentPosition +
                ", ViewPositionBefore=" + transform.position);
        }

        if (shouldBeActive && npc != null)
        {
            transform.position = npc.CurrentPosition;
            transform.rotation = _initialRootRotation;

            ApplyTickLockedDirection(npc);
        }

        gameObject.SetActive(shouldBeActive);

        if (NpcMilitaryViewDebugLogEnabled && isMilitary)
        {
            LogCustom(
                "[NPC-MILITARY-VIEW] BehaviorChanged applied. " +
                "Npc=" + runtimeNpcId +
                ", EventBehavior=" + evt.BehaviorType +
                ", ActiveAfter=" + gameObject.activeSelf +
                ", RuntimePosition=" + npc.CurrentPosition +
                ", ViewPositionAfter=" + transform.position);
        }
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double boundCheckMs = 0.0;
        double tryGetNpcMs = 0.0;
        double currentSystemCheckMs = 0.0;
        double assignStateMs = 0.0;
        double aliveCheckMs = 0.0;
        double applyPositionMs = 0.0;
        double applyDirectionMs = 0.0;
        double destroyMs = 0.0;

        bool wasBound = false;
        bool npcFound = false;
        bool npcAlive = false;
        bool isOffscreenView = false;
        bool destroyRequested = false;

        string currentSystemId = string.Empty;
        string npcSystemId = string.Empty;

        SystemNpcRuntimeState npc = null;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            wasBound =
                IsBound;

            boundCheckMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!wasBound)
                return;

            if (!_isInitialized)
                Initialize();

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            npcFound =
                _runtimeService.TryGetNpc(
                    runtimeNpcId,
                    out npc);

            tryGetNpcMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!npcFound)
            {
                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                destroyRequested = true;
                Destroy(gameObject);

                destroyMs =
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            currentSystemId =
                GetCurrentSystemId();

            npcSystemId =
                npc.CurrentSystemId;

            isOffscreenView =
                !string.IsNullOrWhiteSpace(currentSystemId) &&
                !string.Equals(
                    npcSystemId,
                    currentSystemId,
                    System.StringComparison.Ordinal);

            currentSystemCheckMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (isOffscreenView)
            {
                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                destroyRequested = true;
                Destroy(gameObject);

                destroyMs =
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            systemNpcRuntimeState =
                npc;

            assignStateMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            npcAlive =
                npc.IsAlive;

            aliveCheckMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!npcAlive)
            {
                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                destroyRequested = true;
                Destroy(gameObject);

                destroyMs =
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            transform.position =
                npc.CurrentPosition;

            transform.rotation =
                _initialRootRotation;

            applyPositionMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplyTickLockedDirection(npc);

            applyDirectionMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            if (wasBound)
            {
                RecordUpdateAggregate(
                    elapsedMs,
                    boundCheckMs,
                    tryGetNpcMs,
                    currentSystemCheckMs,
                    assignStateMs,
                    aliveCheckMs,
                    applyPositionMs,
                    applyDirectionMs,
                    destroyMs,
                    isOffscreenView,
                    destroyRequested,
                    npcFound,
                    npcAlive,
                    runtimeNpcId,
                    name,
                    npcSystemId,
                    currentSystemId);
            }

            if (VisualUpdatePerfLog.ShouldLog(elapsedMs))
            {
                VisualUpdatePerfLog.LogMeasured(
                    "SystemNpcView.Update",
                    elapsedMs,
                    "Npc=" + runtimeNpcId +
                    " | Name=" + name +
                    " | WasBound=" + wasBound +
                    " | NpcFound=" + npcFound +
                    " | NpcAlive=" + npcAlive +
                    " | IsOffscreenView=" + isOffscreenView +
                    " | DestroyRequested=" + destroyRequested +
                    " | Type=" + npcType +
                    " | CurrentSystemId=" + currentSystemId +
                    " | NpcSystemId=" + npcSystemId +
                    " | Behavior=" + (npc != null ? npc.CurrentBehavior.ToString() : "") +
                    " | TravelState=" + (npc != null ? npc.TravelState.ToString() : "") +
                    " | BoundCheckMs=" + boundCheckMs.ToString("F3") +
                    " | TryGetNpcMs=" + tryGetNpcMs.ToString("F3") +
                    " | CurrentSystemCheckMs=" + currentSystemCheckMs.ToString("F3") +
                    " | AssignStateMs=" + assignStateMs.ToString("F3") +
                    " | AliveCheckMs=" + aliveCheckMs.ToString("F3") +
                    " | ApplyPositionMs=" + applyPositionMs.ToString("F3") +
                    " | ApplyDirectionMs=" + applyDirectionMs.ToString("F3") +
                    " | DestroyMs=" + destroyMs.ToString("F3"));
            }
        }
    }

    public void Bind(
        SystemNpcRuntimeState npc,
        Sprite sprite,
        float worldSize = 0f)
    {
        Initialize();

        if (npc == null)
        {
            Debug.LogError("[SystemNpcView] Cannot bind null NPC.");
            return;
        }

        runtimeNpcId = npc.RuntimeNpcId;
        npcType = npc.NpcType;
        WorldSize = worldSize;

        transform.position = npc.CurrentPosition;
        transform.rotation = _initialRootRotation;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;

            if (worldSize > 0f)
            {
                SpriteRendererSizeUtility.SetWorldSize(
                    spriteRenderer,
                    worldSize);
            }

            spriteRenderer.transform.localRotation = _lastSpriteRotation;
        }

        gameObject.name = $"SystemNpcView_{npc.NpcType}_{npc.ConfigId}_{npc.RuntimeNpcId}";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsBound)
            return;

        _simpleEventBus?.Publish(
            new SystemObjectsPanelCloseRequestedEvent2A());

        if (!_runtimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
            return;

        if (!npc.IsAlive)
            return;

        _simpleEventBus?.Publish(
            new SystemSelectedTargetInfoPanelRequestedEvent2A(
                runtimeNpcId,
                npc.IsHostileToPlayer
                    ? SystemGameplayTargetType.Enemy
                    : SystemGameplayTargetType.Ally));

        if (!npc.IsEnemy && !npc.IsPirate)
        {
            _playerAttackService?.ClearSelectedTargetIfNoAssignedWeapons();
            _systemTravelService?.SetNpcDestination(runtimeNpcId);
            return;
        }

        _playerAttackService.SetTarget(runtimeNpcId);
    }

    private string GetCurrentSystemId()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return string.Empty;
        }

        return _gameSessionService.State.Player.CurrentSystemId;
    }

    private void ApplyTickLockedDirection(SystemNpcRuntimeState npc)
    {
        if (spriteRenderer == null)
            return;

        Vector3 direction = npc.FacingDirection;
        direction.z = 0f;

        if (!IsFinite(direction) ||
            direction.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            direction = npc.TickMovementDirection;
            direction.z = 0f;
        }

        if (IsFinite(direction) &&
            direction.sqrMagnitude > DirectionThresholdSqrMagnitude)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            _lastSpriteRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle - 90f);
        }

        spriteRenderer.transform.localRotation = _lastSpriteRotation;
    }

    private static void RecordUpdateAggregate(
        double elapsedMs,
        double boundCheckMs,
        double tryGetNpcMs,
        double currentSystemCheckMs,
        double assignStateMs,
        double aliveCheckMs,
        double applyPositionMs,
        double applyDirectionMs,
        double destroyMs,
        bool isOffscreenView,
        bool destroyRequested,
        bool npcFound,
        bool npcAlive,
        string npcId,
        string npcName,
        string npcSystemId,
        string currentSystemId)
    {
        int frame =
            Time.frameCount;

        if (_aggregateFrame != frame)
        {
            FlushUpdateAggregate();
            ResetUpdateAggregate(frame);
        }

        _aggregateCount++;
        _aggregateTotalMs += elapsedMs;
        _aggregateBoundCheckMs += boundCheckMs;
        _aggregateTryGetNpcMs += tryGetNpcMs;
        _aggregateCurrentSystemCheckMs += currentSystemCheckMs;
        _aggregateAssignStateMs += assignStateMs;
        _aggregateAliveCheckMs += aliveCheckMs;
        _aggregateApplyPositionMs += applyPositionMs;
        _aggregateApplyDirectionMs += applyDirectionMs;
        _aggregateDestroyMs += destroyMs;

        if (isOffscreenView)
            _aggregateOffscreenViewCount++;

        if (destroyRequested && !npcFound)
            _aggregateDestroyedMissingCount++;

        if (destroyRequested && npcFound && !npcAlive)
            _aggregateDestroyedDeadCount++;

        if (destroyRequested && isOffscreenView)
            _aggregateDestroyedOffscreenCount++;

        if (elapsedMs > _aggregateMaxMs)
        {
            _aggregateMaxMs = elapsedMs;
            _aggregateMaxNpcId = npcId ?? string.Empty;
            _aggregateMaxNpcName = npcName ?? string.Empty;
            _aggregateMaxNpcSystemId = npcSystemId ?? string.Empty;
            _aggregateMaxCurrentSystemId = currentSystemId ?? string.Empty;
        }
    }

    private static void ResetUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateOffscreenViewCount = 0;
        _aggregateDestroyedMissingCount = 0;
        _aggregateDestroyedDeadCount = 0;
        _aggregateDestroyedOffscreenCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateBoundCheckMs = 0.0;
        _aggregateTryGetNpcMs = 0.0;
        _aggregateCurrentSystemCheckMs = 0.0;
        _aggregateAssignStateMs = 0.0;
        _aggregateAliveCheckMs = 0.0;
        _aggregateApplyPositionMs = 0.0;
        _aggregateApplyDirectionMs = 0.0;
        _aggregateDestroyMs = 0.0;
        _aggregateMaxMs = 0.0;
        _aggregateMaxNpcId = string.Empty;
        _aggregateMaxNpcName = string.Empty;
        _aggregateMaxNpcSystemId = string.Empty;
        _aggregateMaxCurrentSystemId = string.Empty;
    }

    private static void FlushUpdateAggregate()
    {
        if (_aggregateFrame < 0 ||
            _aggregateCount <= 0)
        {
            return;
        }

        bool shouldLog =
            VisualUpdatePerfLog.ShouldLog(_aggregateTotalMs) ||
            _aggregateOffscreenViewCount > 0 ||
            _aggregateDestroyedOffscreenCount > 0;

        if (!shouldLog)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        DebugLogConfig config =
            Bootstrapper.Instance.DebugLogConfig;

        double thresholdMs =
            config != null
                ? config.VisualUpdateSpikeThresholdMs
                : 1.0;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[VISUAL_UPDATE_SPIKE]" +
            " Marker=SystemNpcView.Update.Aggregate" +
            " | UnityFrame=" + _aggregateFrame +
            " | Ms=" + _aggregateTotalMs.ToString("F2") +
            " | ThresholdMs=" + thresholdMs.ToString("F2") +
            " | ViewCount=" + _aggregateCount +
            " | OffscreenViewCount=" + _aggregateOffscreenViewCount +
            " | DestroyedMissingCount=" + _aggregateDestroyedMissingCount +
            " | DestroyedDeadCount=" + _aggregateDestroyedDeadCount +
            " | DestroyedOffscreenCount=" + _aggregateDestroyedOffscreenCount +
            " | MaxSingleMs=" + _aggregateMaxMs.ToString("F3") +
            " | MaxNpc=" + _aggregateMaxNpcId +
            " | MaxNpcName=" + _aggregateMaxNpcName +
            " | MaxNpcSystemId=" + _aggregateMaxNpcSystemId +
            " | MaxCurrentSystemId=" + _aggregateMaxCurrentSystemId +
            " | BoundCheckMs=" + _aggregateBoundCheckMs.ToString("F3") +
            " | TryGetNpcMs=" + _aggregateTryGetNpcMs.ToString("F3") +
            " | CurrentSystemCheckMs=" + _aggregateCurrentSystemCheckMs.ToString("F3") +
            " | AssignStateMs=" + _aggregateAssignStateMs.ToString("F3") +
            " | AliveCheckMs=" + _aggregateAliveCheckMs.ToString("F3") +
            " | ApplyPositionMs=" + _aggregateApplyPositionMs.ToString("F3") +
            " | ApplyDirectionMs=" + _aggregateApplyDirectionMs.ToString("F3") +
            " | DestroyMs=" + _aggregateDestroyMs.ToString("F3"));
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}