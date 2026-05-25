using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SystemNpcRuntimeService : CustomService, ISystemNpcRuntimeService
{
    private readonly List<SystemNpcRuntimeState> _npcs = new();
    private readonly SimpleEventBus _eventBus;

    public IReadOnlyList<SystemNpcRuntimeState> Npcs => _npcs;

    public SystemNpcRuntimeService()
    {
        // _debugStop = true;
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
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
            npc.CurrentSystemId
        ));

        LogCustom(
            $"[SystemNpcRuntimeService] NPC added. " +
            $"Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}, Config: {npc.ConfigId}, System: {npc.CurrentSystemId}");
        LogCustom("_npcs.Count = " + _npcs.Count);
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
            npc.CurrentPosition
        ));
    }

    public void ApplyDamage(string runtimeNpcId, int damage, bool killedByPlayer,
            bool damagedByPlayer)
    {
        if (!TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc))
            return;

        if (!npc.IsAlive)
            return;

        npc.ApplyDamage(damage);

        if (killedByPlayer || damagedByPlayer)
        {
            npc.WasDamagedByPlayer = true;

            if (npc.IsPirate)
                npc.IsAggressiveToPlayer = true;
        }

        _eventBus.Publish(new SystemNpcDamagedEvent(
            npc.RuntimeNpcId,
            damage,
            npc.CurrentHull,
            npc.CurrentShield
        ));

        LogCustom("npc.IsAlive = " + npc.IsAlive);
        if (!npc.IsAlive)
        {
            LogCustom("npc destroyed");
            npc.WasKilledByPlayer = killedByPlayer;

            _eventBus.Publish(new SystemNpcDestroyedEvent(
                npc.GroupRuntimeId,
                npc.RuntimeNpcId,
                npc.NpcType,
                npc.CurrentSystemId,
                killedByPlayer
            ));

            LogCustom(
                $"[SystemNpcRuntimeService] NPC destroyed. " +
                $"Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}, KilledByPlayer: {killedByPlayer}"
            );
        }
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
            npc.CurrentSystemId
        ));

        LogCustom(
            $"[SystemNpcRuntimeService] NPC restored. " +
            $"Id: {npc.RuntimeNpcId}, Type: {npc.NpcType}, Config: {npc.ConfigId}, System: {npc.CurrentSystemId}, Alive: {npc.IsAlive}"
        );
    }

    public void RestoreNpcs(IEnumerable<SystemNpcRuntimeState> npcs)
    {
        if (npcs == null)
            return;

        foreach (var npc in npcs)
            RestoreNpc(npc);
    }
}