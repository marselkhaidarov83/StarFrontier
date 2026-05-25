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
        RefreshPlanetPositions();
    }

    private void RefreshPlanetPositions()
    {
        if (_orbitalMotionService == null)
            return;

        Vector3 position = _orbitalMotionService.GetPlanetCurrentPosition(_planet.PlanetOrbit);
        LogCustom("Postiion = " + position);

        transform.position = _planet.PlanetOrbit.OrbitCenterOffset + position;
    }    
}
