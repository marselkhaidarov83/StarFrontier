using UnityEngine;

public sealed class OrbitalMotionService : CustomService, IOrbitalMotionService
{
    private float _simulationTimeSeconds;

    public void Tick(float deltaTime)
    {
        _simulationTimeSeconds += deltaTime;
    }

    private Vector3 GetPlanetPosition(PlanetOrbitConfig orbitConfig, float simulationTimeSeconds)
    {
        if (orbitConfig == null)
        {
            Debug.LogError("[OrbitalMotionService] PlanetOrbitConfig is null. Returning zero position.");
            return Vector3.zero;
        }

        float angleDegrees = GetPlanetAngleDegrees(orbitConfig, simulationTimeSeconds);
        float angleRadians = angleDegrees * Mathf.Deg2Rad;

        float x = orbitConfig.OrbitCenterOffset.x + Mathf.Cos(angleRadians) * orbitConfig.OrbitRadius;
        float y = orbitConfig.OrbitCenterOffset.y + Mathf.Sin(angleRadians) * orbitConfig.OrbitRadius;

        // if (x == 0f || y == 0f)
        // {
        //     LogCustom("angleDegrees = " + angleDegrees +
        //             ", angleRadians = " + angleRadians +
        //             ", orbitConfig.Id = " + orbitConfig.Id);
        // }

        return new Vector3(x, y, -2);
    }

    public Vector3 GetPlanetCurrentPosition(PlanetOrbitConfig orbitConfig)
    {
        return GetPlanetPosition(orbitConfig, _simulationTimeSeconds);
    }

    public float GetPlanetAngleDegrees(PlanetOrbitConfig orbitConfig, float simulationTimeSeconds)
    {
        if (orbitConfig == null)
            return 0f;

        int direction = orbitConfig.Direction >= 0 ? 1 : -1;

        float angle =
            orbitConfig.StartAngleDeg +
            orbitConfig.OrbitSpeedDegPerSec * simulationTimeSeconds * direction;

        return NormalizeAngle(angle);
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
            angle += 360f;

        return angle;
    }
}