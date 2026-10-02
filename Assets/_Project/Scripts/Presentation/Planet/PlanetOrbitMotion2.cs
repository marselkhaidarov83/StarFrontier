using UnityEngine;

public class PlanetOrbitMotion2 : CustomMonoBehaviour
{
    private PlanetConfig _planet;

    private IOrbitalMotionService _orbitalMotionService;

    public void Initialize(PlanetConfig planet)
    {
        _debugStop = true;
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();

        _planet = planet;
        LogCustom("Planet = " + _planet.Id);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double refreshMs = 0.0;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshPlanetPositions();

            refreshMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            VisualUpdateAggregateLog.Record(
                "PlanetOrbitMotion2.Update",
                elapsedMs,
                "Name=" + name +
                " | Planet=" + (_planet != null ? _planet.Id : string.Empty) +
                " | RefreshMs=" + refreshMs.ToString("F3"));
        }
    }

    private void RefreshPlanetPositions()
    {
        if (_orbitalMotionService == null)
            return;

        Vector3 position =
            _orbitalMotionService.GetPlanetCurrentPosition(
                _planet.PlanetOrbit);

        transform.position =
            _planet.PlanetOrbit.OrbitCenterOffset + position;
    }
}
