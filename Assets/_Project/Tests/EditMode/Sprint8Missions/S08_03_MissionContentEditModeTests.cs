using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

public sealed class S08_03_MissionContentEditModeTests
{
    private const string Root = "Assets/_Project/Content/Configs/";
    private static readonly HashSet<string> SupportedTokens = new()
    {
        "sourceSystem", "sourcePlanet", "targetSystem", "targetPlanet", "itemName",
        "amount", "cargoVolume", "enemyJumps", "fuelCost", "enemyCount", "scanDistance"
    };

    [TestCase("mission_delivery_basic_01.asset", MissionType.Delivery)]
    [TestCase("mission_delivery_basic_02.asset", MissionType.Delivery)]
    [TestCase("mission_recon_jump_point_01.asset", MissionType.Recon)]
    [TestCase("mission_recon_jump_point_02.asset", MissionType.Recon)]
    [TestCase("mission_route_unlock_01.asset", MissionType.RouteSearch)]
    [TestCase("mission_route_improve_01.asset", MissionType.RouteSearch)]
    public void ActiveMissionTemplate_HasObjectiveAndOutcomeCopy(string file, MissionType type)
    {
        string path = Root + "MissionTemplates/" + file;
        var template = AssetDatabase.LoadAssetAtPath<MissionTemplateConfig>(path);
        Assert.That(template, Is.Not.Null, path);
        Assert.That(template.MissionType, Is.EqualTo(type), path);
        Assert.That(template.TitlePattern, Is.Not.Empty, path);
        Assert.That(template.DescriptionPattern, Is.Not.Empty, path);
        Assert.That(template.OutcomeTexts, Is.Not.Null, path);
        Assert.That(template.OutcomeTexts.ReadyToTurnIn, Is.Not.Empty, path);
        Assert.That(template.OutcomeTexts.Completed, Is.Not.Empty, path);
        Assert.That(template.OutcomeTexts.Failed, Is.Not.Empty, path);
        AssertSupportedTokens(template.TitlePattern, path);
        AssertSupportedTokens(template.DescriptionPattern, path);
        AssertSupportedTokens(template.OutcomeTexts.ReadyToTurnIn, path);
        AssertSupportedTokens(template.OutcomeTexts.Completed, path);

        if (type == MissionType.Delivery)
        {
            Assert.That(template.OutcomeTexts.MissionCargoLost, Is.Not.Empty, path);
            Assert.That(template.OutcomeTexts.SystemCaptured, Is.Not.Empty, path);
            AssertSupportedTokens(template.OutcomeTexts.MissionCargoLost, path);
            AssertSupportedTokens(template.OutcomeTexts.SystemCaptured, path);
        }
        if (type == MissionType.RouteSearch)
            Assert.That(template.OutcomeTexts.RouteObjectiveUnavailable, Is.Not.Empty, path);
    }

    [Test]
    public void BattleGameplayConfig_HasSeparateDefenseAndLiberationText()
    {
        var game = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "Game/gameConfig_01.asset");
        Assert.That(game, Is.Not.Null);
        var text = game.MissionGameplay;
        Assert.That(text, Is.Not.Null);
        Assert.That(text.DefenseTitlePattern, Is.Not.Empty);
        Assert.That(text.LiberationTitlePattern, Is.Not.Empty);
        Assert.That(text.DefenseDescriptionPattern, Is.Not.Empty);
        Assert.That(text.LiberationDescriptionPattern, Is.Not.Empty);
        Assert.That(text.DefenseOutcomeTexts, Is.Not.Null);
        Assert.That(text.LiberationOutcomeTexts, Is.Not.Null);
        Assert.That(text.DefenseOutcomeTexts.ReadyToTurnIn, Is.Not.Empty);
        Assert.That(text.LiberationOutcomeTexts.ReadyToTurnIn, Is.Not.Empty);
        Assert.That(text.DefenseOutcomeTexts.NextInvasionStarted, Is.Not.Empty);
        Assert.That(text.LiberationOutcomeTexts.NextInvasionStarted, Is.Not.Empty);
        AssertSupportedTokens(text.DefenseDescriptionPattern, "defense");
        AssertSupportedTokens(text.LiberationDescriptionPattern, "liberation");
    }

    private static void AssertSupportedTokens(string value, string context)
    {
        foreach (Match match in Regex.Matches(value ?? string.Empty, @"\{([^{}]+)\}"))
            Assert.That(SupportedTokens.Contains(match.Groups[1].Value), Is.True,
                context + ": unknown text token " + match.Value);
    }
}
