using UnityEngine;

/// <summary>Compatibility entry for old scene components; all research UI now lives in the docked location screen.</summary>
public sealed class ScienceStationResearchWindow2A : MonoBehaviour
{
    private StationConfig _station;
    public void Initialize(StationConfig station) { _station = station; }
    public bool Open()
    {
        if (_station == null) return false;
        var location = FindFirstObjectByType<PlanetController>(FindObjectsInactive.Include);
        return location != null && location.OpenStationResearch();
    }
}
