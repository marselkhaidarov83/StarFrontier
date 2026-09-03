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
    private ISystemNpcRuntimeService _runtimeService;
    private IConfigService _configService;
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

        bootstrapper.ServiceRegistry.TryGet(out _runtimeService);
        bootstrapper.ServiceRegistry.TryGet(out _configService);

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

        if (!evt.DidHit)
            return;

        Color fxColor = ResolveNpcFxColor(
            evt.TargetNpcId,
            evt.TargetType,
            null);

        SpawnHitFx(evt.HitPosition, fxColor);
    }

    private Color ResolveNpcFxColor(
    string runtimeNpcId,
    CombatTargetType targetType,
    SystemNpcType? fallbackNpcType)
    {
        CombatFxVisualConfig config = ResolveCombatFxVisualConfig();

        if (config == null)
            return Color.white;

        if (targetType == CombatTargetType.Player)
            return config.ResolvePlayerColor();

        if (_runtimeService != null &&
            !string.IsNullOrWhiteSpace(runtimeNpcId) &&
            _runtimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) &&
            npc != null)
        {
            return config.ResolveNpcColor(
                npc.NpcType,
                npc.ConfigId);
        }

        if (fallbackNpcType.HasValue)
        {
            return config.ResolveNpcColor(
                fallbackNpcType.Value,
                string.Empty);
        }

        return config.DefaultEnemyColor;
    }

    private static Color ResolveEnemyFxColor(string configId)
    {
        string normalizedId = string.IsNullOrWhiteSpace(configId)
            ? string.Empty
            : configId.ToLowerInvariant();

        if (normalizedId.Contains("enemy_ai"))
            return new Color(1f, 0.22f, 0.16f, 1f);

        if (normalizedId.Contains("enemy_ancients"))
            return new Color(1f, 0.72f, 0.18f, 1f);

        if (normalizedId.Contains("enemy_infected"))
            return new Color(0.62f, 1f, 0.22f, 1f);

        return ResolveDefaultEnemyFxColor();
    }

    private static Color ResolveAllyFxColor()
    {
        return new Color(0.35f, 0.88f, 1f, 1f);
    }

    private static Color ResolvePirateFxColor()
    {
        return new Color(1f, 0.42f, 0.12f, 1f);
    }

    private static Color ResolveDefaultEnemyFxColor()
    {
        return new Color(1f, 0.25f, 0.18f, 1f);
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        Color fxColor = ResolveNpcFxColor(
            evt.RuntimeNpcId,
            CombatTargetType.Npc,
            evt.NpcType);

        SpawnExplosionFx(evt.Position, fxColor);
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
        return SpawnHitFx(position, ResolveFallbackFxColor());
    }

    private Color ResolveFallbackFxColor()
    {
        CombatFxVisualConfig config = ResolveCombatFxVisualConfig();

        if (config == null)
            return Color.white;

        return config.DefaultEnemyColor;
    }

    private CombatFxVisualConfig ResolveCombatFxVisualConfig()
    {
        if (_configService != null)
            return _configService.CombatFxVisualConfig;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return null;

        if (!bootstrapper.ServiceRegistry.TryGet(out _configService) ||
            _configService == null)
        {
            return null;
        }

        return _configService.CombatFxVisualConfig;
    }

    public GalaxyNpcTimedFxView SpawnHitFx(Vector3 position, Color tint)
    {
        return SpawnFx(
            position,
            hitFxPrefab,
            _hitFxPool,
            hitFxLifetimeSeconds,
            tint);
    }

    public GalaxyNpcTimedFxView SpawnExplosionFx(Vector3 position)
    {
        return SpawnExplosionFx(position, ResolveFallbackFxColor());
    }

    public GalaxyNpcTimedFxView SpawnExplosionFx(Vector3 position, Color tint)
    {
        return SpawnFx(
            position,
            explosionFxPrefab,
            _explosionFxPool,
            explosionFxLifetimeSeconds,
            tint);
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
    float lifetimeSeconds,
    Color tint)
    {
        if (prefab == null)
            return null;

        GalaxyNpcTimedFxView fx = null;

        while (pool.Count > 0 && fx == null)
            fx = pool.Dequeue();

        if (fx == null)
            fx = Instantiate(prefab, fxRoot);

        fx.Init(this, position, lifetimeSeconds, tint);
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