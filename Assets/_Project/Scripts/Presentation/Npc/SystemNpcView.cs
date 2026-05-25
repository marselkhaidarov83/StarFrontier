using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemNpcView : CustomMonoBehaviour, IPointerClickHandler
{
    [Header("View")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SystemNpcRuntimeState systemNpcRuntimeState;

    [Header("Runtime")]
    [SerializeField] private string runtimeNpcId;
    [SerializeField] private SystemNpcType npcType;

    private SimpleEventBus _simpleEventBus;
    private ISystemNpcRuntimeService _runtimeService;
    private IPlayerAttackService _playerAttackService;

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
}