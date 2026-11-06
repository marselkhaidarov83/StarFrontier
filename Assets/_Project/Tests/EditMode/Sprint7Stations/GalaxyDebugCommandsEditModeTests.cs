using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GalaxyDebugCommandsEditModeTests
{
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private readonly List<SectorConfig> _configs = new List<SectorConfig>();
    private SectorConfig Sector(string id, int order)
    {
        var config = ScriptableObject.CreateInstance<SectorConfig>(); _configs.Add(config);
        typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, id);
        typeof(SectorConfig).GetField("order", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, order);
        return config;
    }
    [TearDown] public void Cleanup()
    { foreach (var config in _configs) Object.DestroyImmediate(config); _configs.Clear(); }

    [Test] public void PlusOne_UnlocksExactlyNextSectorAndRequestsSaveWithoutChangingConfigs()
    {
        var session = new Session();
        session.State.Galaxy.Sectors = new List<SectorRuntimeState> {
            new SectorRuntimeState { SectorId = "first", IsUnlocked = true },
            new SectorRuntimeState { SectorId = "third" }, new SectorRuntimeState { SectorId = "second" } };
        var third = Sector("third", 3); var second = Sector("second", 2); var first = Sector("first", 1);
        var events = new SimpleEventBus(); int saves = 0; string unlocked = null;
        events.Subscribe<SaveNeedEvent>(_ => saves++); events.Subscribe<SectorUnlockedEvent>(e => unlocked = e.SectorId);
        Assert.That(GalaxyDebugCommands2A.TryIncreaseLevel(session, new[] { third, second, first }, events, out _), Is.True);
        Assert.That(session.State.Galaxy.Sectors.Find(x => x.SectorId == "second").IsUnlocked, Is.True);
        Assert.That(session.State.Galaxy.Sectors.Find(x => x.SectorId == "third").IsUnlocked, Is.False);
        Assert.That(saves, Is.EqualTo(1)); Assert.That(unlocked, Is.EqualTo("second"));
        Assert.That(second.IsUnlocked, Is.False);
    }
    [Test] public void PlusOne_AtMaximumChangesNothingAndDoesNotSave()
    {
        var session = new Session();
        for (int i = 0; i < 10; i++) session.State.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "s" + i, IsUnlocked = true });
        var events = new SimpleEventBus(); int saves = 0; events.Subscribe<SaveNeedEvent>(_ => saves++);
        string before = JsonUtility.ToJson(session.State);
        Assert.That(GalaxyDebugCommands2A.TryIncreaseLevel(session, new[] { Sector("extra", 11) }, events, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("MaximumGalaxyLevel")); Assert.That(saves, Is.Zero);
        Assert.That(JsonUtility.ToJson(session.State), Is.EqualTo(before));
    }
    [Test] public void PlusOne_WithNoNextSectorDoesNotSave()
    {
        var session = new Session(); session.State.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "only", IsUnlocked = true });
        var events = new SimpleEventBus(); int saves = 0; events.Subscribe<SaveNeedEvent>(_ => saves++);
        Assert.That(GalaxyDebugCommands2A.TryIncreaseLevel(session, new[] { Sector("only", 1) }, events, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("NoLockedSector")); Assert.That(saves, Is.Zero);
    }
}
