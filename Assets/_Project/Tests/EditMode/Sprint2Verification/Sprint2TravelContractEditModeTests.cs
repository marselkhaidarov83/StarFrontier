using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarFrontier.Tests.Sprint2
{
    [TestFixture]
    public sealed class Sprint2TravelContractEditModeTests
    {
        [Test]
        public void TravelContract_IsExposedByAConcreteRuntimeType()
        {
            Type[] allTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(type => type != null)
                .ToArray();

            List<Type> matchingTypes = allTypes
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    HasPublicInstanceMethod(type, "CanTravel") &&
                    HasPublicInstanceMethod(type, "GetTravelFailReason"))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();

            if (matchingTypes.Count == 0)
            {
                string candidates = string.Join(
                    Environment.NewLine,
                    allTypes
                        .Where(type =>
                            type.Name.IndexOf(
                                "Travel",
                                StringComparison.OrdinalIgnoreCase) >= 0 ||
                            type.Name.IndexOf(
                                "Route",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(type => type.FullName, StringComparer.Ordinal)
                        .Select(DescribeType)
                        .Take(40));

                Assert.Fail(
                    "No concrete runtime type exposes both public methods " +
                    "'CanTravel' and 'GetTravelFailReason'." +
                    Environment.NewLine +
                    "Travel/Route candidates loaded by Unity:" +
                    Environment.NewLine +
                    (string.IsNullOrEmpty(candidates)
                        ? "<none>"
                        : candidates));
            }

            foreach (Type matchingType in matchingTypes)
            {
                Debug.Log(
                    "Sprint 2 travel contract provider: " +
                    matchingType.FullName);
            }

            Assert.That(
                matchingTypes,
                Is.Not.Empty,
                "A concrete travel contract provider must be present.");
        }

        [Test]
        public void RouteConfig_ContainsRequiredBidirectionalLookupContract()
        {
            Type type = typeof(RouteConfig);

            foreach (string methodName in new[]
                     {
                         "ContainsSystem",
                         "ConnectsSystems",
                         "GetOtherSystem",
                         "GetEndPointForSystem",
                         "GetDepartureEndpoint",
                         "GetArrivalEndpoint",
                         "GetExitPoint",
                         "GetEntryPoint"
                     })
            {
                AssertPublicMethod(type, methodName);
            }
        }

        private static bool HasPublicInstanceMethod(
            Type type,
            string methodName)
        {
            return type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Any(method =>
                    string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal));
        }

        private static void AssertPublicMethod(
            Type type,
            string methodName)
        {
            MethodInfo[] methods = type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method =>
                    string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal))
                .ToArray();

            Assert.That(
                methods,
                Is.Not.Empty,
                $"{type.FullName} does not expose public method '{methodName}'.");
        }

        private static string DescribeType(Type type)
        {
            string methods = string.Join(
                ", ",
                type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Select(method => method.Name)
                    .Distinct()
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Take(30));

            return $"{type.FullName}: {methods}";
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
