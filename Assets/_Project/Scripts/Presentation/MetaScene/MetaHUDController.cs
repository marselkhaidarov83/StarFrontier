using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaHudController : CustomMonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private TMP_Text cargoText;
    [SerializeField] private TMP_Text systemText;
    [SerializeField] private TMP_Text planetText;
    [SerializeField] private TMP_Text baseText;
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
        Refresh();
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
        simpleEventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
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
        simpleEventBus.Unsubscribe<StarSystemEnteredEvent>(OnSystemEntered);
        simpleEventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        simpleEventBus.Unsubscribe<FuelChangedEvent>(OnFuelChanged);
        simpleEventBus.Unsubscribe<CreditsChangedEvent>(OnCreditsChanged);
        simpleEventBus.Unsubscribe<GameSavedEvent>(OnGameSaved);
    }

    private void OnActiveShipChanged(ActiveShipChangedEvent evt)
    {
        Refresh();
    }

    private void OnSystemEntered(StarSystemEnteredEvent evt)
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
        if (gameSessionService.State.Player != null)
            creditsText.text = $"Кредиты: {gameSessionService.State.Player.Credits}";
        else
            creditsText.text = "Кредиты: -";

        if (gameSessionService.State.Player.PlayerShipState.GetActiveShip() != null)
            fuelText.text = $"Топливо: {gameSessionService.State.Player.PlayerShipState.GetActiveShip().CurrentFuel} / {gameSessionService.State.Player.PlayerShipState.GetActiveShip().FuelCapacity}";
        else
            fuelText.text = "Топливо: -";

        if (gameSessionService.State.Player != null &&
                gameSessionService.State.Player.PlayerShipState.GetActiveShip() != null)
        {
            RuntimeCargoInventory cargo = gameSessionService.State.Player.PlayerShipState.GetActiveShip().Cargo;
            int usedCargo = cargo != null ? cargo.GetUsedCapacity() : 0;
            int maxCargo = gameSessionService.State.Player.PlayerShipState.GetActiveShip().CargoCapacity;
            cargoText.text = $"Груз: {usedCargo} / {maxCargo}";

        }
        else
            cargoText.text = "Груз: -";

        if (gameSessionService.State.Player != null)
        {
            if (gameSessionService.State.Player.CurrentSystemId != null)
            {
                IReadOnlyList<StarSystemConfig> starSystemConfigs = configService.GetAllStarSystems();
                foreach (StarSystemConfig starSystemConfig in configService.GetAllStarSystems())
                    if (starSystemConfig.Id ==
                            gameSessionService.State.Player.CurrentSystemId)
                    {
                        systemText.text = $"Система: {starSystemConfig.DisplayName ?? "-"}";
                        break;
                    }
            }
            else
                systemText.text = "Система:";

            if (!string.IsNullOrWhiteSpace(gameSessionService.State.Player.CurrentPlanetId))
            {
                planetText.text = $"Планета: {GetPlanetDisplayName(gameSessionService.State.Player.CurrentPlanetId)}";
            }
            else
            {
                planetText.text = "Планета:";
            }
        }
        else
        {
            systemText.text = "Система:";
            planetText.text = "Планета:";
        }
    }

    private string GetPlanetDisplayName(string planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return "-";

        if (configService == null)
            return planetId;

        PlanetConfig planetConfig = configService.GetPlanetConfigById(planetId);

        if (planetConfig == null)
            return planetId;

        if (string.IsNullOrWhiteSpace(planetConfig.DisplayName))
            return planetConfig.Id;

        return planetConfig.DisplayName;
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