using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace StarFrontier.Tests.Sprint1
{
    public sealed class SceneConfigurationEditModeTests
    {
        [Test]
        public void BuildSettings_FirstEnabledScene_IsBootstrap()
        {
            EditorBuildSettingsScene firstEnabled = EditorBuildSettings.scenes
                .FirstOrDefault(scene => scene.enabled);

            Assert.That(firstEnabled, Is.Not.Null, "No enabled build scene was found.");
            Assert.That(
                Path.GetFileNameWithoutExtension(firstEnabled.path),
                Is.EqualTo("BootstrapScene"));
        }

        [Test]
        public void BuildSettings_ContainsEnabledBootstrapLoadingAndSystemRoles()
        {
            string[] enabledNames = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToArray();

            Assert.That(enabledNames.Any(name =>
                name.IndexOf("Bootstrap", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True,
                "An enabled Bootstrap scene is required.");
            Assert.That(enabledNames.Any(name =>
                name.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True,
                "An enabled Loading scene is required by 2A-S01-04.");
            Assert.That(enabledNames.Any(name =>
                name.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True,
                "An enabled System gameplay scene is required by 2A-S01-04.");
        }

        [Test]
        public void ProductionCode_DoesNotBypassSceneService()
        {
            string scriptsRoot = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                "Assets/_Project/Scripts");

            Assert.That(Directory.Exists(scriptsRoot), Is.True, scriptsRoot);

            List<string> offenders = Directory
                .GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(
                    "SceneService.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
                .Where(path => File.ReadAllText(path).Contains("SceneManager.LoadScene"))
                .Select(path => MakeProjectRelative(path))
                .OrderBy(path => path)
                .ToList();

            Assert.That(
                offenders,
                Is.Empty,
                "Scene transitions must go through ISceneService:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void CanonicalSceneFiles_ExistAtConfiguredPaths()
        {
            List<string> missing = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !File.Exists(ToAbsoluteProjectPath(path)))
                .ToList();

            Assert.That(
                missing,
                Is.Empty,
                "Enabled scene paths do not resolve in a clean checkout:\n" +
                string.Join("\n", missing));
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            string projectRoot = Directory
                .GetParent(UnityEngine.Application.dataPath)
                .FullName;
            return Path.Combine(projectRoot, projectRelativePath);
        }

        private static string MakeProjectRelative(string absolutePath)
        {
            string projectRoot = Directory
                .GetParent(UnityEngine.Application.dataPath)
                .FullName + Path.DirectorySeparatorChar;
            return absolutePath.Replace(projectRoot, string.Empty);
        }
    }
}
