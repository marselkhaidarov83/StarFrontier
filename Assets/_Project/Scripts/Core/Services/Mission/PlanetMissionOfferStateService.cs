using System.Collections.Generic;

public class PlanetMissionOfferStateService : IPlanetMissionOfferStateService
{
    private IGameSessionService _gameSessionService;
    // private readonly Dictionary<string, PlanetOfferedMissionData> _offersByPlanet = new();

    public PlanetMissionOfferStateService()
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();    
    }

    public PlanetOfferedMissionData GetOffer(string planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return null;

        _gameSessionService.CurrentSave.MissionBlock.OffersByPlanet.TryGetValue(planetId, out PlanetOfferedMissionData offer);
        return offer;
    }

    public void SetOffer(PlanetOfferedMissionData offer)
    {
        if (offer == null || string.IsNullOrWhiteSpace(offer.PlanetId))
            return;

        _gameSessionService.CurrentSave.MissionBlock.OffersByPlanet[offer.PlanetId] = offer;
    }

    public void ClearOffer(string planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return;

        _gameSessionService.CurrentSave.MissionBlock.OffersByPlanet.Remove(planetId);
    }

    public IReadOnlyDictionary<string, PlanetOfferedMissionData> GetAllOffers()
    {
        return _gameSessionService.CurrentSave.MissionBlock.OffersByPlanet;
    }

    public void ClearAll()
    {
        _gameSessionService.CurrentSave.MissionBlock.OffersByPlanet.Clear();
    }
}