using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarFrontier.Tests.Sprint2
{
    [TestFixture]
    public sealed class Sprint2RouteGraphEditModeTests
    {
        private const float DirectionDotTolerance = 0.50f;

        [Test]
        public void RouteGraph_IsConnectedAndContainsNoDuplicateUndirectedEdges()
        {
            GalaxyConfig galaxy = Sprint2TestData.LoadCanonicalGalaxy();
            List<StarSystemConfig> systems = Sprint2TestData.GetSystems(galaxy);
            List<RouteConfig> routes = Sprint2TestData.GetCanonicalRoutes(galaxy);

            Dictionary<string, HashSet<string>> graph =
                systems.ToDictionary(
                    system => system.Id,
                    _ => new HashSet<string>(StringComparer.Ordinal),
                    StringComparer.Ordinal);

            HashSet<string> undirectedPairs =
                new HashSet<string>(StringComparer.Ordinal);

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

                string fromId = route.FromSystem.Id;
                string toId = route.ToSystem.Id;

                Assert.That(
                    graph.ContainsKey(fromId),
                    Is.True,
                    $"Unknown FromSystem '{fromId}' in {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    graph.ContainsKey(toId),
                    Is.True,
                    $"Unknown ToSystem '{toId}' in {Sprint2TestData.RouteLabel(route)}");

                graph[fromId].Add(toId);
                graph[toId].Add(fromId);

                string pairKey = string.CompareOrdinal(fromId, toId) < 0
                    ? $"{fromId}|{toId}"
                    : $"{toId}|{fromId}";

                Assert.That(
                    undirectedPairs.Add(pairKey),
                    Is.True,
                    $"Duplicate route pair detected for '{pairKey}'.");
            }

            string firstSystemId = systems[0].Id;
            HashSet<string> visited =
                new HashSet<string>(StringComparer.Ordinal) { firstSystemId };
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(firstSystemId);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();

                foreach (string next in graph[current])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            List<string> unreachable = graph.Keys
                .Where(systemId => !visited.Contains(systemId))
                .OrderBy(systemId => systemId, StringComparer.Ordinal)
                .ToList();

            Assert.That(
                unreachable,
                Is.Empty,
                "The galaxy route graph is disconnected. Unreachable systems: " +
                string.Join(", ", unreachable));
        }

        [Test]
        public void EveryRoute_HasValidReciprocalEndpointAssignments()
        {
            GalaxyConfig galaxy = Sprint2TestData.LoadCanonicalGalaxy();
            List<RouteConfig> routes = Sprint2TestData.GetCanonicalRoutes(galaxy);

            foreach (RouteConfig route in routes)
            {
                Assert.That(
                    route.FromSystemRouteEndpointConfig,
                    Is.Not.Null,
                    $"From endpoint is missing: {Sprint2TestData.RouteLabel(route)}");

                Assert.That(
                    route.ToSystemRouteEndpointConfig,
                    Is.Not.Null,
                    $"To endpoint is missing: {Sprint2TestData.RouteLabel(route)}");

                if (route.FromSystem == null ||
                    route.ToSystem == null ||
                    route.FromSystemRouteEndpointConfig == null ||
                    route.ToSystemRouteEndpointConfig == null)
                {
                    continue;
                }

                Vector2 mapDirection = new Vector2(
                    route.ToSystem.MapPosition.x - route.FromSystem.MapPosition.x,
                    route.ToSystem.MapPosition.y - route.FromSystem.MapPosition.y);

                Assert.That(
                    mapDirection.sqrMagnitude,
                    Is.GreaterThan(0.0001f),
                    $"Connected systems have the same mapPosition: {Sprint2TestData.RouteLabel(route)}");

                Vector2 reverseDirection = -mapDirection;

                AssertPointAligned(
                    route.FromSystemRouteEndpointConfig.ExitPoint,
                    mapDirection,
                    route,
                    "FromSystem.ExitPoint");

                AssertPointAligned(
                    route.FromSystemRouteEndpointConfig.EntryPoint,
                    mapDirection,
                    route,
                    "FromSystem.EntryPoint");

                AssertPointAligned(
                    route.ToSystemRouteEndpointConfig.ExitPoint,
                    reverseDirection,
                    route,
                    "ToSystem.ExitPoint");

                AssertPointAligned(
                    route.ToSystemRouteEndpointConfig.EntryPoint,
                    reverseDirection,
                    route,
                    "ToSystem.EntryPoint");
            }
        }

        [Test]
        public void RouteConfig_PublicNavigationMethods_WorkInBothDirections()
        {
            GalaxyConfig galaxy = Sprint2TestData.LoadCanonicalGalaxy();
            List<RouteConfig> routes = Sprint2TestData.GetCanonicalRoutes(galaxy);

            foreach (RouteConfig route in routes)
            {
                Assert.That(route.FromSystem, Is.Not.Null);
                Assert.That(route.ToSystem, Is.Not.Null);

                if (route.FromSystem == null || route.ToSystem == null)
                    continue;

                string fromId = route.FromSystem.Id;
                string toId = route.ToSystem.Id;

                Assert.That(route.ContainsSystem(fromId), Is.True);
                Assert.That(route.ContainsSystem(toId), Is.True);
                Assert.That(route.ContainsSystem("__missing_system__"), Is.False);

                Assert.That(route.ConnectsSystems(fromId, toId), Is.True);
                Assert.That(route.ConnectsSystems(toId, fromId), Is.True);

                Assert.That(route.GetOtherSystem(fromId), Is.SameAs(route.ToSystem));
                Assert.That(route.GetOtherSystem(toId), Is.SameAs(route.FromSystem));
                Assert.That(route.GetOtherSystem("__missing_system__"), Is.Null);

                Assert.That(
                    route.GetDepartureEndpoint(fromId),
                    Is.SameAs(route.FromSystemRouteEndpointConfig));

                Assert.That(
                    route.GetArrivalEndpoint(toId),
                    Is.SameAs(route.ToSystemRouteEndpointConfig));

                Assert.That(
                    route.GetExitPoint(fromId),
                    Is.EqualTo(route.FromSystemRouteEndpointConfig.ExitPoint));

                Assert.That(
                    route.GetEntryPoint(toId),
                    Is.EqualTo(route.ToSystemRouteEndpointConfig.EntryPoint));
            }
        }

        private static void AssertPointAligned(
            Vector3 point,
            Vector2 expectedDirection,
            RouteConfig route,
            string pointName)
        {
            Vector2 pointDirection = new Vector2(point.x, point.y);

            Assert.That(
                pointDirection.sqrMagnitude,
                Is.GreaterThan(0.0001f),
                $"{pointName} is zero: {Sprint2TestData.RouteLabel(route)}");

            float dot = Vector2.Dot(
                pointDirection.normalized,
                expectedDirection.normalized);

            Assert.That(
                dot,
                Is.GreaterThanOrEqualTo(DirectionDotTolerance),
                $"{pointName} points to the wrong side of the system. " +
                $"dot={dot:F3}, expected >= {DirectionDotTolerance:F2}. " +
                $"{Sprint2TestData.RouteLabel(route)}");
        }
    }
}
