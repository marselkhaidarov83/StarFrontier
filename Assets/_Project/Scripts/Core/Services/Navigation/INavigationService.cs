public interface INavigationService
    {
        NavigationState State { get; }

        void SetInitialLocation(string sectorId, string systemId, string planetId);
        void EnterSystem(string systemId);
        void SelectPlanet(string planetId);
        void ReturnToGalaxy();
        void ReturnToSystem();
    }