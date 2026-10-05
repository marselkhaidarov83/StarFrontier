using System.Collections.Generic;
using UnityEngine;

public sealed class SystemStatusService : ISystemStatusService
{
    private readonly GalaxyRuntimeState galaxyState;
    private readonly Dictionary<string, int> aliveEnemyGroupsBySystem = new();

    public SystemStatusService(GalaxyRuntimeState galaxyState)
    {
        this.galaxyState = galaxyState;
    }

    public StarSystemStatus GetStatus(string systemId)
    {
        return GetSystemState(systemId).SystemStatus;
    }

    public bool IsSystemSecured(string systemId)
    {
        var state = GetSystemState(systemId);
        return state.IsSecured(GetAliveEnemyGroupsCount(systemId));
    }

    public void SetStatus(string systemId, StarSystemStatus newStatus)
    {
        var state = GetSystemState(systemId);
        state.SystemStatus = newStatus;

        if (newStatus == StarSystemStatus.Captured)
        {
            Debug.Log($"[SystemStatus] System captured: {systemId}. Secured=false.");
        }
    }

    public void OnEnemyGroupSpawned(string systemId)
    {
        aliveEnemyGroupsBySystem[systemId] = GetAliveEnemyGroupsCount(systemId) + 1;
    }

    public void OnEnemyGroupDestroyed(string systemId)
    {
        aliveEnemyGroupsBySystem[systemId] =
            Mathf.Max(0, GetAliveEnemyGroupsCount(systemId) - 1);
    }

    private int GetAliveEnemyGroupsCount(string systemId)
    {
        return aliveEnemyGroupsBySystem.TryGetValue(systemId, out var count)
            ? count
            : 0;
    }

    private StarSystemRuntimeState GetSystemState(string systemId)
    {
        var state = galaxyState.Systems.Find(system => system.SystemId == systemId);

        if (state == null)
        {
            throw new System.InvalidOperationException(
                $"[SystemStatus] StarSystemRuntimeState not found: {systemId}");
        }

        return state;
    }
}