using System.Collections.Generic;

public interface IConfigService
{
    IReadOnlyList<MarketEquipmentCatalogEntry> EquipmentCatalog { get; }
    IReadOnlyList<MarketHullCatalogEntry> HullCatalog { get; }
    MissionGameplayConfig MissionGameplay { get; }
    GameTimeConfig GameTimeConfig { get; }
    DebugSettings DebugSettings { get; }
    SaveConfig SaveConfig { get; }
    GalaxyConfig GalaxyConfig { get; }
    NewGameConfig NewGameConfig { get; }

    PlayerControlConfig PlayerControlConfig { get; }
    ShipMovementConfig ShipMovementConfig { get; }
    SystemCameraConfig SystemCameraConfig { get; }
    TargetingConfig TargetingConfig { get; }
    InteractionConfig InteractionConfig { get; }
    SystemHudConfig SystemHudConfig { get; }
    GameVisualConfig GameVisualConfig { get; }
    CombatFxVisualConfig CombatFxVisualConfig { get; }
    InvasionScheduleConfig InvasionScheduleConfig { get; }
    CombatDamagePopupVisualConfig2A CombatDamagePopupVisualConfig { get; }
    NpcBehaviourTransitionMatrixConfig NpcBehaviourTransitionMatrixConfig { get; }

    IReadOnlyList<SectorConfig> GetAllSectors();

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

    IReadOnlyList<AllyConfig> GetAllAllies();
    EnemyConfig GetEnemyConfigById(string id);
    AllyConfig GetAllyConfigById(string id);
    PirateConfig GetPirateConfigById(string id);
    AllySpawnRuleConfig GetAllySpawnRuleConfigById(string id);
    PirateGroupSpawnRuleConfig GetPirateGroupSpawnRuleConfigById(string id);

    IReadOnlyList<ModuleConfig> GetAllModules();
    ModuleConfig GetModuleConfigById(string moduleId);

    IReadOnlyList<WeaponConfig> GetAllWeapons();
    WeaponConfig GetWeaponConfigById(string weaponId);
}
