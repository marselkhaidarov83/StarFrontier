using System.Collections.Generic;

    public interface IPlanetMissionOfferGenerator
    {
        PlanetOfferedMissionData TryGenerateOffer(
            // PlanetMissionConfig planetConfig,
            StarSystemConfig starSystem,
            PlanetConfig planetConfig,
            IReadOnlyList<MissionInstanceData> playerMissions,
            PlanetOfferedMissionData existingOffer);
    }
