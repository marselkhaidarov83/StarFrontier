using UnityEngine;

    public sealed class AllySystemMapEntity : MonoBehaviour
    {
        [Header("Runtime Binding")]
        [SerializeField] private string runtimeAllyId;

        [Header("View")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private ISystemAllyService _allyService;
        private AllySystemMovementController _movementController;
        private SimpleEventBus _eventBus;

        private bool _isBound;

        public string RuntimeAllyId => runtimeAllyId;
        public bool IsBound => _isBound;

        private void Awake()
        {
            _allyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemAllyService>();
            _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            _movementController = GetComponent<AllySystemMovementController>();
        }

        private void OnEnable()
        {
            if (_eventBus == null)
                _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

            _eventBus.Subscribe<SystemAllyDestroyedEvent>(OnAllyDestroyed);
        }

        private void OnDisable()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SystemAllyDestroyedEvent>(OnAllyDestroyed);
        }

        private void Update()
        {
            if (!_isBound)
                return;

            _allyService.UpdateAllyPosition(runtimeAllyId, transform.position);
        }

        public void Bind(SystemAllyRuntimeState runtimeAlly)
        {
            if (runtimeAlly == null)
            {
                Debug.LogError("[AllySystemMapEntity] Cannot bind null runtime ally.");
                return;
            }

            runtimeAllyId = runtimeAlly.RuntimeAllyId;
            _isBound = true;

            if (spriteRenderer != null && runtimeAlly.AllyConfig != null)
                spriteRenderer.sprite = runtimeAlly.AllyConfig.MapSprite;

            if (_movementController != null)
                _movementController.ApplyRuntimeConfig(runtimeAlly);
        }

        public void ApplyDamage(int damage)
        {
            if (!_isBound)
            {
                Debug.LogWarning("[AllySystemMapEntity] Cannot apply damage: ally view is not bound.");
                return;
            }

            _allyService.ApplyDamage(runtimeAllyId, damage);
        }

        private void OnAllyDestroyed(SystemAllyDestroyedEvent eventData)
        {
            if (!_isBound)
                return;

            if (eventData.RuntimeAllyId != runtimeAllyId)
                return;

            DestroyView();
        }

        [ContextMenu("Debug Damage 10")]
        private void DebugDamage10()
        {
            ApplyDamage(10);
        }

        [ContextMenu("Debug Kill Ally")]
        private void DebugKillAlly()
        {
            ApplyDamage(9999);
        }

        private void DestroyView()
        {
            if (gameObject != null)
                Destroy(gameObject);
        }
    }