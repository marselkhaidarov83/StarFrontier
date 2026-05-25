using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaHudController : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private TMP_Text cargoText;
    [SerializeField] private TMP_Text systemText;
    [SerializeField] private TMP_Text planetText;
    [Header("Images")]
    [SerializeField] private Image savedImage;
    [Header("Buttons")]
    [SerializeField] private Button gotoMenuButton;
    [Header("Parameters")]
    [SerializeField] private float showDuration = 2f;


    private IGameStateMachine _gameStateMachine;
    private IGameSessionService gameSessionService;
    private SimpleEventBus simpleEventBus;
    private IConfigService configService;
    private ISaveService _saveService;
    private Coroutine currentRoutine;

    public void Initialize()
    {
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();

        BindButtons();
        UnsubscribeFromEvents();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnbindButtons();
        UnsubscribeFromEvents();
    }

    private void BindButtons()
    {
        if (gotoMenuButton != null)
            gotoMenuButton.onClick.AddListener(OnGotoMenuClicked);
    }

    private void UnbindButtons()
    {
        if (gotoMenuButton != null)
            gotoMenuButton.onClick.RemoveAllListeners();
    }

    private void OnGotoMenuClicked()
    {
        _gameStateMachine.Enter(new MenuState());
    }

    public void OnClearSaveClicked()
    {
        _saveService.DeleteSave();
    }    

    private void SubscribeToEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Subscribe<ActiveShipChangedEvent>(OnActiveShipChanged);
        simpleEventBus.Subscribe<SystemEnteredEvent>(OnSystemEntered);
        simpleEventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        simpleEventBus.Subscribe<FuelChangedEvent>(OnFuelChanged);
        simpleEventBus.Subscribe<CreditsChangedEvent>(OnCreditsChanged);
        simpleEventBus.Subscribe<GameSavedEvent>(OnGameSaved);
    }

    private void UnsubscribeFromEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Unsubscribe<ActiveShipChangedEvent>(OnActiveShipChanged);
        simpleEventBus.Unsubscribe<SystemEnteredEvent>(OnSystemEntered);
        simpleEventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        simpleEventBus.Unsubscribe<FuelChangedEvent>(OnFuelChanged);
        simpleEventBus.Unsubscribe<CreditsChangedEvent>(OnCreditsChanged);
        simpleEventBus.Unsubscribe<GameSavedEvent>(OnGameSaved);
    }    

    private void OnActiveShipChanged(ActiveShipChangedEvent evt)
    {
        Refresh();
    }

    private void OnSystemEntered(SystemEnteredEvent evt)
    {
        Refresh();
    }

    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        Refresh();
    }

    private void OnFuelChanged(FuelChangedEvent evt)
    {
        Refresh();
    }

    private void OnCreditsChanged(CreditsChangedEvent evt)
    {
        Refresh();
    }

    private void OnGameSaved(GameSavedEvent evt)
    {
        ShowSaved();
    }

    public void Refresh()
    {
        if (gameSessionService.CurrentSave.PlayerProfile != null)
            creditsText.text = $"Credits: {gameSessionService.CurrentSave.PlayerProfile.Credits}";
        else
            creditsText.text = "Credits: -";

        if (gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip() != null)
            fuelText.text = $"Fuel: {gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().CurrentFuel} / {gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().FuelCapacity}";
        else
            fuelText.text = "Fuel: -";

        if (gameSessionService.CurrentSave.PlayerProfile != null &&
                gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip() != null)
        {
            RuntimeCargoInventory cargo = gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo;
            int usedCargo = cargo != null ? cargo.GetUsedCapacity() : 0;
            int maxCargo = gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().CargoCapacity;
            cargoText.text = $"Cargo: {usedCargo} / {maxCargo}";
            
        }
        else
            cargoText.text = "Cargo: -";

        if (gameSessionService.CurrentSave.PlayerProfile != null)
        {
            if (gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId != null)
            {
                IReadOnlyList<StarSystemConfig> starSystemConfigs = configService.GetAllStarSystems();
                foreach (StarSystemConfig starSystemConfig in configService.GetAllStarSystems())
                    if (starSystemConfig.Id ==
                            gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId)
                    {
                        systemText.text = $"System: {starSystemConfig.DisplayName ?? "-"}";
                        break;
                    }
            }
            else
                systemText.text = "System: -";

            if (gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId != null)
                planetText.text = $"Planet: {gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId ?? "-"}";
            else
                planetText.text = "Planet: -";
        }
        else
        {
            systemText.text = "System: -";
            planetText.text = "Planet: -";
        }
    }

    public void ShowSaved()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowSavedText());
    }

    private IEnumerator ShowSavedText()
    {
        if (savedImage != null)
            savedImage.gameObject.SetActive(true);

        yield return new WaitForSeconds(showDuration);

        if (savedImage != null)
            savedImage.gameObject.SetActive(false);

        currentRoutine = null;
    }
}