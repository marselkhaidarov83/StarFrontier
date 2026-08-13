using UnityEngine;

public sealed class PlayerCombatTargetService : CustomService, IPlayerCombatTargetService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly SimpleEventBus _eventBus;
    private readonly IDamageService2A _damageService;
    private readonly ISystemEncounterService _encounterService;

    public PlayerCombatTargetService()
    {
        _debugStop = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _damageService = ResolveDamageService();
        _encounterService = ResolveEncounterService();
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

    public void ApplyDamage(int damage)
    {
        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
            return;

        CombatDamageResult2A result = _damageService.ApplyDamage(
            activeShip.CurrentShield,
            activeShip.CurrentHull,
            damage);

        if (result.AppliedDamage <= 0)
            return;

        activeShip.CurrentShield = result.CurrentShield;
        activeShip.CurrentHull = result.CurrentHull;

        _eventBus.Publish(new PlayerDamagedByNpcEvent(
            result.AppliedDamage,
            activeShip.CurrentShield,
            activeShip.CurrentHull));

        _eventBus.Publish(new CombatDamageEvent2A(
            "player",
            true,
            result.AppliedDamage,
            activeShip.CurrentShield,
            activeShip.CurrentHull));

        if (result.IsDestroyed)
            RegisterPlayerDestroyed();

        _eventBus.Publish(new SaveNeedEvent());
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
}