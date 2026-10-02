using UnityEngine;

public sealed class OrbitalMotionService : CustomService, IOrbitalMotionService
{
    private float _simulationTimeSeconds;
    private readonly ShipMovementConfig _shipMovementConfig;
    private const double PerfLogThresholdMs = 1.0;

    public OrbitalMotionService()
    {
        IConfigService configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _shipMovementConfig =
            configService != null
                ? configService.ShipMovementConfig
                : null;
    }


    public void Tick(float deltaTime)
    {
        long startedAt = BeginPerfMeasure();

        float scaledDeltaTime =
            GetTickScaledDeltaTime(deltaTime);

        float speedMultiplier =
            GetSpeedMultiplier();

        _simulationTimeSeconds +=
            scaledDeltaTime * speedMultiplier;

        LogPlanetPerf(
            EndPerfMeasureMs(startedAt),
            "Tick" +
            " | DeltaTime=" + deltaTime.ToString("0.####") +
            " | ScaledDeltaTime=" + scaledDeltaTime.ToString("0.####") +
            " | SpeedMultiplier=" + speedMultiplier.ToString("0.###") +
            " | SimulationTime=" + _simulationTimeSeconds.ToString("0.###"));
    }

    private float GetSpeedMultiplier()
    {
        if (_shipMovementConfig == null)
            return 1f;

        return _shipMovementConfig.SpeedMultiplier;
    }

    private static float GetTickScaledDeltaTime(float deltaTime)
    {
        return deltaTime /
               Mathf.Max(0.01f, GameTimeState.SecondsPerDay);
    }

    private Vector3 GetPlanetPosition(
        PlanetOrbitConfig orbitConfig,
        float simulationTimeSeconds)
    {
        if (orbitConfig == null)
        {
            Debug.LogError("[OrbitalMotionService] PlanetOrbitConfig is null. Returning zero position.");
            return Vector3.zero;
        }

        float angleDegrees =
            GetPlanetAngleDegrees(
                orbitConfig,
                simulationTimeSeconds);

        float angleRadians =
            angleDegrees * Mathf.Deg2Rad;

        float x =
            orbitConfig.OrbitCenterOffset.x +
            Mathf.Cos(angleRadians) * orbitConfig.OrbitRadius;

        float y =
            orbitConfig.OrbitCenterOffset.y +
            Mathf.Sin(angleRadians) * orbitConfig.OrbitRadius;

        return new Vector3(x, y, -2);
    }

    public Vector3 GetPlanetCurrentPosition(
    PlanetOrbitConfig orbitConfig)
    {
        long startedAt = BeginPerfMeasure();

        Vector3 position =
            GetPlanetPosition(
                orbitConfig,
                _simulationTimeSeconds);

        LogPlanetPerf(
            EndPerfMeasureMs(startedAt),
            "GetPlanetCurrentPosition" +
            " | PlanetOrbitNull=" + (orbitConfig == null) +
            " | Position=" + position);

        return position;
    }

    public float GetPlanetAngleDegrees(
        PlanetOrbitConfig orbitConfig,
        float simulationTimeSeconds)
    {
        if (orbitConfig == null)
            return 0f;

        int direction =
            orbitConfig.Direction >= 0 ? 1 : -1;

        float angle =
            orbitConfig.StartAngleDeg +
            orbitConfig.OrbitSpeedDegPerSec *
            simulationTimeSeconds *
            direction;

        return NormalizeAngle(angle);
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
            angle += 360f;

        return angle;
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private void LogPlanetPerf(double elapsedMs, string message)
    {
        if (elapsedMs < PerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.PlanetMovement))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.PlanetMovement,
            "[OrbitalMotionService] " +
            message +
            " | Ms=" +
            elapsedMs.ToString("F2"));
    }
}