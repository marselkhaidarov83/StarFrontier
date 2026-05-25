using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RefuelPanelController : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private TMP_Text refuelCostText;
    [SerializeField] private TMP_Text refuelWarningText;
    [SerializeField] private Slider refuelCountSlider;

    [Header("Buttons")]
    [SerializeField] private Button refuelButton;

    private SimpleEventBus eventBus;
    private IRefuelService _refuelService;
    private IGameSessionService gameSessionService;

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _refuelService = Bootstrapper.Instance.ServiceRegistry.Get<IRefuelService>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        BindButtons();
        BindSliders();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnbindButtons();
        UnsubscribeFromEvents();
    }

    private void BindButtons()
    {
        if (refuelButton != null)
            refuelButton.onClick.AddListener(HandleRefuelClicked);
    }

    private void BindSliders()
    {
        refuelCountSlider.onValueChanged.AddListener(UpdateQuantity);
    }

    private void UpdateQuantity(float value)
    {
        int amount = Mathf.RoundToInt(value);

        fuelText.text = $"Fuel to refuel: {amount} / {refuelCountSlider.maxValue}";
        refuelCostText.text = $"Cost: {amount * _refuelService.GetFuelUnitPrice()} / {refuelCountSlider.maxValue * _refuelService.GetFuelUnitPrice()}";
    }

    private void UnbindButtons()
    {
        if (refuelButton != null)
            refuelButton.onClick.RemoveAllListeners();
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<RefuelEnteredEvent>(OnRefuelEntered);
        eventBus.Subscribe<FuelChangedEvent>(OnFuelChanged);
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<RefuelEnteredEvent>(OnRefuelEntered);
        eventBus.Unsubscribe<FuelChangedEvent>(OnFuelChanged);
    }  

    private void OnRefuelEntered(RefuelEnteredEvent evt)
    {
        Refresh();
    }    
    private void OnFuelChanged(FuelChangedEvent evt)
    {
        Refresh();
    }    
    public void Refresh()
    {
        if (_refuelService == null)
            return;

        PlayerProfileData player = gameSessionService.CurrentSave.PlayerProfile;

        int fuelCount = 0;
        int maxValue = 0;
        if (refuelCountSlider != null)
        {
            fuelCount = player.PlayerShipState.GetActiveShip().FuelCapacity - player.PlayerShipState.GetActiveShip().CurrentFuel;
            if (fuelCount > 0)
            {
                refuelCountSlider.maxValue = maxValue = fuelCount;                
                refuelCountSlider.minValue = 1;
                refuelCountSlider.value = fuelCount;
            }
            else
            {
                refuelCountSlider.minValue = 0;                
                refuelCountSlider.maxValue = maxValue = 0;     
                refuelCountSlider.value = 0;           
            }
        }

        fuelText.text = $"Fuel to refuel: {maxValue} / {maxValue}";
        refuelCostText.text = $"Cost: {maxValue * _refuelService.GetFuelUnitPrice()} / {maxValue * _refuelService.GetFuelUnitPrice()}";

        bool canRefuel = _refuelService.CanRefuel(1);
        if (refuelButton != null)
            refuelButton.gameObject.SetActive(canRefuel);

        refuelWarningText.text = BuildWarningText();
    }

    private void HandleRefuelClicked()
    {
        RefuelResult result = _refuelService.Refuel(Mathf.RoundToInt(refuelCountSlider.value));
        ShowResult(result);
    }

    private void ShowResult(RefuelResult result)
    {
        if (result.IsSuccess)
        {
            refuelWarningText.text = $"Refueled + {result.fuelAdded}";
            return;
        }

        switch (result.resultType)
        {
            case RefuelResultType.InvalidFuelCapacity:
                refuelWarningText.text = "Invalid Fuel Capacity";
                break;
            case RefuelResultType.InvalidFuelPrice:
                refuelWarningText.text = "Invalid Fuel Price";
                break;
            case RefuelResultType.FuelAlreadyFull:
                refuelWarningText.text = "Fuel tank is full";
                break;
            case RefuelResultType.NotEnoughCredits:
                refuelWarningText.text = "Not enough credits";
                break;
            default:
                refuelWarningText.text = "Refuel failed";
                break;
        }
    }

    private string BuildWarningText()
    {
        ShipRuntimeData ship = gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip();

        if (ship.CurrentFuel >= ship.FuelCapacity)
            return "Fuel tank is full";

        if (!_refuelService.CanRefuel(1))
            return "Refuel unavailable";

        return string.Empty;
    }
}