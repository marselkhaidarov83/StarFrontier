using System;
using System.Collections.Generic;
using System.Linq;

public class MissionService : CustomService, IMissionService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly ISaveService _saveService;
    private readonly IRewardService _rewardService;
    private readonly SimpleEventBus _eventBus;
    private readonly IInventoryService _inventory;
    private readonly IConfigService _config;
    private readonly IRouteService _routes;
    private readonly ITravelService _travel;
    private readonly ISystemNpcRuntimeService _npcs;
    private readonly IPlayerCombatTargetService _playerTarget;

    public MissionService()
    {
        var registry = Bootstrapper.Instance.ServiceRegistry;
        _gameSessionService = registry.Get<IGameSessionService>();
        _saveService = registry.Get<ISaveService>();
        _rewardService = registry.Get<IRewardService>();
        _eventBus = registry.Get<SimpleEventBus>();
        _inventory = registry.Get<IInventoryService>();
        _config = registry.Get<IConfigService>();
        _routes = registry.Get<IRouteService>();
        _travel = registry.Get<ITravelService>();
        _npcs = registry.Get<ISystemNpcRuntimeService>();
        _playerTarget = registry.Get<IPlayerCombatTargetService>();
    }

    private RuntimeMissionSaveBlock Block
    {
        get
        {
            var state = _gameSessionService.State;
            if (state == null)
                return null;
            state.MissionBlock ??= new RuntimeMissionSaveBlock();
            MissionLifecycleRules.EnsureCollections(state.MissionBlock);
            return state.MissionBlock;
        }
    }

    private int CurrentDay
    {
        get
        {
            if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(out var time))
                return Math.Max(1, time.CurrentQuantTick);
            return Math.Max(1, _gameSessionService.State?.Meta?.CurrentGameDay ?? 1);
        }
    }

    public IReadOnlyList<MissionInstanceData> GetAvailableMissions() =>
        Block?.AvailableMissions ?? (IReadOnlyList<MissionInstanceData>)Array.Empty<MissionInstanceData>();
    public IReadOnlyList<MissionInstanceData> GetActiveMissions() =>
        Block?.ActiveMissions ?? (IReadOnlyList<MissionInstanceData>)Array.Empty<MissionInstanceData>();
    public IReadOnlyList<MissionInstanceData> GetCompletedMissions() =>
        Block?.CompletedMissions ?? (IReadOnlyList<MissionInstanceData>)Array.Empty<MissionInstanceData>();
    public IReadOnlyList<MissionInstanceData> GetArchivedMissions() =>
        Block?.ArchivedMissions ?? (IReadOnlyList<MissionInstanceData>)Array.Empty<MissionInstanceData>();

    public void SetAvailableMissions(List<MissionInstanceData> missions)
    {
        var block = Block;
        if (block == null)
            return;
        // Materialize first: callers may pass the current available collection.
        var candidates = missions?.ToArray() ?? Array.Empty<MissionInstanceData>();
        Mutate("SetAvailable", () =>
        {
            block.AvailableMissions.Clear();
            foreach (var mission in candidates)
            {
                if (mission == null || string.IsNullOrWhiteSpace(mission.MissionRuntimeId) ||
                    !MissionLifecycleRules.IsPlayableType(mission.MissionType) ||
                    mission.Status != MissionStatus.Available || GetMissionById(mission.MissionRuntimeId) != null ||
                    block.RewardGrantedMissionIds.Contains(mission.MissionRuntimeId))
                    continue;
                mission.IsReadyToTurnIn = false;
                block.AvailableMissions.Add(mission);
            }
            Log("AvailableUpdated", null, "Count=" + block.AvailableMissions.Count);
        });
    }

    public bool AcceptMission(string missionRuntimeId)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.MissionAccept, missionRuntimeId))
        {
            bool result = AcceptMissionCore(missionRuntimeId, out string reason);
            observation.Complete(result, reason);
            return result;
        }
    }

    private bool AcceptMissionCore(string id, out string reason)
    {
        var block = Block;
        var mission = Find(block?.AvailableMissions, id);
        reason = ValidateAcceptance(mission);
        if (reason != "None")
            return Reject("Accept", mission, reason);

        int day = CurrentDay;
        int expires = mission.DurationDays > 0
            ? (int)Math.Min(int.MaxValue, (long)day + mission.DurationDays)
            : mission.ExpiresAtDay;
        var oldStatus = mission.Status;
        bool oldCargo = mission.Objective.IsMissionCargoGranted;
        bool oldDelivered = mission.Objective.IsMissionCargoDelivered;
        bool oldReady = mission.IsReadyToTurnIn;
        int oldAccepted = mission.AcceptedAtDay;
        int oldExpires = mission.ExpiresAtDay;
        string oldReason = mission.LastTransitionReason;
        int index = block.AvailableMissions.IndexOf(mission);
        try
        {
            Mutate("Accept", () =>
            {
                // Validate before changing either collection. No NPC spawning is performed.
                block.ActiveMissions.Add(mission);
                block.AvailableMissions.Remove(mission);
                mission.Status = MissionStatus.Accepted;
                mission.IsReadyToTurnIn = false;
                mission.AcceptedAtDay = day;
                mission.ExpiresAtDay = expires;
                mission.LastTransitionReason = "Accepted";
                if (mission.MissionType == MissionType.Delivery)
                {
                    if (!MissionCargoOperations.Grant(_gameSessionService.State, mission, _inventory))
                        throw new InvalidOperationException("Mission cargo does not fit or is already granted.");
                    mission.Objective.IsMissionCargoGranted = true;
                    Log("CargoGranted", mission, "Item=" + mission.Objective.MissionCargoItemId +
                        " | Quantity=" + mission.Objective.MissionCargoAmount + " | Volume=" + mission.Objective.MissionCargoVolume);
                    mission.Objective.IsMissionCargoDelivered = false;
                }
            });
        }
        catch (Exception exception)
        {
            MissionCargoOperations.Remove(_gameSessionService.State, mission);
            block.ActiveMissions.Remove(mission);
            if (!block.AvailableMissions.Contains(mission))
                block.AvailableMissions.Insert(Math.Min(index, block.AvailableMissions.Count), mission);
            mission.Status = oldStatus;
            mission.AcceptedAtDay = oldAccepted;
            mission.ExpiresAtDay = oldExpires;
            mission.LastTransitionReason = oldReason;
            mission.IsReadyToTurnIn = oldReady;
            mission.Objective.IsMissionCargoGranted = oldCargo;
            mission.Objective.IsMissionCargoDelivered = oldDelivered;
            reason = "AcceptanceError";
            AppLog.Error(DebugLogChannel.Mission, "[MissionLifecycle] Accept rolled back | Id=" + id + " | " + exception.Message);
            return false;
        }

        Log("Accepted", mission, "ExpiresAtDay=" + expires);
        PublishCargoChanged();
        // Observer failures must not turn a committed acceptance into an apparent failure.
        PublishAccepted(mission);
        try { ReevaluateActiveMissions("Accepted"); }
        catch (Exception exception)
        { AppLog.Error(DebugLogChannel.Mission, "[MissionLifecycle] Post-accept evaluation failed | " + exception.Message); }
        return true;
    }

    private string ValidateAcceptance(MissionInstanceData mission)
    {
        if (mission == null) return "MissionNotFound";
        if (_gameSessionService.State?.Player == null) return "MissingPlayer";
        if (!MissionLifecycleRules.IsPlayableType(mission.MissionType)) return "MissionTypeDisabled";
        if (mission.Status != MissionStatus.Available) return "InvalidStatus";
        if (mission.Objective == null || mission.Objective.RequiredAmount <= 0) return "MissingObjective";
        if (string.IsNullOrWhiteSpace(mission.TargetSystemId) ||
            (mission.MissionType == MissionType.Delivery && string.IsNullOrWhiteSpace(mission.TargetPlanetId)))
            return "MissingTarget";
        if (mission.Reward == null || mission.Reward.Credits < 0 || mission.Reward.Xp < 0) return "InvalidReward";
        if (mission.MissionType == MissionType.Delivery &&
            (mission.Objective.MissionCargoAmount <= 0 || mission.Objective.MissionCargoVolume <= 0 ||
             !_inventory.CanAddItem(mission.Objective.MissionCargoItemId, mission.Objective.MissionCargoAmount))) return "NotEnoughCargoSpace";
        if (mission.MissionType == MissionType.Delivery &&
            !DeliveryMissionTargetSelector.BuildCandidates(_gameSessionService.State.Galaxy, _config.GetAllStarSystems(),
                mission.SourceSystemId, route => _routes.IsRouteUnlocked(route.Id),
                (from, to) => _travel.GetTravelCost(from, to))
                .Any(t => t.System.Id == mission.TargetSystemId && t.Planet.Id == mission.TargetPlanetId))
            return "DeliveryDestinationUnavailable";
        if (mission.MissionType == MissionType.Recon &&
            (!ReconMissionTargetSelector.IsEnemy(_gameSessionService.State.Galaxy, mission.TargetSystemId) ||
             mission.Objective.ReconScanDistance <= 0 ||
             string.IsNullOrWhiteSpace(mission.SourcePlanetId) || string.IsNullOrWhiteSpace(mission.SourceSystemId))) return "InvalidReconTargetOrIssuer";
        if (MissionLifecycleRules.IsExpired(mission, CurrentDay)) return "DeadlineReached";
        if (!MissionCapacityHelper.CanAcceptMoreMissions(this)) return "CapacityReached";
        if (Block.ActiveMissions.Exists(m => m.MissionRuntimeId == mission.MissionRuntimeId) ||
            Block.CompletedMissions.Exists(m => m.MissionRuntimeId == mission.MissionRuntimeId) ||
            Block.ArchivedMissions.Exists(m => m.MissionRuntimeId == mission.MissionRuntimeId) ||
            Block.RewardGrantedMissionIds.Contains(mission.MissionRuntimeId)) return "AlreadyProcessed";
        return "None";
    }

    public bool CompleteMission(string missionRuntimeId)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.MissionComplete, missionRuntimeId))
        {
            bool result = CompleteMissionCore(missionRuntimeId, out string reason);
            observation.Complete(result, reason);
            return result;
        }
    }

    private bool CompleteMissionCore(string id, out string reason)
    {
        var mission = Find(Block?.ActiveMissions, id);
        reason = "MissionNotFound";
        if (mission == null) return Reject("Complete", mission, reason);
        if (MissionLifecycleRules.IsExpired(mission, CurrentDay))
        {
            ExpireMission(id);
            reason = "DeadlineReached";
            return Reject("Complete", mission, reason);
        }
        if (!MissionLifecycleRules.IsPlayableType(mission.MissionType) ||
            !MissionLifecycleRules.IsActive(mission.Status) || !ConditionsSatisfied(mission))
        {
            reason = "ConditionsNotMet";
            return Reject("Complete", mission, reason);
        }
        if (_gameSessionService.State.Player == null)
        {
            reason = "MissingPlayer";
            return Reject("Complete", mission, reason);
        }
        // Readiness and reward must agree for every mission type.
        bool completed = false;
        Mutate("Complete", () =>
        {
            mission.Status = MissionStatus.ReadyToComplete;
            mission.IsReadyToTurnIn = true;
            bool alreadyPaid = Block.RewardGrantedMissionIds.Contains(id);
            if (!alreadyPaid && !_rewardService.TryGrantMissionReward(mission, _gameSessionService.State.Player))
                return;
            if (mission.MissionType == MissionType.Delivery)
            {
                MissionCargoOperations.Remove(_gameSessionService.State, mission);
                mission.Objective.IsMissionCargoDelivered = true;
                Log("CargoDelivered", mission, "Item=" + mission.Objective.MissionCargoItemId +
                    " | Quantity=" + mission.Objective.MissionCargoAmount + " | Volume=" + mission.Objective.MissionCargoVolume);
            }
            mission.Status = MissionStatus.Completed;
            mission.IsReadyToTurnIn = false;
            mission.FinishedAtDay = CurrentDay;
            mission.LastTransitionReason = "Completed";
            Block.ActiveMissions.Remove(mission);
            completed = true;
        });
        reason = completed ? "None" : "RewardNotGranted";
        if (completed)
        {
            Log("Completed", mission, "RecordRemoved=true | RewardLedgerPreserved=true");
            PublishCargoChanged();
            PublishWalletChanged();
            PublishMissionChanged(mission);
        }
        else Reject("Complete", mission, reason);
        return completed;
    }

    public bool FailMission(string missionRuntimeId)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.MissionFail, missionRuntimeId))
        {
            bool result = FinishWithoutReward(missionRuntimeId, MissionStatus.Failed, "Failed");
            observation.Complete(result, result ? "None" : "InvalidTransition");
            return result;
        }
    }

    public bool CancelMission(string missionRuntimeId) =>
        FinishWithoutReward(missionRuntimeId, MissionStatus.Cancelled, "CancelledByPlayer");

    public bool ExpireMission(string missionRuntimeId)
    {
        var mission = Find(Block?.ActiveMissions, missionRuntimeId);
        if (mission == null || !MissionLifecycleRules.IsExpired(mission, CurrentDay))
            return Reject("Expire", mission, "DeadlineNotReached");
        return FinishWithoutReward(missionRuntimeId, MissionStatus.Expired, "DeadlineReached");
    }

    private bool FinishWithoutReward(string id, MissionStatus outcome, string reason)
    {
        var mission = Find(Block?.ActiveMissions, id);
        if (mission == null || !MissionLifecycleRules.IsActive(mission.Status))
            return Reject(outcome.ToString(), mission, "InvalidTransition");
        Mutate(outcome.ToString(), () =>
        {
            Block.ArchivedMissions.Add(mission);
            Block.ActiveMissions.Remove(mission);
            mission.Status = outcome;
            mission.IsReadyToTurnIn = false;
            mission.FinishedAtDay = CurrentDay;
            mission.LastTransitionReason = reason;
            MissionCargoOperations.Remove(_gameSessionService.State, mission);
        });
        PublishCargoChanged();
        PublishMissionChanged(mission);
        Log("Archived", mission, reason);
        return true;
    }

    public void ReevaluateActiveMissions(string reason)
    {
        var block = Block;
        if (block == null) return;
        foreach (var mission in block.ActiveMissions.ToArray())
        {
            if (mission == null) continue;
            if (!MissionLifecycleRules.IsPlayableType(mission.MissionType))
            {
                FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Cancelled, "MissionTypeDisabled");
                continue;
            }
            if (MissionLifecycleRules.IsExpired(mission, CurrentDay))
            {
                ExpireMission(mission.MissionRuntimeId);
                continue;
            }
            if (!MissionLifecycleRules.IsActive(mission.Status)) continue;
            if (mission.MissionType == MissionType.Delivery && mission.Objective?.IsMissionCargoGranted == true &&
                !MissionCargoOperations.HasCargo(_gameSessionService.State, mission, false))
            {
                FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "MissionCargoLost");
                continue;
            }
            if (mission.MissionType == MissionType.Recon && mission.Objective != null)
            {
                bool inside = mission.TargetSystemId == _gameSessionService.State.Player?.CurrentSystemId &&
                    ReconMissionTargetSelector.IsEnemy(_gameSessionService.State.Galaxy, mission.TargetSystemId);
                if (inside != mission.Objective.ReconIsInsideTarget)
                {
                    Mutate("ReconObserved", () =>
                    {
                        mission.Objective.ReconIsInsideTarget = inside;
                    });
                    Log(inside ? "ReconTargetEntered" : "ReconTargetLeft", mission,
                        "Scanned=" + mission.Objective.CurrentAmount + "/" + mission.Objective.RequiredAmount +
                        " | ReturnToPlanet=" + mission.SourcePlanetId);
                    PublishMissionChanged(mission);
                }
            }
            bool ready = ConditionsSatisfied(mission);
            var next = ready ? MissionStatus.ReadyToComplete : MissionStatus.InProgress;
            if (mission.Status != next || mission.IsReadyToTurnIn != ready)
            {
                Mutate("Reevaluate", () =>
                {
                    mission.Status = next;
                    mission.IsReadyToTurnIn = ready;
                    mission.LastTransitionReason = reason;
                });
                Log("Reevaluated", mission, reason);
                PublishMissionChanged(mission);
            }
        }
    }

    public void ObserveReconTargets()
    {
        var block = Block;
        var player = _gameSessionService.State?.Player;
        if (block?.ActiveMissions == null || player == null ||
            !_playerTarget.IsPlayerAvailableInSystem(player.CurrentSystemId)) return;
        foreach (var mission in block.ActiveMissions.ToArray())
        {
            if (mission?.MissionType != MissionType.Recon || !MissionLifecycleRules.IsActive(mission.Status) ||
                mission.Objective == null || mission.TargetSystemId != player.CurrentSystemId ||
                !ReconMissionTargetSelector.IsEnemy(_gameSessionService.State.Galaxy, mission.TargetSystemId) ||
                mission.Objective.CurrentAmount >= mission.Objective.RequiredAmount) continue;
            var objective = mission.Objective;
            objective.ReconObservedEnemyIds ??= new List<string>();
            var enemies = _npcs.GetAliveNpcsInSystemByType(mission.TargetSystemId, SystemNpcType.Enemy);
            var found = ReconMissionSurveyRules.FindNewNearbyEnemyIds(enemies, objective.ReconObservedEnemyIds,
                _playerTarget.GetPlayerPosition(), objective.ReconScanDistance,
                objective.RequiredAmount - objective.CurrentAmount);
            if (found.Count == 0) continue;
            Mutate("ReconEnemyScanned", () =>
            {
                objective.ReconObservedEnemyIds.AddRange(found);
                objective.CurrentAmount = Math.Min(objective.RequiredAmount, objective.ReconObservedEnemyIds.Count);
            });
            Log("ReconEnemyScanned", mission, "EnemyIds=" + string.Join(",", found) +
                " | Scanned=" + objective.CurrentAmount + "/" + objective.RequiredAmount +
                " | Radius=" + objective.ReconScanDistance);
            PublishMissionChanged(mission);
            ReevaluateActiveMissions("ReconProximity");
        }
    }

    private bool ConditionsSatisfied(MissionInstanceData mission)
    {
        var objective = mission.Objective;
        var player = _gameSessionService.State?.Player;
        if (objective == null || player == null || objective.RequiredAmount <= 0) return false;
        switch (mission.MissionType)
        {
            case MissionType.Delivery:
                return objective.IsMissionCargoGranted && !objective.IsMissionCargoDelivered &&
                    MissionCargoOperations.HasCargo(_gameSessionService.State, mission, true) &&
                    !string.IsNullOrWhiteSpace(mission.TargetPlanetId) &&
                    mission.TargetPlanetId == player.CurrentPlanetId &&
                    mission.TargetSystemId == player.CurrentSystemId;
            case MissionType.Recon:
                return objective.CurrentAmount >= objective.RequiredAmount &&
                    !string.IsNullOrWhiteSpace(mission.SourcePlanetId) &&
                    mission.SourcePlanetId == player.CurrentPlanetId && mission.SourceSystemId == player.CurrentSystemId;
            default:
                return false;
        }
    }

    public MissionInstanceData GetMissionById(string id) => Find(Block?.AvailableMissions, id) ??
        Find(Block?.ActiveMissions, id) ?? Find(Block?.CompletedMissions, id) ?? Find(Block?.ArchivedMissions, id);

    private static MissionInstanceData Find(List<MissionInstanceData> missions, string id) =>
        string.IsNullOrWhiteSpace(id) ? null : missions?.FirstOrDefault(m => m != null && m.MissionRuntimeId == id);

    public void ClearAll()
    {
        if (Block == null) return;
        Mutate("ClearAll", () =>
        {
            foreach (var mission in Block.ActiveMissions) MissionCargoOperations.Remove(_gameSessionService.State, mission);
            Block.AvailableMissions.Clear();
            Block.ActiveMissions.Clear();
            Block.CompletedMissions.Clear();
            Block.ArchivedMissions.Clear();
            Block.OffersByPlanet.Clear();
            Block.OffersByPlanet_List.Clear();
            // Reward IDs survive mission-list cleanup. A new session owns a fresh ledger.
        });
        PublishCargoChanged();
        Log("Cleared", null, "RewardLedgerPreserved=true");
    }

    private void Mutate(string operation, Action action)
    {
        bool previous = _saveService.IsSaveEnabled;
        try
        {
            _saveService.EnableSave(false);
            action();
        }
        finally
        {
            _saveService.EnableSave(previous);
            Log("SaveModeRestored", null, "Operation=" + operation + " | Enabled=" + previous);
        }
        if (previous)
        {
            try { _eventBus.Publish(new SaveNeedEvent()); }
            catch (Exception exception)
            { AppLog.Error(DebugLogChannel.Mission, "[MissionLifecycle] Save request observer failed | " + exception.Message); }
        }
    }

    private void PublishMissionChanged(MissionInstanceData mission)
    {
        try { _eventBus.Publish(new MissionStateChangedEvent { MissionRuntimeId = mission.MissionRuntimeId }); }
        catch (Exception exception) { AppLog.Error(DebugLogChannel.Mission, "[MissionLifecycle] UI observer failed | " + exception.Message); }
    }

    private void PublishCargoChanged()
    {
        try { _eventBus.Publish(new CargoChangedEvent(_inventory.GetUsedCargo())); }
        catch (Exception exception) { AppLog.Error(DebugLogChannel.Mission, "[MissionCargo] UI observer failed | " + exception.Message); }
    }

    private void PublishWalletChanged()
    {
        try
        {
            _eventBus.Publish(new CreditsChangedEvent(_gameSessionService.State.Player.Credits));
            Log("WalletUpdated", null, "Credits=" + _gameSessionService.State.Player.Credits);
        }
        catch (Exception exception) { AppLog.Error(DebugLogChannel.Mission, "[MissionReward] Wallet observer failed | " + exception.Message); }
    }

    private void PublishAccepted(MissionInstanceData mission)
    {
        try
        {
            _eventBus.Publish(new MissionAcceptedEvent
            { MissionRuntimeId = mission.MissionRuntimeId, MissionType = mission.MissionType.ToString() });
        }
        catch (Exception exception)
        { AppLog.Error(DebugLogChannel.Mission, "[MissionLifecycle] Acceptance observer failed | " + exception.Message); }
    }

    private static bool Reject(string operation, MissionInstanceData mission, string reason)
    {
        AppLog.Warning(DebugLogChannel.Mission,
            "[MissionLifecycle] Rejected | Operation=" + operation + " | Id=" + mission?.MissionRuntimeId + " | Reason=" + reason);
        return false;
    }

    private static void Log(string operation, MissionInstanceData mission, string details)
    {
        AppLog.Info(DebugLogChannel.Mission, "[MissionLifecycle] " + operation +
            " | Id=" + mission?.MissionRuntimeId + " | Type=" + mission?.MissionType +
            " | Status=" + mission?.Status + " | " + details);
    }
}
