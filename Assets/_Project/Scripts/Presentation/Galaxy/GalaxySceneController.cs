using UnityEngine;

//Скрипт управляет:
//    стартом функционала на сцене Meta
public class GalaxySceneController : CustomMonoBehaviour
{
    [Header("Screen Controllers")]
    [SerializeField] private MetaHudController metaHudController;
    [SerializeField] private GalaxyMapController2 galaxyMapController2;

    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapScreenRoot2;

    // private IGameSessionService gameSessionService;
    // private SimpleEventBus eventBus;

    private void Start()
    {
        // gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        // eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        metaHudController?.Initialize();
        galaxyMapController2?.Initialize();
        
        // if (galaxyMapScreenRoot2 != null)
        //     galaxyMapScreenRoot2.SetActive(false);

        // var currentPlanetId = gameSessionService.State.Player.CurrentPlanetId;
        // var currentSystemId = gameSessionService.State.Player.CurrentSystemId;

        // if (currentPlanetId != null && currentPlanetId != "")
        // {
        //     eventBus.Publish(new PlanetEnteredEvent(currentPlanetId));
        //     LogCustom("enter to planet : " + currentPlanetId);
        // }
        // else if (currentSystemId != null && currentSystemId != "")
        // {
        //     eventBus.Publish(new SystemEnteredEvent(currentSystemId));
        //     LogCustom("enter to system : " + currentSystemId);
        // }

        LogCustom("all galaxy systems initialized.");
    }
}