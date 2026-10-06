using System.Collections.Generic;

public interface IEnemySpawnService
{
    bool TrySpawnSystemGuardGroup(
        StarSystemConfig starSystem,
        EnemyGroupSpawnRuleConfig rule,
        out EnemyGroupState enemyGroupState);

    bool TrySpawnInvasionGroup(
        StarSystemConfig ownerSystem,
        StarSystemConfig targetSystem,
        EnemyGroupSpawnRuleConfig rule,
        string invasionId,
        out EnemyGroupState enemyGroupState);

    bool TryGetEnemyGroupState(
        string runtimeGroupId,
        out EnemyGroupState enemyGroupState);

    IReadOnlyList<EnemyGroupState> GetActiveEnemyGroupsInSystem(
        string systemId);

    void MarkEnemyGroupDestroyed(
        string runtimeGroupId);
}