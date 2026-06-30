using System.Text;
using UnityEngine;

/// <summary>
/// Корневой MonoBehaviour новой сцены локальной системы.
///
/// Назначение:
/// хранить ссылки на основные слои сцены и проверять,
/// что базовая иерархия создана правильно.
///
/// Важно:
/// этот компонент не управляет gameplay-логикой,
/// не двигает корабль и не обращается к Bootstrapper.Instance.
/// </summary>
public sealed class SystemSceneRoot2A : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Roots")]
    [SerializeField] private Transform worldRoot;
    [SerializeField] private Transform backgroundRoot;
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private Transform objectsRoot;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform markersRoot;
    [SerializeField] private Transform vfxRoot;
    [SerializeField] private Transform uiRoot;
    [SerializeField] private Transform debugRoot;

    [Header("Scene Settings")]
    [SerializeField] private bool configureCameraOnAwake = true;

    [SerializeField]
    [Min(0.1f)]
    private float defaultOrthographicSize = 6.5f;

    public Camera MainCamera => mainCamera;

    public Transform WorldRoot => worldRoot;
    public Transform BackgroundRoot => backgroundRoot;
    public Transform BoundsRoot => boundsRoot;
    public Transform ObjectsRoot => objectsRoot;
    public Transform PlayerRoot => playerRoot;
    public Transform MarkersRoot => markersRoot;
    public Transform VfxRoot => vfxRoot;
    public Transform UiRoot => uiRoot;
    public Transform DebugRoot => debugRoot;

    private void Awake()
    {
        if (configureCameraOnAwake)
            ConfigureCamera();

        if (!HasRequiredReferences())
        {
            Debug.LogWarning(
                GetMissingReferencesReport(),
                this);
        }
    }

    public void ConfigureCamera()
    {
        if (mainCamera == null)
            return;

        mainCamera.orthographic = true;

        if (mainCamera.orthographicSize <= 0.1f)
            mainCamera.orthographicSize = defaultOrthographicSize;

        Transform cameraTransform =
            mainCamera.transform;

        cameraTransform.position =
            new Vector3(0f, 0f, -10f);

        cameraTransform.rotation =
            Quaternion.identity;
    }

    public bool HasRequiredReferences()
    {
        return mainCamera != null
            && worldRoot != null
            && backgroundRoot != null
            && boundsRoot != null
            && objectsRoot != null
            && playerRoot != null
            && markersRoot != null
            && vfxRoot != null
            && uiRoot != null
            && debugRoot != null;
    }

    public string GetMissingReferencesReport()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "SystemSceneRoot2A has missing references:");

        AppendMissing(builder, mainCamera, nameof(mainCamera));
        AppendMissing(builder, worldRoot, nameof(worldRoot));
        AppendMissing(builder, backgroundRoot, nameof(backgroundRoot));
        AppendMissing(builder, boundsRoot, nameof(boundsRoot));
        AppendMissing(builder, objectsRoot, nameof(objectsRoot));
        AppendMissing(builder, playerRoot, nameof(playerRoot));
        AppendMissing(builder, markersRoot, nameof(markersRoot));
        AppendMissing(builder, vfxRoot, nameof(vfxRoot));
        AppendMissing(builder, uiRoot, nameof(uiRoot));
        AppendMissing(builder, debugRoot, nameof(debugRoot));

        return builder.ToString();
    }

    private static void AppendMissing(
        StringBuilder builder,
        Object value,
        string fieldName)
    {
        if (value != null)
            return;

        builder.Append("- ");
        builder.AppendLine(fieldName);
    }
}