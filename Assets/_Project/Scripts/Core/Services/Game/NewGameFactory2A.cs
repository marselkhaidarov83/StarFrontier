using System.Collections.Generic;
using UnityEngine;

public sealed class NewGameFactory2A : CustomService
{
    private GalaxyConfig _galaxyConfig;

    public NewGameFactory2A()
    {
        _galaxyConfig = Bootstrapper2A.Instance.ServiceRegistry.Get<IConfigService>().GalaxyConfig;
    }

    public SaveData CreateNewGame()
    {
        SaveData saveData = new SaveData
        {
            PlayerProfile = CreatePlayerProfile(),
            GameState = new ()
        };

        EnsureGalaxyState(saveData.GameState);
        return saveData;
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

    public void EnsureGalaxyState(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogError("SaveService: GameState is null. Cannot ensure GalaxyState.");
            return;
        }

        LogCustom("gameState.Galaxy = " + gameState.Galaxy);
        if (gameState.Galaxy != null)
            return;

        var galaxySimulationService = new GalaxySimulationService();
        gameState.Galaxy = galaxySimulationService.CreateGalaxyState(_galaxyConfig);

        LogCustom("GalaxyState was missing and has been created from GalaxyConfig.");
    }
}