using NUnit.Framework;
using UnityEngine;

public sealed class SaveMigrationEditModeTests
{
    [Test]
    public void Migrate_OldSave_AddsCurrentDataVersion()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Meta.SaveDataVersion =
            SaveDataVersions.LegacyStage2A;

        SaveMigrationService.Migrate(state);

        Assert.AreEqual(
            SaveDataVersions.Current,
            state.Meta.SaveDataVersion);
    }

    [Test]
    public void Migrate_OldSave_AddsDefaultShipDirection()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Meta.SaveDataVersion =
            SaveDataVersions.LegacyStage2A;

        state.Player.SystemMapShipDirection =
            Vector3.zero;

        SaveMigrationService.Migrate(state);

        Assert.AreEqual(
            Vector3.up,
            state.Player.SystemMapShipDirection);
    }

    [Test]
    public void Migrate_NormalizesShipDirection()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Meta.SaveDataVersion =
            SaveDataVersions.Current;

        state.Player.SystemMapShipDirection =
            new Vector3(3f, 4f, 0f);

        SaveMigrationService.Migrate(state);

        Vector3 expected =
            new Vector3(0.6f, 0.8f, 0f);

        Assert.That(
            state.Player.SystemMapShipDirection.x,
            Is.EqualTo(expected.x).Within(0.0001f));

        Assert.That(
            state.Player.SystemMapShipDirection.y,
            Is.EqualTo(expected.y).Within(0.0001f));

        Assert.That(
            state.Player.SystemMapShipDirection.z,
            Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void Migrate_InvalidDirection_UsesUp()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Player.SystemMapShipDirection =
            new Vector3(float.NaN, 0f, 0f);

        SaveMigrationService.Migrate(state);

        Assert.AreEqual(
            Vector3.up,
            state.Player.SystemMapShipDirection);
    }

    [Test]
    public void Migrate_NullNestedBlocks_RecreatesThem()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Meta = null;
        state.Player = null;
        state.Galaxy = null;
        state.Settings = null;
        state.Markets = null;
        state.MissionBlock = null;
        state.SystemEncounter = null;
        state.SystemNpcSimulation = null;

        SaveMigrationService.Migrate(state);

        Assert.IsNotNull(state.Meta);
        Assert.IsNotNull(state.Player);
        Assert.IsNotNull(state.Galaxy);
        Assert.IsNotNull(state.Settings);
        Assert.IsNotNull(state.Markets);
        Assert.IsNotNull(state.MissionBlock);
        Assert.IsNotNull(state.SystemEncounter);
        Assert.IsNotNull(state.SystemNpcSimulation);
    }

    [Test]
    public void Migrate_PlayerShipState_RecreatesOwnedShipsList()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Player.PlayerShipState = null;

        SaveMigrationService.Migrate(state);

        Assert.IsNotNull(state.Player.PlayerShipState);
        Assert.IsNotNull(state.Player.PlayerShipState.OwnedShips);
    }

    [Test]
    public void JsonWithoutNewFields_MigratesToCurrentVersion()
    {
        string oldJson =
            "{"
            + "\"Meta\":{"
            + "\"SaveVersion\":1,"
            + "\"CreatedUtcTicks\":1,"
            + "\"LastSaveUtc\":0,"
            + "\"LastSaveReason\":\"legacy\""
            + "},"
            + "\"Player\":{"
            + "\"PlayerId\":\"player\","
            + "\"PlayerName\":\"Pilot\","
            + "\"Level\":1,"
            + "\"Experience\":0,"
            + "\"Credits\":1000,"
            + "\"CurrentSystemId\":\"system_heliosGate_01\","
            + "\"CurrentPlanetId\":\"\","
            + "\"SystemMapShipPosition\":{\"x\":0,\"y\":0,\"z\":-2}"
            + "}"
            + "}";

        GameRuntimeState state =
            JsonUtility.FromJson<GameRuntimeState>(oldJson);

        SaveMigrationService.Migrate(state);

        Assert.AreEqual(
            SaveDataVersions.Current,
            state.Meta.SaveDataVersion);

        Assert.AreEqual(
            Vector3.up,
            state.Player.SystemMapShipDirection);
    }
}