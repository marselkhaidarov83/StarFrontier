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
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            LogCustom(
                "[SystemSecurityService] Capture failed. System state not found: " +
                systemId);

            return false;
        }

        systemState.MarkCaptured();

        LogCustom(
            "[SystemSecurityService] System captured. " +
            "System: " + systemId +
            " | Status: " + systemState.SystemStatus +
            " | Stability: " + systemState.Stability +
            " | DevelopmentLevel: " + systemState.DevelopmentLevel +
            " | IsSecured: " + IsSystemSecured(systemId));

        return true;
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

    public bool TrySetSystemStableByNpcAutonomy(string systemId)
    {
        LogCustom(
            "[SystemSecurityService] NPC autonomous liberation is blocked " +
            "until owner decision C-05. System: " + systemId);

        return false;
    }

    public bool IsNpcAutonomousLiberationAllowed()
    {
        return false;
    }

    public bool LiberateSystemByPlayer(string systemId)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            LogCustom(
                "[SystemSecurityService] Player liberation failed. System state not found: " +
                systemId);

            return false;
        }

        systemState.MarkLiberatedByPlayer();

        LogCustom(
            "[SystemSecurityService] System liberated by player. " +
            "System: " + systemId +
            " | Status: " + systemState.SystemStatus +
            " | Stability: " + systemState.Stability +
            " | DevelopmentLevel: " + systemState.DevelopmentLevel +
            " | IsSecured: " + IsSystemSecured(systemId));

        return true;
    }

    public bool CanSystemBeTargetedByInvasion(string systemId)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
            return false;

        int aliveEnemyGroupsCount =
            _npcRuntimeService
                .GetAliveEnemyGroupsInSystem(systemId)
                .Count;

        if (!systemState.CanBeTargetedByInvasion())
            return false;

        if (systemState.SystemStatus == StarSystemStatus.Stable &&
            aliveEnemyGroupsCount > 0)
        {
            return false;
        }

        return true;
    }

    public bool ApplyInfrastructureDamageFromWar(
    string systemId,
    int damageAmount,
    bool forceDestroyed)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            LogCustom(
                "[SystemSecurityService] Infrastructure damage failed. System state not found: " +
                systemId);

            return false;
        }

        systemState.ApplyInfrastructureDamageFromWar(
            damageAmount,
            forceDestroyed);

        LogCustom(
            "[SystemSecurityService] Infrastructure damage updated. " +
            "System: " + systemId +
            " | DamageState: " + systemState.InfrastructureDamageState +
            " | Damage: " + systemState.InfrastructureDamage);

        return true;
    }

    public bool TryGetInfrastructureDamageState(
        string systemId,
        out SystemInfrastructureDamageState damageState)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            damageState =
                SystemInfrastructureDamageState.Intact;

            return false;
        }

        damageState =
            systemState.InfrastructureDamageState;

        return true;
    }

    public bool MarkRecoveryHookPending(
    string systemId,
    int currentTick,
    string reason)
    {
        StarSystemRuntimeState systemState =
            FindSystemState(systemId);

        if (systemState == null)
        {
            LogCustom(
                "[SystemSecurityService] Recovery hook failed. System state not found: " +
                systemId);

            return false;
        }

        systemState.MarkRecoveryHookPending(
            currentTick,
            reason);

        LogCustom(
            "[SystemSecurityService] Recovery hook pending. " +
            "System: " + systemId +
            " | CreatedAtTick: " + systemState.RecoveryHookCreatedAtTick +
            " | Reason: " + systemState.RecoveryHookReason);

        return true;
    }
}