using UnityEngine;
using UnityEngine.EventSystems;

public sealed class EnemySystemMapEntity : CustomMonoBehaviour, IPointerClickHandler
{
    [Header("Runtime Binding")]
    [SerializeField] private string runtimeEnemyId;

    [Header("View")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string sortingLayerName = "SystemShipFX";
    [SerializeField] private int sortingOrder = 210;

    private SimpleEventBus _simpleEventBus;
    private ISystemEnemyService _enemyService;
    private IConfigService _configService;
    private EnemySystemMovementController _movementController;

    private bool _isBound;

    public string RuntimeEnemyId => runtimeEnemyId;
    public bool IsBound => _isBound;

    private void Awake()
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet<IConfigService>(
                out _configService);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _movementController = GetComponent<EnemySystemMovementController>();
    }

    private void Update()
    {
        if (!_isBound)
            return;

        _enemyService.UpdateEnemyPosition(runtimeEnemyId, transform.position);
    }

    private void OnEnable()
    {
        if (_simpleEventBus == null)
            _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _simpleEventBus.Subscribe<SystemEnemyDestroyedEvent>(OnEnemyDestroyed);
    }

    private void OnDisable()
    {
        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Unsubscribe<SystemEnemyDestroyedEvent>(OnEnemyDestroyed);
    }

    private void OnEnemyDestroyed(SystemEnemyDestroyedEvent eventData)
    {
        if (!_isBound)
            return;

        if (eventData.RuntimeEnemyId != runtimeEnemyId)
            return;

        DestroyView();
    }

    public void Bind(SystemEnemyRuntimeState runtimeEnemy)
    {
        if (runtimeEnemy == null)
        {
            Debug.LogError("[EnemySystemMapEntity] Cannot bind null runtime enemy.");
            return;
        }

        runtimeEnemyId = runtimeEnemy.RuntimeEnemyId;
        _isBound = true;

        if (spriteRenderer != null)
        {
            if (runtimeEnemy.EnemyConfig != null)
            {
                spriteRenderer.sprite = runtimeEnemy.EnemyConfig.CombatSprite;

                if (_configService != null &&
                    _configService.SystemVisualConfig != null)
                {
                    float worldSize =
                        _configService
                            .SystemVisualConfig
                            .GetEnemyWorldSize(
                                runtimeEnemy.EnemyConfig);

                    if (worldSize > 0f)
                    {
                        SpriteRendererSizeUtility.SetWorldSize(
                            spriteRenderer,
                            worldSize);
                    }
                }
            }

            spriteRenderer.sortingLayerName = sortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = true;
        }

        if (_movementController != null)
            _movementController.ApplyRuntimeConfig(runtimeEnemy);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _simpleEventBus?.Publish(
            new SystemObjectsPanelCloseRequestedEvent2A());

        if (!_isBound)
        {
            Debug.LogWarning("[EnemySystemMapEntity] Click ignored: enemy is not bound.");
            return;
        }

        _simpleEventBus?.Publish(
            new SystemSelectedTargetInfoPanelRequestedEvent2A(
                runtimeEnemyId,
                SystemGameplayTargetType.Enemy));
    }
    public void ApplyDamage(int damage, bool fromPlayer)
    {
        if (!_isBound)
        {
            Debug.LogWarning("[EnemySystemMapEntity] Cannot apply damage: enemy view is not bound.");
            return;
        }

        _enemyService.ApplyDamage(runtimeEnemyId, damage, fromPlayer);
    }

    [ContextMenu("Debug Damage 10 By Player")]
    private void DebugDamageByPlayer()
    {
        ApplyDamage(10, true);
    }

    [ContextMenu("Debug Kill By Player")]
    private void DebugKillByPlayer()
    {
        ApplyDamage(9999, true);
        DestroyView();
    }

    [ContextMenu("Debug Kill By Ally")]
    private void DebugKillByAlly()
    {
        ApplyDamage(9999, false);
        DestroyView();
    }

    private void DestroyView()
    {
        if (gameObject != null)
            Destroy(gameObject);
    }
}
