using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationMilitaryRestoreEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; }
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Config : IConfigService
    {
        public SectorConfig[] Sectors;
        public GameConfig GameConfig => null;
        public DebugSettings DebugSettings => null;
        public SaveConfig SaveConfig => null;
        public GalaxyConfig GalaxyConfig => null;
        public NewGameConfig NewGameConfig => null;
        public PlayerControlConfig PlayerControlConfig => null;
        public ShipMovementConfig ShipMovementConfig => null;
        public SystemCameraConfig SystemCameraConfig => null;
        public TargetingConfig TargetingConfig => null;
        public InteractionConfig InteractionConfig => null;
        public SystemHudConfig SystemHudConfig => null;
        public SystemVisualConfig SystemVisualConfig => null;
        public CombatFxVisualConfig CombatFxVisualConfig => null;
        public InvasionScheduleConfig InvasionScheduleConfig => null;
        public CombatDamagePopupVisualConfig2A CombatDamagePopupVisualConfig => null;
        public NpcBehaviourTransitionMatrixConfig NpcBehaviourTransitionMatrixConfig => null;
        public IReadOnlyList<SectorConfig> GetAllSectors() => Sectors;
        public StarSystemLink GetCurrentStarSystemLink(string targetSystemId) => null;
        public bool ContainsStarSystem(string systemId) => false;
        public IReadOnlyList<StarSystemConfig> GetAllStarSystems() => null;
        public bool TryGetStarSystem(string systemId, out StarSystemConfig config) { config = null; return false; }
        public StarSystemConfig GetStarSystemConfigById(string systemId) =>
            Sectors?.Where(x => x != null).SelectMany(x => x.Systems ?? Array.Empty<StarSystemConfig>()).FirstOrDefault(x => x != null && x.Id == systemId);
        public StarSystemConfig GetCurrentSystemConfig() => null;
        public IReadOnlyList<PlanetConfig> GetAllPlanets() => null;
        public PlanetConfig GetPlanetConfigById(string planetId) => null;
        public PlanetConfig GetCurrentPlanetConfig() => null;
        public IReadOnlyList<ItemConfig> GetAllItems() => null;
        public ItemConfig GetItemConfigById(string id) => null;
        public IReadOnlyList<AllyConfig> GetAllAllies() => null;
        public EnemyConfig GetEnemyConfigById(string id) => null;
        public AllyConfig GetAllyConfigById(string id) => null;
        public PirateConfig GetPirateConfigById(string id) => null;
        public AllySpawnRuleConfig GetAllySpawnRuleConfigById(string id) => null;
        public PirateGroupSpawnRuleConfig GetPirateGroupSpawnRuleConfigById(string id) => null;
        public IReadOnlyList<ModuleConfig> GetAllModules() => null;
        public ModuleConfig GetModuleConfigById(string moduleId) => null;
        public IReadOnlyList<WeaponConfig> GetAllWeapons() => null;
        public WeaponConfig GetWeaponConfigById(string weaponId) => null;
    }
    private sealed class FakeNpcs : ISystemNpcRuntimeService
    {
        public readonly List<SystemNpcRuntimeState> Items = new List<SystemNpcRuntimeState>();
        public IReadOnlyList<SystemNpcRuntimeState> Npcs => Items;
        public void AddNpc(SystemNpcRuntimeState npc) { }
        public bool TryGetNpc(string runtimeNpcId, out SystemNpcRuntimeState npc) { npc = null; return false; }
        public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsInSystem(string systemId) =>
            Items.Where(x => x != null && x.IsAlive && x.CurrentSystemId == systemId).ToList();
        public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsByGroupId(string groupId) => new List<SystemNpcRuntimeState>();
        public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsInSystemByType( string systemId, SystemNpcType npcType) => new List<SystemNpcRuntimeState>();
        public IReadOnlyList<SystemNpcRuntimeState> GetAliveNpcsBySpawnRule( string systemId, string spawnRuleId, SystemNpcType npcType) => new List<SystemNpcRuntimeState>();
        public IReadOnlyList<SystemNpcRuntimeState> GetAliveEnemyGroupsByRule( string systemId, string groupRuleId) => new List<SystemNpcRuntimeState>();
        public IReadOnlyList<SystemNpcRuntimeState> GetAliveEnemyGroupsInSystem( string systemId) => new List<SystemNpcRuntimeState>();
        public void UpdateNpcPosition(string runtimeNpcId, Vector3 position) { }
        public CombatDamageResult2A ApplyDamage( string runtimeNpcId, int damage, bool killedByPlayer, bool damagedByPlayer) => default;
        public bool DespawnNpc(string runtimeNpcId) => false;
        public bool KillNpc(string runtimeNpcId, bool killedByPlayer) => false;
        public bool AnnihilateNpc(string runtimeNpcId) => false;
        public bool ResetNpc(string runtimeNpcId) => false;
        public void ClearAll() { }
        public void RestoreNpc(SystemNpcRuntimeState npc) { }
        public void RestoreNpcs(IEnumerable<SystemNpcRuntimeState> npcs) { }
    }
    private T Asset<T>() where T : ScriptableObject
    { var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset; }
    private static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) { f.SetValue(target, value); return; }
        }
        Assert.Fail(field);
    }
    [TearDown] public void Cleanup()
    { foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset); _assets.Clear(); }

    [Test] public void MilitaryRestore_WaitsForSessionThenUsesLoadedGalaxyAndSector()
    {
        var home = Asset<StarSystemConfig>(); Set(home, "id", "home");
        var safe = Asset<StarSystemConfig>(); Set(safe, "id", "safe");
        var outside = Asset<StarSystemConfig>(); Set(outside, "id", "outside");
        var sector = Asset<SectorConfig>(); Set(sector, "systems", new[] { home, safe });
        var otherSector = Asset<SectorConfig>(); Set(otherSector, "systems", new[] { outside });
        var config = new Config { Sectors = new[] { sector, otherSector } };
        var session = new Session(); var npcs = new FakeNpcs();
        var npc = new SystemNpcRuntimeState { RuntimeNpcId = "restored", NpcType = SystemNpcType.Ally,
            AllyRole = AllyRole2A.Military, IsAlive = true, CurrentSystemId = "home",
            SpawnRuleId = StationMilitaryRules.RuleId("home", "base", "rule"),
            CurrentPlanetId = "stale_planet", IsOnPlanet = true,
            TargetSystemId = "outside", TravelProgress01 = .5f, IsFighting = true };
        npcs.Items.Add(npc);
        var infrastructure = new StationInfrastructureService(session, config, npcs, null, null, new SimpleEventBus());
        Assert.DoesNotThrow(() => infrastructure.RelocateStationMilitary());
        Assert.That(npc.CurrentSystemId, Is.EqualTo("home"));
        var loaded = new GameRuntimeState();
        loaded.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "home", SystemStatus = StarSystemStatus.Captured });
        loaded.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "safe" });
        loaded.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "outside" });
        session.LoadSession(loaded);
        var random = UnityEngine.Random.state;
        try
        {
            Assert.DoesNotThrow(() => infrastructure.RelocateStationMilitary());
            Assert.That(npc.CurrentSystemId, Is.EqualTo("safe"));
            Assert.That(npc.OriginSystemId, Is.EqualTo("home"));
            Assert.That(npc.CurrentPlanetId, Is.Null.Or.Empty); Assert.That(npc.IsOnPlanet, Is.False);
            Assert.That(npc.TargetSystemId, Is.Null.Or.Empty); Assert.That(npc.TravelProgress01, Is.Zero);
            Assert.That(npc.IsFighting, Is.False);
        }
        finally { UnityEngine.Random.state = random; }
    }

    [Test] public void InvalidLoadedSector_DoesNotPlaceMilitaryInCapturedSystem()
    {
        var home = Asset<StarSystemConfig>(); Set(home, "id", "home");
        var sector = Asset<SectorConfig>(); Set(sector, "systems", new[] { home });
        var session = new Session(); var state = new GameRuntimeState();
        state.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "home", SystemStatus = StarSystemStatus.Captured });
        session.LoadSession(state);
        var npcs = new FakeNpcs(); npcs.Items.Add(new SystemNpcRuntimeState { RuntimeNpcId = "invalid",
            NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military, IsAlive = true,
            SpawnRuleId = StationMilitaryRules.RuleId("home", "base", "rule") });
        var service = new StationInfrastructureService(session, new Config { Sectors = new[] { sector } }, npcs, null, null, null);
        Assert.Throws<InvalidOperationException>(() => service.RelocateStationMilitary());
        Assert.That(npcs.Items[0].CurrentSystemId, Is.Null.Or.Empty);
    }

    private Config _repairConfig;
    private StationInfrastructureService RepairFixture(out Session session, out FakeNpcs npcs,
        out StarSystemRuntimeState system, int rate = 7)
    {
        var effects = Asset<StationLevelEffectsConfig>();
        effects.Levels = new[] { new StationLevelEffectsSettings { Level = 2, MaxHealth = 450, HealthPerTick = rate } };
        var station = Asset<StationConfig>(); Set(station, "id", "repair-station"); Set(station, "initialLevel", 2);
        Set(station, "stationTypeConfig", Asset<StationTypeConfig>()); Set(station, "levelEffects", effects);
        var home = Asset<StarSystemConfig>(); Set(home, "id", "repair"); Set(home, "station", station);
        var sector = Asset<SectorConfig>(); Set(sector, "systems", new[] { home });
        var config = new Config { Sectors = new[] { sector } }; _repairConfig = config;
        session = new Session(); session.LoadSession(new GameRuntimeState());
        system = new StarSystemRuntimeState { SystemId = "repair" }; session.State.Galaxy.Systems.Add(system);
        var events = new SimpleEventBus();
        var stations = new StationService(session, config.GetStarSystemConfigById, events);
        stations.InitializeSession(true); system.ApplyInfrastructureDamageFromWar(100, false);
        npcs = new FakeNpcs();
        return new StationInfrastructureService(session, config, npcs, null, stations, events);
    }
    [Test] public void TickRepair_FramesNeverHeal_EndTickHealsExactlyOnce_AndReloadDoesNotRepeat()
    {
        var service = RepairFixture(out var session, out _, out var system);
        for (int i = 0; i < 100; i++) service.AdvanceTime(.01);
        Assert.That(system.Station.Health, Is.EqualTo(350));
        service.CompleteTick(1); service.CompleteTick(1);
        Assert.That(system.Station.Health, Is.EqualTo(357));
        session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(session.State)));
        service.CompleteTick(1);
        Assert.That(session.State.Galaxy.Systems[0].Station.Health, Is.EqualTo(357));
        service.CompleteTick(2);
        Assert.That(session.State.Galaxy.Systems[0].Station.Health, Is.EqualTo(364));
    }
    [Test] public void TickRepair_EnemiesBlockCompletedTicksWithoutCatchUp_AndDestroyedNeverHeals()
    {
        var service = RepairFixture(out _, out var npcs, out var system);
        var enemy = new SystemNpcRuntimeState { NpcType = SystemNpcType.Enemy, IsAlive = true, CurrentSystemId = "repair" };
        npcs.Items.Add(enemy); service.CompleteTick(1); service.CompleteTick(2);
        Assert.That(system.Station.Health, Is.EqualTo(350));
        enemy.IsAlive = false; service.CompleteTick(3);
        Assert.That(system.Station.Health, Is.EqualTo(357));
        system.ApplyInfrastructureDamageFromWar(1, true); service.CompleteTick(4);
        Assert.That(system.Station.Health, Is.Zero); Assert.That(system.Station.IsDestroyed, Is.True);
    }
    [Test] public void TickRepair_OfflineWaitsForWholeTick_AndRemainderSurvivesSave()
    {
        float oldTick = GameTimeState.SecondsPerDay;
        try
        {
            GameTimeState.SecondsPerDay = 1;
            var service = RepairFixture(out var session, out _, out var system);
            service.AdvanceOfflineRepair(.4);
            Assert.That(system.Station.Health, Is.EqualTo(350));
            Assert.That(service.NextRepairBoundary(30), Is.EqualTo(.6).Within(.000001));
            session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(session.State)));
            service.AdvanceOfflineRepair(.6);
            Assert.That(session.State.Galaxy.Systems[0].Station.Health, Is.EqualTo(357));
            service.AdvanceOfflineRepair(2);
            Assert.That(session.State.Galaxy.Systems[0].Station.Health, Is.EqualTo(371));
        }
        finally { GameTimeState.SecondsPerDay = oldTick; }
    }
    [Test] public void TickRepair_OfflineBlockedTicksDoNotTurnIntoLaterHealing()
    {
        float oldTick = GameTimeState.SecondsPerDay;
        try
        {
            GameTimeState.SecondsPerDay = 1;
            var service = RepairFixture(out _, out var npcs, out var system);
            npcs.Items.Add(new SystemNpcRuntimeState { NpcType = SystemNpcType.Pirate, IsAlive = true, CurrentSystemId = "repair" });
            service.AdvanceOfflineRepair(5); Assert.That(system.Station.Health, Is.EqualTo(350));
            npcs.Items.Clear(); service.AdvanceOfflineRepair(.5);
            Assert.That(system.Station.Health, Is.EqualTo(350));
            service.AdvanceOfflineRepair(.5); Assert.That(system.Station.Health, Is.EqualTo(357));
        }
        finally { GameTimeState.SecondsPerDay = oldTick; }
    }
    [TestCase("military", 2)] [TestCase("ranger_base", 1)] [TestCase("trade", 1)]
    [TestCase("science", 1)] [TestCase("medical", 3)]
    public void TickRepair_TypeAssetsContainExplicitLevelRates(string kind, int factor)
    {
        var config = UnityEditor.AssetDatabase.LoadAssetAtPath<StationLevelEffectsConfig>(
            "Assets/_Project/Content/Configs/Stations2A/Levels/station_levels_" + kind + ".asset");
        Assert.That(config, Is.Not.Null);
        for (int level = 1; level <= 10; level++)
            Assert.That(config.GetLevel(level).HealthPerTick, Is.EqualTo(level * factor), kind + " level " + level);
    }

    [Test] public void UpgradeOffline_FirstRepairsThenCountsOnlyRemainingUpgradeTime()
    {
        float oldTick = GameTimeState.SecondsPerDay;
        try
        {
            GameTimeState.SecondsPerDay = 1;
            var infrastructure = RepairFixture(out var session, out var npcs, out var system);
            StationInfrastructureRules.RepairFully(system);
            StationLifecycleRules.BeginPaidOperation(system.Station, new StationOperationRuntimeState {
                OperationId = "offline-upgrade-paused", Kind = StationOperationKind.Upgrade, TargetLevel = 3, DurationSeconds = 10 });
            system.ApplyInfrastructureDamageFromWar(14, false);
            var offline = new StationService(session, _repairConfig.GetStarSystemConfigById, new SimpleEventBus(),
                infrastructure: infrastructure, hasEnemies: id => npcs.GetAliveNpcsInSystem(id).Any(n => n.IsEnemy || n.IsHostileToPlayer));
            long now = DateTime.UtcNow.Ticks; session.State.Meta.LastSaveUtc = now - TimeSpan.FromSeconds(6).Ticks;
            offline.ProcessOffline(session.State, now);
            Assert.That(system.Station.Health, Is.EqualTo(450));
            Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(4).Within(.000001));
            Assert.That(system.Station.Level, Is.EqualTo(2));
            offline.ProcessOffline(session.State, now);
            Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(4).Within(.000001));
        }
        finally { GameTimeState.SecondsPerDay = oldTick; }
    }
    [Test]
    public void ScienceEffect_RepeatedInitializationQueriesAndReload_DoNotStackAndReflectDamage()
    {
        var infrastructure = RepairFixture(out var session, out _, out var system);
        var config = _repairConfig.GetStarSystemConfigById("repair");
        Set(config.Station.StationTypeConfig, "stationType", StationType.Science);
        system.Station.StationType = StationType.Science;
        config.Station.LevelEffects.Levels[0].ScienceRate = 9;
        var stations = new StationService(session, _repairConfig.GetStarSystemConfigById, new SimpleEventBus());
        StationInfrastructureRules.RepairFully(system);
        for (int i = 0; i < 3; i++)
        {
            stations.InitializeSession(false); infrastructure.InitializeSession();
            Assert.That(infrastructure.GetScienceRate(), Is.EqualTo(9));
            session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(session.State)));
            SaveMigrationService.Migrate(session.State);
        }
        system = session.State.Galaxy.Systems[0];
        system.ApplyInfrastructureDamageFromWar(150, false); stations.ReconcileSystem("repair");
        Assert.That(infrastructure.GetScienceRate(), Is.EqualTo(6).Within(.00001));
        system.ApplyInfrastructureDamageFromWar(1, true); stations.ReconcileSystem("repair");
        Assert.That(infrastructure.GetScienceRate(), Is.Zero);
        session.State.Player.CurrentSystemId = "repair";
        Assert.That(StationDebugCommands2A.TryRestoreCurrent(session, stations, new SimpleEventBus(), false, out _), Is.True);
        Assert.That(infrastructure.GetScienceRate(), Is.EqualTo(9));
    }

    [Test]
    public void TickRepair_ReentrantObserverAndNewServiceAfterReload_CannotApplySameTickTwice()
    {
        var infrastructure = RepairFixture(out var session, out var npcs, out var system);
        var events = new SimpleEventBus();
        var stations = new StationService(session, _repairConfig.GetStarSystemConfigById, events);
        stations.InitializeSession(false);
        infrastructure = new StationInfrastructureService(session, _repairConfig, npcs, null, stations, events);
        int notifications = 0;
        events.Subscribe<StationStateChangedEvent>(_ => { notifications++; infrastructure.CompleteTick(1); });
        infrastructure.CompleteTick(1);
        Assert.That(system.Station.Health, Is.EqualTo(357)); Assert.That(notifications, Is.EqualTo(1));
        session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(session.State)));
        SaveMigrationService.Migrate(session.State);
        var restoredStations = new StationService(session, _repairConfig.GetStarSystemConfigById, new SimpleEventBus());
        restoredStations.InitializeSession(false);
        var restoredInfrastructure = new StationInfrastructureService(session, _repairConfig, npcs, null, restoredStations, new SimpleEventBus());
        restoredInfrastructure.InitializeSession(); restoredInfrastructure.CompleteTick(1);
        Assert.That(session.State.Galaxy.Systems[0].Station.Health, Is.EqualTo(357));
        restoredInfrastructure.CompleteTick(2);
        Assert.That(session.State.Galaxy.Systems[0].Station.Health, Is.EqualTo(364));
    }

}
