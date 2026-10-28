using System;
using System.Collections.Generic;

public class PlanetGovernmentMissionService : IPlanetGovernmentMissionService
{
    private readonly IMissionService _missionService;
    private readonly IPlanetMissionOfferStateService _offerStateService;
    private readonly IPlanetMissionOfferGenerator _offerGenerator;
    private readonly IGameSessionService _session;
    private readonly ISaveService _save;
    private readonly SimpleEventBus _events;

    public PlanetGovernmentMissionService()
    {
        _missionService = Bootstrapper.Instance.ServiceRegistry.Get<IMissionService>();
        _offerStateService = Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMissionOfferStateService>();
        _offerGenerator = Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMissionOfferGenerator>();
        _session = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _save = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
        _events = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public PlanetOfferedMissionData GetOrCreateGovernmentOffer(
                StarSystemConfig starSystem, PlanetConfig planetConfig)
    {
        if (planetConfig == null)
            return null;

        if (planetConfig.PlanetMissionConfig == null)
            return null;

        PlanetOfferedMissionData existingOffer = _offerStateService.GetOffer(planetConfig.Id);
        if (_session.State?.Galaxy?.HasActiveInvasionForTarget(starSystem?.Id) == true)
        {
            _missionService.RemoveUnacceptedOffersInSystem(starSystem.Id);
            return null;
        }
        if (!MissionCapacityHelper.CanAcceptMoreMissions(_missionService))
            return existingOffer?.IsDebugGenerated == true ? existingOffer : null;

        PlanetOfferedMissionData result = _offerGenerator.TryGenerateOffer(
            starSystem,
            planetConfig,
            _missionService.GetActiveMissions(),
            existingOffer);

        if (result != null)
            _offerStateService.SetOffer(result);
        else
            _offerStateService.ClearOffer(planetConfig.Id);

        return result;
    }

    // Replaces only the unaccepted offer at the planet where the player is currently landed.
    public bool TryRegenerateGovernmentOffer(StarSystemConfig system, PlanetConfig planet, out PlanetOfferedMissionData offer)
        => TryRegenerateGovernmentOfferInternal(system, planet, null, null, out offer);

    public bool TryRegenerateGovernmentOfferOfType(StarSystemConfig system, PlanetConfig planet,
        MissionType missionType, bool? routeIsUpgrade, out PlanetOfferedMissionData offer)
        => TryRegenerateGovernmentOfferInternal(system, planet, missionType, routeIsUpgrade, out offer);

    private bool TryRegenerateGovernmentOfferInternal(StarSystemConfig system, PlanetConfig planet,
        MissionType? requiredType, bool? routeIsUpgrade, out PlanetOfferedMissionData offer)
    {
        offer = null;
        var state = _session.State;
        if (state?.Player == null)
            return RejectRegeneration("MissingSessionOrPlayer", system, planet);
        if (string.IsNullOrWhiteSpace(state.Player.CurrentPlanetId))
            return RejectRegeneration("PlayerNotLanded", system, planet);
        if (system == null || planet == null || system.Id != state.Player.CurrentSystemId || planet.Id != state.Player.CurrentPlanetId)
            return RejectRegeneration("PlanetOrSystemMismatch", system, planet);
        if (planet.PlanetMissionConfig == null)
            return RejectRegeneration("PlanetMissionConfigMissing", system, planet);
        if (state.Galaxy?.HasActiveInvasionForTarget(system.Id) == true)
        {
            _missionService.RemoveUnacceptedOffersInSystem(system.Id);
            return RejectRegeneration("ActiveInvasionInSourceSystem", system, planet);
        }
        var active = _missionService.GetActiveMissions();
        foreach (var mission in active)
            if (mission != null && MissionLifecycleRules.IsActive(mission.Status) && mission.SourcePlanetId == planet.Id)
                return RejectRegeneration("AcceptedMissionFromThisPlanet:" + mission.MissionRuntimeId, system, planet);
        // Explicit debug rerolls can prepare an offer even when normal acceptance is full.
        state.MissionBlock ??= new RuntimeMissionSaveBlock();
        MissionLifecycleRules.EnsureCollections(state.MissionBlock);
        // Ignore the cached offer and obey the current enabled templates, weights, cargo and target rules.
        // Generate before mutation: an exception must preserve the previous offer and save mode.
        var oldOffer = _offerStateService.GetOffer(planet.Id);
        AppLog.Info(DebugLogChannel.Mission, "[MissionReroll] GenerateRequested | Planet=" + planet.Id +
            " | OldId=" + oldOffer?.OfferedMission?.MissionRuntimeId + " | OldType=" + oldOffer?.OfferedMission?.MissionType +
            " | OldTitle=" + oldOffer?.OfferedMission?.Title);
        PlanetOfferedMissionData generated;
        if (requiredType.HasValue)
        {
            if (_offerGenerator is not PlanetMissionOfferGenerator generator)
                return RejectRegeneration("TypedGeneratorUnavailable", system, planet);
            generated = generator.TryGenerateOfferOfType(system, planet, active,
                requiredType.Value, routeIsUpgrade);
            if (generated == null)
                return RejectRegeneration("NoEligibleMissionOfRequestedType:" + requiredType.Value +
                    (routeIsUpgrade.HasValue ? ":Upgrade=" + routeIsUpgrade.Value : string.Empty), system, planet);
        }
        else
            generated = _offerGenerator.TryGenerateOffer(system, planet, active, null);
        if (generated != null) generated.IsDebugGenerated = true;
        var oldAvailable = new List<MissionInstanceData>(_missionService.GetAvailableMissions());
        var retained = new List<MissionInstanceData>(oldAvailable);
        retained.RemoveAll(m => m != null && m.SourcePlanetId == planet.Id && m.Status == MissionStatus.Available);
        var oldSerializedOffers = new List<PlanetOfferedMissionData>(state.MissionBlock.OffersByPlanet_List);
        bool previous = _save.IsSaveEnabled;
        try
        {
            _save.EnableSave(false);
            _offerStateService.ClearOffer(planet.Id);
            _missionService.SetAvailableMissions(retained);
            if (generated != null) _offerStateService.SetOffer(generated);
            state.MissionBlock.OffersByPlanet_List.RemoveAll(o => o != null && o.PlanetId == planet.Id);
            if (generated != null) state.MissionBlock.OffersByPlanet_List.Add(generated);
        }
        catch
        {
            _offerStateService.ClearOffer(planet.Id);
            if (oldOffer != null) _offerStateService.SetOffer(oldOffer);
            _missionService.SetAvailableMissions(oldAvailable);
            state.MissionBlock.OffersByPlanet_List = oldSerializedOffers;
            throw;
        }
        finally { _save.EnableSave(previous); }
        offer = generated;
        if (previous)
        {
            try { _events.Publish(new SaveNeedEvent()); }
            catch (Exception exception) { AppLog.Error(DebugLogChannel.Mission, "[MissionReroll] Save observer failed | " + exception.Message); }
        }
        AppLog.Info(DebugLogChannel.Mission, "[MissionReroll] Stored | Planet=" + planet.Id +
            " | OldId=" + oldOffer?.OfferedMission?.MissionRuntimeId + " | NewId=" + generated?.OfferedMission?.MissionRuntimeId +
            " | CachedId=" + _offerStateService.GetOffer(planet.Id)?.OfferedMission?.MissionRuntimeId +
            " | Type=" + generated?.OfferedMission?.MissionType + " | Target=" + generated?.OfferedMission?.TargetSystemId +
            " | Result=" + (generated == null ? "NoEligibleMission" : "Regenerated"));
        return true;
    }

    private bool RejectRegeneration(string reason, StarSystemConfig system, PlanetConfig planet)
    {
        AppLog.Warning(DebugLogChannel.Mission, "[MissionReroll] Rejected | Reason=" + reason +
            " | RequestedSystem=" + system?.Id + " | RequestedPlanet=" + planet?.Id +
            " | CurrentSystem=" + _session.State?.Player?.CurrentSystemId +
            " | CurrentPlanet=" + _session.State?.Player?.CurrentPlanetId);
        return false;
    }

    public bool AcceptGovernmentOffer(string planetId)
    {
        PlanetOfferedMissionData offer = _offerStateService.GetOffer(planetId);

        if (offer == null || offer.OfferedMission == null)
            return false;
        if (_session.State?.Galaxy?.HasActiveInvasionForTarget(offer.OfferedMission.SourceSystemId) == true)
        {
            _missionService.RemoveUnacceptedOffersInSystem(offer.OfferedMission.SourceSystemId);
            return false;
        }

        if (!MissionLifecycleRules.IsPlayableType(offer.OfferedMission.MissionType))
        {
            _offerStateService.ClearOffer(planetId);
            AppLog.Warning(DebugLogChannel.Mission, "[MissionLifecycle] Government offer disabled | Planet=" + planetId);
            return false;
        }
        var available = new System.Collections.Generic.List<MissionInstanceData>(_missionService.GetAvailableMissions());
        if (!available.Exists(m => m != null && m.MissionRuntimeId == offer.OfferedMission.MissionRuntimeId))
            available.Add(offer.OfferedMission);
        _missionService.SetAvailableMissions(available);

        bool accepted = _missionService.AcceptMission(offer.OfferedMission.MissionRuntimeId);

        if (accepted)
            _offerStateService.ClearOffer(planetId);
        AppLog.Info(DebugLogChannel.Mission,
            "[MissionLifecycle] Government accept | Planet=" + planetId + " | Accepted=" + accepted);

        return accepted;
    }
}
