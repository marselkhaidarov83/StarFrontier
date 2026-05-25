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

    void UpdateNpcPosition(string runtimeNpcId, Vector3 position);

    void ApplyDamage(string runtimeNpcId, int damage, bool killedByPlayer, bool damagedByPlayer);

    void ClearAll();

    void RestoreNpc(SystemNpcRuntimeState npc);
    void RestoreNpcs(IEnumerable<SystemNpcRuntimeState> npcs);

}