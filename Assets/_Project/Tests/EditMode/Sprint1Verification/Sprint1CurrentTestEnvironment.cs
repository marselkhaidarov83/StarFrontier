using System.Collections.Generic;
using UnityEngine;

namespace StarFrontier.Tests.Sprint1
{
    internal sealed class Sprint1BootstrapFixture
    {
        private readonly GameObject _root;
        public ServiceRegistry Registry { get; }

        public Sprint1BootstrapFixture()
        {
            _root = new GameObject("Sprint1VerificationBootstrap");
            _root.SetActive(false);
            Bootstrapper bootstrapper = _root.AddComponent<Bootstrapper>();
            Registry = new ServiceRegistry();
            bootstrapper.ServiceRegistry = Registry;
            Bootstrapper.Instance = bootstrapper;
        }

        public void Dispose()
        {
            Bootstrapper.Instance = null;
            Object.DestroyImmediate(_root);
        }
    }

    internal sealed class TestGameSessionService : IGameSessionService
    {
        public GameRuntimeState State { get; private set; }
        public bool HasActiveSession => State != null;
        public TestGameSessionService(GameRuntimeState state = null) { State = state; }
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }

    internal sealed class TestConfigService : IConfigService
    {
        public GameConfig GameConfig => null;
        public DebugConfig DebugConfig => null;
        public SaveConfig SaveConfig { get; }
        public GalaxyConfig GalaxyConfig => null;
        public NewGameConfig NewGameConfig => null;
        public PlayerControlConfig PlayerControlConfig => null;
        public ShipMovementConfig ShipMovementConfig => null;
        public SystemCameraConfig SystemCameraConfig => null;
        public TargetingConfig TargetingConfig => null;
        public InteractionConfig InteractionConfig => null;
        public SystemHudConfig SystemHudConfig => null;
        public SystemVisualConfig SystemVisualConfig => null;
        public TestConfigService(SaveConfig saveConfig) { SaveConfig = saveConfig; }
        public IReadOnlyList<SectorConfig> GetAllSectors() => new List<SectorConfig>();
        public StarSystemLink GetCurrentStarSystemLink(string id) => null;
        public bool ContainsStarSystem(string id) => false;
        public IReadOnlyList<StarSystemConfig> GetAllStarSystems() => new List<StarSystemConfig>();
        public bool TryGetStarSystem(string id, out StarSystemConfig config) { config = null; return false; }
        public StarSystemConfig GetStarSystemConfigById(string id) => null;
        public StarSystemConfig GetCurrentSystemConfig() => null;
        public IReadOnlyList<PlanetConfig> GetAllPlanets() => new List<PlanetConfig>();
        public PlanetConfig GetPlanetConfigById(string id) => null;
        public PlanetConfig GetCurrentPlanetConfig() => null;
        public IReadOnlyList<ItemConfig> GetAllItems() => new List<ItemConfig>();
        public ItemConfig GetItemConfigById(string id) => null;
        public IReadOnlyList<AllyConfig> GetAllAllies() => new List<AllyConfig>();
        public EnemyConfig GetEnemyConfigById(string id) => null;
        public AllyConfig GetAllyConfigById(string id) => null;
        public PirateConfig GetPirateConfigById(string id) => null;
        public AllySpawnRuleConfig GetAllySpawnRuleConfigById(string id) => null;
        public PirateGroupSpawnRuleConfig GetPirateGroupSpawnRuleConfigById(string id) => null;
        public IReadOnlyList<ModuleConfig> GetAllModules() => new List<ModuleConfig>();
        public ModuleConfig GetModuleConfigById(string id) => null;
        public IReadOnlyList<WeaponConfig> GetAllWeapons() => new List<WeaponConfig>();
        public WeaponConfig GetWeaponConfigById(string id) => null;
    }

    internal sealed class NullEncounterSaveService : ISystemEncounterSaveService
    {
        public SystemEncounterSaveData Capture() => new SystemEncounterSaveData();
        public void Restore(SystemEncounterSaveData data) { }
    }

    internal sealed class NullNpcSaveService : ISystemNpcSimulationSaveService
    {
        public SystemNpcSimulationSaveData Capture() => new SystemNpcSimulationSaveData();
        public void Restore(SystemNpcSimulationSaveData data) { }
    }
}
