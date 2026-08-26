using UnityEngine;

public interface ISystemTravelService
{
    SystemTravelState State { get; }

    void SetCurrentSystem(string systemId);
    void SetCurrentPlanet(string planetId, Vector3 planetPosition);
    void SetCurrentPosition(Vector3 position);

    void SetPlanetDestination(PlanetConfig planetData);
    void SetStationDestination(StationConfig stationData);
    void SetMapPointDestination(Vector3 mapPosition);
    void SetNpcDestination(string runtimeNpcId);
    void SetSystemExitDestination(StarSystemLink link);
    void SetSystemExitDestination(RouteExitMapChangedEvent evt);

    void StartTravel();
    void CancelTravel();
    void Tick(float deltaTime, int quantTick);
    void CompleteTravel();

    Vector3 GetCurrentDestinationPosition();

    TravelRoutePreview2A GetCurrentRoutePreview2A(
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick);
}
