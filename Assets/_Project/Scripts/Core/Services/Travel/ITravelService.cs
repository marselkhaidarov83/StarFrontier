    public interface ITravelService
    {
        GalaxyGraphModel GalaxyGraph();
        bool CanTravel(string fromSystemId, string toSystemId);
        TravelFailReason GetTravelFailReason(string fromSystemId, string toSystemId);
        int GetTravelCost(string fromSystemId, string toSystemId);
        TravelResult TryTravel(string toSystemId);
        void TryTravelToPlanet(string planetId);
    }