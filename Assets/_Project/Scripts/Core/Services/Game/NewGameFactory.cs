using System.Collections.Generic;

public class NewGameFactory
{
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
        return new ShipRuntimeState
        {
            ActiveShipId = "runtime_ship_001",
            OwnedShips = new List<ShipRuntimeData>
            {
                new ShipRuntimeData
                {
                    ShipId = "runtime_ship_001",
                    AllyConfigId = "ally_ranger_L01_01",
                    CurrentHull = 110,
                    CurrentShield = 80,
                    CurrentEnergy = 110,
                    CurrentFuel = newGameConfig.CurrentFuel,
                    FuelCapacity = newGameConfig.FuelCapacity,
                    CargoCapacity = 30,
                    HullCapacity = 110,
                    EquippedWeaponIds = new List<string>
                    {
                        "weapon_common_pulse_bronze_L01_01"
                    },
                    EquippedModuleIds = new List<string>()
                }
            }
        };
    }
}
