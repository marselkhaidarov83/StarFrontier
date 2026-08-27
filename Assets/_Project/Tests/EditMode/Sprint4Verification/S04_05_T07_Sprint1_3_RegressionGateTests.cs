using NUnit.Framework;
using System.IO;
using System.Linq;

public sealed class S04_05_T07_Sprint1_3_RegressionGateTests
{
    private const string Sprint1Path =
        "Assets/_Project/Tests/EditMode/Sprint1Verification";

    private const string Sprint2Path =
        "Assets/_Project/Tests/EditMode/Sprint2Verification";

    private const string Sprint3Path =
        "Assets/_Project/Tests/EditMode/Sprint3Verification";

    private const string ReportPath =
        "Assets/_Project/QA/S04_05_T07_Sprint1_3_Regression_Report.txt";

    [Test]
    public void Sprint1RegressionTests_ArePresent()
    {
        AssertTestFolderHasTests(
            Sprint1Path,
            "Sprint 1 regression test folder must contain EditMode tests.");
    }

    [Test]
    public void Sprint2RegressionTests_ArePresent()
    {
        AssertTestFolderHasTests(
            Sprint2Path,
            "Sprint 2 regression test folder must contain EditMode tests.");
    }

    [Test]
    public void Sprint3RegressionTests_ArePresent()
    {
        AssertTestFolderHasTests(
            Sprint3Path,
            "Sprint 3 regression test folder must contain EditMode tests.");
    }

    [Test]
    public void RegressionReport_Exists()
    {
        Assert.IsTrue(
            File.Exists(ReportPath),
            "T07 regression report must exist: " + ReportPath);
    }

    [Test]
    public void RegressionReport_ContainsRequiredSections()
    {
        Assert.IsTrue(
            File.Exists(ReportPath),
            "T07 regression report must exist before section validation.");

        string text =
            File.ReadAllText(ReportPath);

        Assert.IsTrue(
            text.Contains("Unity compile:"),
            "Regression report must record Unity compile result.");

        Assert.IsTrue(
            text.Contains("EditMode Sprint 1:"),
            "Regression report must record Sprint 1 EditMode result.");

        Assert.IsTrue(
            text.Contains("EditMode Sprint 2:"),
            "Regression report must record Sprint 2 EditMode result.");

        Assert.IsTrue(
            text.Contains("EditMode Sprint 3:"),
            "Regression report must record Sprint 3 EditMode result.");

        Assert.IsTrue(
            text.Contains("EditMode Sprint 4 smoke:"),
            "Regression report must record Sprint 4 smoke result.");

        Assert.IsTrue(
            text.Contains("Manual smoke:"),
            "Regression report must record manual smoke result.");

        Assert.IsTrue(
            text.Contains("Bugs found:"),
            "Regression report must record found bugs.");

        Assert.IsTrue(
            text.Contains("Result:"),
            "Regression report must record final result.");
    }

    private static void AssertTestFolderHasTests(
        string folderPath,
        string message)
    {
        Assert.IsTrue(
            Directory.Exists(folderPath),
            "Missing test folder: " + folderPath);

        string[] testFiles =
            Directory.GetFiles(
                folderPath,
                "*.cs",
                SearchOption.AllDirectories);

        Assert.IsTrue(
            testFiles.Any(path => File.ReadAllText(path).Contains("[Test]")),
            message);
    }
}
