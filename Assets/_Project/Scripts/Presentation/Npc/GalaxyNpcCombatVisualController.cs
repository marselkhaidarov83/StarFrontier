using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcCombatVisualController : CustomMonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private Transform fxRoot;

    [Header("Prefabs")]
    [SerializeField] private GalaxyNpcProjectileView projectilePrefab;
    [SerializeField] private GalaxyNpcTimedFxView hitFxPrefab;
    [SerializeField] private GalaxyNpcTimedFxView explosionFxPrefab;
    [SerializeField] private CombatBeamView2A beamPrefab;

    [Header("Settings")]
    [SerializeField, Min(0.01f)] private float hitFxLifetimeSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float explosionFxLifetimeSeconds = 0.65f;

    private readonly Dictionary<string, GalaxyNpcProjectileView> _activeProjectiles = new();
    private readonly Queue<GalaxyNpcProjectileView> _projectilePool = new();
    private readonly Dictionary<string, CombatBeamView2A> _activeBeams = new();
    private readonly Dictionary<string, GalaxyNpcTimedFxView> _activeBeamHitFx = new();
    private readonly Queue<CombatBeamView2A> _beamPool = new();
    private readonly Queue<GalaxyNpcTimedFxView> _hitFxPool = new();
    private readonly Queue<GalaxyNpcTimedFxView> _explosionFxPool = new();

    private SimpleEventBus _eventBus;
    private ISystemNpcRuntimeService _runtimeService;
    private IConfigService _configService;
    private ISystemNpcCombatService _combatService;
    private IPlayerCombatTargetService _playerTargetService;
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

        ApplyBeamPrefabRuntimeSettings();
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
        bootstrapper.ServiceRegistry.TryGet(out _combatService);
        bootstrapper.ServiceRegistry.TryGet(out _playerTargetService);

        ApplyBeamPrefabRuntimeSettings();

        _eventBus.Subscribe<GalaxyNpcProjectileCreatedEvent>(OnProjectileCreated);
        _eventBus.Subscribe<GalaxyNpcProjectileImpactEvent>(OnProjectileImpact);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Subscribe<CombatBeamStartedEvent2A>(OnBeamStarted);
        _eventBus.Subscribe<CombatBeamEndedEvent2A>(OnBeamEnded);

        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _eventBus == null)
            return;

        _eventBus.Unsubscribe<GalaxyNpcProjectileCreatedEvent>(OnProjectileCreated);
        _eventBus.Unsubscribe<GalaxyNpcProjectileImpactEvent>(OnProjectileImpact);
        _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Unsubscribe<CombatBeamStartedEvent2A>(OnBeamStarted);
        _eventBus.Unsubscribe<CombatBeamEndedEvent2A>(OnBeamEnded);

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

        foreach (CombatBeamView2A view in _activeBeams.Values)
        {
            if (view == null)
                continue;

            view.Complete();
            _beamPool.Enqueue(view);
        }

        _activeBeams.Clear();

        foreach (GalaxyNpcTimedFxView fx in _activeBeamHitFx.Values)
        {
            if (fx == null)
                continue;

            fx.Complete();
        }

        _activeBeamHitFx.Clear();
    }

    private static bool fxPrefabMatches(
        GalaxyNpcTimedFxView instance,
        GalaxyNpcTimedFxView prefab)
    {
        if (instance == null || prefab == null)
            return false;

        return instance.name.StartsWith(prefab.name);
    }

    private void OnBeamStarted(CombatBeamStartedEvent2A evt)
    {
        SpawnBeam(evt);
        SpawnBeamHitFx(evt);
    }

    private void OnBeamEnded(CombatBeamEndedEvent2A evt)
    {
        CompleteBeam(evt.BeamId);
        CompleteBeamHitFx(evt.BeamId);
    }
    private CombatBeamView2A SpawnBeam(CombatBeamStartedEvent2A evt)
    {
        if (beamPrefab == null)
            return null;

        if (string.IsNullOrWhiteSpace(evt.BeamId))
            return null;

        ApplyBeamPrefabRuntimeSettings();

        CompleteBeam(evt.BeamId);

        CombatBeamView2A view = GetBeamFromPool();
        Color color = ResolveBeamColor(evt);

        view.Init(evt, color);

        _activeBeams[evt.BeamId] = view;
        return view;
    }

    private void ApplyBeamPrefabRuntimeSettings()
    {
        if (beamPrefab == null)
            return;

        beamPrefab.ApplyRuntimeSettings();
    }

    private void CompleteBeam(string beamId)
    {
        if (string.IsNullOrWhiteSpace(beamId))
            return;

        if (!_activeBeams.TryGetValue(
                beamId,
                out CombatBeamView2A view))
        {
            return;
        }

        _activeBeams.Remove(beamId);

        if (view == null)
            return;

        view.Complete();
        _beamPool.Enqueue(view);
    }

    private CombatBeamView2A GetBeamFromPool()
    {
        while (_beamPool.Count > 0)
        {
            CombatBeamView2A pooled = _beamPool.Dequeue();

            if (pooled != null)
                return pooled;
        }

        return Instantiate(beamPrefab, projectileRoot);
    }

    private Color ResolveBeamColor(CombatBeamStartedEvent2A evt)
    {
        CombatFxVisualConfig config = ResolveCombatFxVisualConfig();

        if (config == null)
            return Color.white;

        if (evt.ShooterType == CombatShooterType.Player)
            return config.ResolvePlayerColor();

        if (_runtimeService != null &&
            !string.IsNullOrWhiteSpace(evt.ShooterNpcId) &&
            _runtimeService.TryGetNpc(
                evt.ShooterNpcId,
                out SystemNpcRuntimeState shooter) &&
            shooter != null)
        {
            return config.ResolveNpcColor(
                shooter.NpcType,
                shooter.ConfigId);
        }

        return config.DefaultEnemyColor;
    }

    private void Update()
    {
        UpdateActiveBeamHitFx();
    }

    private void SpawnBeamHitFx(CombatBeamStartedEvent2A evt)
    {
        if (hitFxPrefab == null)
            return;

        if (string.IsNullOrWhiteSpace(evt.BeamId))
            return;

        ResolveCombatService();

        CompleteBeamHitFx(evt.BeamId);

        Color fxColor = ResolveNpcFxColor(
            evt.TargetNpcId,
            evt.TargetType,
            null);

        GalaxyNpcTimedFxView fx = SpawnHitFx(
            ResolveBeamHitFxPosition(evt),
            fxColor);

        if (fx == null)
            return;

        fx.RestartLifetime(evt.DurationSeconds);
        _activeBeamHitFx[evt.BeamId] = fx;
    }

    private void CompleteBeamHitFx(string beamId)
    {
        if (string.IsNullOrWhiteSpace(beamId))
            return;

        if (!_activeBeamHitFx.TryGetValue(
                beamId,
                out GalaxyNpcTimedFxView fx))
        {
            return;
        }

        _activeBeamHitFx.Remove(beamId);

        if (fx != null)
            fx.Complete();
    }

    private void UpdateActiveBeamHitFx()
    {
        if (_activeBeamHitFx.Count == 0)
            return;

        ResolveCombatService();

        if (_combatService == null)
            return;

        List<string> completedBeamIds = null;

        foreach (KeyValuePair<string, GalaxyNpcTimedFxView> pair in _activeBeamHitFx)
        {
            string beamId = pair.Key;
            GalaxyNpcTimedFxView fx = pair.Value;

            if (fx == null)
            {
                completedBeamIds ??= new List<string>();
                completedBeamIds.Add(beamId);
                continue;
            }

            if (!_combatService.TryGetBeam(
                    beamId,
                    out CombatBeamRuntimeState2A beam) ||
                beam == null ||
                beam.IsResolved)
            {
                completedBeamIds ??= new List<string>();
                completedBeamIds.Add(beamId);
                continue;
            }

            fx.SetPosition(ResolveBeamHitFxPosition(beam));
        }

        if (completedBeamIds == null)
            return;

        for (int i = 0; i < completedBeamIds.Count; i++)
            CompleteBeamHitFx(completedBeamIds[i]);
    }

    private Vector3 ResolveBeamHitFxPosition(CombatBeamStartedEvent2A evt)
    {
        if (evt.TargetType == CombatTargetType.Player &&
            _playerTargetService != null)
        {
            return _playerTargetService.GetPlayerPosition();
        }

        return evt.TargetPosition;
    }

    private Vector3 ResolveBeamHitFxPosition(CombatBeamRuntimeState2A beam)
    {
        if (beam.TargetType == CombatTargetType.Player &&
            _playerTargetService != null)
        {
            return _playerTargetService.GetPlayerPosition();
        }

        return beam.TargetPosition;
    }

    private void ResolveCombatService()
    {
        if (_combatService != null &&
            _playerTargetService != null)
        {
            return;
        }

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return;

        if (_combatService == null)
            bootstrapper.ServiceRegistry.TryGet(out _combatService);

        if (_playerTargetService == null)
            bootstrapper.ServiceRegistry.TryGet(out _playerTargetService);
    }
}