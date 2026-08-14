using System.Linq;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public sealed class SystemNpcMovementRouteService : CustomService, ISystemNpcMovementRouteService
{
    private const float InvalidRoutePointSqrMagnitude = 0.001f;

    private readonly IConfigService _configService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IGameSessionService _gameSessionService;
    private IPlayerCombatTargetService _playerCombatTargetService;

    private const float KeepDistanceRadius = 100f;
    private const float PlanetKeepDistanceRadius = 200f;

    public SystemNpcMovementRouteService()
    {
        _debugStop = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _playerCombatTargetService = Bootstrapper.Instance.ServiceRegistry.Get<IPlayerCombatTargetService>();
    }

    public Vector3 GetNextTargetPosition(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return Vector3.zero;

        if (npc.IsAlly)
            return GetAllyTargetPosition(npc);

        if (npc.IsEnemy)
            return GetEnemyTargetPosition(npc);

        if (npc.IsPirate)
            return GetPirateTargetPosition(npc);

        return GetRandomFallbackPosition(npc);
    }

    private Vector3 GetAllyTargetPosition(SystemNpcRuntimeState npc)
    {
        if (TryGetNpcTargetPosition(npc, out Vector3 npcTargetPosition))
            return GetApproachPosition(npc.CurrentPosition, npcTargetPosition, KeepDistanceRadius);

        if (npc.TargetSystemId != null)
        {
            LogCustom("Ally target system link = " +
                      npc.TargetSystemId + ", " +
                      npc.TargetSystemExitPoint);

            if (!IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetSystemExitPoint))
                return npc.TargetSystemExitPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId))
        {
            Vector3 planetPosition = GetPlanetPosition(npc.TargetPlanetId);
            LogCustom("Ally target planet = " + npc.TargetPlanetId + ", " + planetPosition);

            if (!IsInvalidRoutePoint(planetPosition))
                return planetPosition;

            npc.TargetPlanetId = null;
        }

        if (npc.TargetPosition != Vector3.zero &&
            !IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetPosition))
        {
            return npc.TargetPosition;
        }

        npc.TargetPosition = Vector3.zero;

        return GetRandomFallbackPosition(npc);
    }

    private Vector3 GetPirateTargetPosition(SystemNpcRuntimeState npc)
    {
        if (TryGetNpcTargetPosition(npc, out Vector3 npcTargetPosition))
            return GetApproachPosition(npc.CurrentPosition, npcTargetPosition, KeepDistanceRadius);

        if (npc.TargetSystemId != null)
        {
            LogCustom("Ally target system link = " +
                      npc.TargetSystemId + ", " +
                      npc.TargetSystemExitPoint);

            if (!IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetSystemExitPoint))
                return npc.TargetSystemExitPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId))
        {
            Vector3 planetPosition = GetPlanetPosition(npc.TargetPlanetId);
            LogCustom("Ally target planet = " + npc.TargetPlanetId + ", " + planetPosition);

            if (!IsInvalidRoutePoint(planetPosition))
                return planetPosition;

            npc.TargetPlanetId = null;
        }

        if (npc.TargetPosition != Vector3.zero &&
            !IsInvalidSystemPoint(npc.CurrentSystemId, npc.TargetPosition))
        {
            return npc.TargetPosition;
        }

        npc.TargetPosition = Vector3.zero;

        return GetRandomFallbackPosition(npc);
    }

    private Vector3 GetEnemyTargetPosition(SystemNpcRuntimeState npc)
    {
        // 1. Враг сначала сближается с союзником.
        if (TryFindNearestAllyPosition(npc, out Vector3 allyPosition))
        {
            LogCustom("Enemy target = nearest ally");
            return GetApproachPosition(npc.CurrentPosition, allyPosition, KeepDistanceRadius);
        }

        // 2. Если союзников нет, враг сближается с игроком.
        if (TryGetPlayerPositionInSameSystem(npc, out Vector3 playerPosition))
        {
            LogCustom("Enemy target = player");
            return GetApproachPosition(npc.CurrentPosition, playerPosition, KeepDistanceRadius);
        }

        // 3. Если игрока нет, враг летит к ближайшей inhabited planet.
        if (TryFindNearestInhabitedPlanetPosition(npc, out Vector3 planetPosition))
        {
            LogCustom("Enemy target = nearest inhabited planet");
            return GetApproachPosition(npc.CurrentPosition, planetPosition, PlanetKeepDistanceRadius);
        }

        return GetRandomFallbackPosition(npc);
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
        PlanetConfig planet = _configService.GetPlanetConfigById(planetId);

        if (planet == null)
            return Vector3.zero;

        return _orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit);
    }

    private Vector3 GetApproachPosition(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float radius)
    {
        float distance = Vector3.Distance(currentPosition, targetPosition);

        if (distance < radius)
        {
            Vector2 randomOffset = Random.insideUnitCircle * radius;
            return targetPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);
        }

        return targetPosition;
    }

    private Vector3 GetRandomFallbackPosition(SystemNpcRuntimeState npc)
    {
        Vector2 random = Random.insideUnitCircle * 5f;
        return npc.CurrentPosition + new Vector3(random.x, random.y, -2f);
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
}
