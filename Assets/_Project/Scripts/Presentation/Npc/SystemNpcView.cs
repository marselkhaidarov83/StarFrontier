using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemNpcView : CustomMonoBehaviour, IPointerClickHandler
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;

    [Header("View")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SystemNpcRuntimeState systemNpcRuntimeState;

    [Header("Runtime")]
    [SerializeField] private string runtimeNpcId;
    [SerializeField] private SystemNpcType npcType;

    private SimpleEventBus _simpleEventBus;
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

        _simpleEventBus.Subscribe<SystemNpcBehaviorChangedEvent>(OnSystemNpcBehaviorChangedEvent);

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        _simpleEventBus?.Unsubscribe<SystemNpcBehaviorChangedEvent>(OnSystemNpcBehaviorChangedEvent);
    }

    private void OnSystemNpcBehaviorChangedEvent(SystemNpcBehaviorChangedEvent evt)
    {
        if (evt.RuntimeNpcId != runtimeNpcId)
            return;

        gameObject.SetActive(
            evt.BehaviorType != SystemNpcBehaviorType.StayOnPlanetForDays &&
            evt.BehaviorType != SystemNpcBehaviorType.AnnihilateOnPlanet);
    }

    private void Update()
    {
        if (!IsBound)
            return;

        if (!_runtimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
        {
            Destroy(gameObject);
            return;
        }

        systemNpcRuntimeState = npc;

        if (!npc.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = npc.CurrentPosition;
        transform.rotation = _initialRootRotation;

        ApplyTickLockedDirection(npc);
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
