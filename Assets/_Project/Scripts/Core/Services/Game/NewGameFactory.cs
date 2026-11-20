using System.Collections.Generic;
using UnityEngine;

public class NewGameFactory
{
    private const string FallbackStarterShipConfigId = "ally_ranger_L01_01";
    private readonly IConfigService _configService;

    public NewGameFactory()
    {
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public GameRuntimeState CreateNewGame()
    {
        var save = new GameRuntimeState
        {
            Player = CreatePlayerProfile(_configService.NewGameConfig),
            Galaxy = GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(
                _configService.GalaxyConfig,
                false,
                false,
                IsMobileTestMode,
                _configService.DebugSettings != null ? _configService.DebugSettings.mobileTestUnlockedSectorCount : 0,
                _configService.DebugSettings != null ? _configService.DebugSettings.mobileTestAlliedSectorCount : 0)
        };

        return save;
    }

    private bool IsMobileTestMode =>
        _configService.DebugSettings != null && _configService.DebugSettings.mobileTestMode;

    private PlayerState CreatePlayerProfile(NewGameConfig newGameConfig)
    {
        return new PlayerState
        {
            Credits = IsMobileTestMode
                ? Mathf.Max(0, _configService.DebugSettings.mobileTestCredits)
                : newGameConfig.StartCredit,
            Diamonds = IsMobileTestMode
                ? Mathf.Max(0, _configService.DebugSettings.mobileTestDiamonds)
                : Mathf.Max(0, newGameConfig.StartDiamonds),
            CurrentSystemId = newGameConfig.StartSystem.Id,
            SystemMapShipPosition = newGameConfig.StartShipPosition,
            SystemMapShipDirection = GetStartShipDirection(newGameConfig),
            PlayerShipState = CreateStarterShip(newGameConfig)
        };
    }

    private ShipRuntimeState CreateStarterShip(NewGameConfig newGameConfig)
    {
        AllyConfig starterShipConfig =
            ResolveStarterShipConfig(newGameConfig);

        HullConfig starterHull = starterShipConfig?.HullGroup?.PickRandom();
        if (starterHull == null) throw new System.InvalidOperationException("Starter ship has no hull group.");
        var installedHull = starterHull.CreateRuntimeState("installed-hull:runtime_ship_001");
        int hullCapacity = installedHull.MaximumDurability;
        int cargoCapacity = installedHull.CargoCapacity;
        int shieldCapacity = 0;
        int energyCapacity = 0;

        var ship = new ShipRuntimeData
        {
            ShipId = "runtime_ship_001",
            AllyConfigId = starterShipConfig != null
                ? starterShipConfig.Id
                : FallbackStarterShipConfigId,
            InstalledHull = installedHull,
            CurrentHull = hullCapacity,
            CurrentShield = shieldCapacity,
            CurrentEnergy = energyCapacity,
            CargoCapacity = cargoCapacity,
            HullCapacity = hullCapacity,
            EquippedWeaponIds = CreateStarterWeaponIds(starterShipConfig),
            EquippedModuleIds = new List<string>()
        };
        ShipLoadoutRules.Initialize(ship, starterShipConfig, _configService.GetWeaponConfigById, starterHull);
        var tankItem = ShipLoadoutRules.Component(ship, ShipComponentSlot.FuelTank);
        string tankId = tankItem?.ComponentConfigId;
        ship.FuelCapacity = !string.IsNullOrEmpty(tankId) && _configService.GetItemConfigById(tankId) is ShipEquipmentConfig tank
            ? tankItem.GetComponentStats(tank).FuelCapacity
            : 0;
        int startFuel = IsMobileTestMode ? _configService.DebugSettings.mobileTestCurrentFuel : newGameConfig.StartFuel;
        ship.CurrentFuel = Mathf.Clamp(startFuel, 0, ship.FuelCapacity);
        var shieldItem = ShipLoadoutRules.Component(ship, ShipComponentSlot.ShieldGenerator);
        var shieldConfig = _configService.GetItemConfigById(shieldItem?.ComponentConfigId) as ShipEquipmentConfig;
        ship.CurrentShield = shieldItem?.GetComponentStats(shieldConfig)?.ShieldCapacity ?? 0;

        return new ShipRuntimeState
        {
            ActiveShipId = ship.ShipId,
            OwnedShips = new List<ShipRuntimeData> { ship }
        };
    }

    private AllyConfig ResolveStarterShipConfig(
        NewGameConfig newGameConfig)
    {
        if (IsMobileTestMode && _configService.DebugSettings.mobileTestStarterShip != null)
            return _configService.DebugSettings.mobileTestStarterShip;

        if (newGameConfig != null &&
            newGameConfig.StarterShipConfig != null)
        {
            return newGameConfig.StarterShipConfig;
        }

        return _configService.GetAllyConfigById(FallbackStarterShipConfigId);
    }

    private static Vector3 GetStartShipDirection(
        NewGameConfig newGameConfig)
    {
        if (newGameConfig == null ||
            newGameConfig.StartSystem == null ||
            newGameConfig.StartSystem.Sun == null)
        {
            return Vector3.up;
        }

        Vector2 shipPosition =
            new Vector2(
                newGameConfig.StartShipPosition.x,
                newGameConfig.StartShipPosition.y);

        Vector2 sunPosition =
            newGameConfig
                .StartSystem
                .Sun
                .LocalOffset;

        Vector2 directionToSun =
            sunPosition -
            shipPosition;

        if (float.IsNaN(directionToSun.x) ||
            float.IsNaN(directionToSun.y) ||
            float.IsInfinity(directionToSun.x) ||
            float.IsInfinity(directionToSun.y) ||
            directionToSun.sqrMagnitude <= 0.0001f)
        {
            return Vector3.up;
        }

        Vector2 normalizedDirection =
            directionToSun.normalized;

        return new Vector3(
            normalizedDirection.x,
            normalizedDirection.y,
            0f);
    }

    private List<string> CreateStarterWeaponIds(
        AllyConfig starterShipConfig)
    {
        var weaponIds =
            new List<string>();

        if (starterShipConfig != null && starterShipConfig.WeaponConfigs != null)
        {
            IReadOnlyList<WeaponConfig> weapons =
                starterShipConfig.WeaponConfigs;

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponConfig weapon =
                    weapons[i];

                if (weapon == null)
                    continue;

                if (string.IsNullOrWhiteSpace(weapon.Id))
                    continue;

                weaponIds.Add(weapon.Id);
            }
        }

        if (weaponIds.Count == 0)
            weaponIds.Add("weapon_common_pulse_bronze_L01_01");

        return weaponIds;
    }
}
