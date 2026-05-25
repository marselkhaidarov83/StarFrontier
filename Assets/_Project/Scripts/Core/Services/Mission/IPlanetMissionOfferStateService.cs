using System.Collections.Generic;

public interface IPlanetMissionOfferStateService
{
    PlanetOfferedMissionData GetOffer(string planetId);
    void SetOffer(PlanetOfferedMissionData offer);
    void ClearOffer(string planetId);
    IReadOnlyDictionary<string, PlanetOfferedMissionData> GetAllOffers();
    void ClearAll();
}