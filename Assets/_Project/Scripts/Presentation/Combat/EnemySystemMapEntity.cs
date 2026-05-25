using UnityEngine;
using UnityEngine.EventSystems;

public sealed class EnemySystemMapEntity : CustomMonoBehaviour, IPointerClickHandler
{
    [Header("Runtime Binding")]
    [SerializeField] private string runtimeEnemyId;

    [Header("View")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private SimpleEventBus _simpleEventBus;
    private ISystemEnemyService _enemyService;
    private EnemySystemMovementController _movementController;
    private WeaponFireController _playerWeaponFireController;

    private bool _isBound;

    public string RuntimeEnemyId => runtimeEnemyId;
    public bool IsBound => _isBound;

    private void Awake()
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _movementController = GetComponent<EnemySystemMovementController>();
        _playerWeaponFireController = FindObjectOfType<WeaponFireController>();
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

        if (spriteRenderer != null && runtimeEnemy.EnemyConfig != null)
            spriteRenderer.sprite = runtimeEnemy.EnemyConfig.CombatSprite;

        if (_movementController != null)
            _movementController.ApplyRuntimeConfig(runtimeEnemy);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[EnemySystemMapEntity] Pointer click received on {name}");

        if (!_isBound)
        {
            Debug.LogWarning("[EnemySystemMapEntity] Click ignored: enemy is not bound.");
            return;
        }

        if (_playerWeaponFireController == null)
            _playerWeaponFireController = FindObjectOfType<WeaponFireController>();

        if (_playerWeaponFireController == null)
        {
            Debug.LogWarning("[EnemySystemMapEntity] WeaponFireController not found.");
            return;
        }

        _playerWeaponFireController.SelectTarget(this);
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