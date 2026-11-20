using System.Collections.Generic;

public interface IEnemySpawnService
{
    bool TrySpawnSystemGuardGroup(
        StarSystemConfig starSystem,
        EnemyInvasionGroupSpawnRuleConfig rule,
        out EnemyGroupState enemyGroupState);

    bool TrySpawnInvasionGroup(
        StarSystemConfig ownerSystem,
        StarSystemConfig targetSystem,
        EnemyInvasionGroupSpawnRuleConfig rule,
        string invasionId,
        out EnemyGroupState enemyGroupState);

    bool TrySpawnInvasionGroup(
        StarSystemConfig ownerSystem,
        StarSystemConfig targetSystem,
        EnemyInvasionGroupSpawnRuleConfig rule,
        string invasionId,
        string factionId,
        out EnemyGroupState enemyGroupState);

    bool TryGetEnemyGroupState(
        string runtimeGroupId,
        out EnemyGroupState enemyGroupState);

    IReadOnlyList<EnemyGroupState> GetActiveEnemyGroupsInSystem(
        string systemId);

    void MarkEnemyGroupDestroyed(
        string runtimeGroupId);
}