#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class Sprint3ProjectValidator
{
    private const string ReportDirectory = "Documentation/Sprint3/Validation";
    private const string LatestReportName = "Sprint3ValidationReport_LATEST.txt";

    private static readonly RequiredConfig[] RequiredConfigs =
    {
        new(
            "PlayerControlConfig",
            "Assets/_Project/Content/Configs/Sprint3/playerControlConfig_01.asset"),
        new(
            "ShipMovementConfig",
            "Assets/_Project/Content/Configs/Ships/shipMovementConfig_01.asset"),
        new(
            "SystemCameraConfig",
            "Assets/_Project/Content/Configs/Sprint3/systemCameraConfig_01.asset"),
        new(
            "TargetingConfig",
            "Assets/_Project/Content/Configs/Sprint3/targetingConfig_01.asset"),
        new(
            "InteractionConfig",
            "Assets/_Project/Content/Configs/Sprint3/interactionConfig_01.asset")
    };

    private static readonly string[] NonNegativeNameTokens =
    {
        "speed", "acceleration", "deceleration", "distance", "radius", "capacity",
        "cooldown", "duration", "smoothing", "smooth", "sensitivity", "threshold",
        "zoom", "size", "fuel", "cost", "time", "delay", "range"
    };

    [MenuItem("STAR FRONTIER/Проверки Sprint 3/1. Проверить конфигурации")]
    public static void ValidateConfigsMenu()
    {
        ValidationReport report = CreateReport("CONFIG");
        ValidateConfigs(report);
        WriteReport(report);
    }

    [MenuItem("STAR FRONTIER/Проверки Sprint 3/2. Проверить открытую сцену и префабы")]
    public static void ValidateOpenSceneMenu()
    {
        ValidationReport report = CreateReport("SCENE_AND_PREFABS");
        ValidateOpenScene(report);
        ValidatePrefabs(report);
        WriteReport(report);
    }

    [MenuItem("STAR FRONTIER/Проверки Sprint 3/3. Полная проверка")]
    public static void ValidateAllMenu()
    {
        ValidationReport report = CreateReport("FULL");
        ValidateConfigs(report);
        ValidateOpenScene(report);
        ValidatePrefabs(report);
        WriteReport(report);
    }

    [MenuItem("STAR FRONTIER/Проверки Sprint 3/4. Показать последний отчёт")]
    public static void RevealLatestReport()
    {
        string path = Path.Combine(ProjectRoot, ReportDirectory, LatestReportName);
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog(
                "STAR FRONTIER",
                "Последний отчёт не найден. Сначала выполните проверку.",
                "ОК");
            return;
        }

        EditorUtility.RevealInFinder(path);
    }

    private static ValidationReport CreateReport(string mode)
    {
        return new ValidationReport
        {
            Mode = mode,
            StartedUtc = DateTime.UtcNow,
            Branch = RunGit("branch --show-current"),
            Sha = RunGit("rev-parse HEAD"),
            UnityVersion = Application.unityVersion,
            ScenePath = SceneManager.GetActiveScene().path
        };
    }

    private static void ValidateConfigs(ValidationReport report)
    {
        report.Section("1. CONFIG VALIDATION");

        foreach (RequiredConfig required in RequiredConfigs)
        {
            ScriptableObject asset =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(required.Path);

            if (asset == null)
            {
                report.Error(
                    $"{required.TypeName}: asset не найден по пути {required.Path}");
                continue;
            }

            if (!string.Equals(asset.GetType().Name, required.TypeName, StringComparison.Ordinal))
            {
                report.Error(
                    $"{required.Path}: ожидался тип {required.TypeName}, " +
                    $"получен {asset.GetType().Name}");
                continue;
            }

            report.Pass($"{required.TypeName}: найден {required.Path}");

            SerializedObject serialized = new(asset);
            ValidateAllSerializedNumbers(report, serialized, required.Path);

            switch (required.TypeName)
            {
                case "PlayerControlConfig":
                    ValidatePlayerControlConfig(report, serialized, required.Path);
                    break;
                case "ShipMovementConfig":
                    ValidateShipMovementConfig(report, serialized, required.Path);
                    break;
                case "SystemCameraConfig":
                    ValidateCameraConfig(report, serialized, required.Path);
                    break;
                case "TargetingConfig":
                    ValidateTargetingConfig(report, serialized, required.Path);
                    break;
                case "InteractionConfig":
                    ValidateInteractionConfig(report, serialized, required.Path);
                    break;
            }
        }

        ValidateShipConfigs(report);
        ValidateFuelSource(report);
        ValidateFinalShipStatsSource(report);
    }

    private static void ValidatePlayerControlConfig(
        ValidationReport report,
        SerializedObject serialized,
        string path)
    {
        if (TryBool(serialized, out bool useFloatingJoystick, "useFloatingJoystick") &&
            useFloatingJoystick)
        {
            report.Error(
                $"{path}: useFloatingJoystick должен быть выключен. " +
                "В Sprint 3 действует управление кликом/тапом без виртуального стика.");
        }

        ValidateRange(report, serialized, path, 0f, 1f, true, "deadZone");
        ValidatePositive(report, serialized, path, true, "inputSmoothing");
    }

    private static void ValidateShipMovementConfig(
        ValidationReport report,
        SerializedObject serialized,
        string path)
    {
        ValidatePositive(report, serialized, path, true, "maxSpeed");
        ValidatePositive(report, serialized, path, true, "acceleration");
        ValidatePositive(
            report,
            serialized,
            path,
            true,
            "deceleration",
            "brakingDeceleration");
        ValidatePositive(
            report,
            serialized,
            path,
            true,
            "turnSpeed",
            "turnSpeedDegrees");
        ValidatePositive(
    report,
    serialized,
    path,
    false,
    "arrivalRadius",
    "stopDistance",
    "destinationTolerance",
    "arrivalDistance");
        ValidateRange(
            report,
            serialized,
            path,
            0f,
            1f,
            false,
            "rotationSmoothing");

        if (TryBool(serialized, out bool clamp, "clampToSystemBounds") && clamp)
        {
            SerializedProperty halfSize =
                Find(serialized, "systemBoundsHalfSize", "boundsHalfSize");

            if (halfSize == null || halfSize.propertyType != SerializedPropertyType.Vector2)
            {
                report.Error(
                    $"{path}: включён clampToSystemBounds, " +
                    "но поле systemBoundsHalfSize не найдено.");
            }
            else if (halfSize.vector2Value.x <= 0f || halfSize.vector2Value.y <= 0f)
            {
                report.Error(
                    $"{path}: systemBoundsHalfSize должен иметь X > 0 и Y > 0.");
            }
        }
    }

    private static void ValidateCameraConfig(
        ValidationReport report,
        SerializedObject serialized,
        string path)
    {
        ValidateNonNegative(
            report,
            serialized,
            path,
            false,
            "followSmoothTime",
            "followSmoothing",
            "positionSmoothTime");

        ValidateNonNegative(
            report,
            serialized,
            path,
            false,
            "returnSmoothTime",
            "recenterSmoothTime");

        float? minimum = ReadFloat(
            serialized,
            "minOrthographicSize",
            "minZoom",
            "minimumZoom");

        float? normal = ReadFloat(
            serialized,
            "defaultOrthographicSize",
            "defaultZoom",
            "normalZoom");

        float? maximum = ReadFloat(
            serialized,
            "maxOrthographicSize",
            "maxZoom",
            "maximumZoom");

        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            report.Error($"{path}: минимальный масштаб камеры больше максимального.");
        }

        if (minimum.HasValue && normal.HasValue && normal.Value < minimum.Value)
        {
            report.Error($"{path}: обычный масштаб камеры меньше минимального.");
        }

        if (maximum.HasValue && normal.HasValue && normal.Value > maximum.Value)
        {
            report.Error($"{path}: обычный масштаб камеры больше максимального.");
        }
    }

    private static void ValidateTargetingConfig(
        ValidationReport report,
        SerializedObject serialized,
        string path)
    {
        ValidatePositive(report, serialized, path, true, "maxTargetDistance");

        string[] flags =
        {
            "allowPlanets",
            "allowStations",
            "allowTravelPoints",
            "allowEnemies",
            "allowNpcs",
            "allowPlayer"
        };

        bool foundAnyFlag = false;
        bool anyAllowed = false;

        foreach (string flag in flags)
        {
            SerializedProperty property = serialized.FindProperty(flag);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                continue;
            }

            foundAnyFlag = true;
            anyAllowed |= property.boolValue;
        }

        if (foundAnyFlag && !anyAllowed)
        {
            report.Error($"{path}: запрещены все типы целей.");
        }
    }

    private static void ValidateInteractionConfig(
        ValidationReport report,
        SerializedObject serialized,
        string path)
    {
        bool hasSpecificDistance = false;

        foreach (string name in new[]
                 {
                     "planetInteractionDistance",
                     "stationInteractionDistance",
                     "travelPointInteractionDistance",
                     "defaultInteractionDistance"
                 })
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                continue;
            }

            hasSpecificDistance = true;
            if (property.propertyType == SerializedPropertyType.Float &&
                property.floatValue <= 0f)
            {
                report.Error($"{path}: {name} должен быть больше 0.");
            }
        }

        if (!hasSpecificDistance)
        {
            report.Error($"{path}: не найдено ни одного поля дистанции взаимодействия.");
        }

        ValidateNonNegative(
            report,
            serialized,
            path,
            false,
            "holdToInteractSeconds",
            "interactionCooldownSeconds");
    }

    private static void ValidateShipConfigs(ValidationReport report)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:ShipConfig",
            new[] { "Assets/_Project" });

        if (guids.Length == 0)
        {
            report.Error("ShipConfig: в Assets/_Project не найдено ни одного asset.");
            return;
        }

        report.Pass($"ShipConfig: найдено asset-файлов: {guids.Length}");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (asset == null)
            {
                report.Error($"{path}: ShipConfig не загружается.");
                continue;
            }

            SerializedObject serialized = new(asset);

            ValidatePositive(report, serialized, path, true, "baseHull");
            ValidatePositive(report, serialized, path, true, "baseSpeed");
            ValidatePositive(report, serialized, path, true, "baseAcceleration");
            ValidatePositive(report, serialized, path, true, "baseTurnRate");

            SerializedProperty sprite = serialized.FindProperty("combatSprite");
            if (sprite != null &&
                sprite.propertyType == SerializedPropertyType.ObjectReference &&
                sprite.objectReferenceValue == null)
            {
                report.Error($"{path}: combatSprite не назначен.");
            }

            ValidateAllSerializedNumbers(report, serialized, path);
        }
    }

    private static void ValidateFuelSource(ValidationReport report)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:ScriptableObject",
            new[] { "Assets/_Project/Content/Configs" });

        List<string> candidates = new();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                continue;
            }

            SerializedObject serialized = new(asset);
            bool typeLooksLikeFuel =
                asset.GetType().Name.IndexOf("Fuel", StringComparison.OrdinalIgnoreCase) >= 0;

            bool hasFuelCapacity =
                Find(
                    serialized,
                    "fuelCapacity",
                    "maxFuel",
                    "baseFuelCapacity",
                    "fuelTankCapacity") != null;

            if (!typeLooksLikeFuel && !hasFuelCapacity)
            {
                continue;
            }

            candidates.Add(path);
            ValidateAllSerializedNumbers(report, serialized, path);
            ValidatePositive(
                report,
                serialized,
                path,
                false,
                "fuelCapacity",
                "maxFuel",
                "baseFuelCapacity",
                "fuelTankCapacity");
        }

        if (candidates.Count == 0)
        {
            report.Error(
                "Fuel Config/ship fuel source не найден. " +
                "Валидатор обнаружил отсутствие источника ёмкости топлива.");
        }
        else
        {
            report.Pass(
                "Источник параметров топлива найден: " +
                string.Join(", ", candidates));
        }
    }

    private static void ValidateFinalShipStatsSource(ValidationReport report)
    {
        string scriptsRoot = Path.Combine(ProjectRoot, "Assets/_Project/Scripts");
        if (!Directory.Exists(scriptsRoot))
        {
            report.Error("Папка Assets/_Project/Scripts не найдена.");
            return;
        }

        string[] files = Directory.GetFiles(
            scriptsRoot,
            "*.cs",
            SearchOption.AllDirectories);

        bool hasFinalStats = false;
        bool hasMappingOrFallback = false;

        foreach (string file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            bool mentionsFinalStats =
                text.Contains("ShipFinalStats", StringComparison.Ordinal) ||
                text.Contains("FinalShipStats", StringComparison.Ordinal);

            if (!mentionsFinalStats)
            {
                continue;
            }

            hasFinalStats = true;

            if (text.Contains("ShipConfig", StringComparison.Ordinal) ||
                text.Contains("fallback", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Resolve", StringComparison.Ordinal))
            {
                hasMappingOrFallback = true;
            }
        }

        if (!hasFinalStats)
        {
            report.Error(
                "ShipFinalStats/FinalShipStats не найден. " +
                "Финальные параметры корабля невозможно проверить.");
            return;
        }

        report.Pass("Источник финальных параметров корабля найден.");

        if (!hasMappingOrFallback)
        {
            report.Warning(
                "ShipFinalStats найден, но явный mapping/fallback с ShipConfig " +
                "по текстовому аудиту не обнаружен.");
        }
    }

    private static void ValidateOpenScene(ValidationReport report)
    {
        report.Section("2. OPEN SCENE VALIDATION");

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            report.Error("Нет открытой и загруженной сцены.");
            return;
        }

        if (string.IsNullOrWhiteSpace(scene.path))
        {
            report.Error("Открытая сцена ещё не сохранена как asset.");
        }
        else
        {
            report.Pass($"Проверяется сцена: {scene.path}");
        }

        if (scene.isDirty)
        {
            report.Warning(
                "Сцена содержит несохранённые изменения. " +
                "Сохраните её перед фиксацией отчёта.");
        }

        GameObject[] roots = scene.GetRootGameObjects();
        List<GameObject> objects = roots
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Distinct()
            .ToList();

        ValidateMissingScripts(report, objects, scene.path);

        List<MonoBehaviour> behaviours = objects
            .SelectMany(go => go.GetComponents<MonoBehaviour>())
            .Where(component => component != null)
            .ToList();

        RequireExactlyOneActive(report, behaviours, "SystemCameraController2A");
        RequireExactlyOneActive(report, behaviours, "PlayerInputBridge2A");
        RequireAtMostOneActive(report, behaviours, "SystemControlPresentationBridge2A");
        RequireExactlyOneActive(report, behaviours, "EventSystem");

        ValidateHudRoot(report, objects);
        ValidateShipBinder(report, behaviours);
        ValidateMainCamera(report, objects);
        ValidateLegacyOwners(report, behaviours);
        ValidateRequiredObjectReferences(report, behaviours, scene.path);
        ValidateSpriteRenderers(report, objects, scene.path);
        ValidateUiLayers(report, objects);
    }

    private static void ValidatePrefabs(ValidationReport report)
    {
        report.Section("3. PREFAB VALIDATION");

        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { "Assets/_Project" });

        report.Pass($"Префабов для проверки найдено: {guids.Length}");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.Error($"{path}: prefab не загружается.");
                continue;
            }

            List<GameObject> objects = prefab
                .GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToList();

            ValidateMissingScripts(report, objects, path);

            List<MonoBehaviour> behaviours = objects
                .SelectMany(go => go.GetComponents<MonoBehaviour>())
                .Where(component => component != null)
                .ToList();

            ValidateRequiredObjectReferences(report, behaviours, path);
            ValidateSpriteRenderers(report, objects, path);
        }
    }

    private static void ValidateMissingScripts(
        ValidationReport report,
        IEnumerable<GameObject> objects,
        string owner)
    {
        foreach (GameObject gameObject in objects)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (count > 0)
            {
                report.Error(
                    $"{owner}: объект {GetHierarchyPath(gameObject)} " +
                    $"содержит Missing Script: {count}");
            }
        }
    }

    private static void RequireExactlyOneActive(
        ValidationReport report,
        IEnumerable<MonoBehaviour> behaviours,
        string typeName)
    {
        MonoBehaviour[] found = behaviours
            .Where(component =>
                component.isActiveAndEnabled &&
                component.GetType().Name == typeName)
            .ToArray();

        if (found.Length == 1)
        {
            report.Pass(
                $"{typeName}: найден один активный владелец " +
                $"({GetHierarchyPath(found[0].gameObject)}).");
        }
        else
        {
            report.Error(
                $"{typeName}: ожидается ровно 1 активный компонент, " +
                $"найдено {found.Length}.");
        }
    }

    private static void RequireAtMostOneActive(
        ValidationReport report,
        IEnumerable<MonoBehaviour> behaviours,
        string typeName)
    {
        MonoBehaviour[] found = behaviours
            .Where(component =>
                component.isActiveAndEnabled &&
                component.GetType().Name == typeName)
            .ToArray();

        if (found.Length <= 1)
        {
            report.Pass($"{typeName}: активных компонентов {found.Length}.");
        }
        else
        {
            report.Error(
                $"{typeName}: найдено несколько активных владельцев: " +
                string.Join(
                    ", ",
                    found.Select(component => GetHierarchyPath(component.gameObject))));
        }
    }

    private static void ValidateHudRoot(
        ValidationReport report,
        IEnumerable<GameObject> objects)
    {
        string[] expectedNames =
        {
            "PF_HudTopRoot",
            "SystemMapRightHUD",
            "PF_SystemHudBottomRoot"
        };

        GameObject[] found = objects
            .Where(go =>
                go.activeInHierarchy &&
                expectedNames.Contains(go.name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (found.Length == 0)
        {
            report.Error(
                "HUD root не найден. Ожидается один из объектов: " +
                string.Join(", ", expectedNames));
        }
        else
        {
            report.Pass(
                "HUD root найден: " +
                string.Join(", ", found.Select(GetHierarchyPath)));
        }
    }

    private static void ValidateShipBinder(
    ValidationReport report,
    IEnumerable<MonoBehaviour> behaviours)
    {
        string[] acceptedTypeNames =
        {
        "MetaSceneShipStateBridge2A",
        "SystemControlPresentationBridge2A"
    };

        MonoBehaviour[] candidates = behaviours
            .Where(component =>
            {
                if (!component.isActiveAndEnabled)
                {
                    return false;
                }

                string typeName = component.GetType().Name;

                // Точные действующие компоненты STAR FRONTIER.
                if (acceptedTypeNames.Contains(
                        typeName,
                        StringComparer.Ordinal))
                {
                    return true;
                }

                // Запасная проверка для будущих связующих компонентов корабля.
                bool belongsToShip =
                    typeName.IndexOf(
                        "Ship",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                bool performsBinding =
                    typeName.IndexOf(
                        "Binder",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Sync",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Presentation",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Bridge",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                return belongsToShip && performsBinding;
            })
            .ToArray();

        if (candidates.Length == 0)
        {
            report.Error(
                "Активный компонент связи состояния корабля со сценой не найден. " +
                "Ожидается MetaSceneShipStateBridge2A или " +
                "SystemControlPresentationBridge2A.");
            return;
        }

        report.Pass(
            "Компоненты связи корабля со сценой: " +
            string.Join(
                ", ",
                candidates.Select(component =>
                    component.GetType().Name +
                    "@" +
                    GetHierarchyPath(component.gameObject))));
    }

    private static void ValidateMainCamera(
        ValidationReport report,
        IEnumerable<GameObject> objects)
    {
        Camera[] cameras = objects
            .Select(go => go.GetComponent<Camera>())
            .Where(camera => camera != null && camera.isActiveAndEnabled)
            .ToArray();

        Camera[] main = cameras
            .Where(camera => camera.CompareTag("MainCamera"))
            .ToArray();

        if (main.Length != 1)
        {
            report.Error(
                $"MainCamera: ожидается 1 активная камера с тегом MainCamera, " +
                $"найдено {main.Length}.");
        }
        else
        {
            report.Pass($"MainCamera: {GetHierarchyPath(main[0].gameObject)}");
        }

        foreach (Camera camera in cameras)
        {
            if (camera.cullingMask == 0)
            {
                report.Error(
                    $"{GetHierarchyPath(camera.gameObject)}: Culling Mask равен Nothing.");
            }
        }
    }

    private static void ValidateLegacyOwners(
        ValidationReport report,
        IEnumerable<MonoBehaviour> behaviours)
    {
        MonoBehaviour[] suspects = behaviours
            .Where(component =>
            {
                if (!component.isActiveAndEnabled)
                {
                    return false;
                }

                string name = component.GetType().Name;
                if (name == "SystemCameraController2A" ||
                    name == "SystemCameraFreeLook2A")
                {
                    return false;
                }

                return
                    name.IndexOf("Legacy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Deprecated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("OldCamera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.Equals("SystemCameraController", StringComparison.Ordinal);
            })
            .ToArray();

        foreach (MonoBehaviour suspect in suspects)
        {
            report.Warning(
                "Активный legacy-кандидат: " +
                suspect.GetType().Name + "@" +
                GetHierarchyPath(suspect.gameObject));
        }
    }

    private static void ValidateRequiredObjectReferences(
        ValidationReport report,
        IEnumerable<MonoBehaviour> behaviours,
        string owner)
    {
        Dictionary<string, string[]> checks = new()
        {
            {
                "SystemCameraController2A",
                new[]
                {
                    "config", "cameraConfig", "target", "playerShip",
                    "cameraTransform", "controlledCamera"
                }
            },
            {
                "PlayerInputBridge2A",
                new[]
                {
                    "actionsAsset", "inputActions", "worldCamera",
                    "presentationBridge"
                }
            },
            {
                "SystemControlPresentationBridge2A",
                new[]
                {
                    "inputBridge", "shipTransform", "shipView",
                    "cameraController", "hudRoot"
                }
            }
        };

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (!checks.TryGetValue(behaviour.GetType().Name, out string[] propertyNames))
            {
                continue;
            }

            SerializedObject serialized = new(behaviour);

            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null ||
                    property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (property.objectReferenceValue == null)
                {
                    report.Error(
                        $"{owner}: {behaviour.GetType().Name}@" +
                        $"{GetHierarchyPath(behaviour.gameObject)} — " +
                        $"поле {propertyName} не назначено.");
                }
            }
        }
    }

    private static void ValidateSpriteRenderers(
        ValidationReport report,
        IEnumerable<GameObject> objects,
        string owner)
    {
        foreach (SpriteRenderer renderer in objects
                     .Select(go => go.GetComponent<SpriteRenderer>())
                     .Where(renderer => renderer != null && renderer.enabled))
        {
            if (renderer.sprite == null)
            {
                report.Warning(
                    $"{owner}: SpriteRenderer без Sprite — " +
                    GetHierarchyPath(renderer.gameObject));
            }
        }
    }

    private static void ValidateUiLayers(
        ValidationReport report,
        IEnumerable<GameObject> objects)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
        {
            report.Warning("Слой UI не найден в проекте.");
            return;
        }

        foreach (GameObject go in objects)
        {
            if (!go.activeInHierarchy)
            {
                continue;
            }

            string name = go.name;
            bool looksLikeUi =
                name.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0;

            if (looksLikeUi && go.layer != uiLayer)
            {
                report.Warning(
                    $"{GetHierarchyPath(go)} выглядит как UI, " +
                    $"но находится на слое {LayerMask.LayerToName(go.layer)}.");
            }
        }
    }

    private static void ValidateAllSerializedNumbers(
        ValidationReport report,
        SerializedObject serialized,
        string owner)
    {
        SerializedProperty iterator = serialized.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")
            {
                continue;
            }

            if (iterator.propertyType == SerializedPropertyType.Float)
            {
                float value = iterator.floatValue;

                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    report.Error(
                        $"{owner}: {iterator.propertyPath} содержит NaN/Infinity.");
                    continue;
                }

                if (LooksNonNegative(iterator.name) && value < 0f)
                {
                    report.Error(
                        $"{owner}: {iterator.propertyPath} имеет отрицательное значение {value}.");
                }
            }
            else if (iterator.propertyType == SerializedPropertyType.Integer)
            {
                if (LooksNonNegative(iterator.name) && iterator.intValue < 0)
                {
                    report.Error(
                        $"{owner}: {iterator.propertyPath} имеет отрицательное значение " +
                        iterator.intValue);
                }
            }
        }
    }

    private static bool LooksNonNegative(string propertyName)
    {
        return NonNegativeNameTokens.Any(token =>
            propertyName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void ValidatePositive(
        ValidationReport report,
        SerializedObject serialized,
        string owner,
        bool required,
        params string[] names)
    {
        SerializedProperty property = Find(serialized, names);

        if (property == null)
        {
            if (required)
            {
                report.Error(
                    $"{owner}: обязательное поле не найдено: {string.Join("/", names)}");
            }

            return;
        }

        double value = property.propertyType switch
        {
            SerializedPropertyType.Float => property.floatValue,
            SerializedPropertyType.Integer => property.intValue,
            _ => double.NaN
        };

        if (double.IsNaN(value))
        {
            report.Error(
                $"{owner}: поле {property.propertyPath} не является числом.");
        }
        else if (value <= 0d)
        {
            report.Error(
                $"{owner}: {property.propertyPath} должен быть больше 0, " +
                $"текущее значение {value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void ValidateNonNegative(
        ValidationReport report,
        SerializedObject serialized,
        string owner,
        bool required,
        params string[] names)
    {
        SerializedProperty property = Find(serialized, names);

        if (property == null)
        {
            if (required)
            {
                report.Error(
                    $"{owner}: обязательное поле не найдено: {string.Join("/", names)}");
            }

            return;
        }

        double value = property.propertyType switch
        {
            SerializedPropertyType.Float => property.floatValue,
            SerializedPropertyType.Integer => property.intValue,
            _ => double.NaN
        };

        if (double.IsNaN(value))
        {
            report.Error(
                $"{owner}: поле {property.propertyPath} не является числом.");
        }
        else if (value < 0d)
        {
            report.Error(
                $"{owner}: {property.propertyPath} не может быть отрицательным.");
        }
    }

    private static void ValidateRange(
        ValidationReport report,
        SerializedObject serialized,
        string owner,
        float minimum,
        float maximum,
        bool required,
        params string[] names)
    {
        SerializedProperty property = Find(serialized, names);

        if (property == null)
        {
            if (required)
            {
                report.Error(
                    $"{owner}: обязательное поле не найдено: {string.Join("/", names)}");
            }

            return;
        }

        if (property.propertyType != SerializedPropertyType.Float)
        {
            report.Error($"{owner}: {property.propertyPath} не является float.");
            return;
        }

        float value = property.floatValue;
        if (value < minimum || value > maximum)
        {
            report.Error(
                $"{owner}: {property.propertyPath}={value} вне диапазона " +
                $"{minimum}…{maximum}.");
        }
    }

    private static SerializedProperty Find(
        SerializedObject serialized,
        params string[] names)
    {
        foreach (string name in names)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null)
            {
                return property;
            }
        }

        return null;
    }

    private static float? ReadFloat(
        SerializedObject serialized,
        params string[] names)
    {
        SerializedProperty property = Find(serialized, names);
        if (property == null)
        {
            return null;
        }

        return property.propertyType switch
        {
            SerializedPropertyType.Float => property.floatValue,
            SerializedPropertyType.Integer => property.intValue,
            _ => null
        };
    }

    private static bool TryBool(
        SerializedObject serialized,
        out bool value,
        params string[] names)
    {
        SerializedProperty property = Find(serialized, names);

        if (property != null &&
            property.propertyType == SerializedPropertyType.Boolean)
        {
            value = property.boolValue;
            return true;
        }

        value = false;
        return false;
    }

    private static string GetHierarchyPath(GameObject gameObject)
    {
        List<string> names = new();
        Transform current = gameObject.transform;

        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static void WriteReport(ValidationReport report)
    {
        report.FinishedUtc = DateTime.UtcNow;

        string directory = Path.Combine(ProjectRoot, ReportDirectory);
        Directory.CreateDirectory(directory);

        string safeScene = string.IsNullOrWhiteSpace(report.ScenePath)
            ? "NoScene"
            : Path.GetFileNameWithoutExtension(report.ScenePath);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string fileName = $"Sprint3Validation_{safeScene}_{timestamp}.txt";
        string path = Path.Combine(directory, fileName);
        string latestPath = Path.Combine(directory, LatestReportName);

        string text = report.BuildText();

        File.WriteAllText(path, text, Encoding.UTF8);
        File.WriteAllText(latestPath, text, Encoding.UTF8);

        if (report.ErrorCount > 0)
        {
            Debug.LogError(
                $"[Sprint3 Validator] Ошибок: {report.ErrorCount}; " +
                $"предупреждений: {report.WarningCount}; отчёт: {path}");
        }
        else if (report.WarningCount > 0)
        {
            Debug.LogWarning(
                $"[Sprint3 Validator] Ошибок нет; предупреждений: " +
                $"{report.WarningCount}; отчёт: {path}");
        }
        else
        {
            Debug.Log($"[Sprint3 Validator] PASSED; отчёт: {path}");
        }

        EditorUtility.DisplayDialog(
            "STAR FRONTIER — Sprint 3",
            $"Проверка завершена.\n\n" +
            $"Ошибки: {report.ErrorCount}\n" +
            $"Предупреждения: {report.WarningCount}\n" +
            $"Успешные проверки: {report.PassCount}\n\n" +
            $"Отчёт:\n{path}",
            "ОК");
    }

    private static string RunGit(string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                WorkingDirectory = ProjectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

#if UNITY_EDITOR_WIN
            startInfo.FileName = "git";
            startInfo.Arguments = arguments;
#else
            startInfo.FileName = "/usr/bin/env";
            startInfo.Arguments = "git " + arguments;
#endif

            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                return "UNKNOWN";
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit(5000);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : "UNKNOWN" + (string.IsNullOrWhiteSpace(error) ? string.Empty : $" ({error})");
        }
        catch (Exception exception)
        {
            return $"UNKNOWN ({exception.Message})";
        }
    }

    private static string ProjectRoot =>
        Directory.GetParent(Application.dataPath)?.FullName
        ?? throw new InvalidOperationException("Не удалось определить корень проекта.");

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

    private sealed class ValidationReport
    {
        private readonly List<string> _lines = new();

        public string Mode { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public string Branch { get; set; }
        public string Sha { get; set; }
        public string UnityVersion { get; set; }
        public string ScenePath { get; set; }

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int PassCount { get; private set; }

        public void Section(string text)
        {
            _lines.Add(string.Empty);
            _lines.Add("============================================================");
            _lines.Add(text);
            _lines.Add("============================================================");
        }

        public void Error(string text)
        {
            ErrorCount++;
            _lines.Add("[ERROR] " + text);
        }

        public void Warning(string text)
        {
            WarningCount++;
            _lines.Add("[WARN ] " + text);
        }

        public void Pass(string text)
        {
            PassCount++;
            _lines.Add("[PASS ] " + text);
        }

        public string BuildText()
        {
            StringBuilder builder = new();

            builder.AppendLine("STAR FRONTIER — SPRINT 3 VALIDATION REPORT");
            builder.AppendLine($"Mode: {Mode}");
            builder.AppendLine($"Started UTC: {StartedUtc:O}");
            builder.AppendLine($"Finished UTC: {FinishedUtc:O}");
            builder.AppendLine($"Unity: {UnityVersion}");
            builder.AppendLine($"Branch: {Branch}");
            builder.AppendLine($"SHA: {Sha}");
            builder.AppendLine($"Scene: {ScenePath}");
            builder.AppendLine();
            builder.AppendLine($"ERRORS: {ErrorCount}");
            builder.AppendLine($"WARNINGS: {WarningCount}");
            builder.AppendLine($"PASSES: {PassCount}");

            foreach (string line in _lines)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine();
            builder.AppendLine(
                ErrorCount == 0
                    ? "RESULT: PASSED WITH/WITHOUT WARNINGS"
                    : "RESULT: FAILED — FIX ERRORS AND RUN AGAIN");

            return builder.ToString();
        }
    }
}
#endif
