using UnityEngine;

public sealed class SystemNpcCombatVisualController : CustomMonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform enemyVisualsRoot;
    [SerializeField] private Transform allyVisualsRoot;
    [SerializeField] private Transform playerVisualsRoot;

    [Header("Prefabs")]
    [SerializeField] private SystemNpcProjectileVisual projectileVisualPrefab;

    [Header("Visual Flight")]
    [SerializeField] private float fallbackProjectileSpeed = 12f;
    [SerializeField] private float hitscanVisualTravelSeconds = 0.08f;
    [SerializeField] private float minProjectileVisualTravelSeconds = 0.12f;
    [SerializeField] private float maxProjectileVisualTravelSeconds = 3f;

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
        if (_eventBus == null)
            return;

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

        WeaponConfig weaponConfig =
            _configService.GetWeaponConfigById(eventData.WeaponConfigId);

        float projectileSpeed =
            ResolveProjectileVisualSpeed(
                weaponConfig,
                eventData.StartPosition,
                eventData.TargetPosition
            );

        Transform root =
            ResolveProjectileRoot(shooter);

        SystemNpcProjectileVisual projectile = Instantiate(
            projectileVisualPrefab,
            eventData.StartPosition,
            Quaternion.identity,
            root
        );

        projectile.Init(
            eventData.StartPosition,
            eventData.TargetPosition,
            projectileSpeed
        );
    }

    private float ResolveProjectileVisualSpeed(
        WeaponConfig weaponConfig,
        Vector3 startPosition,
        Vector3 targetPosition)
    {
        if (weaponConfig == null)
            return fallbackProjectileSpeed;

        float distance =
            Vector3.Distance(
                startPosition,
                targetPosition
            );

        if (distance <= 0.01f)
            return fallbackProjectileSpeed;

        float travelSeconds =
            ResolveProjectileVisualTravelSeconds(weaponConfig);

        return Mathf.Max(
            0.01f,
            distance / travelSeconds
        );
    }

    private float ResolveProjectileVisualTravelSeconds(
        WeaponConfig weaponConfig)
    {
        if (weaponConfig == null)
            return minProjectileVisualTravelSeconds;

        if (weaponConfig.IsHitscan)
            return Mathf.Max(0.01f, hitscanVisualTravelSeconds);

        int minLifetimeTicks =
            Mathf.Max(
                1,
                weaponConfig.ProjectileLifetimeMin
            );

        int maxLifetimeTicks =
            Mathf.Max(
                minLifetimeTicks,
                weaponConfig.ProjectileLifetimeMax
            );

        float averageLifetimeTicks =
            (minLifetimeTicks + maxLifetimeTicks) * 0.5f;

        float secondsPerTick =
            Mathf.Max(
                0.01f,
                GameTimeService.SecondsPerDay
            );

        float travelSeconds =
            averageLifetimeTicks * secondsPerTick;

        return Mathf.Clamp(
            travelSeconds,
            minProjectileVisualTravelSeconds,
            maxProjectileVisualTravelSeconds
        );
    }

    private Transform ResolveProjectileRoot(
        SystemNpcRuntimeState shooter)
    {
        if (shooter == null)
            return enemyVisualsRoot != null ? enemyVisualsRoot : transform;

        if (shooter.IsEnemy || shooter.IsPirate)
            return enemyVisualsRoot != null ? enemyVisualsRoot : transform;

        if (shooter.IsAlly)
            return allyVisualsRoot != null ? allyVisualsRoot : transform;

        return enemyVisualsRoot != null ? enemyVisualsRoot : transform;
    }

    private void OnProjectileHit(SystemNpcProjectileHitEvent eventData)
    {
        LogCustom(
            $"Hit VFX. Target: {eventData.TargetNpcId}, Damage: {eventData.Damage}"
        );
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent eventData)
    {
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