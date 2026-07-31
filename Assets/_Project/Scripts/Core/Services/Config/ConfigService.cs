using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class ConfigService : IConfigService
{
    public GameConfig GameConfig { get; }
    public DebugConfig DebugConfig { get; }
    public SaveConfig SaveConfig { get; }
    public GalaxyConfig GalaxyConfig { get; }
    public NewGameConfig NewGameConfig { get; }

    public PlayerControlConfig PlayerControlConfig { get; }
    public ShipMovementConfig ShipMovementConfig { get; }
    public SystemCameraConfig SystemCameraConfig { get; }
    public TargetingConfig TargetingConfig { get; }
    public InteractionConfig InteractionConfig { get; }
    public SystemHudConfig SystemHudConfig { get; }
    public SystemVisualConfig SystemVisualConfig { get; }

    private readonly IReadOnlyList<SectorConfig> _sectors;
    private readonly Dictionary<string, SectorConfig> _sectorsById;
    private readonly IReadOnlyList<StarSystemConfig> _starSystems;
    private readonly Dictionary<string, StarSystemConfig> _starSystemsById;
    private readonly IReadOnlyList<PlanetConfig> _planets;
    private readonly Dictionary<string, PlanetConfig> _planetsById;
    private readonly IReadOnlyList<ItemConfig> _items;
    private readonly Dictionary<string, ItemConfig> _itemsById;
    private readonly IReadOnlyList<ShipConfig> _ships;
    private readonly Dictionary<string, ShipConfig> _shipsById;
    private readonly IReadOnlyList<EnemyConfig> _enemies;
    private readonly Dictionary<string, EnemyConfig> _enemiesById;
    private readonly IReadOnlyList<AllyConfig> _allies;
    private readonly Dictionary<string, AllyConfig> _alliesById;
    private readonly IReadOnlyList<AllySpawnRuleConfig> _allySpawnRules;
    private readonly Dictionary<string, AllySpawnRuleConfig> _allySpawnRulesById;
    private readonly IReadOnlyList<PirateConfig> _pirates;
    private readonly Dictionary<string, PirateConfig> _piratesById;
    private readonly IReadOnlyList<PirateGroupSpawnRuleConfig> _pirateGroupSpawnRules;
    private readonly Dictionary<string, PirateGroupSpawnRuleConfig> _pirateGroupSpawnRulesById;
    private readonly IReadOnlyList<ModuleConfig> _modules;
    private readonly Dictionary<string, ModuleConfig> _modulesById;
    private readonly IReadOnlyList<WeaponConfig> _weapons;
    private readonly Dictionary<string, WeaponConfig> _weaponsById;

    private readonly IGameSessionService gameSessionService;

    public ConfigService(GameConfig gameConfig,
                        DebugConfig debugConfig,
                        SaveConfig saveConfig,
                        GalaxyConfig galaxyConfig,
                        NewGameConfig newGameConfig,
                        IEnumerable<SectorConfig> sectors,
                        IEnumerable<StarSystemConfig> starSystems,
                        IEnumerable<PlanetConfig> planets,
                        IEnumerable<ItemConfig> items,
                        IEnumerable<ShipConfig> ships,
                        IEnumerable<EnemyConfig> enemies,
                        IEnumerable<AllyConfig> allies,
                        IEnumerable<AllySpawnRuleConfig> allySpawnRules,
                        IEnumerable<PirateConfig> pirates,
                        IEnumerable<PirateGroupSpawnRuleConfig> pirateGroupSpawnRules,
                        IEnumerable<ModuleConfig> modules,
                        IEnumerable<WeaponConfig> weapons)
    {
        GameConfig = gameConfig;
        DebugConfig = debugConfig;
        SaveConfig = saveConfig;
        GalaxyConfig = galaxyConfig;
        NewGameConfig = newGameConfig;

        BuildIndex(sectors, out _sectors, out _sectorsById, nameof(SectorConfig));
        BuildIndex(starSystems, out _starSystems, out _starSystemsById, nameof(StarSystemConfig));
        BuildIndex(planets, out _planets, out _planetsById, nameof(PlanetConfig));
        BuildIndex(items, out _items, out _itemsById, nameof(ItemConfig));
        BuildIndex(ships, out _ships, out _shipsById, nameof(ShipConfig));
        BuildIndex(enemies, out _enemies, out _enemiesById, nameof(EnemyConfig));
        BuildIndex(allies, out _allies, out _alliesById, nameof(AllyConfig));
        BuildIndex(allySpawnRules, out _allySpawnRules, out _allySpawnRulesById, nameof(AllySpawnRuleConfig));
        BuildIndex(pirates, out _pirates, out _piratesById, nameof(PirateConfig));
        BuildIndex(pirateGroupSpawnRules, out _pirateGroupSpawnRules, out _pirateGroupSpawnRulesById, nameof(PirateGroupSpawnRuleConfig));
        BuildIndex(modules, out _modules, out _modulesById, nameof(ModuleConfig));
        BuildIndex(weapons, out _weapons, out _weaponsById, nameof(WeaponConfig));

        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
    }

    public ConfigService(GameConfig gameConfig,
                        DebugConfig debugConfig,
                        SaveConfig saveConfig,
                        GalaxyConfig galaxyConfig,
                        NewGameConfig newGameConfig,
                        PlayerControlConfig playerControlConfig,
                        ShipMovementConfig shipMovementConfig,
                        SystemCameraConfig systemCameraConfig,
                        TargetingConfig targetingConfig,
                        InteractionConfig interactionConfig,
                        SystemHudConfig systemHudConfig,
                        SystemVisualConfig systemVisualConfig,
                        IEnumerable<ItemConfig> items,
                        IEnumerable<ShipConfig> ships,
                        IEnumerable<EnemyConfig> enemies,
                        IEnumerable<AllyConfig> allies,
                        IEnumerable<AllySpawnRuleConfig> allySpawnRules,
                        IEnumerable<PirateConfig> pirates,
                        IEnumerable<PirateGroupSpawnRuleConfig> pirateGroupSpawnRules,
                        IEnumerable<ModuleConfig> modules,
                        IEnumerable<WeaponConfig> weapons)
    {
        GameConfig = gameConfig;
        DebugConfig = debugConfig;
        SaveConfig = saveConfig;
        GalaxyConfig = galaxyConfig;
        NewGameConfig = newGameConfig;
        PlayerControlConfig = playerControlConfig;
        ShipMovementConfig = shipMovementConfig;
        SystemCameraConfig = systemCameraConfig;
        TargetingConfig = targetingConfig;
        InteractionConfig = interactionConfig;
        SystemHudConfig = systemHudConfig;
        SystemVisualConfig = systemVisualConfig;
        
        List<StarSystemConfig> starSystems = new();
        List<PlanetConfig> planets = new();
        foreach (SectorConfig sector in galaxyConfig.Sectors)
            foreach (StarSystemConfig starSystem in sector.Systems)
            {
                starSystems.Add(starSystem);
                foreach (PlanetConfig planet in starSystem.PlanetRefs)
                    planets.Add(planet);
            }

        BuildIndex(galaxyConfig.Sectors, out _sectors, out _sectorsById, nameof(SectorConfig));
        BuildIndex(starSystems, out _starSystems, out _starSystemsById, nameof(StarSystemConfig));
        BuildIndex(planets, out _planets, out _planetsById, nameof(PlanetConfig));
        BuildIndex(items, out _items, out _itemsById, nameof(ItemConfig));
        BuildIndex(ships, out _ships, out _shipsById, nameof(ShipConfig));
        BuildIndex(enemies, out _enemies, out _enemiesById, nameof(EnemyConfig));
        BuildIndex(allies, out _allies, out _alliesById, nameof(AllyConfig));
        BuildIndex(allySpawnRules, out _allySpawnRules, out _allySpawnRulesById, nameof(AllySpawnRuleConfig));
        BuildIndex(pirates, out _pirates, out _piratesById, nameof(PirateConfig));
        BuildIndex(pirateGroupSpawnRules, out _pirateGroupSpawnRules, out _pirateGroupSpawnRulesById, nameof(PirateGroupSpawnRuleConfig));
        BuildIndex(modules, out _modules, out _modulesById, nameof(ModuleConfig));
        BuildIndex(weapons, out _weapons, out _weaponsById, nameof(WeaponConfig));

        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
    }

    public StarSystemLink GetCurrentStarSystemLink(string targetSystemId)
    {
        StarSystemConfig starSystemConfig = GetCurrentSystemConfig();
        foreach (StarSystemLink link in starSystemConfig.LinkedSystems)
            if (link.LinkedSystem.Id == targetSystemId)
                return link;

        return null;
    }

    private void BuildIndex<TConfig>(
        IEnumerable<TConfig> configs,
        out IReadOnlyList<TConfig> targetList,
        out Dictionary<string, TConfig> targetById,
        string configName)
        where TConfig : BaseConfig
    {
        if (configs == null)
            throw new ArgumentNullException(nameof(configs));

        var mutableList = new List<TConfig>();
        targetById = new(StringComparer.Ordinal);

        foreach (var config in configs)
        {
            if (config == null)
            {
                AppLog.Warning($"ConfigService: null {configName} was skipped.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(config.Id))
            {
                AppLog.Warning($"ConfigService: {configName} with empty Id was skipped.");
                continue;
            }

            var normalizedId = config.Id.Trim();

            if (targetById.ContainsKey(normalizedId))
            {
                AppLog.Warning($"ConfigService: duplicate {configName} Id '{normalizedId}' was skipped.");
                continue;
            }

            mutableList.Add(config);
            targetById.Add(normalizedId, config);
        }

        targetList = new ReadOnlyListView<TConfig>(mutableList);
    }

    /// <summary>
    /// Read-only list wrapper that cannot be cast back to IList&lt;T&gt;.
    /// Config indexes are constructed once and are never mutated afterwards.
    /// </summary>
    private sealed class ReadOnlyListView<T> : IReadOnlyList<T>
    {
        private readonly List<T> _source;

        public ReadOnlyListView(List<T> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public int Count => _source.Count;

        public T this[int index] => _source[index];

        public IEnumerator<T> GetEnumerator()
        {
            return _source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public IReadOnlyList<SectorConfig> GetAllSectors()
    {
        return _sectors;
    }

    public IReadOnlyList<StarSystemConfig> GetAllStarSystems()
    {
        return _starSystems;
    }

    public bool TryGetStarSystem(string systemId, out StarSystemConfig config)
    {
        config = null;

        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        return _starSystemsById.TryGetValue(systemId.Trim(), out config);
    }

    public StarSystemConfig GetStarSystemConfigById(string systemId)
    {
        StarSystemConfig config;

        if (string.IsNullOrWhiteSpace(systemId))
            return null;

        _starSystemsById.TryGetValue(systemId.Trim(), out config);
        return config;
    }

    public StarSystemConfig GetCurrentSystemConfig()
    {
        return GetStarSystemConfigById(gameSessionService.State.Player.CurrentSystemId);
    }
    public IReadOnlyList<PlanetConfig> GetAllPlanets()
    {
        return _planets;
    }

    public PlanetConfig GetPlanetConfigById(string planetId)
    {
        PlanetConfig config;

        if (string.IsNullOrWhiteSpace(planetId))
            return null;

        _planetsById.TryGetValue(planetId.Trim(), out config);
        return config;
    }

    public PlanetConfig GetCurrentPlanetConfig()
    {
        return GetPlanetConfigById(gameSessionService.State.Player.CurrentPlanetId);
    }

    public bool ContainsStarSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        return _starSystemsById.ContainsKey(systemId.Trim());
    }

    public IReadOnlyList<ItemConfig> GetAllItems()
    {
        return _items;
    }
    public ItemConfig GetItemConfigById(string id)
    {
        ItemConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _itemsById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public IReadOnlyList<ShipConfig> GetAllShips()
    {
        return _ships;
    }
    public ShipConfig GetShipConfigById(string id)
    {
        ShipConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _shipsById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public EnemyConfig GetEnemyConfigById(string id)
    {
        EnemyConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _enemiesById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public AllyConfig GetAllyConfigById(string id)
    {
        AllyConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _alliesById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public AllySpawnRuleConfig GetAllySpawnRuleConfigById(string id)
    {
        AllySpawnRuleConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _allySpawnRulesById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public PirateConfig GetPirateConfigById(string id)
    {
        PirateConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _piratesById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public PirateGroupSpawnRuleConfig GetPirateGroupSpawnRuleConfigById(string id)
    {
        PirateGroupSpawnRuleConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _pirateGroupSpawnRulesById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public IReadOnlyList<ModuleConfig> GetAllModules()
    {
        return _modules;
    }
    public ModuleConfig GetModuleConfigById(string id)
    {
        ModuleConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _modulesById.TryGetValue(id.Trim(), out config);
        return config;
    }

    public IReadOnlyList<WeaponConfig> GetAllWeapons()
    {
        return _weapons;
    }
    public WeaponConfig GetWeaponConfigById(string id)
    {
        WeaponConfig config;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        _weaponsById.TryGetValue(id.Trim(), out config);
        return config;
    }
}
