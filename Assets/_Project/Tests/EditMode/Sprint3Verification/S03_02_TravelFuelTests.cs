#if UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_TravelFuelTests
{
    [Test]
    public void TravelService2A_DependsOnRefuelService()
    {
        Type travelType =
            SfTestReflection.RequireType(
                "TravelService2A");

        Type refuelInterface =
            SfTestReflection.RequireType(
                "IRefuelService");

        bool hasDependency =
            travelType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Any(field =>
                    field.FieldType ==
                    refuelInterface);

        Assert.That(
            hasDependency,
            Is.True,
            "TravelService2A must receive/use IRefuelService instead of mutating CurrentFuel directly.");
    }

    [Test]
    public void TravelValidation_UsesCanConsume()
    {
        Type travelType =
            SfTestReflection.RequireType(
                "TravelService2A");

        MethodInfo validation =
            SfTestReflection.RequireMethod(
                travelType,
                "GetTravelFailReason",
                2);

        Assert.That(
            SfTestReflection.MethodReferences(
                validation,
                "IRefuelService",
                "CanConsume") ||
            SfTestReflection.MethodReferences(
                validation,
                "RefuelService",
                "CanConsume"),
            Is.True,
            "GetTravelFailReason must use the authoritative fuel validation.");
    }

    [Test]
    public void SuccessfulTravel_UsesConsumeAndRequestsOneFinalSave()
    {
        Type travelType =
            SfTestReflection.RequireType(
                "TravelService2A");

        MethodInfo tryTravel =
            SfTestReflection.RequireMethod(
                travelType,
                "TryTravel",
                1);

        Assert.That(
            SfTestReflection.MethodReferences(
                tryTravel,
                "IRefuelService",
                "Consume") ||
            SfTestReflection.MethodReferences(
                tryTravel,
                "RefuelService",
                "Consume"),
            Is.True,
            "TryTravel must consume fuel through IRefuelService.");

        Assert.That(
            SfTestReflection.MethodReferences(
                tryTravel,
                "SaveNeedEvent"),
            Is.True,
            "TravelService2A must publish SaveNeedEvent after the complete transaction.");

        Assert.That(
            SfTestReflection.MethodReferences(
                tryTravel,
                "TravelFinishedEvent"),
            Is.True);
    }

    [Test]
    public void LocalSystemTravel_DoesNotConsumeStrategicFuel()
    {
        Type localTravelType =
            SfTestReflection.RequireType(
                "SystemTravelService");

        foreach (MethodInfo method in localTravelType.GetMethods(
                     BindingFlags.Instance |
                     BindingFlags.Public |
                     BindingFlags.NonPublic))
        {
            bool consumesFuel =
                SfTestReflection.MethodReferences(
                    method,
                    "IRefuelService",
                    "Consume") ||
                SfTestReflection.MethodReferences(
                    method,
                    "RefuelService",
                    "Consume");

            Assert.That(
                consumesFuel,
                Is.False,
                "SystemTravelService." + method.Name +
                " must not consume inter-system fuel.");
        }
    }

    [Test]
    public void TravelContracts_ExposePreviewCostAndNegativeReason()
    {
        Type interfaceType =
            SfTestReflection.RequireType(
                "ITravelService");

        Assert.That(
            interfaceType.GetMethods()
                .Any(method =>
                    method.Name == "GetTravelCost" &&
                    method.ReturnType == typeof(int)),
            Is.True);

        Assert.That(
            interfaceType.GetMethods()
                .Any(method =>
                    method.Name == "GetTravelFailReason"),
            Is.True);

        Type failType =
            SfTestReflection.RequireType(
                "TravelFailReason");

        SfTestReflection.AssertEnumContains(
            failType,
            "None",
            "NotEnoughFuel");
    }

    [Test]
    public void TravelResult_ContainsExactFuelSpent()
    {
        Type resultType =
            SfTestReflection.RequireType(
                "TravelResult");

        string[] requiredMembers =
        {
            "Success",
            "FuelSpent",
            "FailReason",
            "FromSystemId",
            "ToSystemId"
        };

        foreach (string memberName in requiredMembers)
        {
            bool found =
                resultType.GetMember(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Length > 0;

            Assert.That(
                found,
                Is.True,
                "TravelResult must expose " +
                memberName + ".");
        }
    }
}

#endif
