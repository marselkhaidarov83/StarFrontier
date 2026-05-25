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

        return result;
    }

    public bool AcceptGovernmentOffer(string planetId)
    {
        PlanetOfferedMissionData offer = _offerStateService.GetOffer(planetId);

        if (offer == null || offer.OfferedMission == null)
            return false;

        _missionService.SetAvailableMissions(new System.Collections.Generic.List<MissionInstanceData>
        {
            offer.OfferedMission
        });

        bool accepted = _missionService.AcceptMission(offer.OfferedMission.MissionRuntimeId);

        if (accepted)
            _offerStateService.ClearOffer(planetId);

        return accepted;
    }
}