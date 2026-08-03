#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Exact-SHA structural and integration verification gate for STAR FRONTIER,
/// Stage 2A / Sprint 3.
///
/// This suite intentionally uses reflection for project classes so the test
/// assembly does not create new compile-time dependencies between production
/// assemblies. It supplements, but does not replace, existing service/unit tests.
/// </summary>
[TestFixture]
public sealed class Stage2A_Sprint3_FullVerificationEditModeTests
{
    private const string ExpectedBranch = "main";
    private const string ExpectedSha = "b57d223341446b6a79da1ee486ff6508174b3a73";
    private const string ExpectedUnityVersion = "6000.3.11f1";
    private const string SystemScenePath = "Assets/_Project/Scenes/SystemScene.unity";
    private const string ValidationReportPath =
        "Documentation/Sprint3/Validation/Sprint3ValidationReport_LATEST.txt";

    private static readonly RequiredConfig[] RequiredConfigs =
    {
        new RequiredConfig(
            "PlayerControlConfig",
            "Assets/_Project/Content/Configs/Sprint3/playerControlConfig_01.asset"),
        new RequiredConfig(
            "ShipMovementConfig",
            "Assets/_Project/Content/Configs/Ships/shipMovementConfig_01.asset"),
        new RequiredConfig(
            "SystemCameraConfig",
            "Assets/_Project/Content/Configs/Sprint3/systemCameraConfig_01.asset"),
        new RequiredConfig(
            "TargetingConfig",
            "Assets/_Project/Content/Configs/Sprint3/targetingConfig_01.asset"),
        new RequiredConfig(
            "InteractionConfig",
            "Assets/_Project/Content/Configs/Sprint3/interactionConfig_01.asset")
    };

    private static readonly string[] RequiredValidationSources =
    {
        "Assets/_Project/Tests/EditMode/Sprint3Validation/Sprint3ProjectValidator.cs",
        "Assets/_Project/Tests/EditMode/Sprint3Validation/Sprint3Validation.asmdef",
        "Assets/_Project/Tests/EditMode/SystemScene/SystemSceneVisual2AEditModeTests.cs",
        ValidationReportPath
    };

    private static readonly string[] RequiredVisualAssets =
    {
        "Assets/Art/SystemFrame/Backgrounds/system_background_deep_space_01.png",
        "Assets/Art/SystemFrame/Backgrounds/system_background_deep_space_02.png",
        "Assets/Art/SystemFrame/Backgrounds/system_nebula_far_overlay_01.png",
        "Assets/Art/SystemFrame/Backgrounds/system_nebula_far_overlay_02.png",
        "Assets/Art/SystemFrame/Backgrounds/system_stars_near_overlay_01.png",
        "Assets/Art/SystemFrame/Backgrounds/system_foreground_dust_vignette_01.png",
        "Assets/Art/SystemFrame/ShipFx/ship_engine_glow_blue_01.png",
        "Assets/Art/SystemFrame/ShipFx/ship_shadow_soft_ellipse_01.png",
        "Assets/Art/SystemFrame/Materials/M_ShipEngineTrail.mat",
        "Assets/Art/Shadows/shadow_ship_soft_oval_01.png",
        "Assets/Art/Shadows/shadow_object_contact_soft_01.png",
        "Assets/Art/Shadows/shadow_planet_volume_overlay_01.png",
        "Assets/Art/Shadows/shadow_station_soft_mass_01.png",
        "Assets/Art/SystemExit/exit_gate_pseudo3d_01.png",
        "Assets/Art/SystemExit/exit_gate_pseudo3d_02.png",
        "Assets/Art/Suns/SunSprites/sun_yellow_pseudo3d_01.png",
        "Assets/Art/Suns/SunSprites/sun_red_pseudo3d_01.png",
        "Assets/Art/Suns/SunSprites/sun_blue_pseudo3d_01.png"
    };

    [Test]
    public void S03_ExactRepositoryHead_MatchesRequestedSha()
    {
        GitHeadInfo info = ReadGitHead();

        Assert.AreEqual(
            ExpectedSha,
            info.Sha,
            "Текущий Git HEAD не совпадает с SHA, для которого подготовлен пакет проверки.");
    }

    [Test]
    public void S03_RepositoryBranch_IsExpectedOrDetachedAtExpectedSha()
    {
        GitHeadInfo info = ReadGitHead();

        Assert.AreEqual(ExpectedSha, info.Sha, "Сначала переключитесь на требуемый SHA.");

        if (string.IsNullOrWhiteSpace(info.RefName))
        {
            Assert.Pass("Репозиторий открыт в detached HEAD на требуемом SHA.");
        }

        Assert.AreEqual(
            "refs/heads/" + ExpectedBranch,
            info.RefName,
            "Открыта другая ветка. Ожидалась рабочая ветка Sprint 3.");
    }

    [Test]
    public void S03_UnityVersion_MatchesProjectVersion()
    {
        Assert.AreEqual(
            ExpectedUnityVersion,
            Application.unityVersion,
            "Откройте проект в зафиксированной версии Unity.");
    }

    [Test]
    public void S03_RequiredValidationSources_Exist()
    {
        foreach (string relativePath in RequiredValidationSources)
        {
            Assert.IsTrue(
                File.Exists(ToAbsolutePath(relativePath)),
                "Не найден обязательный файл проверки: " + relativePath);
        }
    }

    [Test]
    public void S03_RequiredConfigs_ExistAndHaveExpectedTypes()
    {
        foreach (RequiredConfig required in RequiredConfigs)
        {
            ScriptableObject asset =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(required.Path);

            Assert.IsNotNull(asset, "Не найден Config: " + required.Path);
            Assert.AreEqual(
                required.TypeName,
                asset.GetType().Name,
                required.Path + ": загружен Config другого типа.");
        }
    }

    [Test]
    public void S03_ProductionInputActions_IsTheOnlyProjectInputActionAsset()
    {
        const string productionPath = "Assets/_Project/Input/InputActions.inputactions";
        const string legacyPath = "Assets/InputSystem_Actions.inputactions";

        Assert.IsTrue(File.Exists(ToAbsolutePath(productionPath)),
            "Не найден production InputActions.inputactions.");
        Assert.IsFalse(File.Exists(ToAbsolutePath(legacyPath)),
            "Legacy InputSystem_Actions.inputactions не должен возвращаться в production-контур.");

        string assetsRoot = ToAbsolutePath("Assets");
        string[] projectInputAssets = Directory
            .GetFiles(assetsRoot, "*.inputactions", SearchOption.AllDirectories)
            .Select(ToProjectRelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { productionPath },
            projectInputAssets,
            "В Assets найден второй InputActionAsset. В production-контуре должен быть один.");
    }

    [Test]
    public void S03_CoreProductionTypes_AreDiscoverable()
    {
        AssertTypeExists("PlayerControlService2A");
        AssertTypeExists("ShipMovementService2A");
        AssertTypeExists("SystemGameplayStateService");
        AssertTypeExists("SystemBoundsService2A");
        AssertTypeExists("PlayerShipSaveSyncService2A");
        AssertTypeExists("SystemCameraController2A");
        AssertTypeExists("PlayerInputBridge2A");
        AssertTypeExists("SystemControlPresentationBridge2A");
        AssertTypeExists("SystemSceneRoot2A");
    }

    [Test]
    public void S03_SystemScene_FileExistsAndOpens()
    {
        Assert.IsTrue(File.Exists(ToAbsolutePath(SystemScenePath)),
            "Не найдена SystemScene: " + SystemScenePath);

        Scene scene = OpenSystemScene();
        Assert.IsTrue(scene.IsValid(), "SystemScene открылась как недействительная сцена.");
        Assert.IsTrue(scene.isLoaded, "SystemScene не загружена.");
    }

    [Test]
    public void S03_SystemScene_HasNoMissingScripts()
    {
        Scene scene = OpenSystemScene();

        foreach (GameObject gameObject in GetAllSceneGameObjects(scene))
        {
            Component[] components = gameObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Assert.IsNotNull(
                    components[index],
                    "Missing Script найден на объекте: " + GetHierarchyPath(gameObject));
            }
        }
    }

    [Test]
    public void S03_SystemMapRoot_HasRequiredControllers()
    {
        Scene scene = OpenSystemScene();
        GameObject root = FindSceneGameObject(scene, "SystemMapRoot");

        Assert.IsNotNull(root, "SystemMapRoot не найден.");

        string[] requiredComponents =
        {
            "SystemMapController2",
            "SystemShipMarkerController2",
            "SystemDestinationMarkerController2",
            "SystemMapDestinationController2",
            "SystemMapClickArea2",
            "SystemTravelVisualController2A",
            "SystemCameraController2A",
            "SystemCameraDragInput2A",
            "PlayerInputBridge2A",
            "SystemControlPresentationBridge2A",
            "SystemSceneRoot2A"
        };

        foreach (string typeName in requiredComponents)
        {
            AssertComponentOnGameObject(root, typeName);
        }
    }

    [Test]
    public void S03_SystemScene_HasSingleActiveOwners()
    {
        Scene scene = OpenSystemScene();

        AssertSingleActiveOwner(scene, "SystemCameraController2A");
        AssertSingleActiveOwner(scene, "PlayerInputBridge2A");
        AssertSingleActiveOwner(scene, "SystemControlPresentationBridge2A");
        AssertSingleActiveOwner(scene, "EventSystem");
    }

    [Test]
    public void S03_SystemSceneRoot_ReportsNoMissingReferences()
    {
        Scene scene = OpenSystemScene();
        MonoBehaviour root = FindSceneMonoBehaviour(scene, "SystemSceneRoot2A");

        Assert.IsNotNull(root, "SystemSceneRoot2A не найден.");

        MethodInfo hasRequiredReferences = root.GetType().GetMethod(
            "HasRequiredReferences",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo getReport = root.GetType().GetMethod(
            "GetMissingReferencesReport",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsNotNull(hasRequiredReferences,
            "У SystemSceneRoot2A отсутствует HasRequiredReferences().");

        object result = hasRequiredReferences.Invoke(root, null);
        string report = getReport == null
            ? "Метод GetMissingReferencesReport отсутствует."
            : Convert.ToString(getReport.Invoke(root, null));

        Assert.IsTrue(result is bool && (bool)result,
            "SystemSceneRoot2A содержит потерянные обязательные ссылки:\n" + report);
    }

    [Test]
    public void S03_SystemSceneRoot_VisualRootsAreLinked()
    {
        Scene scene = OpenSystemScene();
        MonoBehaviour root = FindSceneMonoBehaviour(scene, "SystemSceneRoot2A");

        string[] requiredReferences =
        {
            "systemMapRoot", "mainCamera", "systemBackground",
            "sunNodesRoot", "planetNodesRoot", "systemExitsNodesRoot",
            "playerRoot", "enemiesRoot", "alliesRoot", "projectilesRoot", "vfxRoot",
            "boundsRoot", "markersRoot", "debugRoot",
            "metaCanvas", "systemMapHudRoot", "planetRoot", "metaHudRoot"
        };

        foreach (string fieldName in requiredReferences)
        {
            AssertObjectReferenceAssigned(root, fieldName);
        }
    }

    [Test]
    public void S03_TravelCameraAndInputBindings_AreAssigned()
    {
        Scene scene = OpenSystemScene();

        MonoBehaviour travel = FindSceneMonoBehaviour(scene, "SystemTravelVisualController2A");
        AssertObjectReferenceAssigned(travel, "travelLineView");
        AssertObjectReferenceAssigned(travel, "engineGlowView");

        MonoBehaviour camera = FindSceneMonoBehaviour(scene, "SystemCameraController2A");
        AssertObjectReferenceAssigned(camera, "targetCamera");
        AssertObjectReferenceAssigned(camera, "shipTarget");
        AssertObjectReferenceAssigned(camera, "cameraConfig");

        MonoBehaviour drag = FindSceneMonoBehaviour(scene, "SystemCameraDragInput2A");
        AssertObjectReferenceAssigned(drag, "cameraController");

        MonoBehaviour input = FindSceneMonoBehaviour(scene, "PlayerInputBridge2A");
        AssertObjectReferenceAssigned(input, "inputActions");
        AssertObjectReferenceAssigned(input, "destinationAdapter");
        AssertSerializedArrayHasItems(input, "blockingUiRoots");

        MonoBehaviour presentation =
            FindSceneMonoBehaviour(scene, "SystemControlPresentationBridge2A");
        AssertObjectReferenceAssigned(presentation, "cameraController");
    }

    [Test]
    public void S03_HudRootsAndMainCamera_Exist()
    {
        Scene scene = OpenSystemScene();

        string[] requiredObjectNames =
        {
            "PF_HudTopRoot",
            "SystemMapRightHUD",
            "PF_SystemHudBottomRoot",
            "SystemMainCamera"
        };

        foreach (string objectName in requiredObjectNames)
        {
            Assert.IsNotNull(
                FindSceneGameObject(scene, objectName),
                "В SystemScene не найден объект: " + objectName);
        }
    }

    [Test]
    public void S03_RequiredVisualAssets_Exist()
    {
        foreach (string path in RequiredVisualAssets)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Assert.IsNotNull(asset, "Не найден обязательный визуальный asset: " + path);
        }
    }

    [Test]
    public void S03_SavedValidationReport_HasZeroErrorsAndExpectedScope()
    {
        string absolutePath = ToAbsolutePath(ValidationReportPath);
        Assert.IsTrue(File.Exists(absolutePath),
            "Не найден сохранённый отчёт Sprint3ProjectValidator.");

        string report = File.ReadAllText(absolutePath);

        StringAssert.Contains("Mode: FULL", report);
        StringAssert.Contains("Unity: " + ExpectedUnityVersion, report);
        StringAssert.Contains("Scene: " + SystemScenePath, report);
        StringAssert.Contains("ERRORS: 0", report);
        StringAssert.Contains("PlayerControlConfig: найден", report);
        StringAssert.Contains("ShipMovementConfig: найден", report);
        StringAssert.Contains("SystemCameraController2A: найден один активный владелец", report);
        StringAssert.Contains("PlayerInputBridge2A: найден один активный владелец", report);
        StringAssert.Contains("RESULT: PASSED", report);
    }

    [Test]
    public void S03_ConfigAssets_DoNotContainBrokenObjectReferences()
    {
        foreach (RequiredConfig required in RequiredConfigs)
        {
            ScriptableObject asset =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(required.Path);
            Assert.IsNotNull(asset, "Не найден Config: " + required.Path);

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (iterator.objectReferenceInstanceIDValue != 0 &&
                    iterator.objectReferenceValue == null)
                {
                    Assert.Fail(required.Path + ": повреждена ссылка " + iterator.propertyPath);
                }
            }
        }
    }

    private static void AssertTypeExists(string typeName)
    {
        Assert.IsNotNull(FindType(typeName), "Не найден production-тип: " + typeName);
    }

    private static Type FindType(string simpleName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).ToArray();
            }

            Type match = types.FirstOrDefault(type => type.Name == simpleName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Scene OpenSystemScene()
    {
        Scene scene = EditorSceneManager.OpenScene(SystemScenePath, OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid(), "Не удалось открыть SystemScene.");
        return scene;
    }

    private static IEnumerable<GameObject> GetAllSceneGameObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                yield return transform.gameObject;
            }
        }
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        return GetAllSceneGameObjects(scene)
            .FirstOrDefault(gameObject => gameObject.name == objectName);
    }

    private static MonoBehaviour FindSceneMonoBehaviour(Scene scene, string typeName)
    {
        return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(component =>
                component != null &&
                component.gameObject.scene == scene &&
                component.GetType().Name == typeName);
    }

    private static List<MonoBehaviour> FindSceneMonoBehaviours(Scene scene, string typeName)
    {
        return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(component =>
                component != null &&
                component.gameObject.scene == scene &&
                component.GetType().Name == typeName)
            .ToList();
    }

    private static void AssertComponentOnGameObject(GameObject gameObject, string typeName)
    {
        bool found = gameObject.GetComponents<MonoBehaviour>()
            .Any(component => component != null && component.GetType().Name == typeName);

        Assert.IsTrue(found,
            typeName + " не найден на " + GetHierarchyPath(gameObject) + ".");
    }

    private static void AssertSingleActiveOwner(Scene scene, string typeName)
    {
        List<MonoBehaviour> components = FindSceneMonoBehaviours(scene, typeName)
            .Where(component => component.enabled && component.gameObject.activeInHierarchy)
            .ToList();

        Assert.AreEqual(
            1,
            components.Count,
            "Ожидался один активный владелец " + typeName +
            ", найдено: " + components.Count + ".");
    }

    private static void AssertObjectReferenceAssigned(
        MonoBehaviour component,
        string fieldName)
    {
        Assert.IsNotNull(component, "Компонент для поля " + fieldName + " не найден.");

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        Assert.IsNotNull(property,
            component.GetType().Name + "." + fieldName + " не найдено.");
        Assert.AreEqual(
            SerializedPropertyType.ObjectReference,
            property.propertyType,
            component.GetType().Name + "." + fieldName + " не является ссылкой на объект.");
        Assert.IsNotNull(
            property.objectReferenceValue,
            component.GetType().Name + "." + fieldName + " не назначено.");
    }

    private static void AssertSerializedArrayHasItems(
        MonoBehaviour component,
        string fieldName)
    {
        Assert.IsNotNull(component, "Компонент для массива " + fieldName + " не найден.");

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        Assert.IsNotNull(property,
            component.GetType().Name + "." + fieldName + " не найдено.");
        Assert.IsTrue(property.isArray,
            component.GetType().Name + "." + fieldName + " не является массивом.");
        Assert.Greater(property.arraySize, 0,
            component.GetType().Name + "." + fieldName + " пуст.");
    }

    private static string GetHierarchyPath(GameObject gameObject)
    {
        List<string> names = new List<string>();
        Transform current = gameObject.transform;

        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string ProjectRoot
    {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    private static string ToAbsolutePath(string projectRelativePath)
    {
        return Path.Combine(
            ProjectRoot,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string root = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar) +
                      Path.DirectorySeparatorChar;
        string relative = absolutePath.StartsWith(root, StringComparison.Ordinal)
            ? absolutePath.Substring(root.Length)
            : absolutePath;
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static GitHeadInfo ReadGitHead()
    {
        string dotGitPath = Path.Combine(ProjectRoot, ".git");
        string gitDirectory = dotGitPath;

        if (File.Exists(dotGitPath))
        {
            string pointer = File.ReadAllText(dotGitPath).Trim();
            const string prefix = "gitdir:";
            Assert.IsTrue(pointer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
                ".git-файл имеет неизвестный формат.");

            string path = pointer.Substring(prefix.Length).Trim();
            gitDirectory = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        Assert.IsTrue(Directory.Exists(gitDirectory),
            "Не найден каталог .git. Откройте обычный Git checkout проекта.");

        string headPath = Path.Combine(gitDirectory, "HEAD");
        Assert.IsTrue(File.Exists(headPath), "Не найден .git/HEAD.");

        string head = File.ReadAllText(headPath).Trim();
        const string refPrefix = "ref:";

        if (!head.StartsWith(refPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new GitHeadInfo(null, NormalizeSha(head));
        }

        string refName = head.Substring(refPrefix.Length).Trim();
        string looseRefPath = Path.Combine(
            gitDirectory,
            refName.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(looseRefPath))
        {
            return new GitHeadInfo(refName, NormalizeSha(File.ReadAllText(looseRefPath)));
        }

        string packedRefsPath = Path.Combine(gitDirectory, "packed-refs");
        if (File.Exists(packedRefsPath))
        {
            foreach (string rawLine in File.ReadAllLines(packedRefsPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("^"))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { ' ' }, 2);
                if (parts.Length == 2 && parts[1] == refName)
                {
                    return new GitHeadInfo(refName, NormalizeSha(parts[0]));
                }
            }
        }

        Assert.Fail("Не удалось разрешить Git ref: " + refName);
        return default(GitHeadInfo);
    }

    private static string NormalizeSha(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private readonly struct RequiredConfig
    {
        public RequiredConfig(string typeName, string path)
        {
            TypeName = typeName;
            Path = path;
        }

        public string TypeName { get; }
        public string Path { get; }
    }

    private readonly struct GitHeadInfo
    {
        public GitHeadInfo(string refName, string sha)
        {
            RefName = refName;
            Sha = sha;
        }

        public string RefName { get; }
        public string Sha { get; }
    }
}

#endif
