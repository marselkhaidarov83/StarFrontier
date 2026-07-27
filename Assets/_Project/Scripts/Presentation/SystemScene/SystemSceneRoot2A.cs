using System.Text;
using UnityEngine;

/// <summary>
/// Корневой scene/view-компонент системной части MetaScene.
///
/// Важно:
/// это не новый gameplay-контроллер.
/// Он только хранит ссылки на уже существующие объекты MetaScene
/// и проверяет, что базовая иерархия системной карты собрана.
///
/// Существующую логику SystemMapController2,
/// SystemShipMarkerController2 и перелётов он не заменяет.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemSceneRoot2A : MonoBehaviour
{
    [Header("Existing MetaScene Core")]
    [SerializeField] private GameObject systemMapRoot;
    [SerializeField] private Camera mainCamera;

    [Header("Existing World Objects")]
    [SerializeField] private GameObject systemBackground;
    [SerializeField] private Transform sunNodesRoot;
    [SerializeField] private Transform planetNodesRoot;
    [SerializeField] private Transform systemExitsNodesRoot;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform enemiesRoot;
    [SerializeField] private Transform alliesRoot;
    [SerializeField] private Transform projectilesRoot;
    [SerializeField] private Transform vfxRoot;

    [Header("Stage 2A Structural Roots")]
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private Transform markersRoot;
    [SerializeField] private Transform debugRoot;

    [Header("Existing UI Roots")]
    [SerializeField] private Canvas metaCanvas;
    [SerializeField] private RectTransform systemMapHudRoot;
    [SerializeField] private RectTransform planetRoot;
    [SerializeField] private RectTransform metaHudRoot;

    [Header("Validation")]
    [SerializeField] private bool warnOnAwake = true;

    public GameObject SystemMapRoot => systemMapRoot;
    public Camera MainCamera => mainCamera;

    public GameObject SystemBackground => systemBackground;
    public Transform SunNodesRoot => sunNodesRoot;
    public Transform PlanetNodesRoot => planetNodesRoot;
    public Transform SystemExitsNodesRoot => systemExitsNodesRoot;
    public Transform PlayerRoot => playerRoot;
    public Transform EnemiesRoot => enemiesRoot;
    public Transform AlliesRoot => alliesRoot;
    public Transform ProjectilesRoot => projectilesRoot;
    public Transform VfxRoot => vfxRoot;

    public Transform BoundsRoot => boundsRoot;
    public Transform MarkersRoot => markersRoot;
    public Transform DebugRoot => debugRoot;

    public Canvas MetaCanvas => metaCanvas;
    public RectTransform SystemMapHudRoot => systemMapHudRoot;
    public RectTransform PlanetRoot => planetRoot;
    public RectTransform MetaHudRoot => metaHudRoot;

    private void Reset()
    {
        systemMapRoot = gameObject;
    }

    private void Awake()
    {
        if (!warnOnAwake)
            return;

        if (!HasRequiredReferences())
        {
            Debug.LogWarning(
                GetMissingReferencesReport(),
                this);
        }
    }

    public bool HasRequiredReferences()
    {
        return systemMapRoot != null
            && mainCamera != null
            && systemBackground != null
            && sunNodesRoot != null
            && planetNodesRoot != null
            && systemExitsNodesRoot != null
            && playerRoot != null
            && enemiesRoot != null
            && alliesRoot != null
            && projectilesRoot != null
            && vfxRoot != null
            && boundsRoot != null
            && markersRoot != null
            && debugRoot != null
            && metaCanvas != null
            && systemMapHudRoot != null
            && planetRoot != null
            && metaHudRoot != null;
    }

    public string GetMissingReferencesReport()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "SystemSceneRoot2A has missing MetaScene references:");

        AppendMissing(builder, systemMapRoot, nameof(systemMapRoot));
        AppendMissing(builder, mainCamera, nameof(mainCamera));

        AppendMissing(builder, systemBackground, nameof(systemBackground));
        AppendMissing(builder, sunNodesRoot, nameof(sunNodesRoot));
        AppendMissing(builder, planetNodesRoot, nameof(planetNodesRoot));
        AppendMissing(builder, systemExitsNodesRoot, nameof(systemExitsNodesRoot));
        AppendMissing(builder, playerRoot, nameof(playerRoot));
        AppendMissing(builder, enemiesRoot, nameof(enemiesRoot));
        AppendMissing(builder, alliesRoot, nameof(alliesRoot));
        AppendMissing(builder, projectilesRoot, nameof(projectilesRoot));
        AppendMissing(builder, vfxRoot, nameof(vfxRoot));

        AppendMissing(builder, boundsRoot, nameof(boundsRoot));
        AppendMissing(builder, markersRoot, nameof(markersRoot));
        AppendMissing(builder, debugRoot, nameof(debugRoot));

        AppendMissing(builder, metaCanvas, nameof(metaCanvas));
        AppendMissing(builder, systemMapHudRoot, nameof(systemMapHudRoot));
        AppendMissing(builder, planetRoot, nameof(planetRoot));
        AppendMissing(builder, metaHudRoot, nameof(metaHudRoot));

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
