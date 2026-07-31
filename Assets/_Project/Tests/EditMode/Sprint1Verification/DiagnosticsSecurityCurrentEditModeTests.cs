using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarFrontier.Tests.Sprint1
{
    public sealed class DiagnosticsSecurityCurrentEditModeTests
    {
        [Test]
        public void CriticalServices_UseCentralLoggingFacade()
        {
            string[] files = { "Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs",
                "Assets/_Project/Scripts/Core/Services/Config/ConfigService.cs",
                "Assets/_Project/Scripts/Core/Services/Save/SaveService2A.cs",
                "Assets/_Project/Scripts/Core/Services/Scenes/SceneService.cs" };
            string[] offenders = files.Where(path => File.ReadAllText(path).Contains("Debug.Log"))
                .ToArray();
            Assert.That(offenders, Is.Empty,
                "Critical services must use the common logging facade:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void SaveWrite_UsesTempAndAtomicReplacement()
        {
            string source = File.ReadAllText(
                "Assets/_Project/Scripts/Core/Services/Save/SaveService2A.cs");
            Assert.That(source.IndexOf("temp", StringComparison.OrdinalIgnoreCase), Is.GreaterThanOrEqualTo(0));
            Assert.That(source.Contains("File.Replace(") || source.Contains("File.Move("), Is.True);
        }

        [Test]
        public void SaveImplementation_DoesNotLogSerializedState()
        {
            string source = File.ReadAllText(
                "Assets/_Project/Scripts/Core/Services/Save/SaveService2A.cs");
            string[] banned = { "Debug.Log(json", "Debug.Log(state", "Debug.Log($\"{json}",
                "Debug.Log($\"{state}" };
            Assert.That(banned.Where(source.Contains), Is.Empty);
        }

        [Test]
        public void DeveloperTools_AreGuardedForNonReleaseBuilds()
        {
            string root = "Assets/_Project/Scripts/Presentation";
            string[] debugFiles = Directory.GetFiles(root, "*Debug*.cs", SearchOption.AllDirectories);
            string[] unguarded = debugFiles.Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains("#if UNITY_EDITOR") &&
                       !source.Contains("#if DEVELOPMENT_BUILD") &&
                       !source.Contains("[Conditional(");
            }).ToArray();
            Assert.That(unguarded, Is.Empty,
                "Developer tools must be unavailable in release:\n" + string.Join("\n", unguarded));
        }

        [Test]
        public void DiagnosticReportContract_Exists()
        {
            Type report = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes)
                .FirstOrDefault(type => type.Name.IndexOf("DiagnosticReport",
                    StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(report, Is.Not.Null);
        }

        private static IEnumerable<Type> SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            { return exception.Types.Where(type => type != null); }
        }
    }
}
