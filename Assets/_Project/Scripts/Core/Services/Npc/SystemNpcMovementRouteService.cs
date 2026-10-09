using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public sealed class SystemNpcMovementRouteService : CustomService, ISystemNpcMovementRouteService
{
    private sealed class PlanetPositionCacheEntry
    {
        public int Tick;
        public Vector3 Position;
    }

    private const float InvalidRoutePointSqrMagnitude = 0.001f;

    private readonly IConfigService _configService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IGameSessionService _gameSessionService;
    private IPlayerCombatTargetService _playerCombatTargetService;

    private const float KeepDistanceRadius = 100f;
    private const float PlanetKeepDistanceRadius = 200f;

    private readonly Dictionary<string, PlanetPositionCacheEntry> _planetPositionCache =
    new Dictionary<string, PlanetPositionCacheEntry>();

    public SystemNpcMovementRouteService()
    {
        _debugEnabled = false;
        _debugStop = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _playerCombatTargetService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerCombatTargetService>();

        LogCustom("[NPC-ROUTE-DEBUG] Service debug enabled.");
    }

    public Vector3 GetNextTargetPosition(SystemNpcRuntimeState npc)
    {
        return GetNextTargetPosition(npc, null);
    }

    public Vector3 GetNextTargetPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats)
    {
        long startedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        long branchStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        Vector3 result;

        if (npc == null)
        {
            result = Vector3.zero;

            if (stats != null)
            {
                stats.GetNextTargetFallbackCount++;
                stats.GetNextTargetFallbackMs += EndPerfMeasureMs(branchStartedAt);
            }
        }
        else if (TryGetPlanetToPlanetTravelTargetPosition(
                     npc,
                     stats,
                     out Vector3 planetToPlanetTargetPosition))
        {
            result = planetToPlanetTargetPosition;

            if (stats != null)
            {
                stats.GetNextTargetAllyCount++;
                stats.GetNextTargetAllyMs += EndPerfMeasureMs(branchStartedAt);
            }
        }
        else if (npc.IsAlly)
        {
            result = GetAllyTargetPosition(npc, stats);

            if (stats != null)
            {
                stats.GetNextTargetAllyCount++;
                stats.GetNextTargetAllyMs += EndPerfMeasureMs(branchStartedAt);
            }
        }
        else if (npc.IsEnemy)
        {
            result = GetEnemyTargetPosition(npc, stats);

            if (stats != null)
            {
                stats.GetNextTargetEnemyCount++;
                stats.GetNextTargetEnemyMs += EndPerfMeasureMs(branchStartedAt);
            }
        }
        else if (npc.IsPirate)
        {
            result = GetPirateTargetPosition(npc, stats);

            if (stats != null)
            {
                stats.GetNextTargetPirateCount++;
                stats.GetNextTargetPirateMs += EndPerfMeasureMs(branchStartedAt);
            }
        }
        else
        {
            result = GetRandomFallbackPosition(npc, stats);

            if (stats != null)
            {
                stats.GetNextTargetFallbackCount++;
                stats.GetNextTargetFallbackMs += EndPerfMeasureMs(branchStartedAt);
            }
        }

        if (stats != null)
        {
            stats.GetNextTargetCount++;
            stats.GetNextTargetMs += EndPerfMeasureMs(startedAt);
        }

        return result;
    }

    private bool TryGetPlanetToPlanetTravelTargetPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats,
        out Vector3 targetPosition)
    {
        targetPosition =
            Vector3.zero;

        if (npc == null)
            return false;

        if (npc.CurrentBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel)
            return false;

        if (string.IsNullOrWhiteSpace(npc.TargetPlanetId))
            return false;

        long planetLookupStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        Vector3 planetPosition =
            GetPlanetPosition(
                npc.TargetPlanetId,
                stats);

        long validationStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool isInvalidRoutePoint =
            IsInvalidRoutePoint(planetPosition);

        if (stats != null)
        {
            stats.PlanetInvalidPointCheckCount++;
            stats.PlanetInvalidPointCheckMs += EndPerfMeasureMs(validationStartedAt);

            stats.PlanetLookupCount++;
            stats.PlanetLookupMs += EndPerfMeasureMs(planetLookupStartedAt);
        }

        if (isInvalidRoutePoint)
        {
            npc.TargetPlanetId = null;
            return false;
        }

        targetPosition =
            planetPosition;

        return true;
    }

    private Vector3 GetAllyTargetPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats)
    {
        long stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasNpcTarget =
            TryGetNpcTargetPosition(npc, out Vector3 npcTargetPosition);

        if (stats != null)
        {
            stats.CombatNpcCheckCount++;
            stats.CombatNpcCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        if (hasNpcTarget)
            return GetCombatApproachPosition(npc, npcTargetPosition, stats);

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc.TargetSystemId != null)
        {
            long detailStartedAt =
                stats != null
                    ? BeginPerfMeasure()
                    : 0L;

            LogCustom("Ally target system link = " +
                      npc.TargetSystemId + ", " +
                      npc.TargetSystemExitPoint);

            if (stats != null)
            {
                stats.SystemExitLogCount++;
                stats.SystemExitLogMs += EndPerfMeasureMs(detailStartedAt);
            }

            detailStartedAt =
                stats != null
                    ? BeginPerfMeasure()
                    : 0L;

            bool isInvalidSystemPoint =
                IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetSystemExitPoint);

            if (stats != null)
            {
                stats.SystemExitInvalidPointCheckCount++;
                stats.SystemExitInvalidPointCheckMs += EndPerfMeasureMs(detailStartedAt);
            }

            if (stats != null)
            {
                stats.SystemExitCheckCount++;
                stats.SystemExitCheckMs += EndPerfMeasureMs(stepStartedAt);
            }

            if (!isInvalidSystemPoint)
            {
                detailStartedAt =
                    stats != null
                        ? BeginPerfMeasure()
                        : 0L;

                Vector3 targetSystemExitPoint =
                    npc.TargetSystemExitPoint;

                if (stats != null)
                {
                    stats.SystemExitReturnCount++;
                    stats.SystemExitReturnMs += EndPerfMeasureMs(detailStartedAt);
                }

                return targetSystemExitPoint;
            }

            detailStartedAt =
                stats != null
                    ? BeginPerfMeasure()
                    : 0L;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;

            if (stats != null)
            {
                stats.SystemExitResetCount++;
                stats.SystemExitResetMs += EndPerfMeasureMs(detailStartedAt);
            }
        }
        else if (stats != null)
        {
            stats.SystemExitCheckCount++;
            stats.SystemExitCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId))
        {
            Vector3 planetPosition =
                GetPlanetPosition(npc.TargetPlanetId, stats);

            LogCustom("Ally target planet = " + npc.TargetPlanetId + ", " + planetPosition);

            long validationStartedAt =
                stats != null
                    ? BeginPerfMeasure()
                    : 0L;

            bool isInvalidRoutePoint =
                IsInvalidRoutePoint(planetPosition);

            if (stats != null)
            {
                stats.PlanetInvalidPointCheckCount++;
                stats.PlanetInvalidPointCheckMs += EndPerfMeasureMs(validationStartedAt);

                stats.PlanetLookupCount++;
                stats.PlanetLookupMs += EndPerfMeasureMs(stepStartedAt);
            }

            if (!isInvalidRoutePoint)
                return planetPosition;

            npc.TargetPlanetId = null;
        }
        else if (stats != null)
        {
            stats.PlanetLookupCount++;
            stats.PlanetLookupMs += EndPerfMeasureMs(stepStartedAt);
        }

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc.TargetPosition != Vector3.zero &&
            !IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetPosition))
        {
            if (stats != null)
            {
                stats.TargetPositionCheckCount++;
                stats.TargetPositionCheckMs += EndPerfMeasureMs(stepStartedAt);
            }

            return npc.TargetPosition;
        }

        if (stats != null)
        {
            stats.TargetPositionCheckCount++;
            stats.TargetPositionCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        npc.TargetPosition = Vector3.zero;

        return GetRandomFallbackPosition(npc, stats);
    }

    private Vector3 GetPirateTargetPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats)
    {
        long stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasNpcTarget =
            TryGetNpcTargetPosition(npc, out Vector3 npcTargetPosition);

        if (stats != null)
        {
            stats.CombatNpcCheckCount++;
            stats.CombatNpcCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        if (hasNpcTarget)
            return GetCombatApproachPosition(npc, npcTargetPosition, stats);

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc.TargetSystemId != null)
        {
            LogCustom("Pirate target system link = " +
                      npc.TargetSystemId + ", " +
                      npc.TargetSystemExitPoint);

            bool isInvalidSystemPoint =
                IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetSystemExitPoint);

            if (stats != null)
            {
                stats.SystemExitCheckCount++;
                stats.SystemExitCheckMs += EndPerfMeasureMs(stepStartedAt);
            }

            if (!isInvalidSystemPoint)
                return npc.TargetSystemExitPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;
        }
        else if (stats != null)
        {
            stats.SystemExitCheckCount++;
            stats.SystemExitCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId))
        {
            Vector3 planetPosition =
                GetPlanetPosition(npc.TargetPlanetId, stats);

            LogCustom("Pirate target planet = " + npc.TargetPlanetId + ", " + planetPosition);

            long validationStartedAt =
                stats != null
                    ? BeginPerfMeasure()
                    : 0L;

            bool isInvalidRoutePoint =
                IsInvalidRoutePoint(planetPosition);

            if (stats != null)
            {
                stats.PlanetInvalidPointCheckCount++;
                stats.PlanetInvalidPointCheckMs += EndPerfMeasureMs(validationStartedAt);

                stats.PlanetLookupCount++;
                stats.PlanetLookupMs += EndPerfMeasureMs(stepStartedAt);
            }

            if (!isInvalidRoutePoint)
                return planetPosition;

            npc.TargetPlanetId = null;
        }
        else if (stats != null)
        {
            stats.PlanetLookupCount++;
            stats.PlanetLookupMs += EndPerfMeasureMs(stepStartedAt);
        }

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc.TargetPosition != Vector3.zero &&
            !IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetPosition))
        {
            if (stats != null)
            {
                stats.TargetPositionCheckCount++;
                stats.TargetPositionCheckMs += EndPerfMeasureMs(stepStartedAt);
            }

            return npc.TargetPosition;
        }

        if (stats != null)
        {
            stats.TargetPositionCheckCount++;
            stats.TargetPositionCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        npc.TargetPosition = Vector3.zero;

        return GetRandomFallbackPosition(npc, stats);
    }

    private Vector3 GetEnemyTargetPosition(SystemNpcRuntimeState npc)
    {
        return GetEnemyTargetPosition(npc, null);
    }

    private Vector3 GetEnemyTargetPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats)
    {
        long stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasNearestAlly =
            TryFindNearestAllyPosition(npc, out Vector3 allyPosition);

        if (stats != null)
        {
            stats.EnemyNearestAllyCheckCount++;
            stats.EnemyNearestAllyCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        if (hasNearestAlly)
        {
            LogCustom("Enemy target = nearest ally");

            return GetApproachPosition(
                npc.CurrentPosition,
                allyPosition,
                ArrivalSafeDistance(),
                stats);
        }

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasPlayer =
            TryGetPlayerPositionInSameSystem(npc, out Vector3 playerPosition);

        if (stats != null)
        {
            stats.PlayerCheckCount++;
            stats.PlayerCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        if (hasPlayer)
        {
            LogCustom("Enemy target = player");

            return GetApproachPosition(
                npc.CurrentPosition,
                playerPosition,
                ArrivalSafeDistance(),
                stats);
        }

        stepStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasInhabitedPlanet =
            TryFindNearestInhabitedPlanetPosition(npc, out Vector3 planetPosition);

        if (stats != null)
        {
            stats.NearestInhabitedPlanetCheckCount++;
            stats.NearestInhabitedPlanetCheckMs += EndPerfMeasureMs(stepStartedAt);
        }

        if (hasInhabitedPlanet)
        {
            LogCustom("Enemy target = nearest inhabited planet");

            return GetApproachPosition(
                npc.CurrentPosition,
                planetPosition,
                PlanetKeepDistanceRadius,
                stats);
        }

        return GetRandomFallbackPosition(npc, stats);
    }

    private bool TryGetNpcTargetPosition(SystemNpcRuntimeState npc, out Vector3 position)
    {
        position = Vector3.zero;

        if (string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
            return false;

        if (!_npcRuntimeService.TryGetNpc(npc.CurrentTargetRuntimeNpcId, out SystemNpcRuntimeState target))
            return false;

        if (target == null || !target.IsAlive)
            return false;

        if (target.IsOnPlanet)
            return false;

        if (target.CurrentSystemId != npc.CurrentSystemId)
            return false;

        position = target.CurrentPosition;
        return true;
    }

    private bool TryFindNearestAllyPosition(SystemNpcRuntimeState enemy, out Vector3 position)
    {
        position = Vector3.zero;

        var allies = _npcRuntimeService.GetAliveNpcsInSystemByType(
            enemy.CurrentSystemId,
            SystemNpcType.Ally
        );

        SystemNpcRuntimeState best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < allies.Count; i++)
        {
            SystemNpcRuntimeState ally = allies[i];

            if (ally == null)
                continue;

            if (!ally.IsAvailableForCombat())
                continue;

            float distance = Vector3.Distance(enemy.CurrentPosition, ally.CurrentPosition);

            if (distance < bestDistance)
            {
                best = ally;
                bestDistance = distance;
            }
        }

        if (best == null)
            return false;

        enemy.CurrentTargetRuntimeNpcId = best.RuntimeNpcId;
        enemy.CombatState = SystemNpcCombatState.HasTarget;
        enemy.IsFighting = true;

        position = best.CurrentPosition;
        return true;
    }

    private bool TryGetPlayerPositionInSameSystem(SystemNpcRuntimeState enemy, out Vector3 position)
    {
        position = Vector3.zero;

        if (_gameSessionService?.State?.Player == null)
            return false;

        var profile = _gameSessionService.State.Player;

        if (profile.CurrentSystemId != enemy.CurrentSystemId)
            return false;

        if (!_playerCombatTargetService.IsPlayerAvailableInSystem(profile.CurrentSystemId))
            return false;

        position = profile.SystemMapShipPosition;
        position.z = 0f;

        enemy.CurrentTargetRuntimeNpcId = null;
        enemy.CombatState = SystemNpcCombatState.HasTarget;
        enemy.IsFighting = true;

        return true;
    }

    private bool TryFindNearestInhabitedPlanetPosition(SystemNpcRuntimeState npc, out Vector3 position)
    {
        position = Vector3.zero;

        StarSystemConfig starSystem = _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null)
            return false;

        PlanetConfig[] inhabitedPlanets = starSystem.PlanetInhabited();

        if (inhabitedPlanets == null || inhabitedPlanets.Length == 0)
            return false;

        PlanetConfig bestPlanet = inhabitedPlanets[0];
        Vector3 bestPosition = _orbitalMotionService.GetPlanetCurrentPosition(bestPlanet.PlanetOrbit);
        float bestDistance = Vector3.Distance(npc.CurrentPosition, bestPosition);

        for (int i = 1; i < inhabitedPlanets.Length; i++)
        {
            PlanetConfig planet = inhabitedPlanets[i];

            if (planet == null)
                continue;

            Vector3 planetPosition = _orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit);
            float distance = Vector3.Distance(npc.CurrentPosition, planetPosition);

            if (distance < bestDistance)
            {
                bestPlanet = planet;
                bestPosition = planetPosition;
                bestDistance = distance;
            }
        }

        npc.CurrentTargetRuntimeNpcId = null;
        npc.TargetPlanetId = bestPlanet.Id;
        npc.CombatState = SystemNpcCombatState.SearchingTarget;
        npc.IsFighting = false;

        position = bestPosition;
        return true;
    }

    private Vector3 GetPlanetPosition(string planetId)
    {
        return GetPlanetPosition(planetId, null);
    }

    private Vector3 GetPlanetPosition(
        string planetId,
        SystemNpcMovementTargetResolveStats stats)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return Vector3.zero;

        int currentFrame =
            Time.frameCount;

        long cacheStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasCachedPosition =
            _planetPositionCache.TryGetValue(
                planetId,
                out PlanetPositionCacheEntry cacheEntry) &&
            cacheEntry != null &&
            cacheEntry.Tick == currentFrame;

        if (stats != null)
        {
            stats.PlanetCacheCheckCount++;
            stats.PlanetCacheCheckMs += EndPerfMeasureMs(cacheStartedAt);
        }

        if (hasCachedPosition)
        {
            if (stats != null)
                stats.PlanetCacheHitCount++;

            return cacheEntry.Position;
        }

        if (stats != null)
            stats.PlanetCacheMissCount++;

        long configStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        PlanetConfig planet =
            _configService.GetPlanetConfigById(planetId);

        if (stats != null)
        {
            stats.PlanetConfigLookupCount++;
            stats.PlanetConfigLookupMs += EndPerfMeasureMs(configStartedAt);
        }

        if (planet == null)
            return Vector3.zero;

        long orbitalStartedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        Vector3 position =
            _orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit);

        if (stats != null)
        {
            stats.PlanetOrbitalPositionCount++;
            stats.PlanetOrbitalPositionMs += EndPerfMeasureMs(orbitalStartedAt);
        }

        _planetPositionCache[planetId] =
            new PlanetPositionCacheEntry
            {
                Tick = currentFrame,
                Position = position
            };

        return position;
    }

    private Vector3 GetApproachPosition(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float radius)
    {
        return GetApproachPosition(
            currentPosition,
            targetPosition,
            radius,
            null);
    }

    private Vector3 GetApproachPosition(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float radius,
        SystemNpcMovementTargetResolveStats stats)
    {
        long startedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        Vector3 fromTargetToCurrent =
            currentPosition - targetPosition;

        fromTargetToCurrent.z = 0f;

        if (fromTargetToCurrent.sqrMagnitude <= 0.0001f)
            fromTargetToCurrent = Vector3.up;

        float safeRadius =
            Mathf.Max(0f, radius);

        Vector3 approachPosition =
            targetPosition +
            fromTargetToCurrent.normalized * safeRadius;

        approachPosition.z = -2f;

        if (stats != null)
        {
            stats.ApproachPositionCount++;
            stats.ApproachPositionMs += EndPerfMeasureMs(startedAt);
        }

        return approachPosition;
    }

    private Vector3 GetCombatApproachPosition(
        SystemNpcRuntimeState npc,
        Vector3 targetPosition)
    {
        return GetCombatApproachPosition(npc, targetPosition, null);
    }

    private Vector3 GetCombatApproachPosition(
        SystemNpcRuntimeState npc,
        Vector3 targetPosition,
        SystemNpcMovementTargetResolveStats stats)
    {
        long startedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null)
        {
            if (stats != null)
            {
                stats.CombatApproachCount++;
                stats.CombatApproachMs += EndPerfMeasureMs(startedAt);
            }

            return targetPosition;
        }

        Vector3 currentPosition =
            npc.CurrentPosition;

        currentPosition.z = -2f;
        targetPosition.z = -2f;

        float combatRange =
            Mathf.Max(
                ArrivalSafeDistance(),
                GetAllWeaponsCanShootRange(npc));

        float arrivalTolerance =
            GetCombatApproachArrivalTolerance(npc);

        float distanceToTarget =
            Vector3.Distance(
                currentPosition,
                targetPosition);

        if (distanceToTarget <= combatRange + arrivalTolerance)
        {
            if (stats != null)
            {
                stats.CombatApproachCount++;
                stats.CombatApproachMs += EndPerfMeasureMs(startedAt);
            }

            return currentPosition;
        }

        Vector3 result =
            GetApproachPosition(
                currentPosition,
                targetPosition,
                combatRange,
                stats);

        if (stats != null)
        {
            stats.CombatApproachCount++;
            stats.CombatApproachMs += EndPerfMeasureMs(startedAt);
        }

        return result;
    }

    private float GetCombatApproachArrivalTolerance(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return ArrivalSafeDistance();

        float turnRadiusPart =
            Mathf.Max(0f, npc.TurnRadius) * 0.05f;

        return Mathf.Clamp(
            turnRadiusPart,
            ArrivalSafeDistance(),
            25f);
    }

    private float GetAllWeaponsCanShootRange(SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            npc.Weapons == null ||
            npc.Weapons.Count == 0)
        {
            return KeepDistanceRadius;
        }

        float minRange = float.MaxValue;

        for (int i = 0; i < npc.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weapon = npc.Weapons[i];

            if (weapon == null ||
                weapon.ShotDistance <= 0f)
            {
                continue;
            }

            minRange =
                Mathf.Min(
                    minRange,
                    weapon.ShotDistance);
        }

        if (minRange == float.MaxValue)
            return KeepDistanceRadius;

        return Mathf.Max(
            ArrivalSafeDistance(),
            minRange * 0.9f);
    }

    private float ArrivalSafeDistance()
    {
        return 3f;
    }

    private Vector3 GetRandomFallbackPosition(SystemNpcRuntimeState npc)
    {
        return GetRandomFallbackPosition(npc, null);
    }

    private Vector3 GetRandomFallbackPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats)
    {
        long startedAt =
            stats != null
                ? BeginPerfMeasure()
                : 0L;

        Vector2 random =
            Random.insideUnitCircle * 5f;

        Vector3 result =
            npc.CurrentPosition + new Vector3(random.x, random.y, -2f);

        if (stats != null)
        {
            stats.FallbackCount++;
            stats.FallbackMs += EndPerfMeasureMs(startedAt);
        }

        return result;
    }

    private bool IsInvalidRoutePoint(Vector3 point)
    {
        if (!IsFinite(point))
            return true;

        return point.sqrMagnitude <= InvalidRoutePointSqrMagnitude;
    }

    private bool IsInvalidSystemPoint(string systemId, Vector3 point)
    {
        if (IsInvalidRoutePoint(point))
            return true;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem == null || starSystem.Sun == null)
            return false;

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            point.z);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float safeRadius = sunRadius + KeepDistanceRadius;

        return Vector3.Distance(point, sunCenter) <= safeRadius;
    }

    private float GetSunWorldSize(SunConfig sun)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetSunWorldSize(sun);
        }

        return sun != null
            ? sun.VisualSize
            : 0f;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        if (startedAt <= 0L)
            return 0d;

        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency;
    }


}
