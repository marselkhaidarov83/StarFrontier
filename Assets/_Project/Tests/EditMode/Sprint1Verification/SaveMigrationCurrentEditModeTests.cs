using NUnit.Framework;
using UnityEngine;

namespace StarFrontier.Tests.Sprint1
{
    [TestFixture]
    public sealed class SaveMigrationCurrentEditModeTests
    {
        [Test]
        public void Migrate_OldSave_AddsCurrentDataVersion()
        {
            var state = new GameRuntimeState();
            state.Meta.SaveDataVersion = SaveDataVersions.LegacyStage2A;

            bool changed = SaveMigrationService.Migrate(state);

            Assert.That(changed, Is.True);
            Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
        }

        [Test]
        public void Migrate_OldSave_AddsDefaultShipDirection()
        {
            var state = new GameRuntimeState();
            state.Meta.SaveDataVersion = SaveDataVersions.LegacyStage2A;
            state.Player.SystemMapShipDirection = Vector3.zero;

            SaveMigrationService.Migrate(state);

            Assert.That(state.Player.SystemMapShipDirection, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void Migrate_NormalizesShipDirection()
        {
            var state = new GameRuntimeState();
            state.Meta.SaveDataVersion = SaveDataVersions.Current;
            state.Player.SystemMapShipDirection = new Vector3(3f, 4f, 7f);

            SaveMigrationService.Migrate(state);

            Assert.That(state.Player.SystemMapShipDirection.x, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(state.Player.SystemMapShipDirection.y, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(state.Player.SystemMapShipDirection.z, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void Migrate_InvalidDirection_UsesUp()
        {
            var state = new GameRuntimeState();
            state.Player.SystemMapShipDirection = new Vector3(float.NaN, 0f, 0f);

            SaveMigrationService.Migrate(state);

            Assert.That(state.Player.SystemMapShipDirection, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void Migrate_NullNestedBlocks_RecreatesAllRequiredRoots()
        {
            var state = new GameRuntimeState
            {
                Meta = null,
                Player = null,
                Galaxy = null,
                Settings = null,
                Markets = null,
                MissionBlock = null,
                SystemEncounter = null,
                SystemNpcSimulation = null
            };

            SaveMigrationService.Migrate(state);

            Assert.That(state.Meta, Is.Not.Null);
            Assert.That(state.Player, Is.Not.Null);
            Assert.That(state.Galaxy, Is.Not.Null);
            Assert.That(state.Settings, Is.Not.Null);
            Assert.That(state.Markets, Is.Not.Null);
            Assert.That(state.MissionBlock, Is.Not.Null);
            Assert.That(state.SystemEncounter, Is.Not.Null);
            Assert.That(state.SystemNpcSimulation, Is.Not.Null);
        }

        [Test]
        public void Migrate_PlayerShipState_RecreatesOwnedShipsList()
        {
            var state = new GameRuntimeState();
            state.Player.PlayerShipState = null;

            SaveMigrationService.Migrate(state);

            Assert.That(state.Player.PlayerShipState, Is.Not.Null);
            Assert.That(state.Player.PlayerShipState.OwnedShips, Is.Not.Null);
        }

        [Test]
        public void JsonWithoutNewFields_MigratesToCurrentVersion()
        {
            const string oldJson =
                "{\"Meta\":{\"SaveVersion\":1,\"CreatedUtcTicks\":1," +
                "\"LastSaveUtc\":0,\"LastSaveReason\":\"legacy\"}," +
                "\"Player\":{\"PlayerId\":\"player\",\"PlayerName\":\"Pilot\"," +
                "\"Level\":1,\"Experience\":0,\"Credits\":1000," +
                "\"CurrentSystemId\":\"system_heliosGate_01\"," +
                "\"CurrentPlanetId\":\"\",\"SystemMapShipPosition\":" +
                "{\"x\":0,\"y\":0,\"z\":-2}}}";

            GameRuntimeState state = JsonUtility.FromJson<GameRuntimeState>(oldJson);
            SaveMigrationService.Migrate(state);

            Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
            Assert.That(state.Player.SystemMapShipDirection, Is.EqualTo(Vector3.up));
            Assert.That(state.Meta.IntegrityChecksum, Is.Not.Null);
        }
    }
}
