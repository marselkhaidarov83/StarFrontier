using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StarFrontier.Tests.Sprint2
{
    internal static class Sprint2TestData
    {
        internal const string CanonicalGalaxyId = "galaxyConfig_01";
        internal const string RoutesFolder = "Assets/_Project/Content/Configs/Galaxy/Routes";

        internal static GalaxyConfig LoadCanonicalGalaxy()
        {
            List<GalaxyConfig> galaxies = LoadAllAssets<GalaxyConfig>();

            Assert.That(
                galaxies,
                Is.Not.Empty,
                "GalaxyConfig assets were not found in the project.");

            GalaxyConfig canonical = galaxies.FirstOrDefault(
                galaxy => galaxy != null &&
                          string.Equals(
                              galaxy.Id,
                              CanonicalGalaxyId,
                              StringComparison.Ordinal));

            if (canonical != null)
                return canonical;

            Assert.That(
                galaxies.Count,
                Is.EqualTo(1),
                "The canonical GalaxyConfig could not be selected. " +
                $"Expected Id '{CanonicalGalaxyId}', but found: " +
                string.Join(", ", galaxies.Where(x => x != null).Select(x => x.Id)));

            return galaxies[0];
        }

        internal static List<SectorConfig> GetSectors(GalaxyConfig galaxy)
        {
            Assert.That(galaxy, Is.Not.Null);
            Assert.That(galaxy.Sectors, Is.Not.Null, "GalaxyConfig.Sectors is null.");

            return galaxy.Sectors.Where(x => x != null).ToList();
        }

        internal static List<StarSystemConfig> GetSystems(GalaxyConfig galaxy)
        {
            List<SectorConfig> sectors = GetSectors(galaxy);

            return sectors
                .SelectMany(sector => sector.Systems ?? Array.Empty<StarSystemConfig>())
                .Where(system => system != null)
                .ToList();
        }

        internal static List<RouteConfig> GetCanonicalRoutes(GalaxyConfig galaxy)
        {
            return GetSystems(galaxy)
                .SelectMany(system => system.Routes ?? new List<RouteConfig>())
                .Where(route => route != null)
                .Distinct()
                .ToList();
        }

        internal static List<T> LoadAllAssets<T>(string[] folders = null)
            where T : UnityEngine.Object
        {
            string filter = $"t:{typeof(T).Name}";
            string[] guids = folders == null
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, folders);

            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
                .Where(asset => asset != null)
                .ToList();
        }

        internal static string AssetPath(UnityEngine.Object asset)
        {
            return asset == null ? "<null>" : AssetDatabase.GetAssetPath(asset);
        }

        internal static string RouteLabel(RouteConfig route)
        {
            if (route == null)
                return "<null route>";

            string from = route.FromSystem == null ? "<null>" : route.FromSystem.Id;
            string to = route.ToSystem == null ? "<null>" : route.ToSystem.Id;

            return $"{route.Id}: {from} -> {to} ({AssetPath(route)})";
        }
    }
}
