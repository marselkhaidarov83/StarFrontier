using UnityEngine;

//Скрипт управляет:
//    стартом функционала на сцене Meta
public class MetaSceneController : CustomMonoBehaviour
{
    [Header("Screen Controllers")]
    [SerializeField] private MetaHudController metaHudController;
    [SerializeField] private GalaxyMapController2 galaxyMapController2;
    [SerializeField] private SystemMapController systemMapController;
    [SerializeField] private SystemMapController2 systemMapController2;
    [SerializeField] private SystemMapHUDController systemMapHUDController;
    [SerializeField] private PlanetController planetController;
    [SerializeField] private PlanetGovernmentMissionPresenter planetGovernmentMissionPresenter;
    [SerializeField] private MarketScreenController marketScreenController;
    [SerializeField] private RefuelPanelController refuelPanelController;
    [SerializeField] private MissionScreenController missionScreenController;
    [SerializeField] private SystemShipMarkerController systemShipMarkerController;
    [SerializeField] private SystemShipMarkerController2 systemShipMarkerController2;

    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapScreenRoot2;
    [SerializeField] private GameObject systemMapScreenRoot;
    [SerializeField] private GameObject systemMapScreenRoot2;
    [SerializeField] private GameObject systemMapHUDRoot;
    [SerializeField] private GameObject planetRoot;
    [SerializeField] private GameObject governmentRoot;
    [SerializeField] private GameObject marketRoot;
    [SerializeField] private GameObject refuelRoot;

    private IGameSessionService gameSessionService;
    private SimpleEventBus eventBus;

    private void Start()
    {
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        metaHudController?.Initialize();
        galaxyMapController2?.Initialize();
        systemMapController?.Initialize(); 
        systemMapController2?.Initialize(); 
        systemMapHUDController?.Initialize(); 
        planetController?.Initialize();    
        planetGovernmentMissionPresenter?.Initialize();
        marketScreenController?.Initialize();
        refuelPanelController?.Initialize();
        missionScreenController?.Initialize();
        systemShipMarkerController?.Initialize();
        systemShipMarkerController2?.Initialize();
        
        if (galaxyMapScreenRoot2 != null)
            galaxyMapScreenRoot2.SetActive(false);
        if (systemMapScreenRoot != null)
            systemMapScreenRoot.SetActive(false);    
        if (systemMapScreenRoot2 != null)
            systemMapScreenRoot2.SetActive(false);    
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(false);    
        if (planetRoot != null)
            planetRoot.SetActive(false);
        if (governmentRoot != null)
            governmentRoot.SetActive(false);
        if (marketRoot != null)
            marketRoot.SetActive(false);
        if (refuelRoot != null)
            refuelRoot.SetActive(false);

        var currentPlanetId = gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId;
        var currentSystemId = gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;

        if (currentPlanetId != null && currentPlanetId != "")
        {
            eventBus.Publish(new PlanetEnteredEvent(currentPlanetId));
            LogCustom("enter to planet : " + currentPlanetId);
        }
        else if (currentSystemId != null && currentSystemId != "")
        {
            eventBus.Publish(new SystemEnteredEvent(currentSystemId));
            LogCustom("enter to system : " + currentSystemId);
        }
        else
        {
            eventBus.Publish(new GalaxyEnteredEvent());
            LogCustom("enter to galaxy");
        }

        if (IsDebug())
            LogCustom("all meta systems initialized.");
    }
}