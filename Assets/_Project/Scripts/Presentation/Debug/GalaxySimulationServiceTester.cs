using UnityEngine;

public class GalaxySimulationServiceTester : MonoBehaviour
{
    [SerializeField] private GalaxyConfig galaxyConfig;

    private void Start()
    {
        var service = new GalaxySimulationService();
        var state = service.CreateGalaxyState(galaxyConfig);

        Debug.Log($"Galaxy created. Current system: {state.CurrentSystemId}");
        Debug.Log($"Sectors: {state.Sectors.Count}");
        Debug.Log($"Systems: {state.Systems.Count}");
        Debug.Log($"Routes: {state.Routes.Count}");
    }
}