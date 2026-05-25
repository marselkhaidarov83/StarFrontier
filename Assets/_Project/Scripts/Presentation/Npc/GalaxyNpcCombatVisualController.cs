using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcCombatVisualController : CustomMonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private Transform vfxRoot;

    [Header("Prefabs")]
    [SerializeField] private GalaxyNpcProjectileView projectilePrefab;
    [SerializeField] private GameObject hitVfxPrefab;

    private SimpleEventBus _eventBus;
    private ISystemTravelService _systemTravelService;
    private IGameSessionService _gameSessionService;

    private readonly Dictionary<string, GalaxyNpcProjectileView> _projectiles = new();

    private void Awake()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        if (projectileRoot == null)
            projectileRoot = transform;

        if (vfxRoot == null)
            vfxRoot = transform;
    }

    private void OnEnable()
    {
        _eventBus.Subscribe<GalaxyNpcProjectileCreatedEvent>(OnProjectileCreated);
        _eventBus.Subscribe<GalaxyNpcProjectileImpactEvent>(OnProjectileImpact);
    }

    private void OnDisable()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<GalaxyNpcProjectileCreatedEvent>(OnProjectileCreated);
        _eventBus.Unsubscribe<GalaxyNpcProjectileImpactEvent>(OnProjectileImpact);
    }

    private void OnProjectileCreated(GalaxyNpcProjectileCreatedEvent evt)
    {
        if (evt.SystemId != GetCurrentSystemId())
            return;

        if (projectilePrefab == null)
        {
            Debug.LogWarning("[GalaxyNpcCombatVisualController] Projectile prefab is missing.");
            return;
        }

        LogCustom("evt.StartPosition = " + evt.StartPosition);
        GalaxyNpcProjectileView projectile = Instantiate(
            projectilePrefab,
            evt.StartPosition,
            Quaternion.identity,
            projectileRoot
        );

        projectile.Init(evt);

        _projectiles[evt.ProjectileId] = projectile;
    }

    private void OnProjectileImpact(GalaxyNpcProjectileImpactEvent evt)
    {
        if (evt.SystemId != GetCurrentSystemId())
            return;

        if (_projectiles.TryGetValue(evt.ProjectileId, out GalaxyNpcProjectileView projectile))
        {
            if (projectile != null)
                projectile.Complete();

            _projectiles.Remove(evt.ProjectileId);
        }

        if (evt.DidHit && hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(
                hitVfxPrefab,
                evt.HitPosition,
                Quaternion.identity,
                vfxRoot
            );

            Destroy(vfx, 0.5f);
        }
    }

    private string GetCurrentSystemId()
    {
        return _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;
    }
}