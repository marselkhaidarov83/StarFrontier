    public interface IPlanetGovernmentMissionService
    {
        PlanetOfferedMissionData GetOrCreateGovernmentOffer(StarSystemConfig starSystem, PlanetConfig planetConfig);
        bool AcceptGovernmentOffer(string planetId);
        bool TryRegenerateGovernmentOffer(StarSystemConfig system, PlanetConfig planet, out PlanetOfferedMissionData offer);
    }