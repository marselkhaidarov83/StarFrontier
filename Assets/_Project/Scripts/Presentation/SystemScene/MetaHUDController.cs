using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaHudController :
    CustomMonoBehaviour
{
    /*
     * Формат чисел с обычным пробелом
     * между группами тысяч.
     *
     * Примеры:
     * 1000       -> 1 000
     * 12500      -> 12 500
     * 1000000    -> 1 000 000
     */
    private static readonly NumberFormatInfo
        AmountNumberFormat =
            new NumberFormatInfo
            {
                NumberGroupSeparator = " ",
                NumberGroupSizes =
                    new[] { 3 },
                NumberDecimalDigits = 0,
                NegativeSign = "-"
            };

    [Header("Texts")]

    [SerializeField]
    private TMP_Text creditsText;

    [SerializeField]
    private TMP_Text diamondText;

    [SerializeField]
    private TMP_Text fuelText;

    [SerializeField]
    private TMP_Text cargoText;

    [SerializeField]
    private TMP_Text systemText;

    [SerializeField]
    private TMP_Text planetText;

    [SerializeField]
    private TMP_Text baseText;

    [Header("Images")]

    [SerializeField]
    private Image savedImage;

    [Header("Buttons")]

    [SerializeField]
    private Button gotoMenuButton;

    [Header("Parameters")]

    [SerializeField]
    private float showDuration =
        2f;

    private IGameStateMachine
        _gameStateMachine;

    private IGameSessionService
        gameSessionService;

    private SimpleEventBus
        simpleEventBus;

    private IConfigService
        configService;

    private ISaveService
        _saveService;

    private Coroutine
        currentRoutine;

    public void Initialize()
    {
        _gameStateMachine =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IGameStateMachine>();

        gameSessionService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IGameSessionService>();

        simpleEventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();

        configService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IConfigService>();

        _saveService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<ISaveService>();

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
        {
            gotoMenuButton
                .onClick
                .AddListener(
                    OnGotoMenuClicked);
        }
    }

    private void UnbindButtons()
    {
        if (gotoMenuButton != null)
        {
            gotoMenuButton
                .onClick
                .RemoveListener(
                    OnGotoMenuClicked);
        }
    }

    private void OnGotoMenuClicked()
    {
        if (_gameStateMachine == null)
            return;

        _gameStateMachine.Enter(
            new MenuState());
    }

    public void OnClearSaveClicked()
    {
        if (_saveService == null)
            return;

        _saveService.DeleteSave();
    }

    private void SubscribeToEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Subscribe<
            ActiveShipChangedEvent>(
                OnActiveShipChanged);

        simpleEventBus.Subscribe<
            StarSystemEnteredEvent>(
                OnSystemEntered);

        simpleEventBus.Subscribe<
            PlanetEnteredEvent>(
                OnPlanetEntered);

        simpleEventBus.Subscribe<
            FuelChangedEvent>(
                OnFuelChanged);

        simpleEventBus.Subscribe<
            CreditsChangedEvent>(
                OnCreditsChanged);

        simpleEventBus.Subscribe<
            GameSavedEvent>(
                OnGameSaved);
    }

    private void UnsubscribeFromEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Unsubscribe<
            ActiveShipChangedEvent>(
                OnActiveShipChanged);

        simpleEventBus.Unsubscribe<
            StarSystemEnteredEvent>(
                OnSystemEntered);

        simpleEventBus.Unsubscribe<
            PlanetEnteredEvent>(
                OnPlanetEntered);

        simpleEventBus.Unsubscribe<
            FuelChangedEvent>(
                OnFuelChanged);

        simpleEventBus.Unsubscribe<
            CreditsChangedEvent>(
                OnCreditsChanged);

        simpleEventBus.Unsubscribe<
            GameSavedEvent>(
                OnGameSaved);
    }

    private void OnActiveShipChanged(
        ActiveShipChangedEvent evt)
    {
        Refresh();
    }

    private void OnSystemEntered(
        StarSystemEnteredEvent evt)
    {
        Refresh();
    }

    private void OnPlanetEntered(
        PlanetEnteredEvent evt)
    {
        Refresh();
    }

    private void OnFuelChanged(
        FuelChangedEvent evt)
    {
        Refresh();
    }

    private void OnCreditsChanged(
        CreditsChangedEvent evt)
    {
        Refresh();
    }

    private void OnGameSaved(
        GameSavedEvent evt)
    {
        ShowSaved();
    }

    public void Refresh()
    {
        PlayerState player =
            GetPlayerState();

        RefreshCurrencies(
            player);

        RefreshFuel(
            player);

        RefreshCargo(
            player);

        RefreshLocation(
            player);
    }

    private PlayerState GetPlayerState()
    {
        if (gameSessionService == null ||
            gameSessionService.State == null)
        {
            return null;
        }

        return
            gameSessionService
                .State
                .Player;
    }

    private void RefreshCurrencies(
        PlayerState player)
    {
        if (player == null)
        {
            SetTextSafe(
                creditsText,
                "Кредиты: -");

            SetTextSafe(
                diamondText,
                "Алмазы: -");

            return;
        }

        SetTextSafe(
            creditsText,
            "Кредиты: " + FormatAmount(player.Credits));

        SetTextSafe(
            diamondText,
            "Алмазы: " + FormatAmount(player.Diamonds));
    }

    private void RefreshFuel(
        PlayerState player)
    {
        ShipRuntimeData activeShip =
            GetActiveShip(
                player);

        if (activeShip == null)
        {
            SetTextSafe(
                fuelText,
                "Топливо: -");

            return;
        }

        SetTextSafe(
            fuelText,
            "Топливо: " +
            FormatAmount(
                activeShip.CurrentFuel) +
            " / " +
            FormatAmount(
                activeShip.FuelCapacity));
    }

    private void RefreshCargo(
        PlayerState player)
    {
        ShipRuntimeData activeShip =
            GetActiveShip(
                player);

        if (activeShip == null)
        {
            SetTextSafe(
                cargoText,
                "Груз: -");

            return;
        }

        RuntimeCargoInventory cargo =
            activeShip.Cargo;

        int usedCargo =
            cargo != null
                ? cargo.GetUsedCapacity()
                : 0;

        int maxCargo =
            activeShip.CargoCapacity;

        SetTextSafe(
            cargoText,
            "Груз: " +
            FormatAmount(
                usedCargo) +
            " / " +
            FormatAmount(
                maxCargo));
    }

    private void RefreshLocation(
        PlayerState player)
    {
        if (player == null)
        {
            SetTextSafe(
                systemText,
                "Система:");

            SetTextSafe(
                planetText,
                "Планета:");

            return;
        }

        string starSystemName =
            GetSystemDisplayName(
                player.CurrentSystemId);

        if (!string.IsNullOrWhiteSpace(
                player.CurrentPlanetId))
        {
            SetTextSafe(
                systemText,
                "Планета: " +
                GetPlanetDisplayName(
                    player.CurrentPlanetId) +
                " (" +
                starSystemName +
                ")");

            return;
        }

        if (!string.IsNullOrWhiteSpace(
                player.CurrentSystemId))
        {
            SetTextSafe(
                systemText,
                "Система: " +
                starSystemName);
        }
        else
        {
            SetTextSafe(
                systemText,
                "Система:");
        }
    }

    private ShipRuntimeData GetActiveShip(
        PlayerState player)
    {
        if (player == null ||
            player.PlayerShipState == null)
        {
            return null;
        }

        return
            player.PlayerShipState
                .GetActiveShip();
    }

    private string GetSystemDisplayName(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(
                systemId))
        {
            return "-";
        }

        if (configService == null)
            return systemId;

        IReadOnlyList<StarSystemConfig>
            starSystemConfigs =
                configService
                    .GetAllStarSystems();

        if (starSystemConfigs == null)
            return systemId;

        foreach (
            StarSystemConfig starSystemConfig
            in starSystemConfigs)
        {
            if (starSystemConfig == null)
                continue;

            if (starSystemConfig.Id !=
                systemId)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(
                    starSystemConfig.DisplayName))
            {
                return
                    starSystemConfig
                        .DisplayName;
            }

            return
                starSystemConfig.Id;
        }

        return systemId;
    }

    private string GetPlanetDisplayName(
        string planetId)
    {
        if (string.IsNullOrWhiteSpace(
                planetId))
        {
            return "-";
        }

        if (configService == null)
            return planetId;

        PlanetConfig planetConfig =
            configService
                .GetPlanetConfigById(
                    planetId);

        if (planetConfig == null)
            return planetId;

        if (string.IsNullOrWhiteSpace(
                planetConfig.DisplayName))
        {
            return planetConfig.Id;
        }

        return planetConfig.DisplayName;
    }

    private static string FormatAmount(
        int amount)
    {
        return amount.ToString(
            "N0",
            AmountNumberFormat);
    }

    private static void SetTextSafe(
        TMP_Text target,
        string value)
    {
        if (target != null)
        {
            target.text =
                value;
        }
    }

    public void ShowSaved()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(
                currentRoutine);
        }

        currentRoutine =
            StartCoroutine(
                ShowSavedText());
    }

    private IEnumerator ShowSavedText()
    {
        if (savedImage != null)
        {
            savedImage
                .gameObject
                .SetActive(true);
        }

        yield return new WaitForSeconds(
            showDuration);

        if (savedImage != null)
        {
            savedImage
                .gameObject
                .SetActive(false);
        }

        currentRoutine =
            null;
    }
}