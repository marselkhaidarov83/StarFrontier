using UnityEngine;

public sealed class PlayerCombatTargetService : CustomService, IPlayerCombatTargetService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly SimpleEventBus _eventBus;
    private readonly IDamageService2A _damageService;

    public PlayerCombatTargetService()
    {
        _debugStop = true;
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _damageService = Bootstrapper.Instance.ServiceRegistry.Get<IDamageService2A>();
    }

    public bool IsPlayerAvailableInSystem(string systemId)
    {
        if (_gameSessionService?.State?.Player == null) return false;
        PlayerState profile = _gameSessionService.State.Player;
        if (profile.CurrentSystemId != systemId) return false;
        if (profile.IsOnPlanet()) return false;

        ShipRuntimeData activeShip = GetActiveShip();
        return activeShip != null && activeShip.CurrentHull > 0;
    }

    public Vector3 GetPlayerPosition()
    {
        if (_gameSessionService?.State?.Player == null) return Vector3.zero;
        Vector3 position = _gameSessionService.State.Player.SystemMapShipPosition;
        position.z = 0f;
        return position;
    }

    public void ApplyDamage(int damage)
    {
        ShipRuntimeData activeShip = GetActiveShip();
        if (activeShip == null) return;

        CombatDamageResult2A result = _damageService.ApplyDamage(
            activeShip.CurrentShield,
            activeShip.CurrentHull,
            damage);

        if (result.AppliedDamage <= 0) return;

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
        {
            _eventBus.Publish(new PlayerShipDestroyedByNpcEvent());
            _eventBus.Publish(new CombatTargetDestroyedEvent2A(
                string.Empty,
                _gameSessionService.State.Player.CurrentSystemId,
                "player",
                "npc"));
        }

        _eventBus.Publish(new SaveNeedEvent());
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService?.State?.Player == null) return null;
        return _gameSessionService.State.Player.GetActiveShip();
    }
}