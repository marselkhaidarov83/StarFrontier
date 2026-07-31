using UnityEngine;

//Скрипт управляет:
//    стартом функционала на сцене Meta
public class GalaxySceneController : CustomMonoBehaviour
{
    [Header("Screen Controllers")]
    [SerializeField] private MetaHudController metaHudController;
    [SerializeField] private GalaxyMapSystemBuilder galaxyMapSystemBuilder;
    [SerializeField] private GalaxyMapSectorBuilder galaxyMapSectorBuilder;
    [SerializeField] private GalaxyMapRoutesBuilder2A galaxyMapRoutesBuilder2A;
    [SerializeField] private GalaxyMapBackgroundClickHandler galaxyMapBackgroundClickHandler;
    [SerializeField] private GalaxySystemInfoPanel2A galaxySystemInfoPanel2A;

    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapScreenRoot2;

    private void Start()
    {
        metaHudController?.Initialize();
        galaxyMapSectorBuilder?.Initialize();
        galaxyMapSystemBuilder?.Initialize();
        galaxyMapRoutesBuilder2A?.Initialize();
        galaxyMapBackgroundClickHandler?.Initialize();
        galaxySystemInfoPanel2A?.Initialize();

        LogCustom("all galaxy systems initialized.");
    }
}