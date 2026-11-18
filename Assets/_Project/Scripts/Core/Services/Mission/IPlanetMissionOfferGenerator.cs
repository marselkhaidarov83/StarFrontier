using System.Collections.Generic;

    public interface IPlanetMissionOfferGenerator
    {
        PlanetOfferedMissionData TryGenerateOffer(
            StarSystemConfig starSystem,
            PlanetConfig planetConfig,
            IReadOnlyList<MissionInstanceData> playerMissions,
            PlanetOfferedMissionData existingOffer);
    }
