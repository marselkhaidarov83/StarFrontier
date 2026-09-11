using System.Collections.Generic;
using UnityEngine;

public interface ISystemNpcRuntimeService
{
    IReadOnlyList<SystemNpcRuntimeState> Npcs { get; }

    void AddNpc(SystemNpcRuntimeState npc);

    bool TryGetNpc(string runtimeNpcId, out SystemNpcRuntimeState npc);

    IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsInSystem(string systemId);
    IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsByGroupId(string groupId);

    IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsInSystemByType(
        string systemId,
        SystemNpcType npcType);

    IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsBySpawnRule(
        string systemId,
        string spawnRuleId,
        SystemNpcType npcType);

    IReadOnlyList<SystemNpcRuntimeState> GetAliveEnemyGroupsByRule(
        string systemId,
        string groupRuleId);

    IReadOnlyList<SystemNpcRuntimeState> GetAliveEnemyGroupsInSystem(
        string systemId);

    void UpdateNpcPosition(string runtimeNpcId, Vector3 position);

    CombatDamageResult2A ApplyDamage(string runtimeNpcId, int damage, bool killedByPlayer, bool damagedByPlayer);

    bool DespawnNpc(string runtimeNpcId);

    bool KillNpc(string runtimeNpcId, bool killedByPlayer);

    bool ResetNpc(string runtimeNpcId);

    void ClearAll();

    void RestoreNpc(SystemNpcRuntimeState npc);
    void RestoreNpcs(IEnumerable<SystemNpcRuntimeState> npcs);

}