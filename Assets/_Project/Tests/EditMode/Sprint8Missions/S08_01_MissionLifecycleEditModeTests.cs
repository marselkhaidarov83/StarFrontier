using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public sealed class S08_01_MissionLifecycleEditModeTests
{
    [Test]
    public void ExistingSerializedEnumValues_RemainStable()
    {
        Assert.That((int)MissionType.Delivery, Is.EqualTo(0));
        Assert.That((int)MissionType.Elimination, Is.EqualTo(1));
        Assert.That((int)MissionType.Recon, Is.EqualTo(2));
        Assert.That((int)MissionStatus.Expired, Is.EqualTo(6));
        Assert.That((int)MissionStatus.Cancelled, Is.EqualTo(7));
    }

    [Test]
    public void LegacyElimination_IsArchivedWithoutChangingPlayerBalance()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.SystemWarState;
        state.Player.Credits = 1234;
        state.MissionBlock.ActiveMissions.Add(Mission("old", MissionType.Elimination));
        Assert.That(SaveMigrationService.Migrate(state), Is.True);
        Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
        Assert.That(state.MissionBlock.ActiveMissions, Is.Empty);
        Assert.That(state.MissionBlock.ArchivedMissions.Count, Is.EqualTo(1));
        Assert.That(state.MissionBlock.ArchivedMissions[0].Status, Is.EqualTo(MissionStatus.Cancelled));
        Assert.That(state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
        Assert.That(state.Player.Credits, Is.EqualTo(1234));
    }

    [Test]
    public void LegacyCompletedMission_SeedsRewardLedger_AndNormalizationIsIdempotent()
    {
        var state = new GameRuntimeState();
        var mission = Mission("paid", MissionType.Recon);
        mission.Status = MissionStatus.Completed;
        state.MissionBlock.CompletedMissions.Add(mission);
        state.MissionBlock.ActiveMissions.Add(Mission("paid", MissionType.Recon));
        MissionLifecycleRules.Normalize(state.MissionBlock, 10);
        string first = JsonUtility.ToJson(state.MissionBlock);
        MissionLifecycleRules.Normalize(state.MissionBlock, 10);
        Assert.That(JsonUtility.ToJson(state.MissionBlock), Is.EqualTo(first));
        Assert.That(state.MissionBlock.ActiveMissions, Is.Empty);
        Assert.That(state.MissionBlock.CompletedMissions, Is.Empty);
        Assert.That(state.MissionBlock.RewardGrantedMissionIds, Is.EquivalentTo(new[] { "paid" }));
        Assert.That(first, Does.Not.Contain("\"MissionRuntimeId\":\"paid\""));
    }

    [Test]
    public void CompletedMissionInActiveList_IsRemovedBeforeSaving()
    {
        var block = new RuntimeMissionSaveBlock();
        var mission = Mission("just_completed", MissionType.Delivery);
        mission.Status = MissionStatus.Completed;
        block.ActiveMissions.Add(mission);

        MissionLifecycleRules.Normalize(block, 4);

        Assert.That(block.ActiveMissions, Is.Empty);
        Assert.That(block.CompletedMissions, Is.Empty);
        Assert.That(block.RewardGrantedMissionIds, Is.EquivalentTo(new[] { "just_completed" }));
        Assert.That(JsonUtility.ToJson(block), Does.Not.Contain("\"MissionRuntimeId\":\"just_completed\""));
    }

    [Test]
    public void PaidMissionOffer_IsRemovedWithoutCompletedHistory()
    {
        var block = new RuntimeMissionSaveBlock();
        block.RewardGrantedMissionIds.Add("paid");
        var offer = Mission("paid", MissionType.Recon);
        offer.Status = MissionStatus.Available;
        block.OffersByPlanet["source"] = new PlanetOfferedMissionData
            { PlanetId = "source", OfferedMission = offer };
        block.OffersByPlanet_List.Add(new PlanetOfferedMissionData
            { PlanetId = "source", OfferedMission = offer });

        MissionLifecycleRules.Normalize(block, 4);

        Assert.That(block.CompletedMissions, Is.Empty);
        Assert.That(block.OffersByPlanet, Is.Empty);
        Assert.That(block.OffersByPlanet_List, Is.Empty);
        Assert.That(block.RewardGrantedMissionIds, Is.EquivalentTo(new[] { "paid" }));
    }

    [Test]
    public void PaidMission_IsNotRestoredAsActiveFromSavedLedger()
    {
        var block = new RuntimeMissionSaveBlock();
        block.RewardGrantedMissionIds.Add("paid");
        block.ActiveMissions.Add(Mission("paid", MissionType.Delivery));

        MissionLifecycleRules.Normalize(block, 4);

        Assert.That(block.ActiveMissions, Is.Empty);
        Assert.That(block.CompletedMissions, Is.Empty);
        Assert.That(block.RewardGrantedMissionIds, Is.EquivalentTo(new[] { "paid" }));
    }

    [TestCase(MissionStatus.Failed)]
    [TestCase(MissionStatus.Cancelled)]
    [TestCase(MissionStatus.Expired)]
    public void TerminalFailures_AreRetainedWithoutReward(MissionStatus outcome)
    {
        var block = new RuntimeMissionSaveBlock();
        var mission = Mission("ended", MissionType.Delivery);
        mission.Status = outcome;
        block.ActiveMissions.Add(mission);
        MissionLifecycleRules.Normalize(block, 4);
        Assert.That(block.ActiveMissions, Is.Empty);
        Assert.That(block.ArchivedMissions[0].Status, Is.EqualTo(outcome));
        Assert.That(block.RewardGrantedMissionIds, Is.Empty);
    }

    [Test]
    public void Deadline_ExpiresExactlyAtConfiguredDay_ZeroMeansUnlimited()
    {
        var mission = Mission("deadline", MissionType.Recon);
        Assert.That(MissionLifecycleRules.IsExpired(mission, 999), Is.False);
        mission.ExpiresAtDay = 12;
        Assert.That(MissionLifecycleRules.IsExpired(mission, 11), Is.False);
        Assert.That(MissionLifecycleRules.IsExpired(mission, 12), Is.True);
        Assert.That(MissionLifecycleRules.IsExpired(mission, 13), Is.True);
    }

    [Test]
    public void MapperRoundTrip_PreservesTargetIdsAndLifecycleFields()
    {
        var mission = Mission("roundtrip", MissionType.Delivery);
        mission.SourcePlanetId = "source_planet";
        mission.TargetPlanetId = "target_planet";
        mission.SourceSystemId = "source_system";
        mission.TargetSystemId = "target_system";
        mission.PirateGroupNpcId = "legacy_group_reference";
        mission.PirateGroupSpawnRuleId = "legacy_rule_reference";
        mission.DurationDays = 5;
        mission.AcceptedAtDay = 10;
        mission.ExpiresAtDay = 15;
        mission.FinishedAtDay = 14;
        mission.LastTransitionReason = "Completed";
        var restored = MissionSaveMapper.ToRuntimeData(MissionSaveMapper.ToSaveData(mission));
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(mission)));
    }

    [Test]
    public void DisabledLegacyOffer_IsRemovedWhileDeliveryOfferRemains()
    {
        var block = new RuntimeMissionSaveBlock();
        var old = Mission("old_offer", MissionType.Elimination);
        var current = Mission("current_offer", MissionType.Delivery);
        old.Status = current.Status = MissionStatus.Available;
        block.OffersByPlanet["old"] = new PlanetOfferedMissionData { PlanetId = "old", OfferedMission = old };
        block.OffersByPlanet["current"] = new PlanetOfferedMissionData { PlanetId = "current", OfferedMission = current };
        MissionLifecycleRules.Normalize(block, 1);
        Assert.That(block.OffersByPlanet.ContainsKey("old"), Is.False);
        Assert.That(block.OffersByPlanet.ContainsKey("current"), Is.True);
    }

    [Test]
    public void LegacyJsonIntegrity_UsesOriginalSchemaAndRejectsTampering()
    {
        // Deliberately omits new fields, as an actual pre-S08 JSON file does.
        const string original = "{\"Meta\":{\"SaveDataVersion\":6,\"IntegrityChecksum\":\"\"},\"Player\":{\"Credits\":1000}}";
        string checksum;
        using (var sha = SHA256.Create())
            checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(original))).Replace("-", "").ToLowerInvariant();
        string json = original.Replace("\"IntegrityChecksum\":\"\"", "\"IntegrityChecksum\":\"" + checksum + "\"");
        var stage = new SaveIntegrityStage();
        Assert.That(stage.VerifySerializedJson(JsonUtility.FromJson<GameRuntimeState>(json), json), Is.EqualTo(SaveIntegrityStatus.Valid));
        string tampered = json.Replace("1000", "9999");
        Assert.That(stage.VerifySerializedJson(JsonUtility.FromJson<GameRuntimeState>(tampered), tampered), Is.EqualTo(SaveIntegrityStatus.Invalid));
    }

    private static MissionInstanceData Mission(string id, MissionType type)
    {
        return new MissionInstanceData
        {
            MissionRuntimeId = id,
            MissionType = type,
            Status = MissionStatus.Accepted,
            Objective = new MissionObjectiveData { RequiredAmount = 1 },
            Reward = new MissionRewardData { Credits = 10, Xp = 1 }
        };
    }
}
