using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlanetMissionOfferGenerator : CustomService, IPlanetMissionOfferGenerator
{
    private readonly IConfigService _config;
    private readonly IGameSessionService _session;
    private readonly IInventoryService _inventory;
    private readonly IRouteService _routes;
    private readonly ITravelService _travel;

    public PlanetMissionOfferGenerator()
    {
        var registry = Bootstrapper.Instance.ServiceRegistry;
        _config = registry.Get<IConfigService>();
        _session = registry.Get<IGameSessionService>();
        _inventory = registry.Get<IInventoryService>();
        _routes = registry.Get<IRouteService>();
        _travel = registry.Get<ITravelService>();
    }

    public PlanetOfferedMissionData TryGenerateOffer(StarSystemConfig system, PlanetConfig planet,
        IReadOnlyList<MissionInstanceData> playerMissions, PlanetOfferedMissionData existingOffer)
        => TryGenerateOfferInternal(system, planet, playerMissions, existingOffer, null, null);

    // Debug-only selection still uses the normal template and destination rules.
    public PlanetOfferedMissionData TryGenerateOfferOfType(StarSystemConfig system, PlanetConfig planet,
        IReadOnlyList<MissionInstanceData> playerMissions, MissionType missionType, bool? routeIsUpgrade)
        => TryGenerateOfferInternal(system, planet, playerMissions, null, missionType, routeIsUpgrade);

    private PlanetOfferedMissionData TryGenerateOfferInternal(StarSystemConfig system, PlanetConfig planet,
        IReadOnlyList<MissionInstanceData> playerMissions, PlanetOfferedMissionData existingOffer,
        MissionType? requiredType, bool? routeIsUpgrade)
    {
        if (system == null || planet?.PlanetMissionConfig?.PossibleMissionOffers == null || _session.State == null) return null;
        if (_session.State.Galaxy?.HasActiveInvasionForTarget(system.Id) == true)
        {
            Log("OfferSkipped | Planet=" + planet.Id + " | Reason=ActiveInvasionInSourceSystem");
            return null;
        }
        if (playerMissions?.Any(m => m != null && MissionLifecycleRules.IsActive(m.Status) && m.SourcePlanetId == planet.Id) == true) return null;
        var old = existingOffer?.OfferedMission;
        if (old != null && old.Status == MissionStatus.Available && !string.IsNullOrWhiteSpace(old.MissionRuntimeId) &&
            ExistingOfferStillValid(system, old)) return existingOffer;

        var candidates = new List<KeyValuePair<PlanetMissionOfferEntryConfig, MissionInstanceData>>();
        foreach (var entry in planet.PlanetMissionConfig.PossibleMissionOffers)
        {
            if (entry == null || !entry.IsEnabled || entry.Weight <= 0) continue;
            MissionTemplateConfig template = entry.MissionTemplateConfig;
            if (template == null || (requiredType.HasValue && template.MissionType != requiredType.Value)) continue;
            if (routeIsUpgrade.HasValue &&
                (template is not RouteMissionTemplateConfig routeTemplate ||
                 routeTemplate.ImproveExistingRoute != routeIsUpgrade.Value)) continue;
            var mission = BuildMission(system, planet, template);
            if (mission != null) candidates.Add(new KeyValuePair<PlanetMissionOfferEntryConfig, MissionInstanceData>(entry, mission));
        }
        if (candidates.Count == 0)
        {
            Log("OfferSkipped | Planet=" + planet.Id + " | Reason=NoEligibleMission");
            return null;
        }
        double roll = UnityEngine.Random.value * candidates.Sum(c => (double)c.Key.Weight);
        var selected = candidates[candidates.Count - 1].Value;
        foreach (var candidate in candidates)
        {
            roll -= candidate.Key.Weight;
            if (roll < 0) { selected = candidate.Value; break; }
        }
        Log("OfferGenerated | Id=" + selected.MissionRuntimeId + " | Type=" + selected.MissionType +
            " | Planet=" + planet.Id + " | Target=" + selected.TargetSystemId + " | Credits=" + selected.Reward.Credits +
            " | XP=" + selected.Reward.Xp + " | Cargo=" + selected.Objective.MissionCargoItemId +
            " | Quantity=" + selected.Objective.MissionCargoAmount + " | Volume=" + selected.Objective.MissionCargoVolume);
        return new PlanetOfferedMissionData { PlanetId = planet.Id, OfferedMission = selected };
    }

    private MissionInstanceData BuildMission(StarSystemConfig source, PlanetConfig planet, MissionTemplateConfig template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.Id) || !MissionLifecycleRules.IsPlayableType(template.MissionType)) return null;
        if (template.SourcePlanet != null && template.SourcePlanet.Id != planet.Id) return null;
        if (!ValidRewards(template)) { Log("TemplateSkipped | Id=" + template.Id + " | Reason=InvalidRewards"); return null; }
        var objective = new MissionObjectiveData();
        StarSystemConfig targetSystem;
        PlanetConfig targetPlanet = null;
        ItemConfig cargoItem = null;
        int rewardCredits;
        if (template is DeliveryMissionTemplateConfig delivery && template.MissionType == MissionType.Delivery)
        {
            var targets = GetDeliveryTargets(source.Id);
            var chosen = DeliveryMissionTargetSelector.Pick(targets, UnityEngine.Random.value);
            if (chosen == null) { Log("TemplateSkipped | Id=" + template.Id + " | Reason=NoReachableAlliedDestination"); return null; }
            if (!TryChooseCargo(delivery, out cargoItem, out int quantity)) return null;
            targetSystem = chosen.System;
            targetPlanet = chosen.Planet;
            objective.RequiredAmount = quantity;
            objective.MissionCargoItemId = cargoItem.Id;
            objective.MissionCargoAmount = quantity;
            objective.MissionCargoVolume = checked(cargoItem.CargoSize * quantity);
            objective.DeliveryFuelCost = chosen.FuelCost;
            rewardCredits = DeliveryMissionTargetSelector.CalculateCredits(chosen.FuelCost, delivery.FuelRewardDeflator);
            Log("DeliveryTargetChosen | Template=" + template.Id + " | Target=" + targetSystem.Id +
                " | Planet=" + targetPlanet.Id + " | Fuel=" + chosen.FuelCost +
                " | Deflator=" + delivery.FuelRewardDeflator + " | Credits=" + rewardCredits);
        }
        else if (template is ReconMissionTemplateConfig recon && template.MissionType == MissionType.Recon)
        {
            if (recon.ScanDistance <= 0) return null;
            var reachable = ReconMissionTargetSelector.BuildCandidates(_session.State.Galaxy,
                _config.GetAllStarSystems(), source.Id,
                route => !recon.RequireUnlockedRoutes || _routes.IsRouteUnlocked(route.Id));
            var assessed = reachable.Select(c => new
            {
                Candidate = c,
                AvailableEnemies = CountAvailableEnemies(c.SystemId)
            }).ToList();
            var candidates = assessed
                .Where(item => ReconMissionSurveyRules.PickRequired(item.AvailableEnemies, recon, 0) > 0)
                .Select(item => item.Candidate).ToList();
            Log("ReconEligibility | Template=" + template.Id + " | ReachableCaptured=" + reachable.Count +
                " | WithEnoughEnemies=" + candidates.Count + " | EnemyCounts=" +
                string.Join(";", assessed.Select(item => item.Candidate.SystemId + ":" + item.AvailableEnemies)));
            Log("ReconCandidateBands | Template=" + template.Id + " | Bands=" + string.Join(";", candidates.GroupBy(c => c.EnemyJumps)
                .OrderBy(g => g.Key).Select(g => g.Key + ":systems=" + g.Count() + ",weight=" + recon.GetDistanceWeight(g.Key))));
            var chosen = ReconMissionTargetSelector.Pick(candidates, recon, UnityEngine.Random.value, UnityEngine.Random.value);
            if (chosen == null)
            {
                string reason = reachable.Count == 0 ? "NoReachableCapturedSystem" :
                    candidates.Count == 0 ? "NoReachableCapturedSystemWithEnoughEnemies" :
                    "NoPositiveDistanceWeight";
                Log("TemplateSkipped | Id=" + template.Id + " | Reason=" + reason);
                return null;
            }
            targetSystem = _config.GetStarSystemConfigById(chosen.SystemId);
            if (targetSystem == null) return null;
            objective.ReconEnemyCountAtIssue = CountAvailableEnemies(chosen.SystemId);
            objective.RequiredAmount = ReconMissionSurveyRules.PickRequired(objective.ReconEnemyCountAtIssue, recon, UnityEngine.Random.value);
            if (objective.RequiredAmount <= 0) return null;
            objective.ReconScanDistance = recon.ScanDistance;
            objective.ReconEnemyJumps = chosen.EnemyJumps;
            objective.ReconLastAlliedSystemId = chosen.LastAlliedSystemId;
            Log("ReconTargetChosen | Template=" + template.Id + " | Target=" + chosen.SystemId +
                " | LastAllied=" + chosen.LastAlliedSystemId + " | EnemyJumps=" + chosen.EnemyJumps +
                " | DistanceWeight=" + recon.GetDistanceWeight(chosen.EnemyJumps) +
                " | AvailableEnemies=" + objective.ReconEnemyCountAtIssue +
                " | Required=" + objective.RequiredAmount + " | ReservePercent=" + recon.EnemyReservePercent +
                " | ScanDistance=" + objective.ReconScanDistance);
            rewardCredits = template.CreditRewardMode == MissionCreditRewardMode.Fixed ? template.RewardCredits : Roll(template.CreditRewardMin, template.CreditRewardMax);
        }
        else if (template is RouteMissionTemplateConfig routeTemplate && template.MissionType == MissionType.RouteSearch &&
                 template.CreditRewardMode == MissionCreditRewardMode.Fixed)
        {
            var allRoutes = RouteMissionRules.AllRoutes(_config.GetAllStarSystems());
            var candidates = RouteMissionRules.BuildCandidates(_session.State.Galaxy, _config.GetAllStarSystems(), source.Id,
                routeTemplate.ImproveExistingRoute, r => _routes.IsRouteUnlocked(r.Id), RouteSystemAvailable)
                .Where(r => _session.State.MissionBlock?.ActiveMissions?.Any(m => m?.MissionType == MissionType.RouteSearch && m.Objective?.RouteId == r.Id) != true).ToList();
            if (candidates.Count == 0)
            {
                var adjacent = allRoutes.Where(r => r.ContainsSystem(source.Id)).Select(r =>
                {
                    var target = r.GetOtherSystem(source.Id);
                    var state = _session.State.Galaxy.Routes?.Find(item => item != null && item.RouteId == r.Id);
                    bool indirect = target != null && RouteMissionRules.HasIndirectPath(allRoutes,
                        source.Id, target.Id, item => _routes.IsRouteUnlocked(item.Id), RouteSystemAvailable);
                    return r.Id + ":unlocked=" + _routes.IsRouteUnlocked(r.Id) +
                           ",indirect=" + indirect +
                           ",targetAvailable=" + (target != null && RouteSystemAvailable(target.Id)) +
                           ",level=" + (state == null ? "missing" : state.DevelopmentLevel.ToString()) +
                           ",alreadyAssigned=" +
                           (_session.State.MissionBlock?.ActiveMissions?.Any(m => m?.MissionType == MissionType.RouteSearch &&
                               m.Objective?.RouteId == r.Id) == true);
                });
                Log("RouteEligibility | Template=" + template.Id + " | Upgrade=" +
                    routeTemplate.ImproveExistingRoute + " | SourceAvailable=" + RouteSystemAvailable(source.Id) +
                    " | Adjacent=" + string.Join(";", adjacent));
                return null;
            }
            var route = candidates[Math.Min(candidates.Count - 1, (int)(UnityEngine.Random.value * candidates.Count))];
            targetSystem = route.GetOtherSystem(source.Id);
            var state = _session.State.Galaxy.Routes.Find(r => r != null && r.RouteId == route.Id);
            objective.RouteId = route.Id;
            objective.RouteIsUpgrade = routeTemplate.ImproveExistingRoute;
            objective.RouteExpectedLevel = MissionBattleRules.RouteLevel(state.DevelopmentLevel);
            objective.RequiredAmount = routeTemplate.ImproveExistingRoute ? 2 : 1;
            rewardCredits = template.RewardCredits;
        }
        else
        {
            Log("TemplateSkipped | Id=" + template.Id + " | Reason=SpecializedTemplateRequired");
            return null;
        }
        objective.TargetSystemId = targetSystem.Id;
        var mission = new MissionInstanceData
        {
            MissionRuntimeId = "mission_runtime_" + Guid.NewGuid().ToString("N"),
            TemplateId = template.Id, MissionType = template.MissionType, Status = MissionStatus.Available,
            SourcePlanetId = planet.Id, SourceSystemId = source.Id,
            TargetPlanetId = targetPlanet != null ? targetPlanet.Id : string.Empty,
            TargetSystemId = targetSystem.Id, DurationDays = template.DurationDays,
            Objective = objective,
            Reward = new MissionRewardData
            {
                Credits = rewardCredits,
                Xp = Roll(template.XpRewardMin, template.XpRewardMax)
            }
        };
        mission.Title = Format(template.TitlePattern, source, planet, targetSystem, targetPlanet, cargoItem, objective);
        mission.Description = Format(template.DescriptionPattern, source, planet, targetSystem, targetPlanet, cargoItem, objective);
        mission.OutcomeTexts = template.OutcomeTexts?.Format(pattern =>
            Format(pattern, source, planet, targetSystem, targetPlanet, cargoItem, objective));
        if (string.IsNullOrWhiteSpace(mission.Title) || string.IsNullOrWhiteSpace(mission.Description))
        { Log("TemplateSkipped | Id=" + template.Id + " | Reason=MissingTextPatterns"); return null; }
        return mission;
    }

    private List<DeliveryMissionTarget> GetDeliveryTargets(string sourceId) =>
        DeliveryMissionTargetSelector.BuildCandidates(_session.State.Galaxy, _config.GetAllStarSystems(), sourceId,
            route => _routes.IsRouteUnlocked(route.Id), (from, to) => _travel.GetTravelCost(from, to));

    private int CountAvailableEnemies(string systemId) =>
        Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>()
            .GetAliveNpcsInSystemByType(systemId, SystemNpcType.Enemy).Count(n => n.IsAvailableForCombat());

    private bool RouteSystemAvailable(string id)
    {
        var galaxy = _session.State?.Galaxy;
        if (galaxy?.Systems?.Any(s => s != null && s.SystemId == id && s.IsDiscovered) != true) return false;
        var sector = _config.GetAllSectors().FirstOrDefault(s => s != null && s.Systems != null && s.Systems.Any(c => c != null && c.Id == id));
        return sector != null && (galaxy.Sectors?.Find(s => s != null && s.SectorId == sector.Id)?.IsUnlocked ?? sector.IsUnlocked);
    }

    private bool ExistingOfferStillValid(StarSystemConfig source, MissionInstanceData old)
    {
        if (old.MissionType == MissionType.Delivery)
            return old.Objective?.MissionCargoVolume > 0 && old.Objective.DeliveryFuelCost > 0 &&
                GetDeliveryTargets(source.Id).Any(t => t.System.Id == old.TargetSystemId && t.Planet.Id == old.TargetPlanetId);
        if (old.MissionType == MissionType.Recon)
            return old.Objective?.ReconScanDistance > 0 && old.Objective.RequiredAmount > 0 &&
                ReconMissionTargetSelector.IsEnemy(_session.State.Galaxy, old.TargetSystemId);
        if (old.MissionType == MissionType.RouteSearch && old.Objective != null)
            return RouteMissionRules.BuildCandidates(_session.State.Galaxy, _config.GetAllStarSystems(), source.Id,
                old.Objective.RouteIsUpgrade, r => _routes.IsRouteUnlocked(r.Id), RouteSystemAvailable)
                .Any(r => r.Id == old.Objective.RouteId && MissionBattleRules.RouteLevel(_session.State.Galaxy.Routes.Find(s => s.RouteId == r.Id).DevelopmentLevel) == old.Objective.RouteExpectedLevel);
        return false;
    }

    private bool TryChooseCargo(DeliveryMissionTemplateConfig template, out ItemConfig item, out int quantity)
    {
        bool selected = DeliveryMissionCargoSelector.TrySelect(template, _inventory.GetFreeCargo(),
            id => _config.GetItemConfigById(id) != null, UnityEngine.Random.value, UnityEngine.Random.value, out item, out quantity);
        if (!selected) Log("CargoSkipped | Template=" + template.Id + " | FreeVolume=" + _inventory.GetFreeCargo() + " | Reason=NoFittingConfiguredCargo");
        return selected;
    }

    private static bool ValidRewards(MissionTemplateConfig template) =>
        template.DurationDays >= 0 && template.XpRewardMin >= 0 && template.XpRewardMax >= template.XpRewardMin &&
        (template.CreditRewardMode == MissionCreditRewardMode.Fixed ? template.RewardCredits >= 0 :
            template.CreditRewardMode == MissionCreditRewardMode.FuelDistance ?
                template is DeliveryMissionTemplateConfig delivery && delivery.FuelRewardDeflator > 0 &&
                !float.IsNaN(delivery.FuelRewardDeflator) && !float.IsInfinity(delivery.FuelRewardDeflator) :
                template.CreditRewardMode == MissionCreditRewardMode.Range && template.CreditRewardMin >= 0 && template.CreditRewardMax >= template.CreditRewardMin);

    private static int Roll(int min, int max) => min == max ? min :
        (int)Math.Min(max, min + Math.Floor((double)UnityEngine.Random.value * ((long)max - min + 1)));

    private static string Format(string pattern, StarSystemConfig source, PlanetConfig planet,
        StarSystemConfig target, PlanetConfig targetPlanet, ItemConfig item, MissionObjectiveData objective)
    {
        return (pattern ?? string.Empty).Replace("{sourceSystem}", source.DisplayName).Replace("{sourcePlanet}", planet.DisplayName)
            .Replace("{targetSystem}", target.DisplayName).Replace("{targetPlanet}", targetPlanet != null ? targetPlanet.DisplayName : string.Empty)
            .Replace("{itemName}", item != null ? item.DisplayName : string.Empty)
            .Replace("{amount}", objective.MissionCargoAmount.ToString()).Replace("{cargoVolume}", objective.MissionCargoVolume.ToString())
            .Replace("{enemyJumps}", objective.ReconEnemyJumps.ToString())
            .Replace("{fuelCost}", objective.DeliveryFuelCost.ToString())
            .Replace("{enemyCount}", objective.RequiredAmount.ToString())
            .Replace("{scanDistance}", objective.ReconScanDistance.ToString("0.##"));
    }

    private static void Log(string text) => AppLog.Info(DebugLogChannel.Mission, "[MissionGeneration] " + text);
}
