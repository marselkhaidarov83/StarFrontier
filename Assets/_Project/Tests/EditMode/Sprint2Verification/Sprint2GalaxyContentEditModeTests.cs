using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace StarFrontier.Tests.Sprint2
{
    [TestFixture]
    public sealed class Sprint2GalaxyContentEditModeTests
    {
        [Test]
        public void CanonicalGalaxy_HasTenOrderedSectorsAndFiftyOneUniqueSystems()
        {
            GalaxyConfig galaxy = Sprint2TestData.LoadCanonicalGalaxy();
            List<SectorConfig> sectors = Sprint2TestData.GetSectors(galaxy);

            Assert.That(
                sectors.Count,
                Is.EqualTo(10),
                "The canonical galaxy must contain exactly 10 sectors.");

            List<SectorConfig> ordered = sectors.OrderBy(x => x.Order).ToList();

            for (int index = 0; index < ordered.Count; index++)
            {
                SectorConfig sector = ordered[index];
                int expectedOrder = index + 1;
                int expectedSystems = expectedOrder == 10 ? 6 : 5;

                Assert.That(
                    sector.Order,
                    Is.EqualTo(expectedOrder),
                    $"Unexpected sector order in {Sprint2TestData.AssetPath(sector)}.");

                Assert.That(
                    sector.Systems,
                    Is.Not.Null,
                    $"Sector '{sector.Id}' has a null Systems array.");

                Assert.That(
                    sector.Systems.Count(system => system != null),
                    Is.EqualTo(expectedSystems),
                    $"Sector '{sector.Id}' must contain {expectedSystems} systems.");
            }

            List<StarSystemConfig> systems = Sprint2TestData.GetSystems(galaxy);

            Assert.That(
                systems.Count,
                Is.EqualTo(51),
                "The canonical galaxy must contain exactly 51 system references.");

            AssertUniqueNonEmptyIds(
                systems.Select(system => (system.Id, Sprint2TestData.AssetPath(system))),
                "StarSystemConfig");

            Assert.That(
                systems.Distinct().Count(),
                Is.EqualTo(51),
                "One StarSystemConfig is assigned to more than one sector.");
        }

        [Test]
        public void CanonicalGalaxy_HasUniqueSectorIdsAndExactlyOneStartSystem()
        {
            GalaxyConfig galaxy = Sprint2TestData.LoadCanonicalGalaxy();
            List<SectorConfig> sectors = Sprint2TestData.GetSectors(galaxy);
            List<StarSystemConfig> systems = Sprint2TestData.GetSystems(galaxy);

            AssertUniqueNonEmptyIds(
                sectors.Select(sector => (sector.Id, Sprint2TestData.AssetPath(sector))),
                "SectorConfig");

            List<StarSystemConfig> startSystems = systems
                .Where(system => system.IsStartSystem)
                .ToList();

            Assert.That(
                startSystems.Count,
                Is.EqualTo(1),
                "Exactly one StarSystemConfig must have IsStartSystem=true.");
        }

        [Test]
        public void CanonicalRoutes_AreBoundToKnownSystemsWithoutOrphans()
        {
            GalaxyConfig galaxy = Sprint2TestData.LoadCanonicalGalaxy();
            List<StarSystemConfig> systems = Sprint2TestData.GetSystems(galaxy);
            List<RouteConfig> routes = Sprint2TestData.GetCanonicalRoutes(galaxy);

            HashSet<StarSystemConfig> knownSystems = new HashSet<StarSystemConfig>(systems);
            HashSet<RouteConfig> canonicalRoutes = new HashSet<RouteConfig>(routes);

            Assert.That(routes, Is.Not.Empty, "No routes are connected to the canonical galaxy.");

            AssertUniqueNonEmptyIds(
                routes.Select(route => (route.Id, Sprint2TestData.AssetPath(route))),
                "RouteConfig");

            foreach (RouteConfig route in routes)
            {
                Assert.That(
                    route.FromSystem,
                    Is.Not.Null,
                    $"FromSystem is null: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.ToSystem,
                    Is.Not.Null,
                    $"ToSystem is null: {Sprint2TestData.RouteLabel(route)}");

                if (route.FromSystem == null || route.ToSystem == null)
                    continue;

                Assert.That(
                    knownSystems.Contains(route.FromSystem),
                    Is.True,
                    $"FromSystem is outside the canonical galaxy: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    knownSystems.Contains(route.ToSystem),
                    Is.True,
                    $"ToSystem is outside the canonical galaxy: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.FromSystem,
                    Is.Not.SameAs(route.ToSystem),
                    $"A route cannot connect a system to itself: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.ParsecDistance,
                    Is.GreaterThan(0),
                    $"ParsecDistance must be positive: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.RequiredScanLevel,
                    Is.GreaterThanOrEqualTo(0),
                    $"RequiredScanLevel cannot be negative: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.FromSystem.Routes,
                    Does.Contain(route),
                    $"Route is absent from FromSystem.Routes: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.ToSystem.Routes,
                    Does.Contain(route),
                    $"Route is absent from ToSystem.Routes: {Sprint2TestData.RouteLabel(route)}");
            }

            List<RouteConfig> productionRouteAssets =
                Sprint2TestData.LoadAllAssets<RouteConfig>(
                    new[] { Sprint2TestData.RoutesFolder });

            Assert.That(
                productionRouteAssets,
                Is.Not.Empty,
                $"No RouteConfig assets were found under {Sprint2TestData.RoutesFolder}.");

            List<RouteConfig> orphanAssets = productionRouteAssets
                .Where(route => !canonicalRoutes.Contains(route))
                .ToList();

            Assert.That(
                orphanAssets,
                Is.Empty,
                "RouteConfig assets exist in the production Routes folder but are not " +
                "connected to the canonical galaxy: " +
                string.Join(", ", orphanAssets.Select(Sprint2TestData.AssetPath)));
        }

        private static void AssertUniqueNonEmptyIds(
            IEnumerable<(string Id, string Path)> values,
            string typeName)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            foreach ((string id, string path) in values)
            {
                Assert.That(
                    id,
                    Is.Not.Null.And.Not.Empty,
                    $"{typeName} has an empty Id: {path}");

                Assert.That(
                    ids.Add(id),
                    Is.True,
                    $"Duplicate {typeName} Id '{id}' found at {path}.");
            }
        }
    }
}
