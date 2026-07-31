using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GalaxyRouteLineView : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private RouteConfig _routeConfig;

    [Header("Линия")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Слой отображения")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int disabledOrder = -101;
    [SerializeField] private int normalOrder = 15;
    [SerializeField] private int selectedOrder = 15;

    [Header("Толщина")]
    [SerializeField] private float normalWidth = 0.03f;
    [SerializeField] private float disabledWidth = 0.00f;
    [SerializeField] private float selectedWidth = 0.06f;

    [Header("Цвета")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.35f, 0.35f, 0.4f, 0.45f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 1f, 1f, 1f);

    [Header("Форма")]
    [SerializeField] private int pointsCount = 24;
    [SerializeField] private float curveStrength = 0.08f;

    [Header("Отладка")]
    [SerializeField] private bool debugLogs;

    private string _fromSystemId;
    private string _toSystemId;
    private string _routeKey;

    public string FromSystemId => _fromSystemId;
    public string ToSystemId => _toSystemId;
    public string RouteKey => _routeKey;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        SetupLineRenderer();
    }

    public void Initialize(
        RouteConfig routeConfig,
        string fromSystemId,
        string toSystemId,
        Vector2 fromPosition,
        Vector2 toPosition,
        GalaxyMapRouteVisualState visualState)
    {
        _routeConfig = routeConfig;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        SetupLineRenderer();

        _fromSystemId = fromSystemId;
        _toSystemId = toSystemId;
        _routeKey = MakeRouteKey(fromSystemId, toSystemId);

        DrawRoute(fromPosition, toPosition);
        SetVisualState(visualState);

        if (debugLogs)
        {
            Debug.Log(
                "[GalaxyRouteLineView] Route " +
                _fromSystemId +
                " -> " +
                _toSystemId +
                " state = " +
                visualState +
                " from = " +
                fromPosition +
                " to = " +
                toPosition
            );
        }
    }

    public void SetVisualState(GalaxyMapRouteVisualState visualState)
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        SetupLineRenderer();

        switch (visualState)
        {
            case GalaxyMapRouteVisualState.SelectedPath:
                ApplyLineVisual(selectedColor, selectedWidth, selectedOrder);
                break;

            case GalaxyMapRouteVisualState.Disabled:
                ApplyLineVisual(disabledColor, disabledWidth, disabledOrder);
                break;

            case GalaxyMapRouteVisualState.Hidden:
                ApplyHidden();
                break;

            default:
                ApplyLineVisual(normalColor, normalWidth, normalOrder);
                break;
        }
    }

    private void ApplyHidden()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.enabled = false;
    }

    private void ApplyLineVisual(Color color, float width, int order)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.enabled = true;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = order;

        if (lineRenderer.material != null)
            lineRenderer.material.color = Color.white;
    }

    private void DrawRoute(Vector2 fromPosition, Vector2 toPosition)
    {
        if (lineRenderer == null)
            return;

        List<Vector3> points = BuildCurvePoints(fromPosition, toPosition);

        lineRenderer.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
            lineRenderer.SetPosition(i, points[i]);
    }

    private List<Vector3> BuildCurvePoints(Vector2 fromPosition, Vector2 toPosition)
    {
        List<Vector3> points = new();

        Vector2 direction = toPosition - fromPosition;

        if (direction.magnitude <= 0.001f)
        {
            points.Add(new Vector3(fromPosition.x, fromPosition.y, 0f));
            points.Add(new Vector3(toPosition.x, toPosition.y, 0f));
            return points;
        }

        Vector2 middle = fromPosition + direction * 0.5f;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
        Vector2 controlPoint = middle + perpendicular * direction.magnitude * curveStrength;

        int safePointsCount = Mathf.Max(2, pointsCount);

        for (int i = 0; i < safePointsCount; i++)
        {
            float t = i / (float)(safePointsCount - 1);

            Vector2 point =
                Mathf.Pow(1f - t, 2f) * fromPosition +
                2f * (1f - t) * t * controlPoint +
                Mathf.Pow(t, 2f) * toPosition;

            points.Add(new Vector3(point.x, point.y, 0f));
        }

        return points;
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;

        lineRenderer.numCornerVertices = 6;
        lineRenderer.numCapVertices = 6;

        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;

        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = normalOrder;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material material = new Material(shader);
            material.color = Color.white;
            lineRenderer.material = material;
        }
    }

    public static string MakeRouteKey(string firstSystemId, string secondSystemId)
    {
        if (string.CompareOrdinal(firstSystemId, secondSystemId) <= 0)
            return firstSystemId + "__" + secondSystemId;

        return secondSystemId + "__" + firstSystemId;
    }
}