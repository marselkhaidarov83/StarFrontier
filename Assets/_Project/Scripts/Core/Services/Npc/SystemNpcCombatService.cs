using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcCombatService : CustomService, ISystemNpcCombatService
{
    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly IConfigService _configService;
    private readonly IPlayerCombatTargetService _playerTargetService;
    private readonly SimpleEventBus _eventBus;

    private readonly List<GalaxyNpcProjectileRuntimeState> _activeProjectiles = new();

    public SystemNpcCombatService()
    {
        _debugStop = true;

        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _playerTargetService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerCombatTargetService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _eventBus.Subscribe<GameDayChangedEvent>(OnGameDayChanged);
    }

    public void Tick(StarSystemConfig starSystem, int quantTick)
    {
        if (starSystem == null)
            return;

        // if (_gameTimeService.IsPaused)
        //     return;

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

        for (int i = 0; i < _activeProjectiles.Count; i++)
        {
            GalaxyNpcProjectileRuntimeState projectile = _activeProjectiles[i];

            if (projectile == null || projectile.IsResolved)
                continue;

            TickProjectile(projectile, deltaTime);
        }
    }

    public void ForceAttackOnce(string shooterNpcId, int quantTick)
    {
        if (!_runtimeService.TryGetNpc(shooterNpcId, out SystemNpcRuntimeState shooter))
        {
            Debug.LogWarning($"[SystemNpcCombatService] Shooter not found: {shooterNpcId}");
            return;
        }

        TryAttack(shooter, quantTick, true);
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

        if (npc.Weapons == null || npc.Weapons.Count == 0)
            return false;

        if (npc.CombatState == SystemNpcCombatState.Retreating)
            return false;

        return true;
    }

    private void TryAttack(
        SystemNpcRuntimeState shooter,
        int quantTick,
        bool ignoreCooldown = false)
    {
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
                ignoreCooldown
            );

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
        bool ignoreCooldown)
    {
        WeaponConfig weaponConfig = _configService.GetWeaponConfigById(
            weaponRuntime.WeaponConfigId
        );

        if (weaponConfig == null)
        {
            Debug.LogWarning($"[SystemNpcCombatService] WeaponConfig not found: {weaponRuntime.WeaponConfigId}");
            return false;
        }

        float distance = Vector3.Distance(
            shooter.CurrentPosition,
            target.Position
        );

        if (distance > weaponConfig.Range)
        {
            LogCustom(
                $"[SystemNpcCombatService] Target out of range. " +
                $"Shooter: {shooter.RuntimeNpcId}, TargetType: {target.TargetType}, " +
                $"Distance: {distance:F2}, Range: {weaponConfig.Range:F2}"
            );

            return false;
        }

        if (!ignoreCooldown && !weaponRuntime.CanShootAtTick(quantTick))
            return false;

        int cooldownTicks = GetCooldownTicks(weaponConfig);
        weaponRuntime.MarkShotAtTick(quantTick, cooldownTicks);

        var projectile = new GalaxyNpcProjectileRuntimeState
        {
            ProjectileId = Guid.NewGuid().ToString("N"),

            SystemId = shooter.CurrentSystemId,
            ShooterNpcId = shooter.RuntimeNpcId,

            TargetType = target.TargetType,
            TargetNpcId = target.TargetNpcId,

            WeaponConfigId = weaponRuntime.WeaponConfigId,

            StartPosition = shooter.CurrentPosition,
            CurrentPosition = shooter.CurrentPosition,
            LastKnownTargetPosition = target.Position,

            Damage = weaponConfig.BaseDamage,

            CreatedTick = quantTick,
            ImpactTick = quantTick,

            ElapsedSeconds = 0f,
            LifetimeSeconds = Mathf.Max(0.01f, GameTimeService.SecondsPerDay),

            IsResolved = false
        };

        _activeProjectiles.Add(projectile);

        _eventBus.Publish(new GalaxyNpcProjectileCreatedEvent(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.ShooterNpcId,
            projectile.TargetType,
            projectile.TargetNpcId,
            projectile.WeaponConfigId,
            projectile.StartPosition,
            projectile.LastKnownTargetPosition,
            weaponConfig.ProjectileSpeed
        ));

        LogCustom(
            $"[SystemNpcCombatService] Projectile created. " +
            $"Projectile: {projectile.ProjectileId}, Shooter: {shooter.RuntimeNpcId}, " +
            $"TargetType: {target.TargetType}, Target: {target.TargetNpcId}, Damage: {projectile.Damage}"
        );

        return true;
    }

    private void TickProjectile(
        GalaxyNpcProjectileRuntimeState projectile,
        float deltaTime)
    {
        projectile.ElapsedSeconds += deltaTime;

        Vector3 targetPosition = projectile.LastKnownTargetPosition;

        if (TryGetCurrentProjectileTargetPosition(projectile, out Vector3 currentTargetPosition))
        {
            targetPosition = currentTargetPosition;
            projectile.LastKnownTargetPosition = currentTargetPosition;
        }

        float lifetime = Mathf.Max(0.01f, projectile.LifetimeSeconds);
        float progress01 = Mathf.Clamp01(projectile.ElapsedSeconds / lifetime);

        projectile.CurrentPosition = Vector3.Lerp(
            projectile.StartPosition,
            targetPosition,
            progress01
        );
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

    private int GetCooldownTicks(WeaponConfig weaponConfig)
    {
        float secondsPerTick = Mathf.Max(0.01f, GameTimeService.SecondsPerDay);

        if (weaponConfig.Cooldown > 0f)
            return Mathf.Max(1, Mathf.CeilToInt(weaponConfig.Cooldown / secondsPerTick));

        if (weaponConfig.FireRate > 0f)
            return Mathf.Max(1, Mathf.CeilToInt((1f / weaponConfig.FireRate) / secondsPerTick));

        return 1;
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

            // return FindNearestNpcTarget(shooter, SystemNpcType.Ally);
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
        LogCustom("targetNpcId = " + targetNpcId);
        if (string.IsNullOrWhiteSpace(targetNpcId))
            return false;

        LogCustom("weaponConfigId = " + weaponConfigId);
        if (string.IsNullOrWhiteSpace(weaponConfigId))
            return false;

        if (!_runtimeService.TryGetNpc(targetNpcId, out SystemNpcRuntimeState target))
        {
            LogCustom("target not found = " + targetNpcId);
            return false;
        }

        LogCustom("target.IsAlive = " + target.IsAlive);
        LogCustom("target.IsOnPlanet = " + target.IsOnPlanet);
        if (!target.IsAlive || target.IsOnPlanet)
            return false;

        if (!_playerTargetService.IsPlayerAvailableInSystem(target.CurrentSystemId))
        {
            LogCustom("target.CurrentSystemId = " + target.CurrentSystemId);
            return false;
        }

        WeaponConfig weaponConfig = _configService.GetWeaponConfigById(weaponConfigId);

        if (weaponConfig == null)
        {
            Debug.LogWarning("[SystemNpcCombatService] WeaponConfig not found: " + weaponConfigId);
            return false;
        }

        Vector3 playerPosition = _playerTargetService.GetPlayerPosition();
        LogCustom("playerPosition = " + playerPosition);
        LogCustom("target.CurrentPosition = " + target.CurrentPosition);

        float distance = Vector3.Distance(playerPosition, target.CurrentPosition);
        LogCustom("distance = " + distance);
        LogCustom("weaponConfig.Range = " + weaponConfig.Range);

        if (distance > weaponConfig.Range)
        {
            LogCustom(
                "[SystemNpcCombatService] Player target out of range. " +
                $"Distance: {distance:F2}, Range: {weaponConfig.Range:F2}"
            );

            return false;
        }

        var projectile = new GalaxyNpcProjectileRuntimeState
        {
            ProjectileId = Guid.NewGuid().ToString("N"),

            SystemId = target.CurrentSystemId,

            ShooterType = CombatShooterType.Player,
            ShooterNpcId = string.Empty,

            TargetType = CombatTargetType.Npc,
            TargetNpcId = target.RuntimeNpcId,

            WeaponConfigId = weaponConfigId,

            StartPosition = playerPosition,
            CurrentPosition = playerPosition,
            LastKnownTargetPosition = target.CurrentPosition,

            Damage = weaponConfig.BaseDamage,

            CreatedTick = quantTick,
            ImpactTick = quantTick,

            ElapsedSeconds = 0f,
            LifetimeSeconds = Mathf.Max(0.01f, GameTimeService.SecondsPerDay),

            IsResolved = false
        };

        _activeProjectiles.Add(projectile);

        _eventBus.Publish(new GalaxyNpcProjectileCreatedEvent(
            projectile.ProjectileId,
            projectile.SystemId,
            projectile.ShooterNpcId,
            projectile.TargetType,
            projectile.TargetNpcId,
            projectile.WeaponConfigId,
            projectile.StartPosition,
            projectile.LastKnownTargetPosition,
            weaponConfig.ProjectileSpeed
        ));

        LogCustom(
            "[SystemNpcCombatService] Player projectile created. " +
            $"Target: {targetNpcId}, Weapon: {weaponConfigId}, Damage: {weaponConfig.BaseDamage}"
        );

        return true;
    }
}