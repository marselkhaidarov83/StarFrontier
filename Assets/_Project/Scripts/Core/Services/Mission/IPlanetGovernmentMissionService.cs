    public interface IPlanetGovernmentMissionService
    {
        PlanetOfferedMissionData GetOrCreateGovernmentOffer(StarSystemConfig starSystem, PlanetConfig planetConfig);
        bool AcceptGovernmentOffer(string planetId);
    }