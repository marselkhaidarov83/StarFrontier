using UnityEngine;

//Скрипт управляет:
//    стартом функционала на сцене Meta
public class GalaxySceneController : CustomMonoBehaviour
{
    [Header("Screen Controllers")]
    [SerializeField] private MetaHudController metaHudController;
    [SerializeField] private GalaxyMapController2A galaxyMapController2a;
    [SerializeField] private GalaxySectorMapBuilder galaxySectorMapBuilder;

    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapScreenRoot2;

    private void Start()
    {
        metaHudController?.Initialize();
        galaxyMapController2a?.Initialize();
        galaxySectorMapBuilder?.Initialize();

        LogCustom("all galaxy systems initialized.");
    }
}