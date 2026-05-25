using UnityEngine;

public interface IOrbitalMotionService
{
    // Vector3 GetPlanetPosition(PlanetOrbitConfig orbitConfig, float simulationTimeSeconds);
    Vector3 GetPlanetCurrentPosition(PlanetOrbitConfig orbitConfig);
    float GetPlanetAngleDegrees(PlanetOrbitConfig orbitConfig, float simulationTimeSeconds);
    void Tick(float deltaTime);
}