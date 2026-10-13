public interface ISystemSecurityService
{
    bool TryGetSystemStatus(
        string systemId,
        out StarSystemStatus systemStatus);

    bool IsSystemSecured(string systemId);

    bool SetSystemStatus(
        string systemId,
        StarSystemStatus newStatus);

    bool CaptureSystem(string systemId);

    bool ApplyInfrastructureDamageFromWar(
    string systemId,
    int damageAmount,
    bool forceDestroyed);

    bool TryGetInfrastructureDamageState(
        string systemId,
        out SystemInfrastructureDamageState damageState);

    bool TrySetSystemStableByNpcAutonomy(string systemId);
    bool LiberateSystemByPlayer(string systemId);


    bool CanSystemBeTargetedByInvasion(string systemId);

    bool MarkRecoveryHookPending(
    string systemId,
    int currentTick,
    string reason);

    bool IsNpcAutonomousLiberationAllowed();
}