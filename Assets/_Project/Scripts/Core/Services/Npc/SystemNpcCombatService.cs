using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcCombatService : CustomService, ISystemNpcCombatService
{
    private const float TickBasedProjectileSpeed = 0f;
    private const bool WaveDamageDebugLogEnabled = false;
    private const bool WeaponAttackTickDebugLogEnabled = true;

    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly IConfigService _configService;
    private readonly IPlayerCombatTargetService _playerTargetService;
    private readonly SimpleEventBus _eventBus;
    private readonly ISystemEncounterService _encounterService;
    private IShipMovementService _shipMovementService;

    private readonly List<GalaxyNpcProjectileRuntimeState> _activeProjectiles = new();
    private readonly List<CombatBeamRuntimeState2A> _activeBeams = new();
    private readonly List<CombatWaveRuntimeState2A> _activeWaves = new();
    private readonly Dictionary<int, int> _waveShotCreateCountByTick = new();
    private readonly Dictionary<string, int> _waveShotCreateCountByTickAndShooter = new();

    public int ActiveProjectileCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                GalaxyNpcProjectileRuntimeState projectile =
                    _activeProjectiles[i];

                if (projectile == null || projectile.IsResolved)
                    continue;

                count++;
            }

            return count;
        }
    }

    public SystemNpcCombatService()
    {
        _debugStop = true;

        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _playerTargetService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerCombatTargetService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        Bootstrapper.Instance.ServiceRegistry.TryGet(out _shipMovementService);

        _eventBus.Subscribe<GameDayChangedEvent>(OnGameDayChanged);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Subscribe<PlayerShipDestroyedByNpcEvent>(OnPlayerShipDestroyedByNpc);
    }

    private void EnsureEncounterForPlayerAttack(
        SystemNpcRuntimeState shooter,
        GalaxyCombatTarget target)
    {
        if (shooter == null)
            return;

        if (target.TargetType != CombatTargetType.Player)
            return;

        if (_encounterService.HasActiveEncounter)
            return;

        IReadOnlyList<SystemNpcRuntimeState> enemies =
            _runtimeService.GetAliveNpcsInSystemByType(
                shooter.CurrentSystemId,
                SystemNpcType.Enemy);

        IReadOnlyList<SystemNpcRuntimeState> allies =
            _runtimeService.GetAliveNpcsInSystemByType(
                shooter.CurrentSystemId,
                SystemNpcType.Ally);

        int enemyCount = Mathf.Max(1, enemies.Count);
        int allyCount = allies != null ? allies.Count : 0;

        string encounterId =
            "encounter_" +
            shooter.CurrentSystemId +
            "_" +
            shooter.GroupRuntimeId +
            "_" +
            shooter.RuntimeNpcId;

        _encounterService.StartEncounter(
            encounterId,
            shooter.CurrentSystemId,
            enemyCount,
            allyCount);

        Debug.Log(
            "[SystemNpcCombatService] Auto-started encounter for NPC player attack. " +
            $"Encounter: {encounterId}, System: {shooter.CurrentSystemId}, " +
            $"Enemies: {enemyCount}, Allies: {allyCount}");
    }

    public void Tick(StarSystemConfig starSystem, int quantTick)
    {
        if (starSystem == null)
            return;

        string systemId = starSystem.Id;
        var npcs = _runtimeService.GetAliveNpcsInSystem(systemId);

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState shooter = npcs[i];

            if (!CanFight(shooter))
                continue;

            TryAttack(shooter, quantTick);
        }
    }

    public void TickProjectiles(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
        {
            GalaxyNpcProjectileRuntimeState projectile = _activeProjectiles[i];

            if (projectile == null)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            if (projectile.IsResolved)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            TickProjectile(projectile, deltaTime);

            if (projectile.IsResolved)
                _activeProjectiles.RemoveAt(i);
        }

        TickBeams(deltaTime);
        TickWaves(deltaTime);
    }

    public void ForceAttackOnce(string shooterNpcId, int quantTick)
    {
        if (!_runtimeService.TryGetNpc(shooterNpcId, out SystemNpcRuntimeState shooter))
        {
            Debug.LogWarning($"[SystemNpcCombatService] Shooter not found: {shooterNpcId}");
            return;
        }

        TryAttack(shooter, quantTick, ignoreTickGate: true);
    }

    public bool TryGetProjectile(
        string projectileId,
        out GalaxyNpcProjectileRuntimeState projectile)
    {
        projectile = null;

        if (string.IsNullOrWhiteSpace(projectileId))
            return false;

        for (int i = 0; i < _activeProjectiles.Count; i++)
        {
            if (_activeProjectiles[i] == null)
                continue;

            if (_activeProjectiles[i].ProjectileId == projectileId)
            {
                projectile = _activeProjectiles[i];
                return true;
            }
        }

        return false;
    }

    private void OnGameDayChanged(GameDayChangedEvent evt)
    {
        ResolveProjectiles(evt.CurrentDay);
    }

    private bool CanFight(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlive)
            return false;

        if (npc.IsOnPlanet)
            return false;

        if (npc.CurrentBehavior != SystemNpcBehaviorType.EngageEnemies &&
            npc.TravelState != SystemNpcTravelState.EngagingEnemy)
            return false;

        if (npc.Weapons == null || npc.Weapons.Count == 0)
            return false;

        if (npc.CombatState == SystemNpcCombatState.Retreating)
            return false;

        return true;
    }

    private void TryAttack(
        SystemNpcRuntimeState shooter,
        int quantTick,
        bool ignoreTickGate = false)
    {
        if (shooter == null)
            return;

        if (shooter.CurrentBehavior != SystemNpcBehaviorType.EngageEnemies &&
            shooter.TravelState != SystemNpcTravelState.EngagingEnemy)
        {
            shooter.CombatState = SystemNpcCombatState.None;
            shooter.CurrentTargetRuntimeNpcId = null;
            shooter.IsFighting = false;
            return;
        }

        if (shooter.Weapons == null || shooter.Weapons.Count == 0)
            return;

        GalaxyCombatTarget target = FindTarget(shooter);

        if (!target.IsValid)
        {
            shooter.CombatState = SystemNpcCombatState.SearchingTarget;
            shooter.CurrentTargetRuntimeNpcId = null;
            shooter.IsFighting = false;
            return;
        }

        shooter.CombatState = SystemNpcCombatState.HasTarget;
        shooter.CurrentTargetRuntimeNpcId = target.IsNpc ? target.TargetNpcId : null;
        shooter.IsFighting = true;

        bool firedAnyWeapon = false;

        for (int i = 0; i < shooter.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weaponRuntime = shooter.Weapons[i];

            if (weaponRuntime == null)
                continue;

            bool fired = TryCreateProjectile(
                shooter,
                target,
                weaponRuntime,
                quantTick,
                ignoreTickGate);

            if (!fired)
                continue;

            firedAnyWeapon = true;
            break;
        }

        if (firedAnyWeapon)
            shooter.CombatState = SystemNpcCombatState.Attacking;
    }

    private bool TryCreateProjectile(
        SystemNpcRuntimeState shooter,
        GalaxyCombatTarget target,
        SystemNpcWeaponRuntimeState weaponRuntime,
        int quantTick,
        bool ignoreTickGate)
    {
        if (shooter == null)
            return false;

        if (weaponRuntime == null)
            return false;

        if (!target.IsValid)
            return false;

        WeaponConfig weaponConfig = _configService.GetWeaponConfigById(
            weaponRuntime.WeaponConfigId);

        if (weaponConfig == null)
        {
            Debug.LogWarning(
                "[SystemNpcCombatService] WeaponConfig not found: " +
                weaponRuntime.WeaponConfigId);

            return false;
        }

        WeaponRuntimeStats weaponStats = weaponConfig.RollRuntimeStats(
            BuildWeaponRollSeed(
                "npc",
                shooter.RuntimeNpcId,
                target.TargetType.ToString(),
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                quantTick.ToString()));

        float distanceAtTickStart = Vector3.Distance(
            shooter.CurrentPosition,
            target.Position);

        if (distanceAtTickStart > weaponStats.Range)
        {
            LogWeaponAttackTick(
                quantTick,
                CombatShooterType.Npc,
                shooter.RuntimeNpcId,
                target.TargetType,
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                weaponStats,
                distanceAtTickStart,
                false,
                "OutOfRange");

            return false;
        }

        if (!ignoreTickGate && !weaponRuntime.CanShootAtTick(quantTick))
        {
            LogWeaponAttackTick(
                quantTick,
                CombatShooterType.Npc,
                shooter.RuntimeNpcId,
                target.TargetType,
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                weaponStats,
                distanceAtTickStart,
                false,
                "Cooldown");

            return false;
        }

        weaponRuntime.MarkShotAtTick(quantTick, cooldownTicks: 1);

        EnsureEncounterForPlayerAttack(shooter, target);

        bool fired;

        if (weaponStats.ShotType == WeaponShotType2A.Wave)
        {
            fired = TryCreateWave(
                shooter.CurrentSystemId,
                CombatShooterType.Npc,
                shooter.RuntimeNpcId,
                target.TargetType,
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                shooter.CurrentPosition,
                weaponConfig,
                weaponStats,
                quantTick);
        }
        else if (weaponStats.ShotType == WeaponShotType2A.Beam)
        {
            fired = TryCreateBeam(
                shooter.CurrentSystemId,
                CombatShooterType.Npc,
                shooter.RuntimeNpcId,
                target.TargetType,
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                shooter.CurrentPosition,
                target.Position,
                weaponStats,
                quantTick);
        }
        else if (IsTimedEnergyProjectile(weaponStats.ShotType))
        {
            fired = TryCreateTimedEnergyProjectiles(
                shooter.CurrentSystemId,
                CombatShooterType.Npc,
                shooter.RuntimeNpcId,
                target.TargetType,
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                shooter.CurrentPosition,
                target.Position,
                weaponConfig,
                weaponStats,
                quantTick);
        }
        else
        {
            fired = TryCreateSingleProjectile(
                shooter.CurrentSystemId,
                CombatShooterType.Npc,
                shooter.RuntimeNpcId,
                target.TargetType,
                target.TargetNpcId,
                weaponRuntime.WeaponConfigId,
                shooter.CurrentPosition,
                target.Position,
                weaponStats,
                quantTick);
        }

        LogWeaponAttackTick(
            quantTick,
            CombatShooterType.Npc,
            shooter.RuntimeNpcId,
            target.TargetType,
            target.TargetNpcId,
            weaponRuntime.WeaponConfigId,
            weaponStats,
            distanceAtTickStart,
            fired,
            fired ? "Fired" : "CreateFailed");

        return fired;
    }

    private bool TryCreateSingleProjectile(
    string systemId,
    CombatShooterType shooterType,
    string shooterNpcId,
    CombatTargetType targetType,
    string targetNpcId,
    string weaponConfigId,
    Vector3 startPosition,
    Vector3 targetPosition,
    WeaponRuntimeStats weaponStats,
    int quantTick)
    {
        int projectileLifetimeTicks =
            Mathf.Max(1, weaponStats.ProjectileLifetime);

        float lifetimeSeconds =
            Mathf.Max(
                0.01f,
                GameTimeService.SecondsPerDay * projectileLifetimeTicks);

        GalaxyNpcProjectileRuntimeState projectile =
            CreateProjectileRuntimeState(
                systemId,
                shooterType,
                shooterNpcId,
                targetType,
                targetNpcId,
                weaponConfigId,
                weaponStats.ShotType,
                startPosition,
                targetPosition,
                Vector3.zero,
                Mathf.Max(0, weaponStats.Damage),
                0,
                1,
                quantTick,
                0f,
                lifetimeSeconds);

        projectile.ImpactTick =
            quantTick + projectileLifetimeTicks - 1;

        _activeProjectiles.Add(projectile);

        PublishProjectileCreated(projectile);

        return true;
    }

    private void TickProjectile(
    GalaxyNpcProjectileRuntimeState projectile,
    float deltaTime)
    {
        projectile.ElapsedSeconds += deltaTime;

        if (!projectile.HasReleased)
        {
            RefreshPendingProjectileLaunch(projectile);

            if (projectile.ElapsedSeconds < projectile.StartDelaySeconds)
            {
                projectile.CurrentPosition = projectile.StartPosition;
                return;
            }

            projectile.HasReleased = true;
            projectile.CurrentPosition = projectile.StartPosition;
        }

        Vector3 targetPosition = projectile.LastKnownTargetPosition;

        if (TryGetCurrentProjectileTargetPosition(
                projectile,
                out Vector3 currentTargetPosition))
        {
            targetPosition = projectile.ShotType == WeaponShotType2A.MissileSwarm
                ? currentTargetPosition
                : currentTargetPosition + projectile.PathOffset;

            projectile.LastKnownTargetPosition = targetPosition;
        }

        if (projectile.ShotType == WeaponShotType2A.MissileSwarm)
        {
            TickMissileProjectile(
                projectile,
                deltaTime,
                targetPosition);

            return;
        }

        float flightElapsedSeconds =
            projectile.ElapsedSeconds - projectile.StartDelaySeconds;

        float lifetimeSeconds =
            Mathf.Max(0.01f, projectile.LifetimeSeconds);

        float progress01 =
            Mathf.Clamp01(flightElapsedSeconds / lifetimeSeconds);

        projectile.CurrentPosition = Vector3.Lerp(
            projectile.StartPosition,
            targetPosition,
            progress01);

        if (progress01 >= 1f)
            ResolveProjectile(projectile, projectile.ShouldDamageOnArrival);
    }

    private bool TryGetCurrentProjectileShooterPosition(
    GalaxyNpcProjectileRuntimeState projectile,
    out Vector3 position)
    {
        position = projectile.StartPosition;

        if (projectile.ShooterType == CombatShooterType.Player)
        {
            if (!_playerTargetService.IsPlayerAvailableInSystem(projectile.SystemId))
                return false;

            position = _playerTargetService.GetPlayerPosition();
            position.z = 0f;
            return true;
        }

        if (projectile.ShooterType == CombatShooterType.Npc)
        {
            if (!_runtimeService.TryGetNpc(projectile.ShooterNpcId, out SystemNpcRuntimeState shooter))
                return false;

            if (!shooter.IsAlive)
                return false;

            if (shooter.IsOnPlanet)
                return false;

            if (shooter.CurrentSystemId != projectile.SystemId)
                return false;

            position = shooter.CurrentPosition;
            position.z = 0f;
            return true;
        }

        return false;
    }

    private void RefreshPendingProjectileLaunch(
        GalaxyNpcProjectileRuntimeState projectile)
    {
        if (projectile == null || projectile.HasReleased)
            return;

        if (!TryGetCurrentProjectileShooterPosition(
                projectile,
                out Vector3 shooterPosition))
        {
            return;
        }

        Vector3 previousPathOffset = projectile.PathOffset;
        Vector3 targetPosition = projectile.LastKnownTargetPosition;

        if (projectile.ShotType != WeaponShotType2A.MissileSwarm)
            targetPosition -= previousPathOffset;

        if (TryGetCurrentProjectileTargetPosition(
                projectile,
                out Vector3 currentTargetPosition))
        {
            targetPosition = currentTargetPosition;
        }

        Vector3 pathDirection = targetPosition - shooterPosition;
        pathDirection.z = 0f;

        Vector3 leftPerpendicular = Vector3.zero;

        if (pathDirection.sqrMagnitude > 0.0001f)
        {
            pathDirection.Normalize();
            leftPerpendicular = new Vector3(-pathDirection.y, pathDirection.x, 0f);
        }

        float laneOffset = previousPathOffset.magnitude;

        projectile.PathOffset = ResolveRapidProjectilePathOffset(
            leftPerpendicular,
            laneOffset,
            projectile.ShotIndex);

        projectile.StartPosition = shooterPosition + projectile.PathOffset;
        projectile.CurrentPosition = projectile.StartPosition;

        projectile.LastKnownTargetPosition = projectile.ShotType == WeaponShotType2A.MissileSwarm
            ? targetPosition
            : targetPosition + projectile.PathOffset;

        if (projectile.ShotType == WeaponShotType2A.MissileSwarm)
        {
            BuildMissileRoute(
                projectile,
                shooterPosition,
                targetPosition,
                projectile.PathOffset,
                projectile.MissileCurveOffset);
        }
    }

    private bool TryGetCurrentProjectileTargetPosition(
        GalaxyNpcProjectileRuntimeState projectile,
        out Vector3 position)
    {
        position = projectile.LastKnownTargetPosition;

        if (projectile.TargetType == CombatTargetType.Player)
        {
            if (!_playerTargetService.IsPlayerAvailableInSystem(projectile.SystemId))
                return false;

            position = _playerTargetService.GetPlayerPosition();
            position.z = 0f;
            return true;
        }

        if (projectile.TargetType == CombatTargetType.Npc)
        {
            if (!_runtimeService.TryGetNpc(projectile.TargetNpcId, out SystemNpcRuntimeState target))
                return false;

            if (!target.IsAlive)
                return false;

            if (target.IsOnPlanet)
                return false;

            if (target.CurrentSystemId != projectile.SystemId)
                return false;

            position = target.CurrentPosition;
            position.z = 0f;
            return true;
        }

        return false;
    }

    private void ResolveProjectiles(int completedTick)
    {
        for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
        {
            GalaxyNpcProjectileRuntimeState projectile = _activeProjectiles[i];

            if (projectile == null)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            if (projectile.IsResolved)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            if (projectile.ImpactTick > completedTick)
                continue;

            ResolveProjectile(projectile);
            _activeProjectiles.RemoveAt(i);
        }
    }

    private void ResolveProjectile(GalaxyNpcProjectileRuntimeState projectile)
    {
        ResolveProjectile(projectile, allowDamage: true);
    }

    private void ResolveProjectile(
        GalaxyNpcProjectileRuntimeState projectile,
        bool allowDamage)
    {
        if (projectile == null)
            return;

        projectile.IsResolved = true;

        bool didHit = false;
        Vector3 hitPosition = projectile.CurrentPosition;

        if (allowDamage && projectile.ShouldDamageOnArrival)
        {
            if (projectile.TargetType == CombatTargetType.Player)
            {
                ResolvePlayerProjectile(projectile, ref hitPosition, ref didHit);
            }
            else if (projectile.TargetType == CombatTargetType.Npc)
            {
                ResolveNpcProjectile(projectile, ref hitPosition, ref didHit);
            }
        }

        bool shouldSpawnHitFx =
            didHit || projectile.ShouldSpawnHitFxOnMiss;

        _eventBus.Publish(new GalaxyNpcProjectileImpactEvent(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.ShooterNpcId,
            projectile.TargetType,
            projectile.TargetNpcId,
            projectile.Damage,
            hitPosition,
            shouldSpawnHitFx
        ));

        _eventBus.Publish(new CombatProjectileImpactEvent2A(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.TargetNpcId,
            projectile.Damage,
            hitPosition,
            didHit));

        LogCustom(
            $"[SystemNpcCombatService] Projectile resolved. " +
            $"Projectile: {projectile.ProjectileId}, TargetType: {projectile.TargetType}, " +
            $"DidHit: {didHit}, SpawnHitFx: {shouldSpawnHitFx}, Damage: {projectile.Damage}"
        );
    }

    private void ResolvePlayerProjectile(
        GalaxyNpcProjectileRuntimeState projectile,
        ref Vector3 hitPosition,
        ref bool didHit)
    {
        if (!_playerTargetService.IsPlayerAvailableInSystem(projectile.SystemId))
            return;

        hitPosition = _playerTargetService.GetPlayerPosition();

        CombatDamageResult2A damageResult =
            _playerTargetService.ApplyDamage(projectile.Damage);

        if (damageResult.AppliedDamage <= 0)
            return;

        PublishDamagePopup(
            CombatTargetType.Player,
            string.Empty,
            damageResult.AppliedDamage,
            hitPosition,
            projectile.StartPosition);

        didHit = true;
    }

    private void ResolveNpcProjectile(
     GalaxyNpcProjectileRuntimeState projectile,
     ref Vector3 hitPosition,
     ref bool didHit)
    {
        if (!_runtimeService.TryGetNpc(projectile.TargetNpcId, out SystemNpcRuntimeState target))
            return;

        hitPosition = target.CurrentPosition;

        if (!target.IsAlive)
            return;

        if (target.IsOnPlanet)
            return;

        if (target.CurrentSystemId != projectile.SystemId)
            return;

        CombatDamageResult2A damageResult = _runtimeService.ApplyDamage(
            projectile.TargetNpcId,
            projectile.Damage,
            killedByPlayer: projectile.ShooterType == CombatShooterType.Player,
            damagedByPlayer: projectile.ShooterType == CombatShooterType.Player);

        if (damageResult.AppliedDamage <= 0)
            return;

        PublishDamagePopup(
            CombatTargetType.Npc,
            projectile.TargetNpcId,
            damageResult.AppliedDamage,
            hitPosition,
            projectile.StartPosition);

        didHit = true;
    }

    private GalaxyCombatTarget FindTarget(SystemNpcRuntimeState shooter)
    {
        if (shooter.IsEnemy)
        {
            GalaxyCombatTarget allyTarget = FindNearestNpcTarget(
                shooter,
                SystemNpcType.Ally
            );

            if (allyTarget.IsValid)
                return allyTarget;

            GalaxyCombatTarget playerTarget = TryFindPlayerTarget(shooter);

            if (playerTarget.IsValid)
                return playerTarget;

            return GalaxyCombatTarget.None();
        }

        if (shooter.IsAlly)
            return FindNearestNpcTarget(shooter, SystemNpcType.Enemy);

        if (shooter.IsPirate)
        {
            if (shooter.IsAggressiveToPlayer)
            {
                GalaxyCombatTarget playerTarget = TryFindPlayerTarget(shooter);

                if (playerTarget.IsValid)
                    return playerTarget;
            }
        }

        return GalaxyCombatTarget.None();
    }

    private GalaxyCombatTarget TryFindPlayerTarget(SystemNpcRuntimeState shooter)
    {
        if (!_playerTargetService.IsPlayerAvailableInSystem(shooter.CurrentSystemId))
            return GalaxyCombatTarget.None();

        Vector3 playerPosition = _playerTargetService.GetPlayerPosition();
        playerPosition.z = 0f;

        return GalaxyCombatTarget.Player(playerPosition);
    }

    private GalaxyCombatTarget FindNearestNpcTarget(
        SystemNpcRuntimeState shooter,
        SystemNpcType targetType)
    {
        var candidates = _runtimeService.GetAliveNpcsInSystemByType(
            shooter.CurrentSystemId,
            targetType
        );

        SystemNpcRuntimeState best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            SystemNpcRuntimeState candidate = candidates[i];

            if (candidate == null)
                continue;

            if (!candidate.IsAvailableForCombat())
                continue;

            float distance = Vector3.Distance(
                shooter.CurrentPosition,
                candidate.CurrentPosition
            );

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best == null)
            return GalaxyCombatTarget.None();

        return GalaxyCombatTarget.Npc(
            best.RuntimeNpcId,
            best.CurrentPosition
        );
    }

    public bool TryCreatePlayerProjectile(
        string targetNpcId,
        string weaponConfigId,
        int quantTick)
    {
        if (string.IsNullOrWhiteSpace(targetNpcId))
            return false;

        if (string.IsNullOrWhiteSpace(weaponConfigId))
            return false;

        if (!_runtimeService.TryGetNpc(targetNpcId, out SystemNpcRuntimeState target))
        {
            LogCustom("target not found = " + targetNpcId);
            return false;
        }

        if (!target.IsAlive || target.IsOnPlanet)
            return false;

        if (!_playerTargetService.IsPlayerAvailableInSystem(target.CurrentSystemId))
            return false;

        WeaponConfig weaponConfig = _configService.GetWeaponConfigById(weaponConfigId);

        if (weaponConfig == null)
        {
            Debug.LogWarning(
                "[SystemNpcCombatService] WeaponConfig not found: " +
                weaponConfigId);

            return false;
        }

        WeaponRuntimeStats weaponStats = weaponConfig.RollRuntimeStats(
            BuildWeaponRollSeed(
                "player",
                target.RuntimeNpcId,
                weaponConfigId,
                quantTick.ToString()));

        Vector3 playerPosition = _playerTargetService.GetPlayerPosition();

        float distanceAtTickStart = Vector3.Distance(
            playerPosition,
            target.CurrentPosition);

        if (distanceAtTickStart > weaponStats.Range)
        {
            LogWeaponAttackTick(
                quantTick,
                CombatShooterType.Player,
                string.Empty,
                CombatTargetType.Npc,
                target.RuntimeNpcId,
                weaponConfigId,
                weaponStats,
                distanceAtTickStart,
                false,
                "OutOfRange");

            return false;
        }

        bool fired;

        if (weaponStats.ShotType == WeaponShotType2A.Wave)
        {
            fired = TryCreateWave(
                target.CurrentSystemId,
                CombatShooterType.Player,
                string.Empty,
                CombatTargetType.Npc,
                target.RuntimeNpcId,
                weaponConfigId,
                playerPosition,
                weaponConfig,
                weaponStats,
                quantTick);
        }
        else if (weaponStats.ShotType == WeaponShotType2A.Beam)
        {
            fired = TryCreateBeam(
                target.CurrentSystemId,
                CombatShooterType.Player,
                string.Empty,
                CombatTargetType.Npc,
                target.RuntimeNpcId,
                weaponConfigId,
                playerPosition,
                target.CurrentPosition,
                weaponStats,
                quantTick);
        }
        else if (IsTimedEnergyProjectile(weaponStats.ShotType))
        {
            fired = TryCreateTimedEnergyProjectiles(
                target.CurrentSystemId,
                CombatShooterType.Player,
                string.Empty,
                CombatTargetType.Npc,
                target.RuntimeNpcId,
                weaponConfigId,
                playerPosition,
                target.CurrentPosition,
                weaponConfig,
                weaponStats,
                quantTick);
        }
        else
        {
            fired = TryCreateSingleProjectile(
                target.CurrentSystemId,
                CombatShooterType.Player,
                string.Empty,
                CombatTargetType.Npc,
                target.RuntimeNpcId,
                weaponConfigId,
                playerPosition,
                target.CurrentPosition,
                weaponStats,
                quantTick);
        }

        LogWeaponAttackTick(
            quantTick,
            CombatShooterType.Player,
            string.Empty,
            CombatTargetType.Npc,
            target.RuntimeNpcId,
            weaponConfigId,
            weaponStats,
            distanceAtTickStart,
            fired,
            fired ? "Fired" : "CreateFailed");

        return fired;
    }

    private void LogWeaponAttackTick(
        int quantTick,
        CombatShooterType shooterType,
        string shooterNpcId,
        CombatTargetType targetType,
        string targetNpcId,
        string weaponConfigId,
        WeaponRuntimeStats weaponStats,
        float distanceAtTickStart,
        bool fired,
        string reason)
    {
        if (!WeaponAttackTickDebugLogEnabled)
            return;

        bool previousDebugEnabled = _debugEnabled;
        bool previousDebugStop = _debugStop;

        _debugEnabled = true;
        _debugStop = false;

        LogCustom(
            "[WeaponAttackTick] " +
            "Tick=" + quantTick +
            " | ShooterType=" + shooterType +
            " | ShooterNpcId=" + (shooterNpcId ?? string.Empty) +
            " | TargetType=" + targetType +
            " | TargetNpcId=" + (targetNpcId ?? string.Empty) +
            " | WeaponConfigId=" + (weaponConfigId ?? string.Empty) +
            " | WeaponType=" + weaponStats.WeaponType +
            " | ShotType=" + weaponStats.ShotType +
            " | DistanceAtTickStart=" + distanceAtTickStart.ToString("F2") +
            " | Range=" + weaponStats.Range.ToString("F2") +
            " | Fired=" + fired +
            " | Reason=" + (reason ?? string.Empty));

        _debugEnabled = previousDebugEnabled;
        _debugStop = previousDebugStop;
    }

    public bool TryGetWave(
    string waveId,
    out CombatWaveRuntimeState2A wave)
    {
        wave = null;

        if (string.IsNullOrWhiteSpace(waveId))
            return false;

        for (int i = 0; i < _activeWaves.Count; i++)
        {
            CombatWaveRuntimeState2A candidate = _activeWaves[i];

            if (candidate == null)
                continue;

            if (candidate.WaveId == waveId)
            {
                wave = candidate;
                return true;
            }
        }

        return false;
    }


    private void LogWaveDamage(string message)
    {
        if (!WaveDamageDebugLogEnabled)
            return;

        bool previousDebugEnabled = _debugEnabled;
        bool previousDebugStop = _debugStop;

        _debugEnabled = true;
        _debugStop = false;

        LogCustom("[WaveDamage] " + message);

        _debugEnabled = previousDebugEnabled;
        _debugStop = previousDebugStop;
    }

    private bool TryCreateWave(
    string systemId,
    CombatShooterType shooterType,
    string shooterNpcId,
    CombatTargetType targetType,
    string primaryTargetNpcId,
    string weaponConfigId,
    Vector3 centerPosition,
    WeaponConfig weaponConfig,
    WeaponRuntimeStats weaponStats,
    int quantTick)
    {
        centerPosition.z = 0f;

        if (!_waveShotCreateCountByTick.TryGetValue(
                quantTick,
                out int createCountForTick))
        {
            createCountForTick = 0;
        }

        createCountForTick++;
        _waveShotCreateCountByTick[quantTick] = createCountForTick;

        string shooterDebugId =
            shooterType == CombatShooterType.Player
                ? "player"
                : shooterNpcId ?? string.Empty;

        string shooterDebugKey =
            quantTick +
            "|" +
            shooterType +
            "|" +
            shooterDebugId;

        if (!_waveShotCreateCountByTickAndShooter.TryGetValue(
                shooterDebugKey,
                out int createCountForShooterThisTick))
        {
            createCountForShooterThisTick = 0;
        }

        createCountForShooterThisTick++;
        _waveShotCreateCountByTickAndShooter[shooterDebugKey] =
            createCountForShooterThisTick;

        float tickSeconds =
            Mathf.Max(0.01f, GameTimeService.SecondsPerDay);

        WaveWeaponVisualSettings2A visualSettings =
            ResolveWaveVisualSettings(weaponConfig);

        float durationSeconds =
            tickSeconds *
            (visualSettings != null
                ? visualSettings.WaveTickDuration01
                : 0.8f);

        CombatWaveRuntimeState2A wave = new CombatWaveRuntimeState2A
        {
            WaveId = Guid.NewGuid().ToString("N"),
            SystemId = systemId,

            ShooterType = shooterType,
            ShooterNpcId = shooterNpcId ?? string.Empty,

            TargetType = targetType,
            PrimaryTargetNpcId = primaryTargetNpcId ?? string.Empty,
            PrimaryTargetNpcType = ResolveWaveTargetNpcType(
                targetType,
                primaryTargetNpcId),

            WeaponConfigId = weaponConfigId ?? string.Empty,

            CenterPosition = centerPosition,

            Damage = Mathf.Max(0, weaponStats.Damage),
            CreatedTick = quantTick,

            ElapsedSeconds = 0f,
            DurationSeconds = Mathf.Max(0.01f, durationSeconds),
            FinalRadius = ResolveWaveDamageRadius(weaponStats),
            CurrentRadius = 0f,

            PlayerDamaged = false,
            IsResolved = false
        };

        LogWaveDamage(
            "[WaveShotDebug] CREATE | " +
            "WaveId=" + wave.WaveId +
            " | Tick=" + quantTick +
            " | CreateCountForTick=" + createCountForTick +
            " | CreateCountForThisShooterThisTick=" + createCountForShooterThisTick +
            " | ShooterType=" + wave.ShooterType +
            " | ShooterNpcId=" + wave.ShooterNpcId +
            " | TargetType=" + wave.TargetType +
            " | PrimaryTargetNpcId=" + wave.PrimaryTargetNpcId +
            " | WeaponConfigId=" + wave.WeaponConfigId +
            " | Damage=" + wave.Damage +
            " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
            " | DurationSeconds=" + wave.DurationSeconds.ToString("F2") +
            " | Center=" + wave.CenterPosition);

        _activeWaves.Add(wave);

        _eventBus.Publish(new CombatWaveStartedEvent2A(
            wave.WaveId,
            wave.SystemId,
            wave.ShooterType,
            wave.ShooterNpcId,
            wave.TargetType,
            wave.PrimaryTargetNpcId,
            wave.WeaponConfigId,
            wave.CenterPosition,
            wave.FinalRadius,
            wave.DurationSeconds));

        return true;
    }

    private void TickWaves(float deltaTime)
    {
        for (int i = _activeWaves.Count - 1; i >= 0; i--)
        {
            CombatWaveRuntimeState2A wave = _activeWaves[i];

            if (wave == null)
            {
                _activeWaves.RemoveAt(i);
                continue;
            }

            if (wave.IsResolved)
            {
                _activeWaves.RemoveAt(i);
                continue;
            }

            TickWave(wave, deltaTime);

            if (wave.IsResolved)
                _activeWaves.RemoveAt(i);
        }
    }

    private void TickWave(
        CombatWaveRuntimeState2A wave,
        float deltaTime)
    {
        if (wave == null)
            return;

        float previousElapsedSeconds =
            wave.ElapsedSeconds;

        float previousRadius =
            wave.CurrentRadius;

        wave.ElapsedSeconds += deltaTime;

        float progress01 = wave.Progress01;
        float smoothProgress01 =
            progress01 * progress01 * (3f - 2f * progress01);

        wave.CurrentRadius = Mathf.Lerp(
            0f,
            wave.FinalRadius,
            smoothProgress01);

        LogWaveDamage(
            "TICK_GEOMETRY | " +
            "WaveId=" + wave.WaveId +
            " | ShooterType=" + wave.ShooterType +
            " | ShooterNpcId=" + wave.ShooterNpcId +
            " | TargetType=" + wave.TargetType +
            " | PrimaryTargetNpcId=" + wave.PrimaryTargetNpcId +
            " | WeaponConfigId=" + wave.WeaponConfigId +
            " | DeltaTime=" + deltaTime.ToString("F3") +
            " | PreviousElapsed=" + previousElapsedSeconds.ToString("F3") +
            " | Elapsed=" + wave.ElapsedSeconds.ToString("F3") +
            " | Duration=" + wave.DurationSeconds.ToString("F3") +
            " | Progress01=" + progress01.ToString("F3") +
            " | SmoothProgress01=" + smoothProgress01.ToString("F3") +
            " | PreviousRadius=" + previousRadius.ToString("F2") +
            " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
            " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
            " | Center=" + wave.CenterPosition +
            " | PlayerDamaged=" + wave.PlayerDamaged +
            " | DamagedNpcCount=" + wave.DamagedNpcIds.Count);

        ApplyWaveDamage(wave);

        if (wave.ElapsedSeconds >= wave.DurationSeconds)
            CompleteWave(wave);
    }

    private void ApplyWaveDamage(CombatWaveRuntimeState2A wave)
    {
        if (wave == null)
        {
            LogWaveDamage("DAMAGE_SKIP | Wave is null.");
            return;
        }

        if (wave.Damage <= 0)
        {
            LogWaveDamage(
                "DAMAGE_SKIP | Damage <= 0 | " +
                "WaveId=" + wave.WaveId +
                " | Damage=" + wave.Damage);
            return;
        }

        if (wave.TargetType == CombatTargetType.Player)
        {
            LogWaveDamage(
                "DAMAGE_ROUTE | Player branch | " +
                "WaveId=" + wave.WaveId +
                " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
                " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
                " | Center=" + wave.CenterPosition);

            ApplyWaveDamageToPlayer(wave);
            return;
        }

        if (wave.TargetType != CombatTargetType.Npc)
        {
            LogWaveDamage(
                "DAMAGE_SKIP | Unsupported TargetType | " +
                "WaveId=" + wave.WaveId +
                " | TargetType=" + wave.TargetType);

            return;
        }

        IReadOnlyList<SystemNpcRuntimeState> candidates =
            _runtimeService.GetAliveNpcsInSystemByType(
                wave.SystemId,
                wave.PrimaryTargetNpcType);

        LogWaveDamage(
            "DAMAGE_ROUTE | NPC branch | " +
            "WaveId=" + wave.WaveId +
            " | CandidateCount=" + candidates.Count +
            " | PrimaryTargetNpcType=" + wave.PrimaryTargetNpcType +
            " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
            " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
            " | Center=" + wave.CenterPosition);

        for (int i = 0; i < candidates.Count; i++)
        {
            SystemNpcRuntimeState candidate = candidates[i];

            if (!CanWaveDamageNpc(wave, candidate))
                continue;

            Vector3 targetPosition =
                candidate.CurrentPosition;

            targetPosition.z = 0f;

            float distance =
                Vector3.Distance(
                    wave.CenterPosition,
                    targetPosition);

            bool isInsideCurrentRadius =
                distance <= wave.CurrentRadius;

            LogWaveDamage(
                "NPC_GEOMETRY | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + candidate.RuntimeNpcId +
                " | TargetPosition=" + targetPosition +
                " | Center=" + wave.CenterPosition +
                " | Distance=" + distance.ToString("F2") +
                " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
                " | RadiusMinusDistance=" + (wave.CurrentRadius - distance).ToString("F2") +
                " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
                " | Progress01=" + wave.Progress01.ToString("F3") +
                " | IsInsideCurrentRadius=" + isInsideCurrentRadius +
                " | AlreadyDamaged=" + wave.DamagedNpcIds.Contains(candidate.RuntimeNpcId));

            if (!isInsideCurrentRadius)
                continue;

            wave.DamagedNpcIds.Add(candidate.RuntimeNpcId);

            LogWaveDamage(
                "NPC_FIRST_ENTER_AND_DAMAGE | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + candidate.RuntimeNpcId +
                " | Damage=" + wave.Damage +
                " | Distance=" + distance.ToString("F2") +
                " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
                " | RadiusMinusDistance=" + (wave.CurrentRadius - distance).ToString("F2") +
                " | Progress01=" + wave.Progress01.ToString("F3"));

            CombatDamageResult2A damageResult = _runtimeService.ApplyDamage(
                candidate.RuntimeNpcId,
                wave.Damage,
                killedByPlayer: wave.ShooterType == CombatShooterType.Player,
                damagedByPlayer: wave.ShooterType == CombatShooterType.Player);

            PublishDamagePopup(
                CombatTargetType.Npc,
                candidate.RuntimeNpcId,
                damageResult.AppliedDamage,
                targetPosition,
                wave.CenterPosition);

            PublishWaveImpact(
                wave,
                CombatTargetType.Npc,
                candidate.RuntimeNpcId,
                targetPosition);
        }
    }

    private void ApplyWaveDamageToPlayer(CombatWaveRuntimeState2A wave)
    {
        if (wave.PlayerDamaged)
        {
            LogWaveDamage(
                "PLAYER_SKIP | Already damaged by this wave | " +
                "WaveId=" + wave.WaveId);

            return;
        }

        bool playerAvailable =
            _playerTargetService.IsPlayerAvailableInSystem(wave.SystemId);

        if (!playerAvailable)
        {
            LogWaveDamage(
                "PLAYER_SKIP | Player is not available in wave system | " +
                "WaveId=" + wave.WaveId +
                " | SystemId=" + wave.SystemId);

            return;
        }

        Vector3 playerPosition =
            _playerTargetService.GetPlayerPosition();

        playerPosition.z = 0f;

        float distance =
            Vector3.Distance(
                wave.CenterPosition,
                playerPosition);

        bool isInsideCurrentRadius =
            distance <= wave.CurrentRadius;

        LogWaveDamage(
            "PLAYER_GEOMETRY | " +
            "WaveId=" + wave.WaveId +
            " | PlayerPosition=" + playerPosition +
            " | Center=" + wave.CenterPosition +
            " | Distance=" + distance.ToString("F2") +
            " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
            " | RadiusMinusDistance=" + (wave.CurrentRadius - distance).ToString("F2") +
            " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
            " | Progress01=" + wave.Progress01.ToString("F3") +
            " | IsInsideCurrentRadius=" + isInsideCurrentRadius +
            " | AlreadyDamaged=" + wave.PlayerDamaged);

        if (!isInsideCurrentRadius)
            return;

        wave.PlayerDamaged = true;

        LogWaveDamage(
            "PLAYER_FIRST_ENTER_AND_DAMAGE | " +
            "WaveId=" + wave.WaveId +
            " | Damage=" + wave.Damage +
            " | Distance=" + distance.ToString("F2") +
            " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
            " | RadiusMinusDistance=" + (wave.CurrentRadius - distance).ToString("F2") +
            " | Progress01=" + wave.Progress01.ToString("F3"));

        CombatDamageResult2A damageResult =
            _playerTargetService.ApplyDamage(wave.Damage);

        PublishDamagePopup(
            CombatTargetType.Player,
            string.Empty,
            damageResult.AppliedDamage,
            playerPosition,
            wave.CenterPosition);

        PublishWaveImpact(
            wave,
            CombatTargetType.Player,
            string.Empty,
            playerPosition);
    }
    
    private bool CanWaveDamageNpc(
        CombatWaveRuntimeState2A wave,
        SystemNpcRuntimeState npc)
    {
        if (wave == null)
        {
            LogWaveDamage("NPC_SKIP | Wave is null.");
            return false;
        }

        if (npc == null)
        {
            LogWaveDamage(
                "NPC_SKIP | Candidate is null | " +
                "WaveId=" + wave.WaveId);

            return false;
        }

        if (!npc.IsAvailableForCombat())
        {
            LogWaveDamage(
                "NPC_SKIP | Candidate is not available for combat | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + npc.RuntimeNpcId);

            return false;
        }

        if (npc.CurrentSystemId != wave.SystemId)
        {
            LogWaveDamage(
                "NPC_SKIP | Candidate in another system | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + npc.RuntimeNpcId +
                " | CandidateSystem=" + npc.CurrentSystemId +
                " | WaveSystem=" + wave.SystemId);

            return false;
        }

        if (wave.ShooterType == CombatShooterType.Npc &&
            string.Equals(
                npc.RuntimeNpcId,
                wave.ShooterNpcId,
                StringComparison.Ordinal))
        {
            LogWaveDamage(
                "NPC_SKIP | Candidate is shooter | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + npc.RuntimeNpcId);

            return false;
        }

        if (wave.DamagedNpcIds.Contains(npc.RuntimeNpcId))
        {
            LogWaveDamage(
                "NPC_SKIP | Candidate already damaged by this wave | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + npc.RuntimeNpcId);

            return false;
        }

        if (npc.NpcType != wave.PrimaryTargetNpcType)
        {
            LogWaveDamage(
                "NPC_SKIP | Wrong npc type | " +
                "WaveId=" + wave.WaveId +
                " | TargetNpcId=" + npc.RuntimeNpcId +
                " | CandidateType=" + npc.NpcType +
                " | RequiredType=" + wave.PrimaryTargetNpcType);

            return false;
        }

        return true;
    }

    private void PublishWaveImpact(
        CombatWaveRuntimeState2A wave,
        CombatTargetType targetType,
        string targetNpcId,
        Vector3 hitPosition)
    {
        if (wave == null)
        {
            LogWaveDamage("IMPACT_SKIP | Wave is null.");
            return;
        }

        float hitDistance =
            Vector3.Distance(
                wave.CenterPosition,
                hitPosition);

        LogWaveDamage(
            "IMPACT_PUBLISH | " +
            "WaveId=" + wave.WaveId +
            " | ShooterType=" + wave.ShooterType +
            " | ShooterNpcId=" + wave.ShooterNpcId +
            " | TargetType=" + targetType +
            " | TargetNpcId=" + (targetNpcId ?? string.Empty) +
            " | Damage=" + wave.Damage +
            " | HitPosition=" + hitPosition +
            " | Center=" + wave.CenterPosition +
            " | HitDistance=" + hitDistance.ToString("F2") +
            " | CurrentRadius=" + wave.CurrentRadius.ToString("F2") +
            " | RadiusMinusDistance=" + (wave.CurrentRadius - hitDistance).ToString("F2") +
            " | FinalRadius=" + wave.FinalRadius.ToString("F2") +
            " | Progress01=" + wave.Progress01.ToString("F3"));

        _eventBus.Publish(new GalaxyNpcProjectileImpactEvent(
            wave.WaveId,
            wave.SystemId,
            wave.ShooterNpcId,
            targetType,
            targetNpcId ?? string.Empty,
            wave.Damage,
            hitPosition,
            true));

        _eventBus.Publish(new CombatProjectileImpactEvent2A(
            wave.WaveId,
            wave.SystemId,
            targetNpcId ?? string.Empty,
            wave.Damage,
            hitPosition,
            true));
    }

    private void CompleteWave(CombatWaveRuntimeState2A wave)
    {
        if (wave == null)
            return;

        if (wave.IsResolved)
            return;

        wave.IsResolved = true;

        LogWaveDamage(
            "COMPLETE | " +
            "WaveId=" + wave.WaveId +
            " | ShooterType=" + wave.ShooterType +
            " | TargetType=" + wave.TargetType +
            " | PlayerDamaged=" + wave.PlayerDamaged +
            " | DamagedNpcCount=" + wave.DamagedNpcIds.Count);

        _eventBus.Publish(new CombatWaveEndedEvent2A(wave.WaveId));
    }

    private SystemNpcType ResolveWaveTargetNpcType(
        CombatTargetType targetType,
        string targetNpcId)
    {
        if (targetType == CombatTargetType.Npc &&
            !string.IsNullOrWhiteSpace(targetNpcId) &&
            _runtimeService.TryGetNpc(
                targetNpcId,
                out SystemNpcRuntimeState target) &&
            target != null)
        {
            return target.NpcType;
        }

        return SystemNpcType.Enemy;
    }

    private static float ResolveWaveDamageRadius(WeaponRuntimeStats weaponStats)
    {
        return Mathf.Max(0f, weaponStats.Range);
    }

    private static WaveWeaponVisualSettings2A ResolveWaveVisualSettings(
        WeaponConfig weaponConfig)
    {
        if (weaponConfig == null)
            return null;

        if (weaponConfig.ProjectilePrefabRef == null)
            return null;

        return weaponConfig.ProjectilePrefabRef
            .GetComponent<WaveWeaponVisualSettings2A>();
    }

    private bool TryCreateTimedEnergyProjectiles(
        string systemId,
        CombatShooterType shooterType,
        string shooterNpcId,
        CombatTargetType targetType,
        string targetNpcId,
        string weaponConfigId,
        Vector3 startPosition,
        Vector3 targetPosition,
        WeaponConfig weaponConfig,
        WeaponRuntimeStats weaponStats,
        int quantTick)
    {
        int shotCount = Mathf.Max(1, weaponStats.ShotCount);

        ProjectileWeaponVisualSettings2A visualSettings =
            ResolveProjectileVisualSettings(weaponConfig);

        float sequenceTickDuration01 =
            visualSettings != null
                ? visualSettings.ShotSequenceTickDuration01
                : 1f;

        float shotGapTickDuration01 =
            visualSettings != null
                ? visualSettings.ShotGapTickDuration01
                : 0.02f;

        float tickSeconds =
            Mathf.Max(0.01f, GameTimeService.SecondsPerDay);

        float sequenceSeconds =
            Mathf.Max(0.01f, tickSeconds * sequenceTickDuration01);

        float slotSeconds =
            sequenceSeconds / shotCount;

        float gapSeconds =
            Mathf.Min(
                tickSeconds * shotGapTickDuration01,
                Mathf.Max(0f, slotSeconds - 0.01f));

        float energyProjectileFlightSeconds =
            Mathf.Max(0.01f, slotSeconds - gapSeconds);

        bool isMissile =
            weaponStats.ShotType == WeaponShotType2A.MissileSwarm;

        Vector3 pathDirection =
            targetPosition - startPosition;

        pathDirection.z = 0f;

        Vector3 leftPerpendicular =
            Vector3.zero;

        if (pathDirection.sqrMagnitude > 0.0001f)
        {
            pathDirection.Normalize();
            leftPerpendicular = new Vector3(-pathDirection.y, pathDirection.x, 0f);
        }

        float laneOffset = 0f;

        if (visualSettings != null)
        {
            laneOffset = isMissile
                ? visualSettings.MissileLaneOffset
                : visualSettings.RapidProjectileLaneOffset;
        }

        bool createdAny = false;

        for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
        {
            float startDelaySeconds = isMissile
                ? Mathf.Max(0f, slotSeconds * (shotIndex + 1) - gapSeconds)
                : slotSeconds * shotIndex;

            float lifetimeSeconds = isMissile
                ? Mathf.Max(
                    0.01f,
                    tickSeconds * Mathf.Max(1, weaponStats.ProjectileLifetime))
                : energyProjectileFlightSeconds;

            Vector3 pathOffset =
                ResolveRapidProjectilePathOffset(
                    leftPerpendicular,
                    laneOffset,
                    shotIndex);

            GalaxyNpcProjectileRuntimeState projectile =
                CreateProjectileRuntimeState(
                    systemId,
                    shooterType,
                    shooterNpcId,
                    targetType,
                    targetNpcId,
                    weaponConfigId,
                    weaponStats.ShotType,
                    startPosition,
                    targetPosition,
                    pathOffset,
                    Mathf.Max(0, weaponStats.Damage),
                    shotIndex,
                    shotCount,
                    quantTick,
                    startDelaySeconds,
                    lifetimeSeconds);

            if (isMissile)
            {
                projectile.RequiresArrivalToDamage = true;
                projectile.ImpactTick = int.MaxValue;

                projectile.InitialSpeedUnitsPerSecond =
                    visualSettings != null
                        ? visualSettings.MissileInitialSpeedUnitsPerSecond
                        : 60f;

                projectile.FinalSpeedUnitsPerSecond =
                    visualSettings != null
                        ? visualSettings.MissileFinalSpeedUnitsPerSecond
                        : 220f;

                projectile.ArrivalDistance =
                    visualSettings != null
                        ? visualSettings.MissileArrivalDistance
                        : 0.25f;

                projectile.MissileCurveOffset =
                    visualSettings != null
                        ? visualSettings.MissileCurveOffset
                        : 0.8f;

                projectile.LastKnownTargetPosition =
                    targetPosition;

                BuildMissileRoute(
                    projectile,
                    startPosition,
                    targetPosition,
                    pathOffset,
                    projectile.MissileCurveOffset);
            }

            _activeProjectiles.Add(projectile);

            PublishProjectileCreated(projectile);

            createdAny = true;
        }

        return createdAny;
    }

    private GalaxyNpcProjectileRuntimeState CreateProjectileRuntimeState(
    string systemId,
    CombatShooterType shooterType,
    string shooterNpcId,
    CombatTargetType targetType,
    string targetNpcId,
    string weaponConfigId,
    WeaponShotType2A shotType,
    Vector3 startPosition,
    Vector3 targetPosition,
    Vector3 pathOffset,
    int damage,
    int shotIndex,
    int shotCount,
    int quantTick,
    float startDelaySeconds,
    float lifetimeSeconds)
    {
        startPosition.z = 0f;
        targetPosition.z = 0f;
        pathOffset.z = 0f;

        Vector3 shiftedStartPosition = startPosition + pathOffset;
        Vector3 shiftedTargetPosition = targetPosition + pathOffset;

        return new GalaxyNpcProjectileRuntimeState
        {
            ProjectileId = Guid.NewGuid().ToString("N"),

            SystemId = systemId,

            ShooterType = shooterType,
            ShooterNpcId = shooterNpcId ?? string.Empty,

            TargetType = targetType,
            TargetNpcId = targetNpcId ?? string.Empty,

            WeaponConfigId = weaponConfigId ?? string.Empty,
            ShotType = shotType,

            StartPosition = shiftedStartPosition,
            CurrentPosition = shiftedStartPosition,
            LastKnownTargetPosition = shiftedTargetPosition,
            PathOffset = pathOffset,

            Damage = damage,

            ShotIndex = Mathf.Max(0, shotIndex),
            ShotCount = Mathf.Max(1, shotCount),

            CreatedTick = quantTick,
            ImpactTick = quantTick,

            ElapsedSeconds = 0f,
            StartDelaySeconds = Mathf.Max(0f, startDelaySeconds),
            LifetimeSeconds = Mathf.Max(0.01f, lifetimeSeconds),

            InitialSpeedUnitsPerSecond = 0f,
            FinalSpeedUnitsPerSecond = 0f,
            TravelledDistance = 0f,
            MissileCurveOffset = 0f,

            RequiresArrivalToDamage = false,
            ArrivalDistance = 0.25f,

            HasReleased = false,
            ShouldDamageOnArrival = true,
            ShouldSpawnHitFxOnMiss = false,
            ResolveAfterTargetLost = false,
            ResolveAtElapsedSeconds = 0f,

            IsResolved = false
        };
    }

    private static void BuildMissileRoute(
    GalaxyNpcProjectileRuntimeState projectile,
    Vector3 startPosition,
    Vector3 targetPosition,
    Vector3 pathOffset,
    float curveOffset)
    {
        if (projectile == null)
            return;

        startPosition.z = 0f;
        targetPosition.z = 0f;
        pathOffset.z = 0f;

        Vector3 launchPosition = startPosition + pathOffset;

        Vector3 directDirection = targetPosition - launchPosition;
        directDirection.z = 0f;

        if (directDirection.sqrMagnitude <= 0.0001f)
            directDirection = targetPosition - startPosition;

        if (directDirection.sqrMagnitude <= 0.0001f)
            directDirection = Vector3.right;

        directDirection.Normalize();

        Vector3 directPerpendicular = new Vector3(
            -directDirection.y,
            directDirection.x,
            0f);

        float side = 0f;

        if (pathOffset.sqrMagnitude > 0.0001f)
            side = Mathf.Sign(Vector3.Dot(pathOffset, directPerpendicular));

        if (Mathf.Approximately(side, 0f))
            side = projectile.ShotIndex % 2 == 0 ? 1f : -1f;

        Quaternion launchRotation =
            Quaternion.AngleAxis(30f * side, Vector3.forward);

        Vector3 launchDirection = launchRotation * directDirection;

        float distanceToTarget =
            Vector3.Distance(launchPosition, targetPosition);

        float launchLegDistance =
            Mathf.Max(curveOffset, distanceToTarget * 0.3f);

        Vector3 firstTurnPosition =
            launchPosition + launchDirection * launchLegDistance;

        Vector3 secondTurnPosition =
            Vector3.Lerp(launchPosition, targetPosition, 0.65f) +
            directPerpendicular * curveOffset * side;

        projectile.RoutePoints.Clear();
        projectile.RoutePoints.Add(launchPosition);
        projectile.RoutePoints.Add(firstTurnPosition);
        projectile.RoutePoints.Add(secondTurnPosition);
        projectile.RoutePoints.Add(targetPosition);
        projectile.RouteSegmentIndex = 1;
    }

    private void TickMissileProjectile(
    GalaxyNpcProjectileRuntimeState projectile,
    float deltaTime,
    Vector3 targetPosition)
    {
        float flightElapsedSeconds =
            projectile.ElapsedSeconds - projectile.StartDelaySeconds;

        if (projectile.ResolveAfterTargetLost &&
            projectile.ElapsedSeconds >= projectile.ResolveAtElapsedSeconds)
        {
            ResolveProjectile(projectile, allowDamage: false);
            return;
        }

        if (flightElapsedSeconds >= projectile.LifetimeSeconds)
        {
            ResolveProjectile(projectile, allowDamage: false);
            return;
        }

        RefreshMissileRouteTarget(projectile, targetPosition);

        float lifetimeProgress01 =
            Mathf.Clamp01(
                flightElapsedSeconds /
                Mathf.Max(0.01f, projectile.LifetimeSeconds));

        float speed =
            Mathf.Lerp(
                projectile.InitialSpeedUnitsPerSecond,
                projectile.FinalSpeedUnitsPerSecond,
                lifetimeProgress01);

        bool arrived =
            MoveProjectileAlongRoute(
                projectile,
                speed,
                deltaTime,
                projectile.ArrivalDistance);

        if (arrived)
            ResolveProjectile(projectile, projectile.ShouldDamageOnArrival);
    }

    private static void RefreshMissileRouteTarget(
    GalaxyNpcProjectileRuntimeState projectile,
    Vector3 targetPosition)
    {
        if (projectile.RoutePoints == null || projectile.RoutePoints.Count == 0)
            return;

        targetPosition.z = 0f;

        projectile.RoutePoints[projectile.RoutePoints.Count - 1] =
            targetPosition;
    }

    private static bool MoveProjectileAlongRoute(
        GalaxyNpcProjectileRuntimeState projectile,
        float speed,
        float deltaTime,
        float arrivalDistance)
    {
        if (projectile.RoutePoints == null || projectile.RoutePoints.Count < 2)
            return false;

        int segmentIndex =
            Mathf.Clamp(
                projectile.RouteSegmentIndex,
                1,
                projectile.RoutePoints.Count - 1);

        Vector3 destination =
            projectile.RoutePoints[segmentIndex];

        SystemTravelMathResult result =
            SystemTravelMath.MoveTowards(
                projectile.CurrentPosition,
                projectile.StartPosition,
                destination,
                speed,
                deltaTime,
                arrivalDistance);

        projectile.CurrentPosition = result.NewPosition;

        if (!result.Arrived)
            return false;

        if (segmentIndex >= projectile.RoutePoints.Count - 1)
            return true;

        projectile.RouteSegmentIndex = segmentIndex + 1;
        return false;
    }

    private static Vector3 ResolveRapidProjectilePathOffset(
    Vector3 leftPerpendicular,
    float laneOffset,
    int shotIndex)
    {
        if (laneOffset <= 0f)
            return Vector3.zero;

        if (leftPerpendicular.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        float side = shotIndex % 2 == 0 ? 1f : -1f;

        return leftPerpendicular.normalized * laneOffset * side;
    }

    private bool TryCreateSingleLegacyProjectile(
        string systemId,
        CombatShooterType shooterType,
        string shooterNpcId,
        CombatTargetType targetType,
        string targetNpcId,
        string weaponConfigId,
        Vector3 startPosition,
        Vector3 targetPosition,
        WeaponRuntimeStats weaponStats,
        int quantTick)
    {
        int projectileLifetimeTicks =
            Mathf.Max(1, weaponStats.ProjectileLifetime);

        GalaxyNpcProjectileRuntimeState projectile =
            new GalaxyNpcProjectileRuntimeState
            {
                ProjectileId = Guid.NewGuid().ToString("N"),

                SystemId = systemId,

                ShooterType = shooterType,
                ShooterNpcId = shooterNpcId ?? string.Empty,

                TargetType = targetType,
                TargetNpcId = targetNpcId ?? string.Empty,

                WeaponConfigId = weaponConfigId,
                ShotType = weaponStats.ShotType,

                StartPosition = startPosition,
                CurrentPosition = startPosition,
                LastKnownTargetPosition = targetPosition,

                Damage = Mathf.Max(0, weaponStats.Damage),

                ShotIndex = 0,
                ShotCount = 1,

                CreatedTick = quantTick,
                ImpactTick = quantTick + projectileLifetimeTicks - 1,

                ElapsedSeconds = 0f,
                StartDelaySeconds = 0f,
                LifetimeSeconds = Mathf.Max(
                    0.01f,
                    GameTimeService.SecondsPerDay * projectileLifetimeTicks),

                IsResolved = false
            };

        _activeProjectiles.Add(projectile);

        PublishProjectileCreated(projectile);

        return true;
    }

    private void PublishProjectileCreated(
     GalaxyNpcProjectileRuntimeState projectile)
    {
        if (projectile == null)
            return;

        _eventBus.Publish(new GalaxyNpcProjectileCreatedEvent(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.ShooterNpcId,
            projectile.TargetType,
            projectile.TargetNpcId,
            projectile.WeaponConfigId,
            projectile.StartPosition,
            projectile.LastKnownTargetPosition,
            TickBasedProjectileSpeed));

        _eventBus.Publish(new CombatProjectileCreatedEvent2A(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.ShooterType == CombatShooterType.Player
                ? "player"
                : projectile.ShooterNpcId,
            projectile.TargetNpcId,
            projectile.WeaponConfigId,
            projectile.StartPosition,
            projectile.LastKnownTargetPosition));
    }

    private static bool IsTimedEnergyProjectile(WeaponShotType2A shotType)
    {
        return shotType == WeaponShotType2A.RapidEnergyVolley ||
               shotType == WeaponShotType2A.HeavyProjectile ||
               shotType == WeaponShotType2A.MissileSwarm;
    }

    private static ProjectileWeaponVisualSettings2A ResolveProjectileVisualSettings(
     WeaponConfig weaponConfig)
    {
        if (weaponConfig == null)
            return null;

        if (weaponConfig.ProjectilePrefabRef == null)
            return null;

        return weaponConfig.ProjectilePrefabRef
            .GetComponent<ProjectileWeaponVisualSettings2A>();
    }

    public bool TryGetBeam(
        string beamId,
        out CombatBeamRuntimeState2A beam)
    {
        beam = null;

        if (string.IsNullOrWhiteSpace(beamId))
            return false;

        for (int i = 0; i < _activeBeams.Count; i++)
        {
            CombatBeamRuntimeState2A candidate = _activeBeams[i];

            if (candidate == null)
                continue;

            if (candidate.BeamId == beamId)
            {
                beam = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryCreateBeam(
    string systemId,
    CombatShooterType shooterType,
    string shooterNpcId,
    CombatTargetType targetType,
    string targetNpcId,
    string weaponConfigId,
    Vector3 startPosition,
    Vector3 targetPosition,
    WeaponRuntimeStats weaponStats,
    int quantTick)
    {
        int shotCount = Mathf.Max(1, weaponStats.ShotCount);

        float beamTickDuration01 =
            CombatBeamRuntimeSettings2A.BeamTickDuration01;

        CombatBeamRuntimeState2A beam = new CombatBeamRuntimeState2A
        {
            BeamId = Guid.NewGuid().ToString("N"),
            SystemId = systemId,

            ShooterType = shooterType,
            ShooterNpcId = shooterNpcId ?? string.Empty,

            TargetType = targetType,
            TargetNpcId = targetNpcId ?? string.Empty,

            WeaponConfigId = weaponConfigId,

            StartPosition = startPosition,
            TargetPosition = targetPosition,

            TotalDamage = Mathf.Max(0, weaponStats.Damage),
            ShotCount = shotCount,
            AppliedShotCount = 0,

            CreatedTick = quantTick,
            ElapsedSeconds = 0f,
            DurationSeconds = Mathf.Max(
                0.01f,
                GameTimeService.SecondsPerDay * beamTickDuration01),

            IsResolved = false
        };

        _activeBeams.Add(beam);

        _eventBus.Publish(new CombatBeamStartedEvent2A(
            beam.BeamId,
            beam.SystemId,
            beam.ShooterType,
            beam.ShooterNpcId,
            beam.TargetType,
            beam.TargetNpcId,
            beam.WeaponConfigId,
            beam.StartPosition,
            beam.TargetPosition,
            beam.DurationSeconds));

        return true;
    }

    private void TickBeams(float deltaTime)
    {
        for (int i = _activeBeams.Count - 1; i >= 0; i--)
        {
            CombatBeamRuntimeState2A beam = _activeBeams[i];

            if (beam == null)
            {
                _activeBeams.RemoveAt(i);
                continue;
            }

            if (beam.IsResolved)
            {
                _activeBeams.RemoveAt(i);
                continue;
            }

            TickBeam(beam, deltaTime);

            if (beam.IsResolved)
                _activeBeams.RemoveAt(i);
        }
    }

    private void TickBeam(
        CombatBeamRuntimeState2A beam,
        float deltaTime)
    {
        beam.ElapsedSeconds += deltaTime;

        if (!TryRefreshBeamPositions(beam))
        {
            CompleteBeam(beam);
            return;
        }

        ApplyDueBeamDamage(beam);

        if (beam.ElapsedSeconds >= beam.DurationSeconds)
            CompleteBeam(beam);
    }

    private void ApplyDueBeamDamage(CombatBeamRuntimeState2A beam)
    {
        int safeShotCount = Mathf.Max(1, beam.ShotCount);
        float safeDuration = Mathf.Max(0.01f, beam.DurationSeconds);
        int damagePerShot = Mathf.Max(0, beam.TotalDamage);

        while (!beam.IsResolved && beam.AppliedShotCount < safeShotCount)
        {
            int nextShotIndex = beam.AppliedShotCount;

            float requiredTime =
                safeDuration * (nextShotIndex + 1) / safeShotCount;

            if (beam.ElapsedSeconds < requiredTime)
                return;

            Debug.Log(
                "[BEAM_DAMAGE_SHOT] " +
                "BeamId=" + beam.BeamId +
                ", WeaponConfigId=" + beam.WeaponConfigId +
                ", ShooterType=" + beam.ShooterType +
                ", ShooterNpcId=" + beam.ShooterNpcId +
                ", TargetType=" + beam.TargetType +
                ", TargetNpcId=" + beam.TargetNpcId +
                ", Shot=" + (nextShotIndex + 1) + "/" + safeShotCount +
                ", Damage=" + damagePerShot);

            ApplyBeamDamage(beam, damagePerShot);
            beam.AppliedShotCount++;
        }
    }

    private void ApplyBeamDamage(
        CombatBeamRuntimeState2A beam,
        int damage)
    {
        if (damage <= 0)
            return;

        if (beam.TargetType == CombatTargetType.Player)
        {
            CombatDamageResult2A damageResult =
                _playerTargetService.ApplyDamage(damage);

            PublishDamagePopup(
                CombatTargetType.Player,
                string.Empty,
                damageResult.AppliedDamage,
                beam.TargetPosition,
                beam.StartPosition);

            return;
        }

        if (beam.TargetType != CombatTargetType.Npc)
            return;

        CombatDamageResult2A npcDamageResult = _runtimeService.ApplyDamage(
            beam.TargetNpcId,
            damage,
            killedByPlayer: beam.ShooterType == CombatShooterType.Player,
            damagedByPlayer: beam.ShooterType == CombatShooterType.Player);

        PublishDamagePopup(
            CombatTargetType.Npc,
            beam.TargetNpcId,
            npcDamageResult.AppliedDamage,
            beam.TargetPosition,
            beam.StartPosition);
    }

    private bool TryRefreshBeamPositions(CombatBeamRuntimeState2A beam)
    {
        if (!TryGetBeamStartPosition(beam, out Vector3 startPosition))
            return false;

        if (!TryGetBeamTargetPosition(beam, out Vector3 targetPosition))
            return false;

        beam.StartPosition = startPosition;
        beam.TargetPosition = targetPosition;

        return true;
    }

    private bool TryGetBeamStartPosition(
        CombatBeamRuntimeState2A beam,
        out Vector3 position)
    {
        position = beam.StartPosition;

        if (beam.ShooterType == CombatShooterType.Player)
        {
            if (!_playerTargetService.IsPlayerAvailableInSystem(beam.SystemId))
                return false;

            position = _playerTargetService.GetPlayerPosition();
            position.z = 0f;
            return true;
        }

        if (beam.ShooterType == CombatShooterType.Npc)
        {
            if (!_runtimeService.TryGetNpc(
                    beam.ShooterNpcId,
                    out SystemNpcRuntimeState shooter))
            {
                return false;
            }

            if (!shooter.IsAvailableForCombat())
                return false;

            if (shooter.CurrentSystemId != beam.SystemId)
                return false;

            position = shooter.CurrentPosition;
            position.z = 0f;
            return true;
        }

        return false;
    }

    private bool TryGetBeamTargetPosition(
        CombatBeamRuntimeState2A beam,
        out Vector3 position)
    {
        position = beam.TargetPosition;

        if (beam.TargetType == CombatTargetType.Player)
        {
            if (!_playerTargetService.IsPlayerAvailableInSystem(beam.SystemId))
                return false;

            position = _playerTargetService.GetPlayerPosition();
            position.z = 0f;
            return true;
        }

        if (beam.TargetType == CombatTargetType.Npc)
        {
            if (!_runtimeService.TryGetNpc(
                    beam.TargetNpcId,
                    out SystemNpcRuntimeState target))
            {
                return false;
            }

            if (!target.IsAvailableForCombat())
                return false;

            if (target.CurrentSystemId != beam.SystemId)
                return false;

            position = target.CurrentPosition;
            position.z = 0f;
            return true;
        }

        return false;
    }

    private void CompleteBeam(CombatBeamRuntimeState2A beam)
    {
        if (beam == null || beam.IsResolved)
            return;

        beam.IsResolved = true;

        _eventBus.Publish(new CombatBeamEndedEvent2A(beam.BeamId));
    }

    private static int BuildWeaponRollSeed(params string[] parts)
    {
        unchecked
        {
            int hash = 17;

            if (parts == null)
                return hash;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (string.IsNullOrEmpty(part))
                {
                    hash = hash * 31;
                    continue;
                }

                for (int j = 0; j < part.Length; j++)
                    hash = hash * 31 + part[j];
            }

            return hash;
        }
    }

    private static int GetBeamDamagePortion(
      int totalDamage,
      int shotCount,
      int shotIndex)
    {
        int safeShotCount = Mathf.Max(1, shotCount);
        int safeDamage = Mathf.Max(0, totalDamage);
        int safeShotIndex = Mathf.Clamp(shotIndex, 0, safeShotCount - 1);

        int baseDamage = safeDamage / safeShotCount;
        int remainder = safeDamage % safeShotCount;

        if (safeShotIndex == safeShotCount - 1)
            return baseDamage + remainder;

        return baseDamage;
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        CompleteBeamsForNpc(evt.RuntimeNpcId);
        MarkProjectilesTargetingNpcAsMiss(evt.RuntimeNpcId, evt.Position);
    }

    private void OnPlayerShipDestroyedByNpc(PlayerShipDestroyedByNpcEvent evt)
    {
        CompleteBeamsMatching(beam =>
            beam.ShooterType == CombatShooterType.Player ||
            beam.TargetType == CombatTargetType.Player);

        MarkProjectilesTargetingPlayerAsMiss();
    }

    private void CompleteProjectilesForNpc(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        CompleteProjectilesMatching(projectile =>
            string.Equals(
                projectile.ShooterNpcId,
                runtimeNpcId,
                StringComparison.Ordinal) ||
            string.Equals(
                projectile.TargetNpcId,
                runtimeNpcId,
                StringComparison.Ordinal));
    }

    private void CompleteProjectilesMatching(
        Predicate<GalaxyNpcProjectileRuntimeState> predicate)
    {
        if (predicate == null)
            return;

        for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
        {
            GalaxyNpcProjectileRuntimeState projectile =
                _activeProjectiles[i];

            if (projectile == null)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            if (projectile.IsResolved)
            {
                _activeProjectiles.RemoveAt(i);
                continue;
            }

            if (!predicate(projectile))
                continue;

            ResolveProjectile(projectile, allowDamage: false);
            _activeProjectiles.RemoveAt(i);
        }
    }

    private void CompleteBeamsForNpc(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        CompleteBeamsMatching(beam =>
            string.Equals(
                beam.ShooterNpcId,
                runtimeNpcId,
                StringComparison.Ordinal) ||
            string.Equals(
                beam.TargetNpcId,
                runtimeNpcId,
                StringComparison.Ordinal));
    }

    private void MarkProjectilesTargetingNpcAsMiss(
    string runtimeNpcId,
    Vector3 lastKnownTargetPosition)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        lastKnownTargetPosition.z = 0f;

        for (int i = 0; i < _activeProjectiles.Count; i++)
        {
            GalaxyNpcProjectileRuntimeState projectile =
                _activeProjectiles[i];

            if (projectile == null || projectile.IsResolved)
                continue;

            if (projectile.TargetType != CombatTargetType.Npc)
                continue;

            if (!string.Equals(
                    projectile.TargetNpcId,
                    runtimeNpcId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            MarkProjectileAsMiss(
                projectile,
                lastKnownTargetPosition);
        }
    }

    private void MarkProjectilesTargetingPlayerAsMiss()
    {
        for (int i = 0; i < _activeProjectiles.Count; i++)
        {
            GalaxyNpcProjectileRuntimeState projectile =
                _activeProjectiles[i];

            if (projectile == null || projectile.IsResolved)
                continue;

            if (projectile.TargetType != CombatTargetType.Player)
                continue;

            Vector3 targetPosition = projectile.LastKnownTargetPosition;

            if (projectile.ShotType != WeaponShotType2A.MissileSwarm)
                targetPosition -= projectile.PathOffset;

            MarkProjectileAsMiss(
                projectile,
                targetPosition);
        }
    }

    private void MarkProjectileAsMiss(
    GalaxyNpcProjectileRuntimeState projectile,
    Vector3 lastKnownTargetPosition)
    {
        if (projectile == null)
            return;

        lastKnownTargetPosition.z = 0f;

        projectile.ShouldDamageOnArrival = false;

        if (projectile.ShotType == WeaponShotType2A.MissileSwarm)
        {
            projectile.ShouldSpawnHitFxOnMiss = true;
            projectile.ResolveAfterTargetLost = true;
            projectile.ResolveAtElapsedSeconds =
                projectile.ElapsedSeconds +
                Mathf.Max(0.01f, GameTimeService.SecondsPerDay);

            projectile.LastKnownTargetPosition =
                lastKnownTargetPosition;

            if (projectile.RoutePoints != null &&
                projectile.RoutePoints.Count > 0)
            {
                projectile.RoutePoints[projectile.RoutePoints.Count - 1] =
                    lastKnownTargetPosition;
            }

            return;
        }

        projectile.LastKnownTargetPosition =
            lastKnownTargetPosition + projectile.PathOffset;
    }

    private void CompleteBeamsMatching(
        Predicate<CombatBeamRuntimeState2A> predicate)
    {
        if (predicate == null)
            return;

        for (int i = 0; i < _activeBeams.Count; i++)
        {
            CombatBeamRuntimeState2A beam = _activeBeams[i];

            if (beam == null || beam.IsResolved)
                continue;

            if (predicate(beam))
                CompleteBeam(beam);
        }
    }

    private void PublishDamagePopup(
        CombatTargetType targetType,
        string targetNpcId,
        int appliedDamage,
        Vector3 targetPosition,
        Vector3 damageSourcePosition)
    {
        if (appliedDamage <= 0)
            return;

        targetPosition.z = 0f;
        damageSourcePosition.z = 0f;

        Vector3 popupDirection = ResolveDamagePopupDirection(
            targetType,
            targetNpcId,
            targetPosition,
            damageSourcePosition);

        _eventBus.Publish(new CombatDamagePopupEvent2A(
            targetType,
            targetNpcId,
            appliedDamage,
            targetPosition,
            damageSourcePosition,
            popupDirection));
    }

    private Vector3 ResolveDamagePopupDirection(
    CombatTargetType targetType,
    string targetNpcId,
    Vector3 targetPosition,
    Vector3 damageSourcePosition)
    {
        if (targetType == CombatTargetType.Player &&
            TryGetPlayerMovementDirection(out Vector3 playerMovementDirection))
        {
            return RotateTailDirection(playerMovementDirection);
        }

        if (targetType == CombatTargetType.Npc &&
            TryGetNpcMovementDirection(targetNpcId, out Vector3 npcMovementDirection))
        {
            return RotateTailDirection(npcMovementDirection);
        }

        Vector3 fallbackDirection =
            targetPosition - damageSourcePosition;

        fallbackDirection.z = 0f;

        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return fallbackDirection.normalized;
    }

    private bool TryGetPlayerMovementDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (_shipMovementService == null &&
            Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet(out _shipMovementService);
        }

        ShipMovementRuntimeState movementState =
            _shipMovementService != null
                ? _shipMovementService.State
                : null;

        if (movementState == null || !movementState.IsMoving)
            return false;

        Vector2 facingDirection =
            movementState.FacingDirection;

        if (facingDirection.sqrMagnitude <= 0.0001f)
            return false;

        direction = new Vector3(
            facingDirection.x,
            facingDirection.y,
            0f).normalized;

        return true;
    }

    private bool TryGetNpcMovementDirection(
    string targetNpcId,
    out Vector3 direction)
    {
        direction = Vector3.zero;

        if (string.IsNullOrWhiteSpace(targetNpcId))
            return false;

        if (!_runtimeService.TryGetNpc(
                targetNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            return false;
        }

        if (npc.TravelState == SystemNpcTravelState.Idle ||
            npc.TravelState == SystemNpcTravelState.OnPlanet)
        {
            return false;
        }

        direction = npc.TickMovementDirection;
        direction.z = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = npc.FacingDirection;
            direction.z = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = npc.TargetPosition - npc.CurrentPosition;
            direction.z = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    private static Vector3 RotateTailDirection(Vector3 movementDirection)
    {
        movementDirection.z = 0f;

        if (movementDirection.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        Vector3 tailDirection =
            -movementDirection.normalized;

        return Quaternion.AngleAxis(
            30f,
            Vector3.forward) * tailDirection;
    }
}
