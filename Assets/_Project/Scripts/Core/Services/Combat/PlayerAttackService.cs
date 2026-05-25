using System.Collections.Generic;

public sealed class PlayerAttackService : CustomService, IPlayerAttackService
{
    private readonly IGameSessionService _gameSessionService;
    // private readonly IGameTimeService _gameTimeService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly ISystemNpcCombatService _combatService;
    private readonly SimpleEventBus _eventBus;

    private readonly Dictionary<string, int> _lastShotTickByWeapon = new();

    private int _prevTick = -1;

    public string CurrentTargetNpcId { get; private set; }

    public PlayerAttackService()
    {
        // _debugStop = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _combatService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcCombatService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _eventBus.Subscribe<GameTickStartedEvent>(OnGameTickStarted);
    }

    private void OnGameTickStarted(GameTickStartedEvent evt)
    {
        Tick(evt.CurrentTick);
    }

    public void SetTarget(string targetNpcId)
    {
        if (string.IsNullOrWhiteSpace(targetNpcId))
            return;

        if (!_npcRuntimeService.TryGetNpc(targetNpcId, out SystemNpcRuntimeState npc))
            return;

        if (!npc.IsAlive || !npc.IsHostileToPlayer)
            return;

        CurrentTargetNpcId = targetNpcId;

        LogCustom("[PlayerAttackService] Target selected: " + targetNpcId);
    }

    public void ClearTarget()
    {
        CurrentTargetNpcId = null;
    }

    public void Tick(int quantTick)
    {
        if (_prevTick == quantTick)
            return;

        _prevTick = quantTick;

        if (string.IsNullOrWhiteSpace(CurrentTargetNpcId))
            return;

        if (!_npcRuntimeService.TryGetNpc(CurrentTargetNpcId, out SystemNpcRuntimeState target))
        {
            ClearTarget();
            return;
        }

        if (!target.IsAlive || !target.IsHostileToPlayer || target.IsOnPlanet)
        {
            ClearTarget();
            return;
        }

        ShipRuntimeData activeShip =
            _gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip();

        if (activeShip == null)
            return;

        if (activeShip.CurrentHull <= 0)
            return;

        if (activeShip.EquippedWeaponIds == null || activeShip.EquippedWeaponIds.Count == 0)
            return;

        for (int i = 0; i < activeShip.EquippedWeaponIds.Count; i++)
        {
            string weaponConfigId = activeShip.EquippedWeaponIds[i];

            if (string.IsNullOrWhiteSpace(weaponConfigId))
                continue;

            if (_lastShotTickByWeapon.TryGetValue(weaponConfigId, out int lastTick))
            {
                if (lastTick == quantTick)
                    continue;
            }

            bool fired = _combatService.TryCreatePlayerProjectile(
                CurrentTargetNpcId,
                weaponConfigId,
                quantTick
            );

            LogCustom("fired = " + fired + ", CurrentTargetNpcId = " + CurrentTargetNpcId +
                    ", weaponConfigId = " + weaponConfigId + ", quantTick = " + quantTick);

            if (fired)
                _lastShotTickByWeapon[weaponConfigId] = quantTick;
        }
    }
}