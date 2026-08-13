using System.Collections.Generic;

public class NewGameFactory
{
    private const string FallbackStarterAllyConfigId = "ally_ranger_L01_01";
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
                Bootstrapper.Instance.SectorAllOpened,
                Bootstrapper.Instance.RouteAllUnlocked)
        };

        return save;
    }

    private PlayerState CreatePlayerProfile(NewGameConfig newGameConfig)
    {
        return new PlayerState
        {
            Credits = newGameConfig.StartCredit,
            CurrentSystemId = newGameConfig.StartSystem.Id,
            PlayerShipState = CreateStarterShip(newGameConfig)
        };
    }

    private ShipRuntimeState CreateStarterShip(NewGameConfig newGameConfig)
    {
        AllyConfig starterAllyConfig =
            ResolveStarterAllyConfig();

        int hullCapacity =
            starterAllyConfig != null
                ? starterAllyConfig.BaseHull
                : 110;

        int shieldCapacity =
            starterAllyConfig != null
                ? starterAllyConfig.BaseShield
                : 80;

        int energyCapacity =
            starterAllyConfig != null
                ? starterAllyConfig.BaseEnergy
                : 110;

        int cargoCapacity =
            starterAllyConfig != null
                ? starterAllyConfig.BaseCargoCapacity
                : 30;

        return new ShipRuntimeState
        {
            ActiveShipId = "runtime_ship_001",
            OwnedShips = new List<ShipRuntimeData>
            {
                new ShipRuntimeData
                {
                    ShipId = "runtime_ship_001",
                    AllyConfigId = starterAllyConfig != null
                        ? starterAllyConfig.Id
                        : FallbackStarterAllyConfigId,
                    CurrentHull = hullCapacity,
                    CurrentShield = shieldCapacity,
                    CurrentEnergy = energyCapacity,
                    CurrentFuel = newGameConfig.CurrentFuel,
                    FuelCapacity = newGameConfig.FuelCapacity,
                    CargoCapacity = cargoCapacity,
                    HullCapacity = hullCapacity,
                    EquippedWeaponIds = CreateStarterWeaponIds(starterAllyConfig),
                    EquippedModuleIds = new List<string>()
                }
            }
        };
    }

    private AllyConfig ResolveStarterAllyConfig()
    {
        if (_configService.StarterAllyConfig != null)
            return _configService.StarterAllyConfig;

        return _configService.GetAllyConfigById(FallbackStarterAllyConfigId);
    }

    private List<string> CreateStarterWeaponIds(
        AllyConfig starterAllyConfig)
    {
        var weaponIds =
            new List<string>();

        if (starterAllyConfig != null && starterAllyConfig.WeaponConfigs != null)
        {
            IReadOnlyList<WeaponConfig> weapons =
                starterAllyConfig.WeaponConfigs;

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
