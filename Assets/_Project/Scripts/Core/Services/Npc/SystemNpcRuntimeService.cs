using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SystemNpcRuntimeService : CustomService, ISystemNpcRuntimeService
{
    private readonly List<SystemNpcRuntimeState> _npcs = new();
    private readonly SimpleEventBus _eventBus;
    private readonly IDamageService2A _damageService;
    private readonly ISystemEncounterService _encounterService;

    public IReadOnlyList<SystemNpcRuntimeState> Npcs => _npcs;

    public SystemNpcRuntimeService()
    {
        _debugStop = true;
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _damageService = ResolveDamageService();
        _encounterService = ResolveEncounterService();
    }

    public void AddNpc(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        if (_npcs.Any(x => x.RuntimeNpcId == npc.RuntimeNpcId))
            return;

        _npcs.Add(npc);

        _eventBus.Publish(new SystemNpcCreatedEvent(
            npc.RuntimeNpcId,
            npc.NpcType,
            npc.ConfigId,
            npc.CurrentSystemId));

        LogCustom(
            $"[SystemNpcRuntimeService] NPC added. Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}, Config: {npc.ConfigId}, System: {npc.CurrentSystemId}");
    }

    public bool TryGetNpc(string runtimeNpcId, out SystemNpcRuntimeState npc)
    {
        npc = _npcs.FirstOrDefault(x => x.RuntimeNpcId == runtimeNpcId);
        return npc != null;
    }

    public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsInSystem(string systemId)
    {
        return _npcs
            .Where(x => x.IsAlive && x.CurrentSystemId == systemId)
            .ToList();
    }

    public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsByGroupId(string groupId)
    {
        return _npcs
            .Where(x => x.IsAlive && x.GroupRuntimeId == groupId)
            .ToList();
    }

    public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsInSystemByType(
        string systemId,
        SystemNpcType npcType)
    {
        return _npcs
            .Where(x =>
                x.IsAlive &&
                x.CurrentSystemId == systemId &&
                x.NpcType == npcType)
            .ToList();
    }

    public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsBySpawnRule(
        string systemId,
        string spawnRuleId,
        SystemNpcType npcType)
    {
        return _npcs
            .Where(x =>
                x.IsAlive &&
                x.OriginSystemId == systemId &&
                x.NpcType == npcType &&
                x.SpawnRuleId == spawnRuleId)
            .ToList();
    }

    public IReadOnlyList<SystemNpcRuntimeState> GetAliveEnemyGroupsInSystem(
        string systemId)
    {
        return _npcs
            .Where(npc =>
                npc.IsAlive &&
                npc.CurrentSystemId == systemId &&
                npc.NpcType == SystemNpcType.Enemy)
            .GroupBy(npc =>
                string.IsNullOrEmpty(npc.GroupRuntimeId)
                    ? npc.RuntimeNpcId
                    : npc.GroupRuntimeId)
            .Select(group => group.First())
            .ToList();
    }

    public IReadOnlyList<SystemNpcRuntimeState> GetAliveEnemyGroupsByRule(
        string systemId,
        string groupRuleId)
    {
        return _npcs
            .Where(x =>
                x.IsAlive &&
                x.CurrentSystemId == systemId &&
                x.NpcType == SystemNpcType.Enemy &&
                x.SpawnRuleId == groupRuleId)
            .GroupBy(x => x.GroupRuntimeId)
            .Select(g => g.First())
            .ToList();
    }

    public void UpdateNpcPosition(string runtimeNpcId, Vector3 position)
    {
        if (!TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
            return;

        if (!npc.IsAlive)
            return;

        npc.CurrentPosition = position;

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition));
    }

    public CombatDamageResult2A ApplyDamage(
        string runtimeNpcId,
        int damage,
        bool killedByPlayer,
        bool damagedByPlayer)
    {
        if (!TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
            return default;

        if (!npc.IsAlive)
            return default;

        CombatDamageResult2A result = _damageService.ApplyDamage(
            npc.CurrentShield,
            npc.CurrentHull,
            damage);

        if (result.AppliedDamage <= 0)
            return default;

        npc.ApplyDamageResult(
            result.CurrentShield,
            result.CurrentHull);

        if (killedByPlayer || damagedByPlayer)
        {
            npc.WasDamagedByPlayer = true;

            if (npc.IsPirate)
                npc.IsAggressiveToPlayer = true;
        }

        _eventBus.Publish(new SystemNpcDamagedEvent(
            npc.RuntimeNpcId,
            result.AppliedDamage,
            npc.CurrentHull,
            npc.CurrentShield));

        if (!npc.IsAlive)
            DestroyNpc(npc, killedByPlayer);

        return result;
    }

    public void ClearAll()
    {
        _npcs.Clear();
    }

    public void RestoreNpc(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        _npcs.RemoveAll(x => x.RuntimeNpcId == npc.RuntimeNpcId);
        _npcs.Add(npc);

        _eventBus.Publish(new SystemNpcCreatedEvent(
            npc.RuntimeNpcId,
            npc.NpcType,
            npc.ConfigId,
            npc.CurrentSystemId));

        LogCustom(
            $"[SystemNpcRuntimeService] NPC restored. Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}, Config: {npc.ConfigId}, System: {npc.CurrentSystemId}, Alive: {npc.IsAlive}");
    }

    public void RestoreNpcs(IEnumerable<SystemNpcRuntimeState> npcs)
    {
        if (npcs == null)
            return;

        foreach (var npc in npcs)
            RestoreNpc(npc);
    }

    private void DestroyNpc(
    SystemNpcRuntimeState npc,
    bool killedByPlayer)
    {
        npc.WasKilledByPlayer = killedByPlayer;
        npc.DestroyedAtTick = GetCurrentQuantTick();
        npc.NextRespawnTick = ScheduleRespawn(npc);

        if (_encounterService != null)
        {
            if (npc.IsEnemy)
                _encounterService.RegisterEnemyDestroyed(killedByPlayer);
            else if (npc.IsAlly)
                _encounterService.RegisterAllyDestroyed();
        }

        PublishNpcDestroyedEvent(
            npc,
            killedByPlayer);

        LogCustom(
            $"[SystemNpcRuntimeService] NPC destroyed. Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}, KilledByPlayer: {killedByPlayer}");
    }

    private int GetCurrentQuantTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
            return 1;

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService))
            return Mathf.Max(1, gameTimeService.CurrentQuantTick);

        return 1;
    }

    private int ScheduleRespawn(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return 0;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
            return 0;

        if (!Bootstrapper.Instance.ServiceRegistry.TryGet<
                ISystemNpcPopulationService>(
                out ISystemNpcPopulationService populationService))
            return 0;

        int nextRespawnTick = populationService.ScheduleRespawn(
            npc,
            npc.DestroyedAtTick);

        if (nextRespawnTick <= 0 ||
            string.IsNullOrWhiteSpace(npc.GroupRuntimeId))
            return nextRespawnTick;

        for (int i = 0; i < _npcs.Count; i++)
        {
            SystemNpcRuntimeState groupNpc = _npcs[i];

            if (groupNpc == null || groupNpc.IsAlive)
                continue;

            if (groupNpc.GroupRuntimeId != npc.GroupRuntimeId)
                continue;

            groupNpc.NextRespawnTick = nextRespawnTick;
        }

        return nextRespawnTick;
    }

    private static IDamageService2A ResolveDamageService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out IDamageService2A damageService))
            return damageService;

        return new DamageService2A();
    }

    private static ISystemEncounterService ResolveEncounterService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out ISystemEncounterService encounterService))
            return encounterService;

        return null;
    }

    public bool DespawnNpc(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            Debug.LogWarning("[SystemNpcRuntimeService] DespawnNpc failed. RuntimeNpcId is empty.");
            return false;
        }

        if (!TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) || npc == null)
        {
            Debug.LogWarning(
                "[SystemNpcRuntimeService] DespawnNpc failed. NPC not found. RuntimeNpcId: " +
                runtimeNpcId);

            return false;
        }

        _npcs.Remove(npc);

        PublishNpcDestroyedEvent(
            npc,
            false);

        LogCustom(
            $"[SystemNpcRuntimeService] NPC despawned. Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}");

        return true;
    }

    private void PublishNpcDestroyedEvent(
    SystemNpcRuntimeState npc,
    bool killedByPlayer)
    {
        if (npc == null)
            return;

        _eventBus.Publish(new SystemNpcDestroyedEvent(
            npc.GroupRuntimeId,
            npc.RuntimeNpcId,
            npc.NpcType,
            npc.CurrentSystemId,
            killedByPlayer,
            npc.CurrentPosition));
    }

    public bool KillNpc(string runtimeNpcId, bool killedByPlayer)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            Debug.LogWarning("[SystemNpcRuntimeService] KillNpc failed. RuntimeNpcId is empty.");
            return false;
        }

        if (!TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) || npc == null)
        {
            Debug.LogWarning(
                "[SystemNpcRuntimeService] KillNpc failed. NPC not found. RuntimeNpcId: " +
                runtimeNpcId);

            return false;
        }

        if (!npc.IsAlive || npc.LifeState != SystemNpcLifeState.Alive)
        {
            Debug.LogWarning(
                "[SystemNpcRuntimeService] KillNpc failed. NPC is not alive. RuntimeNpcId: " +
                runtimeNpcId +
                ", LifeState: " +
                npc.LifeState);

            return false;
        }

        int lethalDamage =
            Mathf.Max(
                npc.CurrentHull + npc.CurrentShield,
                999999);

        ApplyDamage(
            runtimeNpcId,
            lethalDamage,
            killedByPlayer,
            true);

        return !npc.IsAlive ||
               npc.LifeState == SystemNpcLifeState.Destroyed;
    }

    public bool ResetNpc(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            Debug.LogWarning("[SystemNpcRuntimeService] ResetNpc failed. RuntimeNpcId is empty.");
            return false;
        }

        if (!TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) || npc == null)
        {
            Debug.LogWarning(
                "[SystemNpcRuntimeService] ResetNpc failed. NPC not found. RuntimeNpcId: " +
                runtimeNpcId);

            return false;
        }

        npc.IsAlive = true;
        npc.LifeState = SystemNpcLifeState.Alive;
        npc.DestroyedAtTick = 0;
        npc.NextRespawnTick = 0;

        npc.CurrentHull = npc.MaxHull;
        npc.CurrentShield = npc.MaxShield;
        npc.CurrentEnergy = npc.MaxEnergy;

        npc.WasKilledByPlayer = false;
        npc.WasDamagedByPlayer = false;
        npc.IsFighting = false;
        npc.IsAggressiveToPlayer = false;

        npc.CurrentTargetRuntimeNpcId = null;
        npc.BehaviorTargetRuntimeNpcId = null;

        npc.PrevBehavior = SystemNpcBehaviorType.None;

        npc.CurrentBehavior = npc.IsEnemy
            ? SystemNpcBehaviorType.EngageEnemies
            : SystemNpcBehaviorType.StayOnPlanetForDays;

        npc.CombatState = npc.IsEnemy
            ? SystemNpcCombatState.SearchingTarget
            : SystemNpcCombatState.None;

        npc.TravelState = npc.IsOnPlanet
            ? SystemNpcTravelState.OnPlanet
            : SystemNpcTravelState.Idle;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = npc.CurrentPosition;
        npc.CurrentMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementArrived = true;

        _eventBus.Publish(new SystemNpcCreatedEvent(
            npc.RuntimeNpcId,
            npc.NpcType,
            npc.ConfigId,
            npc.CurrentSystemId));

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition));

        LogCustom(
            $"[SystemNpcRuntimeService] NPC reset. Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}");

        return true;
    }


}