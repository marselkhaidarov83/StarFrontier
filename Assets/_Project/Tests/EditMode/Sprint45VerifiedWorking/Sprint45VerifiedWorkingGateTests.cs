using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;

public sealed class Sprint45VerifiedWorkingGateTests
{
    private const string ExpectedBranch =
        "stage-2a-sprint-4-2026.08.03";

    private const string ExpectedSha =
        "481c09cedaf0ce11bf43d00a633ebafed94dc790";

    private const string SmokeEvidencePath =
        "Assets/_Project/QA/VerifiedWorking/Sprint45VerifiedWorkingSmokeEvidence.md";

    [Test]
    public void Sprint4_VerifiedWorking_RequiresCombatCoreCoverage()
    {
        AssertExistingTests(
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_01_DamageServiceEditModeTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_01_TargetingCombatContractTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_01_ProjectileLifecycleEditModeTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_01_CombatStateEditModeTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_02_SystemMapCombatLifecycleTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_03_ShieldBeforeHullDamageTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_03_EnemyDestroyedCanonicalEventTests.cs");
    }

    [Test]
    public void Sprint4_VerifiedWorking_RequiresCombatVisualAndUiCoverage()
    {
        AssertExistingTests(
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_03_ProjectileVisualPlayModeTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_03_ProjectileVisualLifecycleTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T01_WeaponTargetMarkerTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T02_EnemyStatusMarkerTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T03_PlayerThreatListHudTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T04_PlayerWeaponListHudTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T05_CombatMarkerVisibilityTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T06_CombatUiCleanupTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T08_CombatUiMarkerLifecycleTests.cs");
    }

    [Test]
    public void Sprint4_VerifiedWorking_RequiresRegressionAndBindingGates()
    {
        AssertExistingTests(
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_05_T06_CombatSceneBindingsValidatorTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_05_T07_Sprint1_3_RegressionGateTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint4Verification/S04_04_T07_CombatUiAndroidReadabilityTests.cs");
    }

    [Test]
    public void Sprint5_VerifiedWorking_RequiresPopulationLifecycleCoverage()
    {
        AssertExistingTests(
            "Assets/_Project/Tests/EditMode/Sprint5Verification/SystemPopulationGalaxyLevelEditModeTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint5Verification/SystemNpcPersistentSaveEditModeTests.cs");
    }

    [Test]
    public void Sprint5_VerifiedWorking_RequiresNpcRoleActivityStatusAndDebugCoverage()
    {
        AssertExistingTests(
            "Assets/_Project/Tests/EditMode/Sprint5Verification/S05_02_AllyRoleScenarioMatrixEditModeTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint5Verification/S05_03_T08_NpcActivityVerificationTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint5Verification/S05_04_NpcIdentificationAndStatusPanelTests.cs",
            "Assets/_Project/Tests/EditMode/Sprint5Verification/S05_05_NpcDebugVerificationTests.cs");
    }

    [Test]
    public void VerifiedWorking_RequiresEnoughSprint4AndSprint5TestMethods()
    {
        Assert.That(
            CountTestMethods("Assets/_Project/Tests/EditMode/Sprint4Verification"),
            Is.GreaterThanOrEqualTo(148));

        Assert.That(
            CountTestMethods("Assets/_Project/Tests/EditMode/Sprint5Verification"),
            Is.GreaterThanOrEqualTo(29));
    }

    private static void AssertExistingTests(params string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
            AssertFileExists(paths[i]);
    }

    private static void AssertFileExists(string path)
    {
        Assert.That(File.Exists(path), Is.True, "Missing file: " + path);
    }

    private static int CountTestMethods(string folder)
    {
        Assert.That(Directory.Exists(folder), Is.True, "Missing folder: " + folder);

        int count = 0;
        string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            string text = File.ReadAllText(files[i]);
            count += Regex.Matches(text, @"\[(Unity)?Test\]").Count;
        }

        return count;
    }

    private static void RequireAll(string text, params string[] required)
    {
        for (int i = 0; i < required.Length; i++)
            Assert.That(text, Does.Contain(required[i]));
    }

    private static void RequireAny(string text, params string[] expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (text.Contains(expected[i]))
                return;
        }

        Assert.Fail("Expected at least one marker: " + string.Join(", ", expected));
    }
}