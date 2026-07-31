#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class Stage2A_S03_03_03_TargetRecenterAndClearStateEditModeTests
{
    [SetUp]
    public void OpenScene()
    {
        Stage2A_S03_03_TestUtility.OpenSystemScene();
    }

    [Test]
    public void SystemDestinationMarkerController2_ExistsInSystemScene()
    {
        MonoBehaviour controller =
            Stage2A_S03_03_TestUtility
                .FindMonoBehaviourInOpenScenes(
                    "SystemDestinationMarkerController2");

        Assert.NotNull(
            controller,
            "SystemDestinationMarkerController2 is missing from SystemScene.");
    }

    [Test]
    public void DestinationMarkerController_HasPlanetAndMapPointMarkersAssigned()
    {
        MonoBehaviour controller =
            Stage2A_S03_03_TestUtility
                .FindMonoBehaviourInOpenScenes(
                    "SystemDestinationMarkerController2");

        Assert.NotNull(controller);

        object planetMarker =
            Stage2A_S03_03_TestUtility
                .GetSerializedObjectReference(
                    controller,
                    "planetDestinationMarker");

        object mapPointMarker =
            Stage2A_S03_03_TestUtility
                .GetSerializedObjectReference(
                    controller,
                    "mapPointDestinationMarker");

        Assert.NotNull(
            planetMarker,
            "planetDestinationMarker is not assigned.");

        Assert.NotNull(
            mapPointMarker,
            "mapPointDestinationMarker is not assigned.");
    }

    [Test]
    public void SystemTravelServiceContract_HasCancelTravelForClearTargetFlow()
    {
        Type interfaceType =
            Stage2A_S03_03_TestUtility
                .FindType("ISystemTravelService");

        Assert.NotNull(
            interfaceType,
            "ISystemTravelService is missing.");

        Assert.NotNull(
            Stage2A_S03_03_TestUtility
                .FindParameterlessMethod(
                    interfaceType,
                    "CancelTravel"),
            "ISystemTravelService must expose CancelTravel for clear target flow.");
    }

    [Test]
    public void RecenterOrReturnToShipButton_ExistsAndHasButtonComponent()
    {
        string[] candidateNames =
        {
            "ReturnToShipButton",
            "RecenterButton",
            "GoToShipButton",
            "BackToShipButton"
        };

        GameObject buttonObject =
            candidateNames
                .Select(Stage2A_S03_03_TestUtility.FindGameObjectInOpenScenes)
                .FirstOrDefault(candidate => candidate != null);

        Assert.NotNull(
            buttonObject,
            "Recenter / ReturnToShip HUD button is missing.");

        Assert.NotNull(
            buttonObject.GetComponent<Button>(),
            "Recenter / ReturnToShip HUD object must have UnityEngine.UI.Button.");
    }

    [Test]
    public void PlayerShipMarker_ExistsInSystemScene()
    {
        GameObject shipMarker =
            Stage2A_S03_03_TestUtility
                .FindGameObjectInOpenScenes("ShipMarker");

        Assert.NotNull(
            shipMarker,
            "ShipMarker is missing from SystemScene.");

        bool hasView =
            Stage2A_S03_03_TestUtility
                .HasMonoBehaviourNamed(
                    shipMarker,
                    "ShipMarkerView2") ||
            shipMarker
                .GetComponentsInChildren<MonoBehaviour>(true)
                .Any(component =>
                    component != null &&
                    component.GetType().Name == "ShipMarkerView2");

        Assert.IsTrue(
            hasView,
            "ShipMarker must have ShipMarkerView2 in itself or children.");
    }
}
#endif
