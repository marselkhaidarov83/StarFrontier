using UnityEngine;

public class PlanetOrbitMotion : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Vector2 _center;
    private PlanetConfig _planet;

    private IGameTimeService _gameTimeService;
    private IOrbitalMotionService _orbitalMotionService;

    public void Initialize(Vector2 center, PlanetConfig planet)
    {
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();

        _rectTransform = GetComponent<RectTransform>();
        _center = center;
        _planet = planet;
    }

    private void Update()
    {
        RefreshPlanetPositions();
    }

    private void RefreshPlanetPositions()
    {
        if (_gameTimeService == null || _orbitalMotionService == null)
            return;

        Vector2 position = _orbitalMotionService.GetPlanetCurrentPosition(
            _planet.PlanetOrbit);

        _rectTransform.anchoredPosition = _center + position;
    }    
}
