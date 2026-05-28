using System.Collections.Generic;

public sealed class NewGameFactory2A
{
    public SaveData CreateNewGame()
    {
        return new SaveData
        {
            PlayerProfile = CreatePlayerProfile()
        };
    }

    private PlayerProfileData CreatePlayerProfile()
    {
        return new PlayerProfileData
        {
            Credits = 1000,
            CurrentSystemId = "system_heliosGate_01",
            PlayerShipState = CreateStarterShip()
        };
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
                }
            }
        };
    }
}