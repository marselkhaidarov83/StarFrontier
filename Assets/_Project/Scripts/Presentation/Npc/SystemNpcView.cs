using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemNpcView : CustomMonoBehaviour, IPointerClickHandler
{
    private const float DirectionThresholdSqrMagnitude = 0.001f;

    [Header("View")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SystemNpcRuntimeState systemNpcRuntimeState;

    [Header("Runtime")]
    [SerializeField] private string runtimeNpcId;
    [SerializeField] private SystemNpcType npcType;

    private SimpleEventBus _simpleEventBus;
    private ISystemNpcRuntimeService _runtimeService;
    private IPlayerAttackService _playerAttackService;
    private Vector3 _lastPosition;
    private bool _hasLastPosition;

    public bool IsBound => !string.IsNullOrWhiteSpace(runtimeNpcId);

    private void Initialize()
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _playerAttackService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerAttackService>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _simpleEventBus.Subscribe<SystemNpcBehaviorChangedEvent>(OnSystemNpcBehaviorChangedEvent);
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

        Vector3 lookDirection =
            npc.CurrentMovementTargetPosition != Vector3.zero
                ? npc.CurrentMovementTargetPosition - npc.CurrentPosition
                : Vector3.zero;

        if (!SetDirection(lookDirection) && _hasLastPosition)
            SetDirection(transform.position - _lastPosition);

        _lastPosition = transform.position;
        _hasLastPosition = true;
    }

    public void Bind(SystemNpcRuntimeState npc, Sprite sprite)
    {
        Initialize();

        if (npc == null)
        {
            Debug.LogError("[SystemNpcView] Cannot bind null NPC.");
            return;
        }

        runtimeNpcId = npc.RuntimeNpcId;
        npcType = npc.NpcType;

        transform.position = npc.CurrentPosition;
        _lastPosition = npc.CurrentPosition;
        _hasLastPosition = true;

        if (spriteRenderer != null)
            spriteRenderer.sprite = sprite;

        gameObject.name = $"SystemNpcView_{npc.NpcType}_{npc.ConfigId}_{npc.RuntimeNpcId}";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LogCustom("");

        if (!IsBound)
            return;

        if (!_runtimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
            return;

        if (!npc.IsEnemy && !npc.IsPirate)
            return;

        LogCustom("runtimeNpcId = " + runtimeNpcId);
        _playerAttackService.SetTarget(runtimeNpcId);
    }

    private bool SetDirection(Vector3 movementDirection)
    {
        if (!IsFinite(movementDirection) ||
            movementDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        float angle =
            Mathf.Atan2(
                movementDirection.y,
                movementDirection.x)
            * Mathf.Rad2Deg;

        if (spriteRenderer != null)
        {
            spriteRenderer
                .transform
                .localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle - 90f);
        }

        return true;
    }

    private static bool IsFinite(Vector3 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}
