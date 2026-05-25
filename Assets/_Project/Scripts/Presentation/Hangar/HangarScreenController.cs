using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HangarScreenController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Ship Preview")]
    [SerializeField] private Image shipImage;
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private TMP_Text shipDescriptionText;

    [Header("Stats")]
    [SerializeField] private TMP_Text hullText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text cargoText;

    [Header("Ship Buttons")]
    [SerializeField] private Button scoutButton;
    [SerializeField] private Button frigateButton;
    [SerializeField] private Button traderButton;

    [Header("Weapon Buttons")]
    [SerializeField] private Button pulseLaserButton;
    [SerializeField] private Button missileLauncherButton;

    [Header("Module Buttons")]
    [SerializeField] private Button shieldBoosterButton;
    [SerializeField] private Button afterburnerButton;
    [SerializeField] private Button cargoExpansionButton;
    [SerializeField] private Button energyCapacitorButton;
    [SerializeField] private Button hullReinforcementButton;

    [Header("Actions")]
    [SerializeField] private Button repairButton;
    [SerializeField] private Button backButton;

    [Header("Message")]
    [SerializeField] private TMP_Text messageText;

    private IHangarService _hangarService;

    private void Awake()
    {
        _hangarService = Bootstrapper.Instance.ServiceRegistry.Get<IHangarService>();

        scoutButton?.onClick.AddListener(() => SwitchShip("runtime_ship_001"));
        frigateButton?.onClick.AddListener(() => SwitchShip("runtime_ship_002"));
        traderButton?.onClick.AddListener(() => SwitchShip("runtime_ship_003"));

        pulseLaserButton?.onClick.AddListener(() => EquipWeapon("weapon_laser_pulse_01"));
        missileLauncherButton?.onClick.AddListener(() => EquipWeapon("weapon_missile_light_01"));

        shieldBoosterButton?.onClick.AddListener(() => EquipModule("module_shield_booster_small_01"));
        afterburnerButton?.onClick.AddListener(() => EquipModule("module_afterburner_01"));
        cargoExpansionButton?.onClick.AddListener(() => EquipModule("module_cargo_expansion_01"));
        energyCapacitorButton?.onClick.AddListener(() => EquipModule("module_energy_capacitor_01"));
        hullReinforcementButton?.onClick.AddListener(() => EquipModule("module_hull_reinforcement_01"));

        repairButton?.onClick.AddListener(Repair);    
        backButton?.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Open()
    {
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        root.SetActive(false);
    }

    private void SwitchShip(string shipId)
    {
        Debug.Log("[HangarScreenController] SwitchShip " + shipId);
        var result = _hangarService.SwitchShip(shipId);

        if (!result.Success)
        {
            ShowError(result.Error);
            return;
        }

        ShowMessage("Ship switched");
        Refresh();
    }

    private void EquipWeapon(string weaponId)
    {
        var result = _hangarService.EquipWeapon(weaponId);

        if (!result.Success)
        {
            ShowError(result.Error);
            return;
        }

        ShowMessage("Weapon equipped");
        Refresh();
    }

    private void EquipModule(string moduleId)
    {
        var result = _hangarService.EquipModule(moduleId);

        if (!result.Success)
        {
            ShowError(result.Error);
            return;
        }

        ShowMessage("Module equipped");
        Refresh();
    }

    private void Repair()
    {
        var result = _hangarService.RepairActiveShip();

        if (!result.Success)
        {
            ShowError(result.Error);
            return;
        }

        ShowMessage("Ship repaired");
        Refresh();
    }

    private void Refresh()
    {
        if (_hangarService == null)
            return;

        var shipData = _hangarService.GetActiveShipData();
        var stats = _hangarService.GetActiveShipStats();

        if (shipData == null || stats == null)
        {
            ShowMessage("No active ship");
            return;
        }

        shipNameText.text = shipData.DisplayName;
        shipDescriptionText.text = shipData.Description;

        if (shipData.Icon != null)
        {
            shipImage.sprite = shipData.Icon;
            shipImage.enabled = true;
        }
        else
        {
            shipImage.enabled = false;
        }

        hullText.text = $"Hull: {stats.MaxHull}";
        shieldText.text = $"Shield: {stats.MaxShield}";
        energyText.text = $"Energy: {stats.MaxEnergy}";
        speedText.text = $"Speed: {stats.Speed:0.0}";
        cargoText.text = $"Cargo: {stats.CargoCapacity}";
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }

    private void ShowError(HangarError error)
    {
        var message = error switch
        {
            HangarError.ActiveShipMissing => "No active ship",
            HangarError.ShipNotOwned => "Ship is not owned",
            HangarError.ShipNotFound => "Ship not found",
            HangarError.WeaponNotFound => "Weapon not found",
            HangarError.ModuleNotFound => "Module not found",
            HangarError.NoWeaponSlots => "No free weapon slots",
            HangarError.NoModuleSlots => "No free module slots",
            HangarError.AlreadyEquipped => "Already equipped",
            HangarError.NotEquipped => "Not equipped",
            HangarError.InvalidWeaponId => "Invalid weapon",
            HangarError.InvalidModuleId => "Invalid module",
            _ => "Hangar operation failed"
        };

        ShowMessage(message);
    }
}