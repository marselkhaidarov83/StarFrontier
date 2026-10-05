public interface ISystemStatusService
{
    StarSystemStatus GetStatus(string systemId);
    bool IsSystemSecured(string systemId);
    void SetStatus(string systemId, StarSystemStatus newStatus);
    void OnEnemyGroupSpawned(string systemId);
    void OnEnemyGroupDestroyed(string systemId);
}