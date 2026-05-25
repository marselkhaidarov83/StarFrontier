using TMPro;
using UnityEngine;

public class PlanetGovernmentMissionPresenter : CustomMonoBehaviour
{
    [SerializeField] private TMP_Text governmentPlanetTitleText;
    [SerializeField] private TMP_Text governmentOfferTitleText;
    [SerializeField] private TMP_Text governmentOfferDescriptionText;
    [SerializeField] private TMP_Text governmentOfferStateText;

    private IPlanetGovernmentMissionService _governmentMissionService;
    private StarSystemConfig _systemConfig;
    private PlanetConfig _planetConfig;
    private IConfigService _configService;
    private SimpleEventBus _eventBus;

    public void Initialize()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _governmentMissionService = Bootstrapper.Instance.ServiceRegistry.Get<IPlanetGovernmentMissionService>();

        SubscribeToEvents();            
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
    }

    private void UnsubscribeFromEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
    } 

    private void OnGovernmentEntered(GovernmentEnteredEvent evt)
    {
        _systemConfig = _configService.GetCurrentSystemConfig();
        _planetConfig = _configService.GetCurrentPlanetConfig();

        CheckOffer(); 
    }

    public void CheckOffer()
    {
        PlanetOfferedMissionData offer = _governmentMissionService.GetOrCreateGovernmentOffer(_systemConfig, _planetConfig);
        RefreshUi(offer);
    }

    public void AcceptOffer()
    {
        LogCustom("[PlanetGovernmentMissionPresenter] _planetConfig = " + _planetConfig);
        LogCustom("[PlanetGovernmentMissionPresenter] _planetConfig.Id = " + _planetConfig.Id);
        bool accepted = _governmentMissionService.AcceptGovernmentOffer(_planetConfig.Id);
        LogCustom("[PlanetGovernmentMissionPresenter] AcceptOffer.accepted = " + accepted);
        if (governmentOfferStateText != null)
            governmentOfferStateText.text = accepted ? "Mission accepted." : "Mission accept failed.";

        if (_eventBus != null)
        {
            LogCustom("[PlanetGovernmentMissionPresenter] AcceptOffer.accepted = published");
            _eventBus.Publish(new MissionAcceptedEvent());
        }
    }

    private void RefreshUi(PlanetOfferedMissionData offer)
    {
        if (governmentPlanetTitleText != null)
            governmentPlanetTitleText.text = $"Government: {_planetConfig.Id}";

        if (offer?.OfferedMission == null)
        {
            if (governmentOfferTitleText != null)
                governmentOfferTitleText.text = "No offer";
            if (governmentOfferDescriptionText != null)
                governmentOfferDescriptionText.text = string.Empty;
            if (governmentOfferStateText != null)
                governmentOfferStateText.text = "No mission available now.";
            return;
        }

        if (governmentOfferTitleText != null) governmentOfferTitleText.text = offer.OfferedMission.Title;
        if (governmentOfferDescriptionText != null) governmentOfferDescriptionText.text = offer.OfferedMission.Description;
        if (governmentOfferStateText != null) governmentOfferStateText.text = $"Offered mission from {_planetConfig.Id}";
    }
}