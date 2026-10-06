using System.Linq;

public sealed class SystemSecurityService :
    CustomService,
    ISystemSecurityService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly SimpleEventBus _eventBus;

    public SystemSecurityService()
    {
        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<ISystemNpcRuntimeService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry
            .Get<SimpleEventBus>();
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

        SystemInfrastructureDamageState previousDamageState =
            systemState.InfrastructureDamageState;

        systemState.ApplyInfrastructureDamageFromWar(
            damageAmount,
            forceDestroyed);

        LogCustom(
            "[SystemSecurityService] Infrastructure damage updated. " +
            "System: " + systemId +
            " | DamageState: " + systemState.InfrastructureDamageState +
            " | Damage: " + systemState.InfrastructureDamage);

        PublishInfrastructureWarNews(
            systemId,
            previousDamageState,
            systemState.InfrastructureDamageState);

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

    private void PublishInfrastructureWarNews(
    string systemId,
    SystemInfrastructureDamageState previousState,
    SystemInfrastructureDamageState currentState)
    {
        if (previousState == currentState)
            return;

        if (currentState == SystemInfrastructureDamageState.Damaged)
        {
            _eventBus?.Publish(
                new WarNewsItemCreatedEvent(
                    WarNewsEventKind.InfrastructureDamaged,
                    systemId,
                    string.Empty,
                    "Инфраструктура повреждена",
                    "Война повредила инфраструктуру системы " +
                    systemId +
                    ".",
                    GetCurrentQuantTick(),
                    70));

            return;
        }

        if (currentState == SystemInfrastructureDamageState.Destroyed)
        {
            _eventBus?.Publish(
                new WarNewsItemCreatedEvent(
                    WarNewsEventKind.InfrastructureDestroyed,
                    systemId,
                    string.Empty,
                    "Инфраструктура разрушена",
                    "Инфраструктура системы " +
                    systemId +
                    " разрушена войной.",
                    GetCurrentQuantTick(),
                    85));
        }
    }

    private int GetCurrentQuantTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return 1;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet(
                out IGameTimeService gameTimeService))
        {
            return System.Math.Max(
                1,
                gameTimeService.CurrentQuantTick);
        }

        return 1;
    }
}