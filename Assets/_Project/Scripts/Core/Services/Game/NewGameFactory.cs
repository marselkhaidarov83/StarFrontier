using System.Collections.Generic;

public class NewGameFactory
{
    public SaveData CreateNewGame()
    {
        var save = new SaveData
        {
            PlayerProfile = CreatePlayerProfile()
        };
        return save;
    }

    private PlayerProfileData CreatePlayerProfile()
    {
        var starterShip = CreateStarterShip();

        var profile = new PlayerProfileData
        {
            Credits = 1000,
            CurrentSystemId = "system_heliosGate_01",
            PlayerShipState = starterShip
        };

        return profile;
    }

    private ShipRuntimeState CreateStarterShip()
    {
        return new ShipRuntimeState
        {
            ActiveShipId = "runtime_ship_001",
            OwnedShips = new List<ShipRuntimeData>
            {
                new ShipRuntimeData
                {
                    ShipId = "runtime_ship_001",
                    ShipConfigId = "ship_scout_01",
                    CurrentHull = 70,
                    CurrentShield = 50,
                    CurrentEnergy = 100,
                    CurrentFuel = 3,
                    FuelCapacity = 20,
                    CargoCapacity = 5,
                    HullCapacity = 100,
                    EquippedWeaponIds = new List<string>
                    {
                        "weapon_missile_light_01"
                    },
                    EquippedModuleIds = new List<string>
                    {
                        "module_shield_booster_01"
                    }
                },
                new ShipRuntimeData
                {
                    ShipId = "runtime_ship_002",
                    ShipConfigId = "ship_frigate_01",
                    CurrentHull = 80,
                    CurrentShield = 60,
                    CurrentEnergy = 110,
                    CurrentFuel = 4,
                    FuelCapacity = 25,
                    CargoCapacity = 6,
                    HullCapacity = 110,
                    EquippedWeaponIds = new List<string>
                    {
                        "weapon_laser_pulse_01"
                    },
                    EquippedModuleIds = new List<string>()
                },
                new ShipRuntimeData
                {
                    ShipId = "runtime_ship_003",
                    ShipConfigId = "ship_trader_01",
                    CurrentHull = 90,
                    CurrentShield = 70,
                    CurrentEnergy = 120,
                    CurrentFuel = 5,
                    FuelCapacity = 30,
                    CargoCapacity = 10,
                    HullCapacity = 120,
                    EquippedWeaponIds = new List<string>
                    {
                        "weapon_missile_light_01"
                    },
                    EquippedModuleIds = new List<string>()
                }
            }
        };
    }
}