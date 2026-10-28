using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionService
{
    private int InvasionGeneration(string systemId) => _gameSessionService.State?.Galaxy?.Systems?
        .Find(s => s != null && s.SystemId == systemId)?.InvasionGeneration ?? 0;

    private bool MissionSystemAvailable(string systemId)
    {
        var galaxy = _gameSessionService.State?.Galaxy;
        if (galaxy?.Systems?.Any(s => s != null && s.SystemId == systemId && s.IsDiscovered) != true) return false;
        var sector = _config.GetAllSectors().FirstOrDefault(s => s != null && s.Systems != null && s.Systems.Any(c => c != null && c.Id == systemId));
        if (sector == null) return false;
        return galaxy.Sectors?.Find(s => s != null && s.SectorId == sector.Id)?.IsUnlocked ?? sector.IsUnlocked;
    }

    private bool RouteMissionIsValid(MissionInstanceData mission)
    {
        var galaxy = _gameSessionService.State?.Galaxy;
        var objective = mission?.Objective;
        if (objective == null || !MissionSystemAvailable(mission.SourceSystemId) || !MissionSystemAvailable(mission.TargetSystemId) ||
            ReconMissionTargetSelector.IsEnemy(galaxy, mission.TargetSystemId)) return false;
        var route = RouteMissionRules.AllRoutes(_config.GetAllStarSystems()).Find(r => r.Id == objective.RouteId);
        var state = galaxy.Routes?.Find(r => r != null && r.RouteId == objective.RouteId);
        if (route == null || state == null || !route.ConnectsSystems(mission.SourceSystemId, mission.TargetSystemId)) return false;
        return objective.RouteIsUpgrade
            ? state.IsUnlocked && _routes.IsRouteUnlocked(route.Id) && MissionBattleRules.RouteLevel(state.DevelopmentLevel) == objective.RouteExpectedLevel && objective.RouteExpectedLevel < 5
            : !state.IsUnlocked;
    }

    public void ObserveBattleParticipation(string systemId) => RecordBattleProgress(null, systemId);

    public void RecordBattleKill(string npcId, string systemId)
    {
        if (!string.IsNullOrWhiteSpace(npcId)) RecordBattleProgress(npcId, systemId);
    }

    private void RecordBattleProgress(string npcId, string systemId)
    {
        var state = _gameSessionService.State;
        if (state?.Player?.CurrentSystemId != systemId ||
            !_playerTarget.IsPlayerAvailableInSystem(systemId) || Block == null) return;
        var registry = Bootstrapper.Instance.ServiceRegistry;
        if (!registry.TryGet<ISystemEncounterService>(out var encounterService)) return;
        bool liberation = ReconMissionTargetSelector.IsEnemy(state.Galaxy, systemId);
        bool defense = state.Galaxy?.HasActiveInvasionForTarget(systemId) == true;
        if (!liberation && !defense) return;
        var encounter = encounterService.Current;
        if (encounter == null || encounter.SystemId != systemId || encounter.State != SystemEncounterState.Active)
        {
            int enemies = _npcs.GetAliveNpcsInSystemByType(systemId, SystemNpcType.Enemy).Count;
            if (enemies <= 0) return;
            if (encounterService.HasActiveEncounter && encounter != null)
                encounterService.RegisterPlayerLeftSystem(encounter.SystemId);
            encounterService.StartEncounter("mission_encounter_" + Guid.NewGuid().ToString("N"), systemId, enemies,
                _npcs.GetAliveNpcsInSystemByType(systemId, SystemNpcType.Ally).Count);
            encounter = encounterService.Current;
        }
        var mission = Block.ActiveMissions.Find(m => m != null && MissionBattleRules.IsBattle(m.MissionType) &&
            m.TargetSystemId == systemId && m.Objective?.BattleEncounterId == encounter.EncounterId);
        if (mission == null)
        {
            // At capacity, wait for a personal kill before adding the exceptional fourth mission.
            if (string.IsNullOrWhiteSpace(npcId) && !MissionCapacityHelper.CanAcceptMoreMissions(this)) return;
            string id = "mission_battle_" + encounter.EncounterId;
            if (GetMissionById(id) != null || Block.RewardGrantedMissionIds.Contains(id)) return;
            var settings = _config.GameConfig?.MissionGameplay;
            if (settings == null) { AppLog.Error(DebugLogChannel.Mission, "[MissionBattle] Missing MissionGameplayConfig"); return; }
            int deflator = Math.Max(0, liberation ? settings.LiberationKillRewardDeflator : settings.DefenseKillRewardDeflator);
            string systemName = _config.GetStarSystemConfigById(systemId)?.DisplayName ?? systemId;
            string titlePattern = liberation ? settings.LiberationTitlePattern : settings.DefenseTitlePattern;
            string descriptionPattern = liberation ? settings.LiberationDescriptionPattern : settings.DefenseDescriptionPattern;
            MissionOutcomeTexts outcomePatterns = liberation ? settings.LiberationOutcomeTexts : settings.DefenseOutcomeTexts;
            mission = new MissionInstanceData
            {
                MissionRuntimeId = id, TemplateId = liberation ? "automatic_liberation" : "automatic_defense",
                MissionType = liberation ? MissionType.Liberation : MissionType.Defense,
                Status = MissionStatus.InProgress, TargetSystemId = systemId, SourceSystemId = systemId,
                AcceptedAtDay = CurrentDay,
                Title = string.IsNullOrWhiteSpace(titlePattern) ? (liberation ? "Освобождение: " : "Защита: ") + systemName :
                    titlePattern.Replace("{targetSystem}", systemName),
                Description = string.IsNullOrWhiteSpace(descriptionPattern) ?
                    "Победите в бою, уничтожив хотя бы одного врага лично. Награда за все личные убийства выдаётся на любой планете этой системы до следующего вторжения. Отлёт до победы — провал." :
                    descriptionPattern.Replace("{targetSystem}", systemName),
                OutcomeTexts = outcomePatterns?.Format(pattern => (pattern ?? string.Empty).Replace("{targetSystem}", systemName)),
                Reward = new MissionRewardData(),
                Objective = new MissionObjectiveData { TargetSystemId = systemId, RequiredAmount = 1,
                    BattleEncounterId = encounter.EncounterId, BattleInvasionGeneration = InvasionGeneration(systemId), BattleRewardDeflator = deflator }
            };
            var created = mission;
            Mutate("BattleStarted", () => Block.ActiveMissions.Add(created));
            PublishAccepted(mission);
        }
        if (mission.Objective.BattleWon || string.IsNullOrWhiteSpace(npcId)) return;
        mission.Objective.BattleKilledEnemyIds ??= new List<string>();
        if (mission.Objective.BattleKilledEnemyIds.Contains(npcId)) return;
        Mutate("BattleKill", () =>
        {
            mission.Objective.BattleKilledEnemyIds.Add(npcId);
            mission.Objective.CurrentAmount = mission.Objective.BattleKilledEnemyIds.Count;
            mission.Reward.Credits = MissionBattleRules.Credits(mission.Objective.CurrentAmount, mission.Objective.BattleRewardDeflator);
        });
        Log("BattleKill", mission, "Npc=" + npcId + " | Kills=" + mission.Objective.CurrentAmount);
        PublishMissionChanged(mission);
    }

    public void ResolveBattle(string encounterId, string systemId, bool victory)
    {
        var mission = Block?.ActiveMissions.Find(m => m != null && MissionBattleRules.IsBattle(m.MissionType) &&
            m.TargetSystemId == systemId && m.Objective?.BattleEncounterId == encounterId && !m.Objective.BattleWon);
        if (mission == null) return;
        if (victory)
        {
            var encounter = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>().Current;
            if (encounter == null || encounter.EncounterId != encounterId || encounter.EnemiesAlive > 0 ||
                encounter.PlayerDestroyed || encounter.State == SystemEncounterState.Defeated) return;
        }
        if (!victory || _gameSessionService.State.Player?.CurrentSystemId != systemId || mission.Objective.CurrentAmount <= 0)
        { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "BattleLostOrLeft"); return; }
        Mutate("BattleWon", () =>
        {
            mission.Objective.BattleWon = true;
            mission.Objective.BattleInvasionGeneration = InvasionGeneration(systemId);
            mission.LastTransitionReason = "BattleWon";
            // World victory happens at battle end, independently of the later reward claim.
            var registry = Bootstrapper.Instance.ServiceRegistry;
            if (registry.TryGet<IInvasionService>(out var invasion)) invasion.ResolveInvasionFromCombatOutcome(systemId, true);
            if (mission.MissionType == MissionType.Liberation && registry.TryGet<ISystemSecurityService>(out var security) &&
                security.TryGetSystemStatus(systemId, out var status) && status == StarSystemStatus.Captured)
                security.LiberateSystemByPlayer(systemId);
        });
        Log("BattleWon", mission, "Kills=" + mission.Objective.CurrentAmount + " | Credits=" + mission.Reward.Credits);
        PublishMissionChanged(mission);
        ReevaluateActiveMissions("BattleWon");
    }

    private bool ReevaluateGameplayMission(MissionInstanceData mission)
    {
        var objective = mission.Objective;
        if (MissionBattleRules.IsBattle(mission.MissionType))
        {
            if (objective == null) { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "MissingBattleObjective"); return false; }
            if (MissionBattleRules.RewardExpired(objective, InvasionGeneration(mission.TargetSystemId)))
            { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Expired, "NextInvasionStarted"); return false; }
            if (!objective.BattleWon)
            {
                if (_gameSessionService.State.Player?.CurrentSystemId != mission.TargetSystemId)
                { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "LeftBeforeVictory"); return false; }
                var encounter = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>().Current;
                if (encounter != null && encounter.EncounterId == objective.BattleEncounterId)
                {
                    if (encounter.State == SystemEncounterState.Defeated)
                    { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "BattleDefeated"); return false; }
                    if (encounter.State == SystemEncounterState.VictoryPendingReward)
                    { ResolveBattle(encounter.EncounterId, encounter.SystemId, true); return false; }
                    if (encounter.EnemiesAlive == 0)
                    { ResolveBattle(encounter.EncounterId, encounter.SystemId, true); return false; }
                }
                if (mission.MissionType == MissionType.Defense && ReconMissionTargetSelector.IsEnemy(_gameSessionService.State.Galaxy, mission.TargetSystemId))
                { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "SystemCaptured"); return false; }
            }
        }
        if (mission.MissionType == MissionType.RouteSearch && !RouteMissionIsValid(mission))
        { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Cancelled, "RouteObjectiveUnavailable"); return false; }
        return true;
    }

    public void ObserveMissionTravel(TravelFinishedEvent travel)
    {
        if (travel == null || !travel.Success || Block == null) return;
        foreach (var mission in Block.ActiveMissions.ToArray())
        {
            if (MissionBattleRules.IsBattle(mission.MissionType) && mission.TargetSystemId == travel.FromSystemId && !mission.Objective.BattleWon)
            { FinishWithoutReward(mission.MissionRuntimeId, MissionStatus.Failed, "LeftBeforeVictory"); continue; }
            if (mission.MissionType != MissionType.RouteSearch || !RouteMissionIsValid(mission)) continue;
            if (!_routes.HasUnlockedRoute(travel.FromSystemId, travel.ToSystemId)) continue;
            Mutate("RouteSurveyHop", () =>
            {
                RouteMissionRules.ObserveHop(mission.Objective, mission.SourceSystemId,
                    mission.TargetSystemId, travel.FromSystemId, travel.ToSystemId);
                mission.Objective.CurrentAmount = (mission.Objective.RouteIndirectSurveyed ? 1 : 0) +
                    (mission.Objective.RouteIsUpgrade && mission.Objective.RouteDirectSurveyed ? 1 : 0);
            });
            Log("RouteSurveyHop", mission, travel.FromSystemId + " -> " + travel.ToSystemId +
                " | Direct=" + mission.Objective.RouteDirectSurveyed + " | Indirect=" + mission.Objective.RouteIndirectSurveyed);
            PublishMissionChanged(mission);
        }
        ReevaluateActiveMissions("TravelFinished");
    }

    // Debug UI fixture: creates a claimable battle reward without changing combat or system ownership.
    public bool TryCreateDebugReadyBattleMission(MissionType type, int personalKills,
        out MissionInstanceData mission, out string reason)
    {
        mission = null;
        reason = "None";
        if (!MissionBattleRules.IsBattle(type)) { reason = "NotBattleMission"; return false; }
        if (personalKills < 1) { reason = "NoPersonalKills"; return false; }

        var player = _gameSessionService.State?.Player;
        var block = Block;
        if (player == null || block == null || string.IsNullOrWhiteSpace(player.CurrentSystemId) ||
            string.IsNullOrWhiteSpace(player.CurrentPlanetId))
        { reason = "PlayerNotLanded"; return false; }

        string systemId = player.CurrentSystemId;
        string planetId = player.CurrentPlanetId;
        var system = _config.GetStarSystemConfigById(systemId);
        var planet = _config.GetPlanetConfigById(planetId);
        if (system == null || planet?.IsInhabited != true ||
            system.PlanetRefs?.Any(p => p != null && p.Id == planetId) != true)
        { reason = "CurrentPlanetNotInhabitedOrInvalid"; return false; }
        var settings = _config.GameConfig?.MissionGameplay;
        if (settings == null) { reason = "MissingMissionGameplayConfig"; return false; }
        bool liberation = type == MissionType.Liberation;
        int deflator = Math.Max(0, liberation
            ? settings.LiberationKillRewardDeflator : settings.DefenseKillRewardDeflator);
        string systemName = string.IsNullOrWhiteSpace(system.DisplayName) ? systemId : system.DisplayName;
        string titlePattern = liberation ? settings.LiberationTitlePattern : settings.DefenseTitlePattern;
        string descriptionPattern = liberation ? settings.LiberationDescriptionPattern : settings.DefenseDescriptionPattern;
        MissionOutcomeTexts outcomePatterns = liberation
            ? settings.LiberationOutcomeTexts : settings.DefenseOutcomeTexts;
        string id = "mission_debug_battle_" + Guid.NewGuid().ToString("N");
        var enemyIds = new List<string>(personalKills);
        for (int i = 0; i < personalKills; i++) enemyIds.Add(id + "_enemy_" + (i + 1));
        var created = new MissionInstanceData
        {
            MissionRuntimeId = id,
            TemplateId = liberation ? "automatic_liberation" : "automatic_defense",
            MissionType = type,
            Status = MissionStatus.ReadyToComplete,
            IsReadyToTurnIn = true,
            SourcePlanetId = planetId,
            SourceSystemId = systemId,
            TargetSystemId = systemId,
            AcceptedAtDay = CurrentDay,
            LastTransitionReason = "DebugBattleWon",
            Title = string.IsNullOrWhiteSpace(titlePattern)
                ? (liberation ? "Освобождение: " : "Защита: ") + systemName
                : titlePattern.Replace("{targetSystem}", systemName),
            Description = string.IsNullOrWhiteSpace(descriptionPattern)
                ? "Награда за личный вклад в победу готова."
                : descriptionPattern.Replace("{targetSystem}", systemName),
            OutcomeTexts = outcomePatterns?.Format(pattern =>
                (pattern ?? string.Empty).Replace("{targetSystem}", systemName)),
            Reward = new MissionRewardData
            {
                Credits = MissionBattleRules.Credits(personalKills, deflator),
                Xp = 0
            },
            Objective = new MissionObjectiveData
            {
                TargetSystemId = systemId,
                RequiredAmount = 1,
                CurrentAmount = personalKills,
                BattleEncounterId = "debug_" + id,
                BattleInvasionGeneration = InvasionGeneration(systemId),
                BattleRewardDeflator = deflator,
                BattleWon = true,
                BattleKilledEnemyIds = enemyIds
            }
        };

        Mutate("DebugBattleReward", () => block.ActiveMissions.Add(created));
        mission = created;
        Log("DebugBattleRewardCreated", created,
            "PersonalKills=" + personalKills + " | Credits=" + created.Reward.Credits);
        PublishAccepted(created);
        PublishMissionChanged(created);
        return true;
    }
}
