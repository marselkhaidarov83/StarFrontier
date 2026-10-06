using System.Collections.Generic;

public interface IEnemyFactionService
{
    IReadOnlyList<EnemyFactionState> GetFactions();

    bool TryGetFaction(
        string factionId,
        out EnemyFactionState factionState);

    EnemyFactionState GetOrCreateFaction(
        string factionId,
        string displayName);

    bool RegisterFaction(
        string factionId,
        string displayName);

    bool FactionOwnsSystem(
        string factionId,
        string systemId);

    bool HasTerritoryAccess(
        string factionId,
        string systemId);

    bool AddOwnedSystem(
        string factionId,
        string systemId);

    bool RemoveOwnedSystem(
        string factionId,
        string systemId);

    bool AddTerritorySystem(
        string factionId,
        string systemId);

    bool RemoveTerritorySystem(
        string factionId,
        string systemId);

    bool RegisterEnemyGroup(
        string factionId,
        string enemyGroupRuntimeId);

    bool UnregisterEnemyGroup(
        string factionId,
        string enemyGroupRuntimeId);

    bool RegisterInvasion(
        string factionId,
        string invasionId);

    bool UnregisterInvasion(
        string factionId,
        string invasionId);

    void RepairFactionState();
}