using UnityEngine;

    public sealed class SystemNpcCombatVisualController : CustomMonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private Transform enemyVisualsRoot;
        [SerializeField] private Transform allyVisualsRoot;
        [SerializeField] private Transform playerVisualsRoot;

        [Header("Prefabs")]
        [SerializeField] private SystemNpcProjectileVisual projectileVisualPrefab;

        private ISystemNpcRuntimeService _runtimeService;
        private ISystemTravelService _systemTravelService;
        private IConfigService _configService;
        private SimpleEventBus _eventBus;

        private void Awake()
        {
            _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
            _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
            _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
            _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

            if (enemyVisualsRoot == null)
                enemyVisualsRoot = transform;
            if (allyVisualsRoot == null)
                allyVisualsRoot = transform;
            if (playerVisualsRoot == null)
                playerVisualsRoot = transform;
        }

        private void OnEnable()
        {
            _eventBus.Subscribe<SystemNpcWeaponFiredEvent>(OnWeaponFired);
            _eventBus.Subscribe<SystemNpcProjectileHitEvent>(OnProjectileHit);
            _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        }

        private void OnDisable()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SystemNpcWeaponFiredEvent>(OnWeaponFired);
            _eventBus.Unsubscribe<SystemNpcProjectileHitEvent>(OnProjectileHit);
            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        }

        private void OnWeaponFired(SystemNpcWeaponFiredEvent eventData)
        {
            if (projectileVisualPrefab == null)
                return;

            if (!_runtimeService.TryGetNpc(eventData.ShooterNpcId, out SystemNpcRuntimeState shooter))
                return;

            if (shooter.CurrentSystemId != GetCurrentSystemId())
                return;

            float projectileSpeed = 12f;

            WeaponConfig weaponConfig = _configService.GetWeaponConfigById(eventData.WeaponConfigId);

            if (weaponConfig != null)
                projectileSpeed = weaponConfig.ProjectileSpeed;

            SystemNpcProjectileVisual projectile = Instantiate(
                projectileVisualPrefab,
                eventData.StartPosition,
                Quaternion.identity,
                enemyVisualsRoot
            );

            projectile.Init(
                eventData.StartPosition,
                eventData.TargetPosition,
                projectileSpeed
            );
        }

        private void OnProjectileHit(SystemNpcProjectileHitEvent eventData)
        {
            // Пока только лог. Позже сюда можно добавить hit VFX.
            LogCustom(
                $"Hit VFX. Target: {eventData.TargetNpcId}, Damage: {eventData.Damage}"
            );
        }

        private void OnNpcDestroyed(SystemNpcDestroyedEvent eventData)
        {
            // Пока только лог. Позже сюда можно добавить explosion VFX.
            LogCustom(
                $"Destroyed VFX. NPC: {eventData.RuntimeNpcId}"
            );
        }

        private string GetCurrentSystemId()
        {
            if (_systemTravelService == null || _systemTravelService.State == null)
                return string.Empty;

            return _systemTravelService.State.CurrentSystemId;
        }
    }