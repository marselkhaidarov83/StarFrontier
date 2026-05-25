using UnityEngine;

public interface ISystemTravelService
{
    SystemTravelState State { get; }

    void SetCurrentSystem(string systemId);
    void SetCurrentPlanet(string planetId, Vector3 planetPosition);
    void SetCurrentPosition(Vector3 position);

    void SetPlanetDestination(PlanetConfig planetData);
    void SetMapPointDestination(Vector3 mapPosition);
    void SetSystemExitDestination(StarSystemLink link);

    void StartTravel();
    void CancelTravel();
    void Tick(float deltaTime, int quantTick);
    void CompleteTravel();

    Vector3 GetCurrentDestinationPosition();
}