#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_BootstrapContractTests
{
    [Test]
    public void Bootstrap_RegistersRequiredSubstageServices()
    {
        Type bootstrapType =
            SfTestReflection.RequireType(
                "Bootstrapper");

        MethodInfo initialize =
            SfTestReflection.RequireMethod(
                bootstrapType,
                "InitializeServices",
                0);

        IReadOnlyList<MethodBase> references =
            SfTestReflection.GetReferencedMethods(
                initialize);

        AssertRegistration(
            references,
            "ISystemGameplayStateService",
            "SystemGameplayStateService");

        AssertRegistration(
            references,
            "IRefuelService",
            "RefuelService");

        AssertRegistration(
            references,
            "ITravelService",
            "TravelService2A");

        AssertRegistration(
            references,
            "ITargetService2A",
            "TargetService2A");

        AssertRegistration(
            references,
            "IInteractionService2A",
            "InteractionService2A");
    }

    [Test]
    public void Bootstrap_RegistersRefuelBeforeTravel()
    {
        Type bootstrapType =
            SfTestReflection.RequireType(
                "Bootstrapper");

        MethodInfo initialize =
            SfTestReflection.RequireMethod(
                bootstrapType,
                "InitializeServices",
                0);

        IReadOnlyList<MethodBase> references =
            SfTestReflection.GetReferencedMethods(
                initialize);

        int refuelIndex =
            FindRegistrationIndex(
                references,
                "IRefuelService",
                "RefuelService");

        int travelIndex =
            FindRegistrationIndex(
                references,
                "ITravelService",
                "TravelService2A");

        Assert.That(
            refuelIndex,
            Is.GreaterThanOrEqualTo(0));

        Assert.That(
            travelIndex,
            Is.GreaterThanOrEqualTo(0));

        Assert.That(
            refuelIndex,
            Is.LessThan(travelIndex),
            "IRefuelService must be registered before TravelService2A is constructed.");
    }

    private static void AssertRegistration(
        IReadOnlyList<MethodBase> methods,
        string interfaceName,
        string implementationName)
    {
        int index =
            FindRegistrationIndex(
                methods,
                interfaceName,
                implementationName);

        Assert.That(
            index,
            Is.GreaterThanOrEqualTo(0),
            "Bootstrapper.InitializeServices does not register " +
            implementationName + " as " + interfaceName + ".");
    }

    private static int FindRegistrationIndex(
        IReadOnlyList<MethodBase> methods,
        string interfaceName,
        string implementationName)
    {
        for (int index = 0;
             index < methods.Count;
             index++)
        {
            if (!(methods[index] is MethodInfo method))
                continue;

            if (!method.IsGenericMethod)
                continue;

            Type[] arguments =
                method.GetGenericArguments();

            bool containsInterface =
                arguments.Any(type =>
                    type.Name == interfaceName);

            bool containsImplementation =
                arguments.Any(type =>
                    type.Name == implementationName);

            if (containsInterface &&
                containsImplementation)
            {
                return index;
            }
        }

        return -1;
    }
}

#endif
