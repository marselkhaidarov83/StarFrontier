using UnityEngine;

    public class NavigationService : INavigationService
    {
        public NavigationState State { get; private set; }

        public NavigationService()
        {
            State = new NavigationState();
        }

        public void SetInitialLocation(string sectorId, string systemId, string planetId)
        {
            State.CurrentSectorId = sectorId;
            State.CurrentSystemId = systemId;
            State.CurrentPlanetId = planetId;

            Debug.Log($"[NavigationService] Initial location set: sector={sectorId}, system={systemId}, planet={planetId}");
        }

        public void EnterSystem(string systemId)
        {
            State.CurrentSystemId = systemId;
            State.CurrentPlanetId = null;

            Debug.Log($"[NavigationService] Entered system: {systemId}");
        }

        public void SelectPlanet(string planetId)
        {
            State.CurrentPlanetId = planetId;

            Debug.Log($"[NavigationService] Selected planet: {planetId}");
        }

        public void ReturnToGalaxy()
        {
            Debug.Log("[NavigationService] Returned to galaxy map");
        }

        public void ReturnToSystem()
        {
            Debug.Log("[NavigationService] Returned to system map");
        }
    }