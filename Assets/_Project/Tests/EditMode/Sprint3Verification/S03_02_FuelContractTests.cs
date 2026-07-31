#if UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_FuelContractTests
{
    [Test]
    public void Fuel_Interface_ContainsRequiredOperations()
    {
        Type interfaceType =
            SfTestReflection.RequireType("IRefuelService");

        Assert.That(interfaceType.IsInterface, Is.True);

        AssertMethod(interfaceType, "GetCurrentFuel", 0, typeof(int));
        AssertMethod(interfaceType, "GetFuelCapacity", 0, typeof(int));
        AssertMethod(interfaceType, "GetFuelUnitPrice", 0, typeof(int));
        AssertMethod(interfaceType, "CanRefuel", 1, typeof(bool));
        AssertMethod(interfaceType, "Refuel", 1, null);
        AssertMethod(interfaceType, "RefuelToFull", 0, null);

        Type reasonType =
            SfTestReflection.RequireType("FuelConsumeFailReason");

        MethodInfo canConsume =
            interfaceType.GetMethods()
                .FirstOrDefault(method =>
                    method.Name == "CanConsume" &&
                    method.ReturnType == typeof(bool));

        MethodInfo consume =
            interfaceType.GetMethods()
                .FirstOrDefault(method =>
                    method.Name == "Consume" &&
                    method.ReturnType == typeof(bool));

        Assert.That(canConsume, Is.Not.Null);
        Assert.That(consume, Is.Not.Null);

        AssertOutReason(canConsume, reasonType);
        AssertOutReason(consume, reasonType);
    }

    [Test]
    public void Fuel_FailReason_ContainsRequiredValues()
    {
        Type reasonType =
            SfTestReflection.RequireType("FuelConsumeFailReason");

        SfTestReflection.AssertEnumContains(
            reasonType,
            "None",
            "MissingPlayerProfile",
            "MissingPlayerShipState",
            "MissingActiveShip",
            "InvalidFuelCount",
            "NotEnoughFuel");
    }

    [Test]
    public void Refuel_ResultType_ContainsDefensiveValues()
    {
        Type resultType =
            SfTestReflection.RequireType("RefuelResultType");

        SfTestReflection.AssertEnumContains(
            resultType,
            "Success",
            "InvalidFuelCount",
            "MissingActiveShip",
            "RefuelUnavailable",
            "FuelCapacityExceeded");
    }

    [Test]
    public void RefuelService_HasTestableDependencyConstructor()
    {
        Type serviceType =
            SfTestReflection.RequireType("RefuelService");

        Type sessionType =
            SfTestReflection.RequireType("IGameSessionService");

        Type busType =
            SfTestReflection.RequireType("SimpleEventBus");

        Type costType =
            SfTestReflection.RequireType("IRefuelCostProvider");

        Type availabilityType =
            SfTestReflection.RequireType("IRefuelAvailabilityProvider");

        ConstructorInfo constructor =
            serviceType.GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                {
                    Type[] parameters = candidate
                        .GetParameters()
                        .Select(parameter => parameter.ParameterType)
                        .ToArray();

                    return parameters.Length == 4 &&
                           parameters[0] == sessionType &&
                           parameters[1] == busType &&
                           parameters[2] == costType &&
                           parameters[3] == availabilityType;
                });

        Assert.That(
            constructor,
            Is.Not.Null,
            "RefuelService needs an explicit dependency constructor for deterministic EditMode tests.");
    }

    [Test]
    public void Consume_PublishesFuelChanged_ButDoesNotRequestSaveDirectly()
    {
        Type serviceType =
            SfTestReflection.RequireType("RefuelService");

        MethodInfo consume =
            SfTestReflection.RequireMethod(
                serviceType,
                "Consume",
                2);

        Assert.That(
            SfTestReflection.MethodReferences(
                consume,
                "FuelChangedEvent"),
            Is.True,
            "Consume must publish FuelChangedEvent after successful mutation.");

        Assert.That(
            SfTestReflection.MethodReferences(
                consume,
                "SaveNeedEvent"),
            Is.False,
            "Consume must not save a partial travel transaction. TravelService2A owns the final SaveNeedEvent.");
    }

    [Test]
    public void Refuel_RequestsSaveAfterStandaloneTransaction()
    {
        Type serviceType =
            SfTestReflection.RequireType("RefuelService");

        MethodInfo refuel =
            SfTestReflection.RequireMethod(
                serviceType,
                "Refuel",
                1);

        Assert.That(
            SfTestReflection.MethodReferences(
                refuel,
                "FuelChangedEvent"),
            Is.True);

        Assert.That(
            SfTestReflection.MethodReferences(
                refuel,
                "CreditsChangedEvent"),
            Is.True);

        Assert.That(
            SfTestReflection.MethodReferences(
                refuel,
                "SaveNeedEvent"),
            Is.True,
            "Standalone refuel must request save after Fuel and Credits are updated.");
    }

    private static void AssertMethod(
        Type type,
        string name,
        int parameterCount,
        Type returnType)
    {
        MethodInfo method =
            type.GetMethods()
                .FirstOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == parameterCount);

        Assert.That(
            method,
            Is.Not.Null,
            type.Name + "." + name + " was not found.");

        if (returnType != null)
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
    }

    private static void AssertOutReason(
        MethodInfo method,
        Type reasonType)
    {
        ParameterInfo[] parameters =
            method.GetParameters();

        Assert.That(parameters.Length, Is.EqualTo(2));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)));
        Assert.That(parameters[1].IsOut, Is.True);
        Assert.That(
            parameters[1].ParameterType,
            Is.EqualTo(reasonType.MakeByRefType()));
    }
}

#endif
