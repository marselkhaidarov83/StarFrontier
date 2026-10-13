using System.IO;
using NUnit.Framework;

public sealed class Stage2A_S06_04_T08_WarMapUiVerificationTests
{
    [Test]
    public void T08_MapStateMapping_UsesWarStatusColorsAndIcons()
    {
        string nodeViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/System/StarSystemNodeView2A.cs");

        Assert.That(nodeViewText, Does.Contain("GetWarStateColor"));
        Assert.That(nodeViewText, Does.Contain("ApplyWarStateIcon"));
        Assert.That(nodeViewText, Does.Contain("warStateIcon"));

        Assert.That(nodeViewText, Does.Contain("case StarSystemStatus.Threat"));
        Assert.That(nodeViewText, Does.Contain("case StarSystemStatus.Invasion"));
        Assert.That(nodeViewText, Does.Contain("case StarSystemStatus.Captured"));

        Assert.That(nodeViewText, Does.Contain("systemColorThreat"));
        Assert.That(nodeViewText, Does.Contain("systemColorInvasion"));
        Assert.That(nodeViewText, Does.Contain("systemColorCaptured"));

        Assert.That(nodeViewText, Does.Contain("warThreatSprite"));
        Assert.That(nodeViewText, Does.Contain("warInvasionSprite"));
        Assert.That(nodeViewText, Does.Contain("warCapturedSprite"));
    }

    [Test]
    public void T08_MapUi_DoesNotUsePermanentThreatTextLabels()
    {
        string nodeViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/System/StarSystemNodeView2A.cs");

        Assert.That(
            nodeViewText,
            Does.Not.Contain("SetText(\"Threat"),
            "Threat state must not be shown as permanent text label on the map.");

        Assert.That(
            nodeViewText,
            Does.Not.Contain("SetText(\"Invasion"),
            "Invasion state must not be shown as permanent text label on the map.");

        Assert.That(
            nodeViewText,
            Does.Not.Contain("SetText(\"Captured"),
            "Captured state must not be shown as permanent text label on the map.");
    }

    [Test]
    public void T08_InvasionWarnings_UsePriorityAndSupportSimultaneousInvasions()
    {
        string builderText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/System/GalaxyMapSystemBuilder.cs");

        Assert.That(builderText, Does.Contain("TryGetPriorityInvasion"));
        Assert.That(builderText, Does.Contain("GetInvasionPriority"));
        Assert.That(builderText, Does.Contain("GetActiveInvasions"));
        Assert.That(builderText, Does.Contain("EscalationPressure"));

        Assert.That(builderText, Does.Contain("InvasionLifecycleState.Active"));
        Assert.That(builderText, Does.Contain("InvasionLifecycleState.Preparing"));

        Assert.That(
            builderText,
            Does.Contain("for (int i = 0; i < invasions.Count; i++)"),
            "The map must inspect multiple active invasions and choose the priority one.");
    }

    [Test]
    public void T08_InvasionWarnings_HaveSpamLimits()
    {
        string builderText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/System/GalaxyMapSystemBuilder.cs");

        Assert.That(builderText, Does.Contain("invasionWarningCooldownSeconds"));
        Assert.That(builderText, Does.Contain("maxInvasionWarningsPerMapSession"));
        Assert.That(builderText, Does.Contain("CanShowInvasionWarningNow"));
        Assert.That(builderText, Does.Contain("MarkInvasionWarningShown"));
        Assert.That(builderText, Does.Contain("Time.unscaledTime"));
        Assert.That(builderText, Does.Contain("lastShownInvasionWarningId"));
    }

    [Test]
    public void T08_WarNewsEvents_ArePublishedForNewsIntegration()
    {
        string eventText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Data/Events/War/WarNewsItemCreatedEvent.cs");

        string invasionServiceText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Core/Services/Enemy/InvasionService.cs");

        Assert.That(eventText, Does.Contain("WarNewsItemCreatedEvent"));
        Assert.That(eventText, Does.Contain("WarNewsEventKind"));
        Assert.That(eventText, Does.Contain("InvasionStarted"));
        Assert.That(eventText, Does.Contain("SystemCaptured"));
        Assert.That(eventText, Does.Contain("SystemLiberated"));
        Assert.That(eventText, Does.Contain("InvasionCancelled"));

        Assert.That(invasionServiceText, Does.Contain("PublishWarNews"));
        Assert.That(invasionServiceText, Does.Contain("WarNewsEventKind.InvasionStarted"));
        Assert.That(invasionServiceText, Does.Contain("WarNewsEventKind.SystemCaptured"));
        Assert.That(invasionServiceText, Does.Contain("WarNewsEventKind.SystemLiberated"));
        Assert.That(invasionServiceText, Does.Contain("WarNewsEventKind.InvasionCancelled"));
    }

    [Test]
    public void T08_GalaxyMapUi_IsReadOnlyForWarState()
    {
        string nodeViewText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/System/StarSystemNodeView2A.cs");

        string builderText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/System/GalaxyMapSystemBuilder.cs");

        AssertUiDoesNotMutateWarState(nodeViewText);
        AssertUiDoesNotMutateWarState(builderText);
    }

    [Test]
    public void T08_GalaxyScene_UsesSafeAreaAndTouchCameraSupport()
    {
        string sceneText =
            ReadProjectFile(
                "Assets/_Project/Scenes/GalaxyScene.unity");

        string viewFitterText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/Camera/GalaxyMapViewFitter.cs");

        string cameraText =
            ReadProjectFile(
                "Assets/_Project/Scripts/Presentation/Galaxy/Camera/MapCameraController2A.cs");

        Assert.That(sceneText, Does.Contain("useSafeArea: 1"));
        Assert.That(sceneText, Does.Contain("SystemHudSafeAreaAdapter2A"));

        Assert.That(viewFitterText, Does.Contain("Screen.safeArea"));

        Assert.That(cameraText, Does.Contain("EnhancedTouchSupport.Enable"));
        Assert.That(cameraText, Does.Contain("HandleTouchDrag"));
        Assert.That(cameraText, Does.Contain("HandleTouchZoom"));
    }

    private static void AssertUiDoesNotMutateWarState(
        string sourceText)
    {
        Assert.That(sourceText, Does.Not.Contain("SetSystemStatus("));
        Assert.That(sourceText, Does.Not.Contain("MarkSystemThreat("));
        Assert.That(sourceText, Does.Not.Contain("TryStartInvasion("));
        Assert.That(sourceText, Does.Not.Contain("ResolveInvasion("));
        Assert.That(sourceText, Does.Not.Contain("CancelInvasion("));
        Assert.That(sourceText, Does.Not.Contain("CaptureSystem("));
        Assert.That(sourceText, Does.Not.Contain("LiberateSystemByPlayer("));
    }

    private static string ReadProjectFile(
        string path)
    {
        Assert.That(
            File.Exists(path),
            Is.True,
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}