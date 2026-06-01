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

        _gameSessionService.State.MissionBlock.OffersByPlanet.TryGetValue(planetId, out PlanetOfferedMissionData offer);
        return offer;
    }

    public void SetOffer(PlanetOfferedMissionData offer)
    {
        if (offer == null || string.IsNullOrWhiteSpace(offer.PlanetId))
            return;

        _gameSessionService.State.MissionBlock.OffersByPlanet[offer.PlanetId] = offer;
    }

    public void ClearOffer(string planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return;

        _gameSessionService.State.MissionBlock.OffersByPlanet.Remove(planetId);
    }

    public IReadOnlyDictionary<string, PlanetOfferedMissionData> GetAllOffers()
    {
        return _gameSessionService.State.MissionBlock.OffersByPlanet;
    }

    public void ClearAll()
    {
        _gameSessionService.State.MissionBlock.OffersByPlanet.Clear();
    }
}