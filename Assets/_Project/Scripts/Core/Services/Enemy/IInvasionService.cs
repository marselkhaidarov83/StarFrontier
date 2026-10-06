using System.Collections.Generic;

public interface IInvasionService
{
    IReadOnlyList<InvasionState> GetActiveInvasions();

    bool TryGetInvasion(
        string invasionId,
        out InvasionState invasionState);

    bool HasActiveInvasionForTarget(
        string targetSystemId);

    bool TryStartInvasion(
        StarSystemConfig sourceSystem,
        StarSystemConfig targetSystem,
        EnemyGroupSpawnRuleConfig invasionSpawnRule,
        string factionId,
        int resolveAfterTicks,
        out InvasionState invasionState);

    void UpdateInvasions();

    bool ResolveInvasion(
        string invasionId,
        bool capturedByEnemy);

    bool CancelInvasion(
        string invasionId);

    bool CleanupInvasion(
        string invasionId);
}