using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarFrontier.Tests.Sprint2
{
    [TestFixture]
    public sealed class Sprint2GalaxyStateRoundTripEditModeTests
    {
        [Test]
        public void RuntimeStateTypes_AreSerializableAndDoNotDependOnUnityObjects()
        {
            Type galaxyType = RequireType("GalaxyRuntimeState");
            Type sectorType = RequireType("SectorRuntimeState");
            Type systemType = RequireType("StarSystemRuntimeState");
            Type routeType = RequireType("RouteRuntimeState");

            foreach (Type type in new[]
                     {
                         galaxyType,
                         sectorType,
                         systemType,
                         routeType
                     })
            {
                Assert.That(
                    type.IsSerializable,
                    Is.True,
                    $"{type.FullName} must be marked [Serializable].");

                Assert.That(
                    typeof(UnityEngine.Object).IsAssignableFrom(type),
                    Is.False,
                    $"{type.FullName} must be a pure State class, not a UnityEngine.Object.");

                List<string> unityObjectFields = type
                    .GetFields(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic)
                    .Where(field =>
                        typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                    .Select(field => field.Name)
                    .ToList();

                Assert.That(
                    unityObjectFields,
                    Is.Empty,
                    $"{type.FullName} contains direct Unity object references: " +
                    string.Join(", ", unityObjectFields));
            }
        }

        [Test]
        public void GalaxyRuntimeState_JsonRoundTripPreservesGalaxySectorSystemAndRouteData()
        {
            Type galaxyType = RequireType("GalaxyRuntimeState");
            Type sectorType = RequireType("SectorRuntimeState");
            Type systemType = RequireType("StarSystemRuntimeState");
            Type routeType = RequireType("RouteRuntimeState");

            object galaxy = Sprint2Reflection.CreateInstance(galaxyType);
            object sector = Sprint2Reflection.CreateInstance(sectorType);
            object systemA = Sprint2Reflection.CreateInstance(systemType);
            object systemB = Sprint2Reflection.CreateInstance(systemType);
            object route = Sprint2Reflection.CreateInstance(routeType);

            Sprint2Reflection.SetRequired(
                galaxy,
                37,
                "GalaxyDay",
                "galaxyDay");

            Sprint2Reflection.SetRequired(
                galaxy,
                "system_test_b",
                "CurrentSystemId",
                "currentSystemId");

            Sprint2Reflection.SetRequired(
                sector,
                "sector_test_01",
                "SectorId",
                "sectorId",
                "Id",
                "id");

            Sprint2Reflection.SetRequired(
                sector,
                true,
                "IsUnlocked",
                "isUnlocked");

            PopulateSystemState(
                systemA,
                "system_test_a",
                isDiscovered: true,
                isVisited: true);

            PopulateSystemState(
                systemB,
                "system_test_b",
                isDiscovered: true,
                isVisited: false);

            Sprint2Reflection.SetRequired(
                route,
                "route_test_a_b",
                "RouteId",
                "routeId",
                "Id",
                "id");

            Sprint2Reflection.SetRequired(
                route,
                true,
                "IsUnlocked",
                "isUnlocked");

            Sprint2Reflection.SetCollectionRequired(
                galaxy,
                new[] { sector },
                "Sectors",
                "sectors");

            Sprint2Reflection.SetCollectionRequired(
                galaxy,
                new[] { systemA, systemB },
                "Systems",
                "systems");

            Sprint2Reflection.SetCollectionRequired(
                galaxy,
                new[] { route },
                "Routes",
                "routes");

            string json = JsonUtility.ToJson(galaxy, true);

            Assert.That(json, Is.Not.Null.And.Not.Empty);
            Assert.That(json, Does.Contain("system_test_b"));
            Assert.That(json, Does.Contain("route_test_a_b"));

            object restored = JsonUtility.FromJson(json, galaxyType);

            Assert.That(restored, Is.Not.Null);
            Assert.That(
                Sprint2Reflection.ReadInt(restored, "GalaxyDay", "galaxyDay"),
                Is.EqualTo(37));

            Assert.That(
                Sprint2Reflection.ReadString(
                    restored,
                    "CurrentSystemId",
                    "currentSystemId"),
                Is.EqualTo("system_test_b"));

            IReadOnlyList<object> restoredSectors =
                Sprint2Reflection.GetItems(restored, "Sectors", "sectors");
            IReadOnlyList<object> restoredSystems =
                Sprint2Reflection.GetItems(restored, "Systems", "systems");
            IReadOnlyList<object> restoredRoutes =
                Sprint2Reflection.GetItems(restored, "Routes", "routes");

            Assert.That(restoredSectors.Count, Is.EqualTo(1));
            Assert.That(restoredSystems.Count, Is.EqualTo(2));
            Assert.That(restoredRoutes.Count, Is.EqualTo(1));

            Assert.That(
                Sprint2Reflection.ReadString(
                    restoredSectors[0],
                    "SectorId",
                    "sectorId",
                    "Id",
                    "id"),
                Is.EqualTo("sector_test_01"));

            Assert.That(
                Sprint2Reflection.ReadBool(
                    restoredSectors[0],
                    "IsUnlocked",
                    "isUnlocked"),
                Is.True);

            object restoredCurrentSystem = restoredSystems.Single(
                state => string.Equals(
                    Sprint2Reflection.ReadString(
                        state,
                        "SystemId",
                        "systemId",
                        "StarSystemId",
                        "starSystemId",
                        "Id",
                        "id"),
                    "system_test_b",
                    StringComparison.Ordinal));

            Assert.That(
                Sprint2Reflection.ReadBool(
                    restoredCurrentSystem,
                    "IsDiscovered",
                    "isDiscovered"),
                Is.True);

            Assert.That(
                Sprint2Reflection.ReadBool(
                    restoredCurrentSystem,
                    "IsVisited",
                    "isVisited"),
                Is.False);

            Assert.That(
                Sprint2Reflection.ReadString(
                    restoredRoutes[0],
                    "RouteId",
                    "routeId",
                    "Id",
                    "id"),
                Is.EqualTo("route_test_a_b"));

            Assert.That(
                Sprint2Reflection.ReadBool(
                    restoredRoutes[0],
                    "IsUnlocked",
                    "isUnlocked"),
                Is.True);
        }
        private static void PopulateSystemState(
            object state,
            string id,
            bool isDiscovered,
            bool isVisited)
        {
            Sprint2Reflection.SetRequired(
                state,
                id,
                "SystemId",
                "systemId",
                "StarSystemId",
                "starSystemId",
                "Id",
                "id");

            Sprint2Reflection.SetRequired(
                state,
                isDiscovered,
                "IsDiscovered",
                "isDiscovered");

            Sprint2Reflection.SetRequired(
                state,
                isVisited,
                "IsVisited",
                "isVisited");
        }

        private static Type RequireType(string typeName)
        {
            Type type = Sprint2Reflection.FindType(typeName);

            Assert.That(
                type,
                Is.Not.Null,
                $"Required runtime type '{typeName}' was not found in loaded assemblies.");

            return type;
        }
    }
}
