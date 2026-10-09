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
            LogSystemStatusDebug(
                "SYSTEM_STATUS_SET_FAILED" +
                " | Reason=SystemStateNotFound" +
                " | SystemId=" + (systemId ?? string.Empty) +
                " | RequestedStatus=" + newStatus);

            LogCustom(
                "[SystemSecurityService] System state not found: " +
                systemId);

            return false;
        }

        StarSystemStatus previousStatus =
            systemState.SystemStatus;

        systemState.SetSystemStatus(newStatus);

        LogSystemStatusDebug(
            "SYSTEM_STATUS_CHANGED" +
            " | Source=SetSystemStatus" +
            " | SystemId=" + systemId +
            " | PreviousStatus=" + previousStatus +
            " | NewStatus=" + systemState.SystemStatus +
            " | RequestedStatus=" + newStatus +
            " | IsSecured=" + IsSystemSecured(systemId));

        LogCustom(
            "[SystemSecurityService] Status changed. " +
            "System: " + systemId +
            " | PreviousStatus: " + previousStatus +
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
            LogSystemStatusDebug(
                "SYSTEM_STATUS_CAPTURE_FAILED" +
                " | Reason=SystemStateNotFound" +
                " | SystemId=" + (systemId ?? string.Empty));

            LogCustom(
                "[SystemSecurityService] Capture failed. System state not found: " +
                systemId);

            return false;
        }

        StarSystemStatus previousStatus =
            systemState.SystemStatus;

        systemState.MarkCaptured();

        LogSystemStatusDebug(
            "SYSTEM_STATUS_CHANGED" +
            " | Source=CaptureSystem" +
            " | SystemId=" + systemId +
            " | PreviousStatus=" + previousStatus +
            " | NewStatus=" + systemState.SystemStatus +
            " | Stability=" + systemState.Stability +
            " | DevelopmentLevel=" + systemState.DevelopmentLevel +
            " | InfrastructureDamageState=" + systemState.InfrastructureDamageState +
            " | InfrastructureDamage=" + systemState.InfrastructureDamage +
            " | IsSecured=" + IsSystemSecured(systemId));

        LogCustom(
            "[SystemSecurityService] System captured. " +
            "System: " + systemId +
            " | PreviousStatus: " + previousStatus +
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
            LogSystemStatusDebug(
                "SYSTEM_STATUS_LIBERATION_FAILED" +
                " | Reason=SystemStateNotFound" +
                " | SystemId=" + (systemId ?? string.Empty));

            LogCustom(
                "[SystemSecurityService] Player liberation failed. System state not found: " +
                systemId);

            return false;
        }

        StarSystemStatus previousStatus =
            systemState.SystemStatus;

        systemState.MarkLiberatedByPlayer();

        LogSystemStatusDebug(
            "SYSTEM_STATUS_CHANGED" +
            " | Source=LiberateSystemByPlayer" +
            " | SystemId=" + systemId +
            " | PreviousStatus=" + previousStatus +
            " | NewStatus=" + systemState.SystemStatus +
            " | Stability=" + systemState.Stability +
            " | DevelopmentLevel=" + systemState.DevelopmentLevel +
            " | IsSecured=" + IsSystemSecured(systemId));

        LogCustom(
            "[SystemSecurityService] System liberated by player. " +
            "System: " + systemId +
            " | PreviousStatus: " + previousStatus +
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
            LogSystemStatusDebug(
                "RECOVERY_HOOK_PENDING_FAILED" +
                " | Reason=SystemStateNotFound" +
                " | SystemId=" + (systemId ?? string.Empty) +
                " | Tick=" + currentTick +
                " | HookReason=" + (reason ?? string.Empty));

            LogCustom(
                "[SystemSecurityService] Recovery hook failed. System state not found: " +
                systemId);

            return false;
        }

        bool hadPendingHook =
            systemState.HasPendingRecoveryHook;

        string previousHookReason =
            systemState.RecoveryHookReason;

        int previousHookTick =
            systemState.RecoveryHookCreatedAtTick;

        systemState.MarkRecoveryHookPending(
            currentTick,
            reason);

        LogSystemStatusDebug(
            "RECOVERY_HOOK_PENDING_SET" +
            " | SystemId=" + systemId +
            " | SystemStatus=" + systemState.SystemStatus +
            " | HadPendingHook=" + hadPendingHook +
            " | PreviousHookTick=" + previousHookTick +
            " | PreviousHookReason=" + previousHookReason +
            " | NewHookTick=" + systemState.RecoveryHookCreatedAtTick +
            " | NewHookReason=" + systemState.RecoveryHookReason);

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

    private void LogSystemStatusDebug(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        if (!Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.Combat))
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.Combat,
            "[SystemSecurityService][SystemStatus] " + message);
    }
}