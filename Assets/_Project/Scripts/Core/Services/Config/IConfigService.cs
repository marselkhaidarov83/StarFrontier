using System.Collections.Generic;

public interface IConfigService
{
    StarSystemLink GetCurrentStarSystemLink(string targetSystemId);
    bool ContainsStarSystem(string systemId);
    IReadOnlyList<StarSystemConfig> GetAllStarSystems();
    bool TryGetStarSystem(string systemId, out StarSystemConfig config);
    StarSystemConfig GetStarSystemConfigById(string systemId);
    StarSystemConfig GetCurrentSystemConfig();

    IReadOnlyList<PlanetConfig> GetAllPlanets();
    PlanetConfig GetPlanetConfigById(string planetId);
    PlanetConfig GetCurrentPlanetConfig();

    IReadOnlyList<ItemConfig> GetAllItems();
    ItemConfig GetItemConfigById(string id);

    IReadOnlyList<ShipConfig> GetAllShips();
    ShipConfig GetShipConfigById(string shipId);

    EnemyConfig GetEnemyConfigById(string id);
    AllyConfig GetAllyConfigById(string id);
    PirateConfig GetPirateConfigById(string id);
    AllySpawnRuleConfig GetAllySpawnRuleConfigById(string id);
    PirateGroupSpawnRuleConfig GetPirateGroupSpawnRuleConfigById(string id);

    IReadOnlyList<ModuleConfig> GetAllModules();
    ModuleConfig GetModuleConfigById(string moduleId);

    IReadOnlyList<WeaponConfig> GetAllWeapons();
    WeaponConfig GetWeaponConfigById(string weaponId);

    GameBootstrapConfig GameBootstrapConfig { get; }
    SaveConfig SaveConfig { get; }
    DebugConfig DebugConfig { get; }
    GalaxyConfig GalaxyConfig { get; }
}