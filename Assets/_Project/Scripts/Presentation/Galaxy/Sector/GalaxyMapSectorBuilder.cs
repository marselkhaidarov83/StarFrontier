using UnityEngine;

public class GalaxyMapSectorBuilder : CustomMonoBehaviour
{
    [Header("Сцена")]
    [SerializeField] private Transform sectorNodesRoot;
    [SerializeField] private GalaxySectorNodeView2A sectorPrefab;

    [Header("Отладка")]
    [SerializeField] private bool debugForceClosedSectors;

    private GalaxyRuntimeState _galaxyRuntimeState;
    private GalaxyConfig _galaxyConfig;

    public void Rebuild()
    {
        LogCustom("");

        if (_galaxyConfig == null)
        {
            Debug.LogError("[GalaxySectorMapBuilder] GalaxyConfig не назначен.");
            return;
        }

        if (sectorNodesRoot == null)
        {
            Debug.LogError("[GalaxySectorMapBuilder] SectorNodesRoot не назначен.");
            return;
        }

        if (sectorPrefab == null)
        {
            Debug.LogError("[GalaxySectorMapBuilder] SectorPrefab не назначен.");
            return;
        }

        Clear();

        if (_galaxyConfig.Sectors == null)
            return;

        LogCustom("_galaxyConfig.Sectors.Count = " + _galaxyConfig.Sectors.Count);
        foreach (SectorConfig sectorConfig in _galaxyConfig.Sectors)
        {
            if (sectorConfig == null)
                continue;

            SectorRuntimeState runtimeState =
                FindSectorRuntimeState(sectorConfig.Id);

            bool isUnlocked = runtimeState != null
                ? runtimeState.IsUnlocked
                : sectorConfig.Order == 1;

            if (debugForceClosedSectors)
                isUnlocked = false;

            GalaxySectorNodeView2A sectorView =
                Instantiate(sectorPrefab, sectorNodesRoot);

            sectorView.Initialize(sectorConfig, isUnlocked);
        }
    }

    public void Initialize()
    {
        IConfigService configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _galaxyConfig = configService.GalaxyConfig;
        IGameSessionService gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _galaxyRuntimeState = gameSessionService.State.Galaxy;

        Rebuild();
    }

    private void Clear()
    {
        for (int i = sectorNodesRoot.childCount - 1; i >= 0; i--)
            Destroy(sectorNodesRoot.GetChild(i).gameObject);
    }

    private SectorRuntimeState FindSectorRuntimeState(string sectorId)
    {
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Sectors == null)
            return null;

        return _galaxyRuntimeState.Sectors.Find(
            sector => sector.SectorId == sectorId
        );
    }
}
