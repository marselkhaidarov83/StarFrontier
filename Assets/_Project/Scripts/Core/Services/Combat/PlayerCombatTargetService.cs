using UnityEngine;

public sealed class PlayerCombatTargetService : CustomService, IPlayerCombatTargetService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly SimpleEventBus _eventBus;
    private readonly ISaveService _saveService;

    public PlayerCombatTargetService()
    {
        _debugStop = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
    }

    public bool IsPlayerAvailableInSystem(string systemId)
    {
        if (_gameSessionService?.CurrentSave?.PlayerProfile == null)
            return false;

        PlayerProfileData profile = _gameSessionService.CurrentSave.PlayerProfile;

        if (profile.CurrentSystemId != systemId)
            return false;

        // Если игрок сидит на планете, его нельзя атаковать в космосе.
        if (profile.IsOnPlanet())
            return false;

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
            return false;

        if (activeShip.CurrentHull <= 0)
            return false;

        return true;
    }

    public Vector3 GetPlayerPosition()
    {
        if (_gameSessionService?.CurrentSave?.PlayerProfile == null)
            return Vector3.zero;

        Vector3 position = _gameSessionService.CurrentSave.PlayerProfile.SystemMapShipPosition;
        position.z = 0f;

        return position;
    }

    public void ApplyDamage(int damage)
    {
        if (damage <= 0)
            return;

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
        {
            Debug.LogWarning("[PlayerCombatTargetService] Cannot apply damage: active ship is null.");
            return;
        }

        if (activeShip.CurrentHull <= 0)
        {
            Debug.Log("[PlayerCombatTargetService] Damage ignored: player ship already destroyed.");
            return;
        }

        int initialShield = activeShip.CurrentShield;
        int initialHull = activeShip.CurrentHull;

        int remainingDamage = damage;

        if (activeShip.CurrentShield > 0)
        {
            int shieldDamage = Mathf.Min(activeShip.CurrentShield, remainingDamage);

            activeShip.CurrentShield -= shieldDamage;
            remainingDamage -= shieldDamage;
        }

        if (remainingDamage > 0)
        {
            activeShip.CurrentHull -= remainingDamage;

            if (activeShip.CurrentHull < 0)
                activeShip.CurrentHull = 0;
        }

        Debug.Log(
            "[PlayerCombatTargetService] Player damaged. " +
            $"Damage: {damage}, " +
            $"Shield: {initialShield} -> {activeShip.CurrentShield}, " +
            $"Hull: {initialHull} -> {activeShip.CurrentHull}"
        );

        _eventBus.Publish(new PlayerDamagedByNpcEvent(
            damage,
            activeShip.CurrentShield,
            activeShip.CurrentHull
        ));

        if (activeShip.CurrentHull <= 0)
        {
            _eventBus.Publish(new PlayerShipDestroyedByNpcEvent());

            Debug.Log("[PlayerCombatTargetService] Player ship destroyed by NPC.");
        }

        _saveService.Save();
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService?.CurrentSave?.PlayerProfile == null)
            return null;

        return _gameSessionService.CurrentSave.PlayerProfile.GetActiveShip();
    }
}