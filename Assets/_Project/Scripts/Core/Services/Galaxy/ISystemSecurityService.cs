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

    bool TrySetSystemStableByNpcAutonomy(string systemId);
}