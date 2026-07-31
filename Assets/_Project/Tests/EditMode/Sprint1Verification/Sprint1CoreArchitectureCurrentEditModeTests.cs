using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarFrontier.Tests.Sprint1
{
    public sealed class Sprint1CoreArchitectureCurrentEditModeTests
    {
        [Test]
        public void TickInfrastructure_HasRequiredContract()
        {
            Assert.That(typeof(ITickable).GetMethod("Tick"), Is.Not.Null);
            Assert.That(typeof(ITickable).IsAssignableFrom(typeof(ITickService)), Is.True);
            string[] methods = typeof(ITickService).GetMethods().Select(method => method.Name).ToArray();
            foreach (string required in new[] { "Register", "Unregister", "Contains", "Clear" })
                Assert.That(methods, Does.Contain(required));
            Assert.That(typeof(ITickService).IsAssignableFrom(typeof(TickService)), Is.True);
        }

        [Test]
        public void Bootstrapper_HasSingleRegistryAndTickOwner()
        {
            FieldInfo[] fields = typeof(Bootstrapper).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(fields.Count(field => field.FieldType == typeof(IServiceRegistry) ||
                                              field.FieldType == typeof(ServiceRegistry)), Is.EqualTo(1));
            Assert.That(fields.Count(field => field.FieldType == typeof(ITickService) ||
                                              field.FieldType == typeof(TickService)), Is.EqualTo(1));
        }

        [Test]
        public void Services_ArePureCSharpObjects()
        {
            Type[] services = { typeof(ConfigService), typeof(SaveService2A), typeof(SceneService),
                typeof(TickService), typeof(ServiceRegistry) };
            Assert.That(services.Any(type => typeof(MonoBehaviour).IsAssignableFrom(type)), Is.False);
        }

        [Test]
        public void GameRuntimeState_IsSerializableAndRoundTrips()
        {
            Assert.That(typeof(GameRuntimeState).IsDefined(typeof(SerializableAttribute), false), Is.True);
            var state = new GameRuntimeState();
            state.Player.PlayerId = "qa";
            GameRuntimeState restored = JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(state));
            Assert.That(restored.Player.PlayerId, Is.EqualTo("qa"));
        }

        [Test]
        public void SavePipeline_HasMigrationValidationIntegrityAndAtomicStages()
        {
            Type[] types = typeof(SaveService2A).Assembly.GetTypes();
            Assert.That(types.Any(type => type.Name.Contains("Migration")), Is.True);
            Assert.That(types.Any(type => type.Name.Contains("Validation")), Is.True,
                "Sprint 1 requires post-load validation/normalization.");
            Assert.That(types.Any(type => type.Name.Contains("Integrity") || type.Name.Contains("Checksum")),
                Is.True, "Sprint 1 requires save integrity verification.");
            string source = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Core/Services/Save/SaveService2A.cs");
            Assert.That(source.IndexOf("temp", StringComparison.OrdinalIgnoreCase), Is.GreaterThanOrEqualTo(0));
            Assert.That(source.Contains("File.Replace(") || source.Contains("File.Move("), Is.True);
        }

        [Test]
        public void SceneService_HasAsyncCompletionAndFailureContract()
        {
            MethodInfo[] methods = typeof(ISceneService).GetMethods();
            Assert.That(methods.Any(method => method.Name.Contains("Async") ||
                                              method.Name.Contains("Cancel") || method.ReturnType != typeof(void)), Is.True);
            Assert.That(methods.Any(method => method.Name.Contains("Fail") ||
                                              method.Name.Contains("Error") || method.Name.Contains("Fallback")), Is.True);
        }

        [Test]
        public void DiagnosticsFoundation_HasLoggerHudAndReleaseGate()
        {
            Type[] types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => new[] { "Core", "Data", "Presentation", "Tools" }
                    .Contains(assembly.GetName().Name))
                .SelectMany(SafeTypes).ToArray();
            Assert.That(types.Any(type => type.Name == "ILogger" || type.Name.Contains("LogService")), Is.True);
            Assert.That(types.Any(type => type.Name.IndexOf("DebugHud", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
            Assert.That(types.Any(type => type.Name.Contains("DeveloperMode") || type.Name.Contains("DebugBuild")), Is.True);
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type != null); }
        }
    }
}
