using UnityEngine;

public sealed class PlayerCombatTargetService : CustomService, IPlayerCombatTargetService
{
    private const bool PlayerDamageDebugLogEnabled = true;
    private readonly IGameSessionService _gameSessionService;
    private readonly SimpleEventBus _eventBus;
    private readonly IDamageService2A _damageService;
    private readonly ISystemEncounterService _encounterService;
    private readonly IConfigService _configService;

    public PlayerCombatTargetService()
    {
        _debugStop = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _damageService = ResolveDamageService();
        _encounterService = ResolveEncounterService();
        _configService = ResolveConfigService();
    }


    private void LogPlayerDamage(string message)
    {
        if (!PlayerDamageDebugLogEnabled)
            return;

        bool previousDebugEnabled = _debugEnabled;
        bool previousDebugStop = _debugStop;

        _debugEnabled = true;
        _debugStop = false;

        LogCustom("[PlayerDamage] " + message);

        _debugEnabled = previousDebugEnabled;
        _debugStop = previousDebugStop;
    }

    public bool IsPlayerAvailableInSystem(string systemId)
    {
        if (_gameSessionService?.State?.Player == null)
            return false;

        PlayerState profile = _gameSessionService.State.Player;

        if (profile.CurrentSystemId != systemId)
            return false;

        if (profile.IsOnPlanet())
            return false;

        ShipRuntimeData activeShip = GetActiveShip();

        return activeShip != null && activeShip.CurrentHull > 0;
    }

    public Vector3 GetPlayerPosition()
    {
        if (_gameSessionService?.State?.Player == null)
            return Vector3.zero;

        Vector3 position = _gameSessionService.State.Player.SystemMapShipPosition;
        position.z = 0f;

        return position;
    }

    public CombatDamageResult2A ApplyDamage(int damage)
    {
        LogPlayerDamage(
            "REQUEST | " +
            "Damage=" + damage);

        if (IsGodModeEnabled())
        {
            LogPlayerDamage(
                "BLOCKED | God mode enabled | " +
                "RequestedDamage=" + damage);

            LogCustom(
                "[PlayerCombatTargetService] Damage blocked by player god mode. " +
                "RequestedDamage: " +
                damage);

            return default;
        }

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
        {
            LogPlayerDamage(
                "BLOCKED | Active ship is null | " +
                "RequestedDamage=" + damage);

            LogCustom("[PlayerCombatTargetService] Damage ignored: active ship is null.");

            return default;
        }

        if (activeShip.CurrentHull <= 0)
        {
            LogPlayerDamage(
                "BLOCKED | Active ship already destroyed | " +
                "ShipId=" + activeShip.ShipId +
                " | RequestedDamage=" + damage +
                " | CurrentShield=" + activeShip.CurrentShield +
                " | CurrentHull=" + activeShip.CurrentHull);

            LogCustom(
                "[PlayerCombatTargetService] Damage ignored: active ship is already destroyed. " +
                "ShipId: " +
                activeShip.ShipId);

            return default;
        }

        CombatDamageResult2A result = _damageService.ApplyDamage(
            activeShip.CurrentShield,
            activeShip.CurrentHull,
            Mathf.Max(1, damage));

        if (result.AppliedDamage <= 0)
        {
            LogPlayerDamage(
                "BLOCKED | Applied damage is zero | " +
                "ShipId=" + activeShip.ShipId +
                " | RequestedDamage=" + damage +
                " | CurrentShield=" + activeShip.CurrentShield +
                " | CurrentHull=" + activeShip.CurrentHull);

            LogCustom(
                "[PlayerCombatTargetService] Damage ignored: applied damage is zero. " +
                "ShipId: " +
                activeShip.ShipId +
                ", RequestedDamage: " +
                damage);

            return default;
        }

        activeShip.CurrentShield = result.CurrentShield;
        activeShip.CurrentHull = result.CurrentHull;

        LogPlayerDamage(
            "APPLIED | " +
            "ShipId=" + activeShip.ShipId +
            " | RequestedDamage=" + damage +
            " | AppliedDamage=" + result.AppliedDamage +
            " | CurrentShield=" + activeShip.CurrentShield +
            " | CurrentHull=" + activeShip.CurrentHull +
            " | IsDestroyed=" + result.IsDestroyed);

        _eventBus.Publish(new PlayerDamagedByNpcEvent(
            result.AppliedDamage,
            activeShip.CurrentShield,
            activeShip.CurrentHull));

        _eventBus.Publish(new PlayerCombatStatsChangedEvent(
            activeShip.CurrentHull,
            Mathf.Max(0, activeShip.HullCapacity),
            activeShip.CurrentShield,
            GetActiveShipShieldCapacity(activeShip),
            activeShip.CurrentEnergy,
            activeShip.CurrentEnergy));

        _eventBus.Publish(new ShipStatsChangedEvent(
            activeShip.ShipId));

        _eventBus.Publish(new CombatDamageEvent2A(
            "player",
            true,
            result.AppliedDamage,
            activeShip.CurrentShield,
            activeShip.CurrentHull));

        if (result.IsDestroyed)
            RegisterPlayerDestroyed();

        _eventBus.Publish(new SaveNeedEvent());

        LogCustom(
            "[PlayerCombatTargetService] Player damaged. " +
            "ShipId: " +
            activeShip.ShipId +
            ", AppliedDamage: " +
            result.AppliedDamage +
            ", CurrentShield: " +
            activeShip.CurrentShield +
            ", CurrentHull: " +
            activeShip.CurrentHull);

        return result;
    }

    private void RegisterPlayerDestroyed()
    {
        _eventBus.Publish(new PlayerShipDestroyedByNpcEvent());

        if (_gameSessionService?.State?.Player != null)
        {
            _eventBus.Publish(new CombatTargetDestroyedEvent2A(
                string.Empty,
                _gameSessionService.State.Player.CurrentSystemId,
                "player",
                "npc"));
        }

        if (_encounterService != null)
            _encounterService.RegisterPlayerDestroyed();
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService?.State?.Player == null)
            return null;

        return _gameSessionService.State.Player.GetActiveShip();
    }

    private int GetActiveShipShieldCapacity(
        ShipRuntimeData activeShip)
    {
        if (activeShip == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(activeShip.AllyConfigId))
        {
            return Mathf.Max(0, activeShip?.CurrentShield ?? 0);
        }

        AllyConfig activeShipConfig =
            _configService.GetAllyConfigById(
                activeShip.AllyConfigId);

        if (activeShipConfig == null)
            return Mathf.Max(0, activeShip.CurrentShield);

        return Mathf.Max(
            activeShip.CurrentShield,
            activeShipConfig.BaseShieldMax);
    }

    private bool IsGodModeEnabled()
    {
        return _configService != null &&
               _configService.DebugConfig != null &&
               _configService.DebugConfig.enableGodMode;
    }

    private static IDamageService2A ResolveDamageService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out IDamageService2A damageService))
        {
            return damageService;
        }

        return new DamageService2A();
    }

    private static ISystemEncounterService ResolveEncounterService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out ISystemEncounterService encounterService))
        {
            return encounterService;
        }

        return null;
    }

    private static IConfigService ResolveConfigService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out IConfigService configService))
        {
            return configService;
        }

        return null;
    }
}
