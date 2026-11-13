using System;
using System.Collections.Generic;
using UnityEngine;

public class GameSessionService : IGameSessionService
{
    public GameRuntimeState State { get; private set; }

    public bool HasActiveSession => State != null;

    private ISystemTravelService _systemTravelService;

    public GameSessionService()
    {
    }

    public void StartNewSession(GameRuntimeState state)
    {
        State = state;
        InitializeShipLoadouts();
        if (State?.Player != null) State.Player.CurrentStationId = string.Empty;
        InitializeStations(newGame: true);
        InitializeMissionSession();
        ForcePauseOnSessionOpen();
        InitializeSystemTravelService();
        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IStationVisitService>(out var visit)) visit.InitializeSession();
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.NewGame, true);
    }

    public void LoadSession(GameRuntimeState state)
    {
        State = state;
        InitializeShipLoadouts();
        InitializeStations(newGame: false);
        InitializeMissionSession();
        ForcePauseOnSessionOpen();
        InitializeSystemTravelService();
        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IStationVisitService>(out var visit)) visit.InitializeSession();
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.ContinueGame, true);
    }

    private void InitializeShipLoadouts()
    {
        var ships = State?.Player?.PlayerShipState?.OwnedShips;
        var registry = Bootstrapper.Instance?.ServiceRegistry;
        if (ships == null || registry == null || !registry.TryGet<IConfigService>(out var config)) return;
        foreach (var ship in ships)
        {
            if (ship == null) continue;
            ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
            MigrateComponentStacks(ship.Cargo, ship.CargoEquipment, config);
            if (ship.HasModularLoadout)
            {
                MigrateInstalledComponents(ship, config);
                continue;
            }
            var hull = config.GetAllyConfigById(ship.AllyConfigId);
            ShipLoadoutRules.Initialize(ship, hull, config.GetWeaponConfigById);
            if (ship.HasModularLoadout && ship.FuelCapacity <= 0 &&
                config.GetItemConfigById(ShipLoadoutRules.ComponentId(ship, ShipComponentSlot.FuelTank)) is ShipComponentConfig tank)
                ship.FuelCapacity = Mathf.Max(0, tank.FuelCapacity);
        }
        if (State.Markets != null)
            foreach (var market in State.Markets)
            {
                if (market == null) continue;
                market.PersonalEquipment ??= new List<SystemNpcWeaponRuntimeState>();
                MigrateComponentStacks(market.PersonalStorage, market.PersonalEquipment, config);
            }
    }

    private static void MigrateInstalledComponents(ShipRuntimeData ship, IConfigService config)
    {
        if (ship.InstalledComponentIds == null || ship.InstalledComponentIds.Count == 0) return;
        ship.InstalledComponents ??= new List<SystemNpcWeaponRuntimeState>();
        for (int slot = 0; slot < ship.InstalledComponentIds.Count; slot++)
        {
            if (config.GetItemConfigById(ship.InstalledComponentIds[slot]) is not ShipComponentConfig component)
                continue;
            if (ShipLoadoutRules.Component(ship, (ShipComponentSlot)slot) == null)
                ship.InstalledComponents.Add(SystemNpcWeaponRuntimeState.NewComponent(component,
                    "installed-component:" + ship.ShipId + ":" + slot, slot));
        }
        ship.InstalledComponentIds.Clear();
    }

    private static void MigrateComponentStacks(RuntimeCargoInventory cargo, List<SystemNpcWeaponRuntimeState> equipment,
        IConfigService config)
    {
        if (cargo?.entries == null) return;
        for (int i = cargo.entries.Count - 1; i >= 0; i--)
        {
            var entry = cargo.entries[i];
            if (entry == null || entry.Quantity <= 0 || !string.IsNullOrEmpty(entry.MissionRuntimeId) ||
                config.GetItemConfigById(entry.ItemId) is not ShipComponentConfig component) continue;
            for (int j = 0; j < entry.Quantity; j++)
                equipment.Add(SystemNpcWeaponRuntimeState.NewComponent(component, Guid.NewGuid().ToString("N")));
            cargo.entries.RemoveAt(i);
        }
    }

    private void InitializeStations(bool newGame)
    {
        if (Bootstrapper.Instance != null && Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet<IStationService>(out var stations))
            stations.InitializeSession(newGame);
        if (Bootstrapper.Instance != null && Bootstrapper.Instance.ServiceRegistry != null)
        {
            if (Bootstrapper.Instance.ServiceRegistry.TryGet<IStationInfrastructureService>(out var infrastructure)) infrastructure.InitializeSession();
            if (Bootstrapper.Instance.ServiceRegistry.TryGet<IResearchService>(out var research)) research.InitializeSession();
        }
    }

    private void InitializeMissionSession()
    {
        if (State == null) return;
        State.MissionBlock ??= new RuntimeMissionSaveBlock();
        int completedBefore = (State.MissionBlock.CompletedMissions?.Count ?? 0) +
            (State.MissionBlock.ActiveMissions?.FindAll(item => item != null && item.Status == MissionStatus.Completed).Count ?? 0) +
            (State.MissionBlock.AvailableMissions?.FindAll(item => item != null && item.Status == MissionStatus.Completed).Count ?? 0);
        MissionLifecycleRules.Normalize(State.MissionBlock, State.Meta?.CurrentGameDay ?? 1);
        if (completedBefore > 0)
            AppLog.Info(DebugLogChannel.Mission,
                "[MissionLifecycle] Completed records removed on session initialization | Count=" + completedBefore +
                " | RewardIds=" + State.MissionBlock.RewardGrantedMissionIds.Count);
        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IRewardService>(out var rewards))
            rewards.ImportGrantedMissionIds(State.MissionBlock.RewardGrantedMissionIds);
        AppLog.Info(DebugLogChannel.Mission,
            "[MissionLifecycle] Session restored | Active=" + State.MissionBlock.ActiveMissions.Count +
            " | Archived=" + State.MissionBlock.ArchivedMissions.Count +
            " | RewardIds=" + State.MissionBlock.RewardGrantedMissionIds.Count);
    }

    private void InitializeSystemTravelService()
    {
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _systemTravelService.State.SetCurrentPosition(State.Player.SystemMapShipPosition);
    }

    public void ClearSession()
    {
        State = null;
    }

    private void ForcePauseOnSessionOpen()
    {
        if (State?.Meta != null)
            State.Meta.IsGameTimePaused = true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService))
        {
            gameTimeService.SetPaused(true);
        }
    }
}
