using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarFrontier.Tests.Sprint1
{
    [TestFixture]
    public sealed class SceneFlowContractCurrentEditModeTests
    {
        [Test]
        public void SceneService_InvalidScene_RaisesFailureAndDoesNotStartOperation()
        {
            var service = new SceneService();
            string failedScene = null;
            string failureMessage = null;
            service.SceneLoadFailed += (scene, error) =>
            {
                failedScene = scene;
                failureMessage = error;
            };

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "Scene load failed"));

            AsyncOperation operation = service.LoadSceneAsync(string.Empty);

            Assert.That(operation, Is.Null);
            Assert.That(failedScene, Is.EqualTo(string.Empty));
            Assert.That(failureMessage, Does.Contain("empty"));
        }

        [Test]
        public void SceneService_ExposesExplicitLoadingTransition()
        {
            MethodInfo loadingMethod = typeof(ISceneService)
                .GetMethods()
                .FirstOrDefault(method =>
                    method.Name.IndexOf("LoadLoading", StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.That(loadingMethod, Is.Not.Null,
                "2A-S01-04 requires an explicit Loading transition in the scene-flow contract.");
        }

        [Test]
        public void SceneService_ExposesCancellationContract()
        {
            bool hasCancellation = typeof(ISceneService)
                .GetMethods()
                .Any(method =>
                    method.Name.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    method.GetParameters().Any(parameter =>
                        parameter.ParameterType.Name.IndexOf(
                            "CancellationToken",
                            StringComparison.OrdinalIgnoreCase) >= 0));

            Assert.That(hasCancellation, Is.True,
                "2A-S01-04 requires cancellation/shutdown for an unfinished scene load.");
        }

        [Test]
        public void BootstrapFlow_ReferencesLoadingBeforeGameplay()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string bootstrapStatePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/Core/StateMachine/BootstrapState.cs");
            string bootstrapSource = File.ReadAllText(bootstrapStatePath);

            Assert.That(
                bootstrapSource.IndexOf("Loading", StringComparison.OrdinalIgnoreCase),
                Is.GreaterThanOrEqualTo(0),
                "Bootstrap flow currently starts New Game directly and bypasses Loading.");
        }

        [Test]
        public void LoadingScene_ContainsUiRootAndCanvas()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string loadingScenePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scenes/LoadingScene.unity");

            Assert.That(File.Exists(loadingScenePath), Is.True);

            string sceneSource = File.ReadAllText(loadingScenePath);
            Assert.That(sceneSource, Does.Contain("m_Name: LoadingSceneRoot"));
            Assert.That(sceneSource, Does.Contain("m_Name: Canvas"));
        }
    }
}
