using System.Linq;

public sealed class SystemSecurityService :
    CustomService,
    ISystemSecurityService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;

    public SystemSecurityService()
    {
        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<ISystemNpcRuntimeService>();
    }

    public bool TryGetSystemStatus(
        string systemId,
        out StarSystemStatus systemStatus)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            systemStatus = StarSystemStatus.Stable;
            return false;
        }

        systemStatus = systemState.SystemStatus;
        return true;
    }

    public bool IsSystemSecured(string systemId)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
            return false;

        int aliveEnemyGroupsCount =
            _npcRuntimeService
                .GetAliveEnemyGroupsInSystem(systemId)
                .Count;

        return systemState.IsSecured(aliveEnemyGroupsCount);
    }

    public bool SetSystemStatus(
        string systemId,
        StarSystemStatus newStatus)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            LogCustom(
                "[SystemSecurityService] System state not found: " +
                systemId);

            return false;
        }

        systemState.SetSystemStatus(newStatus);

        LogCustom(
            "[SystemSecurityService] Status changed. " +
            "System: " + systemId +
            " | Status: " + newStatus +
            " | IsSecured: " + IsSystemSecured(systemId));

        return true;
    }

    public bool CaptureSystem(string systemId)
    {
        return SetSystemStatus(
            systemId,
            StarSystemStatus.Captured);
    }

    private StarSystemRuntimeState FindSystemState(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return null;

        GameRuntimeState gameState =
            _gameSessionService.State;

        if (gameState == null ||
            gameState.Galaxy == null ||
            gameState.Galaxy.Systems == null)
        {
            return null;
        }

        return gameState.Galaxy.Systems.FirstOrDefault(
            system => system != null &&
                      system.SystemId == systemId);
    }
}