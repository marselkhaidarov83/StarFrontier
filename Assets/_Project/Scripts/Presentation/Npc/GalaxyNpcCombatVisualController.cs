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
    [SerializeField] private CombatWaveView2A wavePrefab;

    [Header("Settings")]
    [SerializeField, Min(0.01f)] private float hitFxLifetimeSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float explosionFxLifetimeSeconds = 0.65f;

    private readonly Dictionary<string, GalaxyNpcProjectileView> _activeProjectiles = new();
    private readonly Queue<GalaxyNpcProjectileView> _projectilePool = new();

    private readonly Dictionary<string, CombatBeamView2A> _activeBeams = new();
    private readonly Dictionary<string, GalaxyNpcTimedFxView> _activeBeamHitFx = new();
    private readonly Queue<CombatBeamView2A> _beamPool = new();

    private readonly Dictionary<string, CombatWaveView2A> _activeWaves = new();
    private readonly Dictionary<int, int> _waveVisualStartCountByTick = new();
    private readonly Queue<CombatWaveView2A> _wavePool = new();

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

    private void Update()
    {
        UpdateActiveBeamHitFx();
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
        _eventBus.Subscribe<CombatWaveStartedEvent2A>(OnWaveStarted);
        _eventBus.Subscribe<CombatWaveEndedEvent2A>(OnWaveEnded);
        _eventBus.Subscribe<CombatDamagePopupEvent2A>(OnDamagePopupRequested);

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
        _eventBus.Unsubscribe<CombatWaveStartedEvent2A>(OnWaveStarted);
        _eventBus.Unsubscribe<CombatWaveEndedEvent2A>(OnWaveEnded);
        _eventBus.Unsubscribe<CombatDamagePopupEvent2A>(OnDamagePopupRequested);

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

        GalaxyNpcProjectileView prefab =
            ResolveProjectilePrefab(evt.WeaponConfigId);

        if (prefab == null)
            return null;

        GalaxyNpcProjectileView view =
            Instantiate(prefab, projectileRoot);

        view.Init(evt);

        _activeProjectiles[evt.ProjectileId] = view;
        return view;
    }

    private GalaxyNpcProjectileView ResolveProjectilePrefab(
        string weaponConfigId)
    {
        if (_configService != null &&
            !string.IsNullOrWhiteSpace(weaponConfigId))
        {
            WeaponConfig weaponConfig =
                _configService.GetWeaponConfigById(weaponConfigId);

            if (weaponConfig != null &&
                weaponConfig.ProjectilePrefabRef != null &&
                weaponConfig.ProjectilePrefabRef.TryGetComponent(
                    out GalaxyNpcProjectileView configuredPrefab))
            {
                return configuredPrefab;
            }
        }

        return projectilePrefab;
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

    private void OnWaveStarted(CombatWaveStartedEvent2A evt)
    {
        ResolveCombatService();

        int createdTick =
            ResolveWaveCreatedTick(evt.WaveId);

        if (!_waveVisualStartCountByTick.TryGetValue(
                createdTick,
                out int visualStartCountForTick))
        {
            visualStartCountForTick = 0;
        }

        visualStartCountForTick++;
        _waveVisualStartCountByTick[createdTick] =
            visualStartCountForTick;

        LogWaveVisualDebug(
            "[WaveVisualDebug] EVENT_RECEIVED | " +
            "WaveId=" + evt.WaveId +
            " | CreatedTick=" + createdTick +
            " | VisualStartCountForTick=" + visualStartCountForTick +
            " | ShooterType=" + evt.ShooterType +
            " | ShooterNpcId=" + evt.ShooterNpcId +
            " | TargetType=" + evt.TargetType +
            " | PrimaryTargetNpcId=" + evt.PrimaryTargetNpcId +
            " | WeaponConfigId=" + evt.WeaponConfigId +
            " | Center=" + evt.CenterPosition +
            " | FinalRadius=" + evt.FinalRadius.ToString("F2") +
            " | DurationSeconds=" + evt.DurationSeconds.ToString("F2"));

        SpawnWave(evt);
    }

    private void OnWaveEnded(CombatWaveEndedEvent2A evt)
    {
        CompleteWave(evt.WaveId);
    }

    private CombatWaveView2A SpawnWave(CombatWaveStartedEvent2A evt)
    {
        ResolveCombatService();

        if (string.IsNullOrWhiteSpace(evt.WaveId))
        {
            LogWaveVisualDebug(
                "[WaveVisualDebug] SPAWN_SKIP | WaveId is empty.");

            return null;
        }

        bool hadActiveWaveWithSameId =
            _activeWaves.ContainsKey(evt.WaveId);

        CombatWaveView2A prefab =
            ResolveWavePrefab(evt.WeaponConfigId);

        string prefabSource =
            ResolveWavePrefabSource(evt.WeaponConfigId);

        string selectedPrefabName =
            GetObjectDebugName(prefab);

        if (prefab == null)
        {
            LogWaveVisualDebug(
                "[WaveVisualDebug] SPAWN_SKIP | " +
                "WaveId=" + evt.WaveId +
                " | Reason=Prefab is null" +
                " | ShooterType=" + evt.ShooterType +
                " | WeaponConfigId=" + evt.WeaponConfigId +
                " | PrefabSource=" + prefabSource);

            return null;
        }

        CompleteWave(evt.WaveId);

        int poolCountBefore =
            _wavePool.Count;

        CombatWaveView2A view =
            GetWaveFromPool(prefab);

        int poolCountAfter =
            _wavePool.Count;

        Color color =
            ResolveWaveColor(evt);

        string colorSource =
            ResolveWaveColorSource(evt);

        string pooledViewName =
            GetObjectDebugName(view);

        view.Init(evt, color);

        _activeWaves[evt.WaveId] = view;

        LogWaveVisualDebug(
            "[WaveVisualDebug] SPAWN | " +
            "WaveId=" + evt.WaveId +
            " | CreatedTick=" + ResolveWaveCreatedTick(evt.WaveId) +
            " | ShooterType=" + evt.ShooterType +
            " | ShooterNpcId=" + evt.ShooterNpcId +
            " | TargetType=" + evt.TargetType +
            " | PrimaryTargetNpcId=" + evt.PrimaryTargetNpcId +
            " | WeaponConfigId=" + evt.WeaponConfigId +
            " | HadActiveWaveWithSameId=" + hadActiveWaveWithSameId +
            " | PrefabSource=" + prefabSource +
            " | SelectedPrefab=" + selectedPrefabName +
            " | PooledView=" + pooledViewName +
            " | PoolCountBefore=" + poolCountBefore +
            " | PoolCountAfter=" + poolCountAfter +
            " | ColorSource=" + colorSource +
            " | Tint=" + DescribeColor(color) +
            " | Center=" + evt.CenterPosition +
            " | FinalRadius=" + evt.FinalRadius.ToString("F2") +
            " | DurationSeconds=" + evt.DurationSeconds.ToString("F2"));

        return view;
    }



    private CombatWaveView2A ResolveWavePrefab(string weaponConfigId)
    {
        if (_configService != null &&
            !string.IsNullOrWhiteSpace(weaponConfigId))
        {
            WeaponConfig weaponConfig =
                _configService.GetWeaponConfigById(weaponConfigId);

            if (weaponConfig != null &&
                weaponConfig.ProjectilePrefabRef != null &&
                weaponConfig.ProjectilePrefabRef.TryGetComponent(
                    out CombatWaveView2A configuredPrefab))
            {
                return configuredPrefab;
            }
        }

        return wavePrefab;
    }

    private CombatWaveView2A GetWaveFromPool(CombatWaveView2A prefab)
    {
        return Instantiate(prefab, projectileRoot);
    }

    private void CompleteWave(string waveId)
    {
        if (string.IsNullOrWhiteSpace(waveId))
            return;

        if (!_activeWaves.TryGetValue(
                waveId,
                out CombatWaveView2A view))
        {
            return;
        }

        _activeWaves.Remove(waveId);

        if (view == null)
            return;

        view.Complete();
        Destroy(view.gameObject);
    }

    private Color ResolveWaveColor(CombatWaveStartedEvent2A evt)
    {
        CombatFxVisualConfig config =
            ResolveCombatFxVisualConfig();

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

    private string ResolveWaveColorSource(CombatWaveStartedEvent2A evt)
    {
        CombatFxVisualConfig config =
            ResolveCombatFxVisualConfig();

        if (config == null)
            return "NoCombatFxVisualConfig";

        if (evt.ShooterType == CombatShooterType.Player)
            return "PlayerColor";

        if (_runtimeService != null &&
            !string.IsNullOrWhiteSpace(evt.ShooterNpcId) &&
            _runtimeService.TryGetNpc(
                evt.ShooterNpcId,
                out SystemNpcRuntimeState shooter) &&
            shooter != null)
        {
            return "NpcColor(" +
                   shooter.NpcType +
                   ", ConfigId=" +
                   shooter.ConfigId +
                   ")";
        }

        return "DefaultEnemyColor";
    }

    private string ResolveWavePrefabSource(string weaponConfigId)
    {
        if (_configService != null &&
            !string.IsNullOrWhiteSpace(weaponConfigId))
        {
            WeaponConfig weaponConfig =
                _configService.GetWeaponConfigById(weaponConfigId);

            if (weaponConfig != null &&
                weaponConfig.ProjectilePrefabRef != null &&
                weaponConfig.ProjectilePrefabRef.TryGetComponent(
                    out CombatWaveView2A configuredPrefab) &&
                configuredPrefab != null)
            {
                return "WeaponConfig.ProjectilePrefabRef";
            }
        }

        return "GalaxyNpcCombatVisualController.wavePrefab";
    }

    private int ResolveWaveCreatedTick(string waveId)
    {
        if (_combatService == null ||
            string.IsNullOrWhiteSpace(waveId))
        {
            return -1;
        }

        if (!_combatService.TryGetWave(
                waveId,
                out CombatWaveRuntimeState2A wave) ||
            wave == null)
        {
            return -1;
        }

        return wave.CreatedTick;
    }

    private string DescribeColor(Color color)
    {
        return "rgba(" +
               color.r.ToString("F2") +
               ", " +
               color.g.ToString("F2") +
               ", " +
               color.b.ToString("F2") +
               ", " +
               color.a.ToString("F2") +
               ")";
    }

    private string GetObjectDebugName(Object target)
    {
        return target != null
            ? target.name
            : "null";
    }

    private void LogWaveVisualDebug(string message)
    {
        bool previousDebugEnabled = _debugEnabled;
        bool previousDebugStop = _debugStop;

        _debugEnabled = true;
        _debugStop = false;

        LogCustom(message);

        _debugEnabled = previousDebugEnabled;
        _debugStop = previousDebugStop;
    }

    public GalaxyNpcTimedFxView SpawnHitFx(Vector3 position)
    {
        return SpawnHitFx(position, ResolveFallbackFxColor());
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

        foreach (CombatWaveView2A view in _activeWaves.Values)
        {
            if (view == null)
                continue;

            view.Complete();
            Destroy(view.gameObject);
        }

        _activeWaves.Clear();

        while (_wavePool.Count > 0)
        {
            CombatWaveView2A pooledWave = _wavePool.Dequeue();

            if (pooledWave != null)
                Destroy(pooledWave.gameObject);
        }
    }

    private static bool fxPrefabMatches(
        GalaxyNpcTimedFxView instance,
        GalaxyNpcTimedFxView prefab)
    {
        if (instance == null || prefab == null)
            return false;

        return instance.name.StartsWith(prefab.name);
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

    private void OnDamagePopupRequested(CombatDamagePopupEvent2A evt)
    {
        SpawnDamagePopup(evt);
    }

    private CombatDamagePopupView2A SpawnDamagePopup(CombatDamagePopupEvent2A evt)
    {
        CombatDamagePopupVisualConfig2A config =
            ResolveDamagePopupVisualConfig();

        if (config == null)
            return null;

        if (!config.Enabled)
            return null;

        if (evt.Damage <= 0)
            return null;

        Transform parent =
            fxRoot != null ? fxRoot : transform;

        GameObject popupObject =
            new GameObject("DamagePopup_" + evt.Damage);

        popupObject.transform.SetParent(parent, false);

        CombatDamagePopupView2A view =
            popupObject.AddComponent<CombatDamagePopupView2A>();

        view.Init(
            config,
            evt.Damage,
            evt.TargetPosition,
            evt.DamageSourcePosition,
            evt.PopupDirection);

        return view;
    }

    private CombatDamagePopupVisualConfig2A ResolveDamagePopupVisualConfig()
    {
        if (_configService != null)
            return _configService.CombatDamagePopupVisualConfig;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return null;

        if (!bootstrapper.ServiceRegistry.TryGet(out _configService) ||
            _configService == null)
        {
            return null;
        }

        return _configService.CombatDamagePopupVisualConfig;
    }
}