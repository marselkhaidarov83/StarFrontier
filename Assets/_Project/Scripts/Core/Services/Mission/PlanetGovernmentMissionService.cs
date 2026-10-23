public class PlanetGovernmentMissionService : IPlanetGovernmentMissionService
{
    private readonly IMissionService _missionService;
    private readonly IPlanetMissionOfferStateService _offerStateService;
    private readonly IPlanetMissionOfferGenerator _offerGenerator;

    public PlanetGovernmentMissionService()
    {
        _missionService = Bootstrapper.Instance.ServiceRegistry.Get<IMissionService>();
        _offerStateService = Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMissionOfferStateService>();
        _offerGenerator = Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMissionOfferGenerator>();
    }

    public PlanetOfferedMissionData GetOrCreateGovernmentOffer(
                StarSystemConfig starSystem, PlanetConfig planetConfig)
    {
        if (planetConfig == null)
            return null;

        if (planetConfig.PlanetMissionConfig == null)
            return null;

        if (!MissionCapacityHelper.CanAcceptMoreMissions(_missionService))
            return null;

        PlanetOfferedMissionData existingOffer = _offerStateService.GetOffer(planetConfig.Id);

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

    public bool AcceptGovernmentOffer(string planetId)
    {
        PlanetOfferedMissionData offer = _offerStateService.GetOffer(planetId);

        if (offer == null || offer.OfferedMission == null)
            return false;

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