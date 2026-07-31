using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarFrontier.Tests.Sprint1
{
    public sealed class SaveService2ACurrentEditModeTests
    {
        private Sprint1BootstrapFixture _bootstrap;
        private SaveConfig _config;
        private SaveService2A _service;
        private TestGameSessionService _session;
        private SimpleEventBus _bus;

        [SetUp]
        public void SetUp()
        {
            string token = Guid.NewGuid().ToString("N");
            _config = ScriptableObject.CreateInstance<SaveConfig>();
            _config.SaveFileName = "sf-s1-" + token + ".json";
            _config.BackupFileName = "sf-s1-" + token + ".backup.json";
            _config.AutosaveIntervalSeconds = 1;
            _bootstrap = new Sprint1BootstrapFixture();
            _session = new TestGameSessionService(NewState());
            _bus = new SimpleEventBus();
            _bootstrap.Registry.Register<IConfigService>(new TestConfigService(_config));
            _bootstrap.Registry.Register<SimpleEventBus>(_bus);
            _bootstrap.Registry.Register<IGameSessionService>(_session);
            _bootstrap.Registry.Register<ISystemEncounterSaveService>(new NullEncounterSaveService());
            _bootstrap.Registry.Register<ISystemNpcSimulationSaveService>(new NullNpcSaveService());
            _service = new SaveService2A();
        }

        [TearDown]
        public void TearDown()
        {
            _service.DeleteSave();
            _bus.Clear();
            UnityEngine.Object.DestroyImmediate(_config);
            _bootstrap.Dispose();
        }

        [Test]
        public void SaveAndLoad_RoundTripCoreValues()
        {
            _session.State.Player.PlayerId = "roundtrip";
            _session.State.Player.Credits = 777;
            _service.Save();
            GameRuntimeState loaded = _service.Load();
            Assert.That(loaded.Player.PlayerId, Is.EqualTo("roundtrip"));
            Assert.That(loaded.Player.Credits, Is.EqualTo(777));
        }

        [Test]
        public void Save_PublishesExactlyOneEvent()
        {
            int count = 0;
            _bus.Subscribe<GameSavedEvent>(_ => count++);
            _service.Save();
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void SecondSave_CreatesBackupRecoverableAfterMainCorruption()
        {
            _session.State.Player.Credits = 100;
            _service.Save();
            _session.State.Player.Credits = 200;
            _service.Save();
            File.WriteAllText(_service.GetSavePath(), "{broken");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to load"));
            LogAssert.Expect(LogType.Warning, "[SaveService] Main save failed. Trying backup.");
            LogAssert.Expect(LogType.Warning, "[SaveService] Backup save loaded.");
            Assert.That(_service.Load().Player.Credits, Is.EqualTo(100));
        }

        [Test]
        public void BothCorruptFiles_ReturnNull()
        {
            File.WriteAllText(_service.GetSavePath(), "bad");
            File.WriteAllText(_service.GetBackupPath(), "bad");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to load"));
            LogAssert.Expect(LogType.Warning, "[SaveService] Main save failed. Trying backup.");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to load"));
            LogAssert.Expect(LogType.Warning, "[SaveService] No valid save found.");
            Assert.That(_service.Load(), Is.Null);
        }

        [Test]
        public void SaveNull_DoesNotCreateFile()
        {
            LogAssert.Expect(LogType.Warning, "[SaveService] Save skipped: GameState is null.");
            _service.Save((GameRuntimeState)null);
            Assert.That(File.Exists(_service.GetSavePath()), Is.False);
        }

        [Test]
        public void AutosaveTick_WritesCurrentSession()
        {
            _service.Tick(1f);
            Assert.That(File.Exists(_service.GetSavePath()), Is.True);
        }

        [Test]
        public void DeleteSave_RemovesMainAndBackup()
        {
            _service.Save();
            _service.Save();
            _service.DeleteSave();
            Assert.That(_service.HasSave(), Is.False);
        }

        [Test]
        public void Save_WritesCurrentSchemaVersion()
        {
            _session.State.Meta.SaveDataVersion = 0;
            _service.Save();
            Assert.That(_service.Load().Meta.SaveDataVersion,
                Is.EqualTo(SaveDataVersions.Current));
        }

        [Test]
        public void Save_WritesIntegrityChecksum()
        {
            _service.Save();

            string json = File.ReadAllText(_service.GetSavePath());
            GameRuntimeState saved =
                JsonUtility.FromJson<GameRuntimeState>(json);

            Assert.That(saved.Meta.IntegrityChecksum, Is.Not.Null.And.Not.Empty);
            Assert.That(saved.Meta.IntegrityChecksum, Has.Length.EqualTo(64));
        }

        [Test]
        public void TamperedMainSave_FallsBackToValidBackup()
        {
            _session.State.Player.Credits = 100;
            _service.Save();

            _session.State.Player.Credits = 200;
            _service.Save();

            string mainPath = _service.GetSavePath();
            string tamperedJson = File.ReadAllText(mainPath)
                .Replace("\"Credits\": 200", "\"Credits\": 201");

            Assert.That(tamperedJson, Does.Contain("\"Credits\": 201"));
            File.WriteAllText(mainPath, tamperedJson);

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "integrity verification failed"));
            LogAssert.Expect(
                LogType.Warning,
                "[SaveService] Main save failed. Trying backup.");
            LogAssert.Expect(
                LogType.Warning,
                "[SaveService] Backup save loaded.");

            GameRuntimeState loaded = _service.Load();
            Assert.That(loaded.Player.Credits, Is.EqualTo(100));
        }

        private static GameRuntimeState NewState()
        {
            var state = new GameRuntimeState();
            state.Player.PlayerId = "test-player";
            return state;
        }
    }
}
