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

        int hullCapacity =
            starterShipConfig != null
                ? starterShipConfig.BaseHull
                : 110;

        int shieldCapacity =
            starterShipConfig != null
                ? starterShipConfig.BaseShield
                : 80;

        int energyCapacity =
            starterShipConfig != null
                ? starterShipConfig.BaseEnergy
                : 110;

        int cargoCapacity =
            starterShipConfig != null
                ? starterShipConfig.BaseCargoCapacity
                : 30;

        int fuelCapacity = IsMobileTestMode
            ? Mathf.Max(1, _configService.DebugSettings.mobileTestFuelCapacity)
            : newGameConfig.FuelCapacity;
        int currentFuel = IsMobileTestMode
            ? Mathf.Clamp(_configService.DebugSettings.mobileTestCurrentFuel, 0, fuelCapacity)
            : newGameConfig.CurrentFuel;

        return new ShipRuntimeState
        {
            ActiveShipId = "runtime_ship_001",
            OwnedShips = new List<ShipRuntimeData>
            {
                new ShipRuntimeData
                {
                    ShipId = "runtime_ship_001",
                    AllyConfigId = starterShipConfig != null
                        ? starterShipConfig.Id
                        : FallbackStarterShipConfigId,
                    CurrentHull = hullCapacity,
                    CurrentShield = shieldCapacity,
                    CurrentEnergy = energyCapacity,
                    CurrentFuel = currentFuel,
                    FuelCapacity = fuelCapacity,
                    CargoCapacity = cargoCapacity,
                    HullCapacity = hullCapacity,
                    EquippedWeaponIds = CreateStarterWeaponIds(starterShipConfig),
                    EquippedModuleIds = new List<string>()
                }
            }
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
