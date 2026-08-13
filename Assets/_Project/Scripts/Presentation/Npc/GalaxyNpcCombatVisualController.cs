using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcCombatVisualController : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private Transform fxRoot;

    [Header("Prefabs")]
    [SerializeField] private GalaxyNpcProjectileView projectilePrefab;
    [SerializeField] private GalaxyNpcTimedFxView hitFxPrefab;
    [SerializeField] private GalaxyNpcTimedFxView explosionFxPrefab;

    [Header("Settings")]
    [SerializeField, Min(0.01f)] private float hitFxLifetimeSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float explosionFxLifetimeSeconds = 0.65f;

    private readonly Dictionary<string, GalaxyNpcProjectileView> _activeProjectiles = new();
    private readonly Queue<GalaxyNpcProjectileView> _projectilePool = new();
    private readonly Queue<GalaxyNpcTimedFxView> _hitFxPool = new();
    private readonly Queue<GalaxyNpcTimedFxView> _explosionFxPool = new();

    private SimpleEventBus _eventBus;
    private bool _isSubscribed;

    public int ActiveProjectileCount => _activeProjectiles.Count;
    public int ProjectilePoolCount => _projectilePool.Count;
    public int HitFxPoolCount => _hitFxPool.Count;
    public int ExplosionFxPoolCount => _explosionFxPool.Count;

    private void Awake()
    {
        if (projectileRoot == null)
            projectileRoot = transform;

        if (fxRoot == null)
            fxRoot = transform;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearActiveProjectiles();
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return;

        if (!bootstrapper.ServiceRegistry.TryGet(out _eventBus) || _eventBus == null)
            return;

        _eventBus.Subscribe<GalaxyNpcProjectileCreatedEvent>(OnProjectileCreated);
        _eventBus.Subscribe<GalaxyNpcProjectileImpactEvent>(OnProjectileImpact);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);

        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _eventBus == null)
            return;

        _eventBus.Unsubscribe<GalaxyNpcProjectileCreatedEvent>(OnProjectileCreated);
        _eventBus.Unsubscribe<GalaxyNpcProjectileImpactEvent>(OnProjectileImpact);
        _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);

        _isSubscribed = false;
        _eventBus = null;
    }

    private void OnProjectileCreated(GalaxyNpcProjectileCreatedEvent evt)
    {
        SpawnProjectile(evt);
    }

    private void OnProjectileImpact(GalaxyNpcProjectileImpactEvent evt)
    {
        CompleteProjectile(evt.ProjectileId);

        if (evt.DidHit)
            SpawnHitFx(evt.HitPosition);
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        SpawnExplosionFx(evt.Position);
    }

    public GalaxyNpcProjectileView SpawnProjectile(GalaxyNpcProjectileCreatedEvent evt)
    {
        if (string.IsNullOrWhiteSpace(evt.ProjectileId))
            return null;

        CompleteProjectile(evt.ProjectileId);

        GalaxyNpcProjectileView view = GetProjectileFromPool();
        view.Init(evt);

        _activeProjectiles[evt.ProjectileId] = view;
        return view;
    }

    public void MoveProjectile(string projectileId, Vector3 position)
    {
        if (!_activeProjectiles.TryGetValue(projectileId, out GalaxyNpcProjectileView view))
            return;

        view.SetPosition(position);
    }

    public void CompleteProjectile(string projectileId)
    {
        if (string.IsNullOrWhiteSpace(projectileId))
            return;

        if (!_activeProjectiles.TryGetValue(projectileId, out GalaxyNpcProjectileView view))
            return;

        _activeProjectiles.Remove(projectileId);
        view.Complete();
        _projectilePool.Enqueue(view);
    }

    public GalaxyNpcTimedFxView SpawnHitFx(Vector3 position)
    {
        return SpawnFx(position, hitFxPrefab, _hitFxPool, hitFxLifetimeSeconds);
    }

    public GalaxyNpcTimedFxView SpawnExplosionFx(Vector3 position)
    {
        return SpawnFx(position, explosionFxPrefab, _explosionFxPool, explosionFxLifetimeSeconds);
    }

    public void ReturnFxToPool(GalaxyNpcTimedFxView fx)
    {
        if (fx == null)
            return;

        if (fxPrefabMatches(fx, hitFxPrefab))
        {
            if (!_hitFxPool.Contains(fx))
                _hitFxPool.Enqueue(fx);

            return;
        }

        if (!_explosionFxPool.Contains(fx))
            _explosionFxPool.Enqueue(fx);
    }

    private GalaxyNpcProjectileView GetProjectileFromPool()
    {
        while (_projectilePool.Count > 0)
        {
            GalaxyNpcProjectileView pooled = _projectilePool.Dequeue();

            if (pooled != null)
                return pooled;
        }

        return Instantiate(projectilePrefab, projectileRoot);
    }

    private GalaxyNpcTimedFxView SpawnFx(
        Vector3 position,
        GalaxyNpcTimedFxView prefab,
        Queue<GalaxyNpcTimedFxView> pool,
        float lifetimeSeconds)
    {
        if (prefab == null)
            return null;

        GalaxyNpcTimedFxView fx = null;

        while (pool.Count > 0 && fx == null)
            fx = pool.Dequeue();

        if (fx == null)
            fx = Instantiate(prefab, fxRoot);

        fx.Init(this, position, lifetimeSeconds);
        return fx;
    }

    private void ClearActiveProjectiles()
    {
        foreach (GalaxyNpcProjectileView view in _activeProjectiles.Values)
        {
            if (view == null)
                continue;

            view.Complete();
            _projectilePool.Enqueue(view);
        }

        _activeProjectiles.Clear();
    }

    private static bool fxPrefabMatches(
        GalaxyNpcTimedFxView instance,
        GalaxyNpcTimedFxView prefab)
    {
        if (instance == null || prefab == null)
            return false;

        return instance.name.StartsWith(prefab.name);
    }
}