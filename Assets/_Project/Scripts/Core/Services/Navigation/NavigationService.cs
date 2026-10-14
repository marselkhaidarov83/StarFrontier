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

            AppLog.Info(DebugLogChannel.Navigation,
                typeof(NavigationService),
                $"Initial location set: sector={sectorId}, system={systemId}, planet={planetId}");
        }

        public void EnterSystem(string systemId)
        {
            State.CurrentSystemId = systemId;
            State.CurrentPlanetId = null;

            AppLog.Info(DebugLogChannel.Navigation,
                typeof(NavigationService),
                $"Entered system: {systemId}");
        }

        public void SelectPlanet(string planetId)
        {
            State.CurrentPlanetId = planetId;

            AppLog.Info(DebugLogChannel.Navigation,
                typeof(NavigationService),
                $"Selected planet: {planetId}");
        }

        public void ReturnToGalaxy()
        {
            AppLog.Info(DebugLogChannel.Navigation,
                typeof(NavigationService),
                "Returned to galaxy map");
        }

        public void ReturnToSystem()
        {
            AppLog.Info(DebugLogChannel.Navigation,
                typeof(NavigationService),
                "Returned to system map");
        }
    }