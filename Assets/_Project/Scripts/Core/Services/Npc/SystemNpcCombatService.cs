using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcCombatService : CustomService, ISystemNpcCombatService
{
    private const float TickBasedProjectileSpeed = 0f;

    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly IConfigService _configService;
    private readonly IPlayerCombatTargetService _playerTargetService;
    private readonly SimpleEventBus _eventBus;
    private readonly ISystemEncounterService _encounterService;

    private readonly List<GalaxyNpcProjectileRuntimeState> _activeProjectiles = new();

    private readonly List<CombatBeamRuntimeState2A> _activeBeams = new();

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

            if (fired)
                firedAnyWeapon = true;
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

        float distance = Vector3.Distance(
            shooter.CurrentPosition,
            target.Position);

        if (distance > weaponStats.Range)
        {
            LogCustom(
                "[SystemNpcCombatService] Target out of range. " +
                "Shooter: " +
                shooter.RuntimeNpcId +
                ", TargetType: " +
                target.TargetType +
                ", Distance: " +
                distance.ToString("F2") +
                ", Range: " +
                weaponStats.Range.ToString("F2"));

            return false;
        }

        if (!ignoreTickGate && !weaponRuntime.CanShootAtTick(quantTick))
            return false;

        weaponRuntime.MarkShotAtTick(quantTick, cooldownTicks: 1);

        EnsureEncounterForPlayerAttack(shooter, target);

        if (weaponStats.ShotType == WeaponShotType2A.Beam)
        {
            return TryCreateBeam(
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

        if (IsTimedEnergyProjectile(weaponStats.ShotType))
        {
            return TryCreateTimedEnergyProjectiles(
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

        return TryCreateSingleProjectile(
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

        Vector3 targetPosition = projectile.LastKnownTargetPosition;

        if (TryGetCurrentProjectileTargetPosition(
                projectile,
                out Vector3 currentTargetPosition))
        {
            targetPosition = currentTargetPosition + projectile.PathOffset;
            projectile.LastKnownTargetPosition = targetPosition;
        }

        if (projectile.ElapsedSeconds < projectile.StartDelaySeconds)
        {
            projectile.CurrentPosition = projectile.StartPosition;
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
            ResolveProjectile(projectile);
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
        projectile.IsResolved = true;

        bool didHit = false;
        Vector3 hitPosition = projectile.CurrentPosition;

        if (projectile.TargetType == CombatTargetType.Player)
        {
            ResolvePlayerProjectile(projectile, ref hitPosition, ref didHit);
        }
        else if (projectile.TargetType == CombatTargetType.Npc)
        {
            ResolveNpcProjectile(projectile, ref hitPosition, ref didHit);
        }

        _eventBus.Publish(new GalaxyNpcProjectileImpactEvent(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.ShooterNpcId,
            projectile.TargetType,
            projectile.TargetNpcId,
            projectile.Damage,
            hitPosition,
            didHit
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
            $"DidHit: {didHit}, Damage: {projectile.Damage}"
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

        _playerTargetService.ApplyDamage(projectile.Damage);
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

        _runtimeService.ApplyDamage(
            projectile.TargetNpcId,
            projectile.Damage,
            killedByPlayer: projectile.ShooterType == CombatShooterType.Player,
            damagedByPlayer: projectile.ShooterType == CombatShooterType.Player
        );

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

        float distance = Vector3.Distance(
            playerPosition,
            target.CurrentPosition);

        if (distance > weaponStats.Range)
        {
            LogCustom(
                "[SystemNpcCombatService] Player target out of range. " +
                $"Distance: {distance:F2}, Range: {weaponStats.Range:F2}");

            return false;
        }

        if (weaponStats.ShotType == WeaponShotType2A.Beam)
        {
            return TryCreateBeam(
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

        if (IsTimedEnergyProjectile(weaponStats.ShotType))
        {
            return TryCreateTimedEnergyProjectiles(
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

        return TryCreateSingleProjectile(
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
                : 0.8f;

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

        float flightSeconds =
            Mathf.Max(0.01f, slotSeconds - gapSeconds);

        Vector3 pathDirection = targetPosition - startPosition;
        pathDirection.z = 0f;

        Vector3 leftPerpendicular = Vector3.zero;

        if (pathDirection.sqrMagnitude > 0.0001f)
        {
            pathDirection.Normalize();
            leftPerpendicular = new Vector3(-pathDirection.y, pathDirection.x, 0f);
        }

        float rapidLaneOffset =
            weaponStats.ShotType == WeaponShotType2A.RapidEnergyVolley &&
            visualSettings != null
                ? visualSettings.RapidProjectileLaneOffset
                : 0f;

        bool createdAny = false;

        for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
        {
            float startDelaySeconds =
                slotSeconds * shotIndex;

            Vector3 pathOffset =
                ResolveRapidProjectilePathOffset(
                    leftPerpendicular,
                    rapidLaneOffset,
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
                    flightSeconds);

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

            IsResolved = false
        };
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

    private static bool IsTimedEnergyProjectile(
     WeaponShotType2A shotType)
    {
        return shotType == WeaponShotType2A.RapidEnergyVolley ||
               shotType == WeaponShotType2A.HeavyProjectile;
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
            _playerTargetService.ApplyDamage(damage);
            return;
        }

        if (beam.TargetType != CombatTargetType.Npc)
            return;

        _runtimeService.ApplyDamage(
            beam.TargetNpcId,
            damage,
            killedByPlayer: beam.ShooterType == CombatShooterType.Player,
            damagedByPlayer: beam.ShooterType == CombatShooterType.Player);
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
    }

    private void OnPlayerShipDestroyedByNpc(PlayerShipDestroyedByNpcEvent evt)
    {
        CompleteBeamsMatching(beam =>
            beam.ShooterType == CombatShooterType.Player ||
            beam.TargetType == CombatTargetType.Player);
    }

    private void CompleteBeamsForNpc(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        CompleteBeamsMatching(beam =>
            beam.ShooterType == CombatShooterType.Npc &&
            string.Equals(
                beam.ShooterNpcId,
                runtimeNpcId,
                StringComparison.Ordinal));
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
}
