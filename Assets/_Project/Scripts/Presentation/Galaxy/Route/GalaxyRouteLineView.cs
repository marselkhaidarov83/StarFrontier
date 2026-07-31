using System.Collections.Generic;
using UnityEngine;

public class GalaxyRouteLineView :
    MonoBehaviour
{
    [Header("Config")]

    [SerializeField]
    private RouteConfig _routeConfig;

    [Header("Точки маршрута")]

    [Tooltip(
        "Объект, в который будут добавляться " +
        "созданные точки маршрута."
    )]
    [SerializeField]
    private Transform pointsRoot;

    [Tooltip(
        "Неактивный шаблон одной точки. " +
        "Должен содержать SpriteRenderer."
    )]
    [SerializeField]
    private SpriteRenderer pointTemplate;

    [Tooltip(
        "Количество внутренних расчётных точек. " +
        "Не влияет на количество видимых точек, " +
        "а обеспечивает равномерные интервалы."
    )]
    [SerializeField]
    [Min(16)]
    private int curveSamplingResolution =
        96;

    [Header("Слой отображения")]

    [SerializeField]
    private string sortingLayerName =
        "Default";

    [SerializeField]
    private int disabledOrder =
        -101;

    [SerializeField]
    private int normalOrder =
        15;

    [SerializeField]
    private int selectedOrder =
        15;

    [Header("Цвета")]

    /*
     * Имена полей сохранены от прежнего компонента.
     * Благодаря этому значения цветов в существующем
     * префабе не должны сброситься.
     */

    [SerializeField]
    private Color normalColor =
        new Color(
            0.2f,
            0.8f,
            1f,
            1f);

    [SerializeField]
    private Color disabledColor =
        new Color(
            0.35f,
            0.35f,
            0.4f,
            0.45f);

    [SerializeField]
    private Color selectedColor =
        new Color(
            0.95f,
            1f,
            1f,
            1f);

    [Header("Отладка")]

    [SerializeField]
    private bool debugLogs;

    private readonly List<SpriteRenderer>
        _routePoints =
            new();

    private string _fromSystemId;
    private string _toSystemId;
    private string _routeKey;

    public string FromSystemId =>
        _fromSystemId;

    public string ToSystemId =>
        _toSystemId;

    public string RouteKey =>
        _routeKey;

    private const float DefaultPointSpacing = 0.25f;

    private float _pointSpacing =
        DefaultPointSpacing;

    private void Awake()
    {
        /*
         * Шаблон нужен только для создания копий.
         * Сам шаблон на карте отображаться не должен.
         */
        if (pointTemplate != null)
        {
            pointTemplate
                .gameObject
                .SetActive(false);
        }
    }

    public void Initialize(
        RouteConfig routeConfig,
        string fromSystemId,
        string toSystemId,
        Vector2 fromPosition,
        Vector2 toPosition,
        float pointSpacing,
        GalaxyMapRouteVisualState visualState)
    {
        _routeConfig =
            routeConfig;

        _pointSpacing =
            pointSpacing > 0f
                ? pointSpacing
                : DefaultPointSpacing;

        _fromSystemId =
            fromSystemId;

        _toSystemId =
            toSystemId;

        _routeKey =
            MakeRouteKey(
                fromSystemId,
                toSystemId);

        BuildRoutePoints(
            fromPosition,
            toPosition);

        SetVisualState(
            visualState);

        if (debugLogs)
        {
            Debug.Log(
                "[GalaxyRouteLineView] Route " +
                _fromSystemId +
                " -> " +
                _toSystemId +
                "; visible points = " +
                _routePoints.Count +
                "; curve = " +
                GetCurveStrength()
            );
        }
    }

    public void SetVisualState(
        GalaxyMapRouteVisualState visualState)
    {
        switch (visualState)
        {
            case GalaxyMapRouteVisualState
                .SelectedPath:

                ApplyPointVisual(
                    selectedColor,
                    selectedOrder);

                break;

            case GalaxyMapRouteVisualState
                .Disabled:

                ApplyPointVisual(
                    disabledColor,
                    disabledOrder);

                break;

            case GalaxyMapRouteVisualState
                .Hidden:

                ApplyHidden();

                break;

            default:

                ApplyPointVisual(
                    normalColor,
                    normalOrder);

                break;
        }
    }

    private void ApplyHidden()
    {
        SetPointsVisible(false);
    }

    private void ApplyPointVisual(
        Color color,
        int sortingOrder)
    {
        SetPointsVisible(true);

        foreach (
            SpriteRenderer point
            in _routePoints)
        {
            if (point == null)
                continue;

            point.color =
                color;

            point.sortingLayerName =
                sortingLayerName;

            point.sortingOrder =
                sortingOrder;
        }
    }

    private void SetPointsVisible(
        bool visible)
    {
        if (pointsRoot == null)
            return;

        pointsRoot
            .gameObject
            .SetActive(visible);
    }

    private void BuildRoutePoints(
        Vector2 fromPosition,
        Vector2 toPosition)
    {
        if (pointsRoot == null)
        {
            Debug.LogError(
                "[GalaxyRouteLineView] " +
                "Points Root не назначен.",
                this
            );

            return;
        }

        if (pointTemplate == null)
        {
            Debug.LogError(
                "[GalaxyRouteLineView] " +
                "Point Template не назначен.",
                this
            );

            return;
        }

        ClearGeneratedPoints();

        int visiblePointCount =
            CalculateVisiblePointCount(
                fromPosition,
                toPosition);

        List<Vector3> positions =
            BuildEvenlySpacedCurvePoints(
                fromPosition,
                toPosition,
                visiblePointCount,
                GetCurveStrength());

        for (
            int i = 0;
            i < positions.Count;
            i++)
        {
            SpriteRenderer point =
                Instantiate(
                    pointTemplate,
                    pointsRoot);

            point.gameObject.name =
                "RoutePoint_" +
                i.ToString("00");

            /*
             * Копия неактивного шаблона также создаётся
             * неактивной, поэтому включаем её вручную.
             */
            point.gameObject.SetActive(true);

            point.transform.SetPositionAndRotation(
                positions[i],
                Quaternion.identity);

            /*
             * Размер не изменяется.
             * Каждая копия наследует одинаковый Scale
             * от PointTemplate.
             */
            _routePoints.Add(
                point);
        }
    }

    private int CalculateVisiblePointCount(
    Vector2 fromPosition,
    Vector2 toPosition)
    {
        float distance =
            Vector2.Distance(
                fromPosition,
                toPosition);

        if (distance <= 0.001f)
            return 1;

        float safeSpacing =
            Mathf.Max(
                0.01f,
                _pointSpacing);

        /*
         * Сначала вычисляем количество промежутков.
         *
         * RoundToInt выбирает количество промежутков,
         * при котором фактическое расстояние между
         * точками будет максимально близко
         * к значению из GameConfig.
         */
        int segmentsCount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    distance /
                    safeSpacing));

        /*
         * Для N промежутков требуется N + 1 точка:
         *
         * точка — промежуток — точка.
         */
        return segmentsCount + 1;
    }

    private void ClearGeneratedPoints()
    {
        _routePoints.Clear();

        if (pointsRoot == null)
            return;

        for (
            int i =
                pointsRoot.childCount - 1;
            i >= 0;
            i--)
        {
            GameObject child =
                pointsRoot
                    .GetChild(i)
                    .gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private float GetCurveStrength()
    {
        if (_routeConfig == null)
            return 0f;

        return
            _routeConfig
                .GalaxyMapCurveStrength;
    }

    /*
     * Сначала строится подробная временная кривая,
     * затем видимые точки расставляются по её длине
     * через одинаковые интервалы.
     *
     * Благодаря этому точки не скапливаются
     * возле начала, конца или центра изгиба.
     */
    private List<Vector3>
        BuildEvenlySpacedCurvePoints(
            Vector2 fromPosition,
            Vector2 toPosition,
            int visiblePointCount,
            float curveStrength)
    {
        List<Vector3> result =
            new();

        int safeVisiblePointCount =
            Mathf.Clamp(
                visiblePointCount,
                2,
                128);

        Vector2 direction =
            toPosition -
            fromPosition;

        float routeLength =
            direction.magnitude;

        if (routeLength <= 0.001f)
        {
            result.Add(
                new Vector3(
                    fromPosition.x,
                    fromPosition.y,
                    0f));

            return result;
        }

        int sampleCount =
            Mathf.Max(
                curveSamplingResolution,
                safeVisiblePointCount * 8);

        sampleCount =
            Mathf.Max(
                sampleCount,
                16);

        List<Vector2> samples =
            new(sampleCount);

        float[] cumulativeLengths =
            new float[sampleCount];

        Vector2 previousPoint =
            EvaluateCurvePoint(
                fromPosition,
                toPosition,
                0f,
                curveStrength);

        samples.Add(
            previousPoint);

        cumulativeLengths[0] =
            0f;

        float totalLength =
            0f;

        for (
            int i = 1;
            i < sampleCount;
            i++)
        {
            float t =
                i /
                (float)(sampleCount - 1);

            Vector2 point =
                EvaluateCurvePoint(
                    fromPosition,
                    toPosition,
                    t,
                    curveStrength);

            totalLength +=
                Vector2.Distance(
                    previousPoint,
                    point);

            samples.Add(
                point);

            cumulativeLengths[i] =
                totalLength;

            previousPoint =
                point;
        }

        if (totalLength <= 0.001f)
        {
            result.Add(
                new Vector3(
                    fromPosition.x,
                    fromPosition.y,
                    0f));

            return result;
        }

        int currentSampleIndex =
            1;

        for (
            int pointIndex = 0;
            pointIndex <
                safeVisiblePointCount;
            pointIndex++)
        {
            float normalizedDistance =
                pointIndex /
                (float)(
                    safeVisiblePointCount -
                    1);

            float targetDistance =
                totalLength *
                normalizedDistance;

            while (
                currentSampleIndex <
                    sampleCount - 1 &&
                cumulativeLengths[
                    currentSampleIndex] <
                    targetDistance)
            {
                currentSampleIndex++;
            }

            int previousSampleIndex =
                Mathf.Max(
                    0,
                    currentSampleIndex - 1);

            float segmentStartDistance =
                cumulativeLengths[
                    previousSampleIndex];

            float segmentEndDistance =
                cumulativeLengths[
                    currentSampleIndex];

            float segmentLength =
                segmentEndDistance -
                segmentStartDistance;

            float segmentT =
                segmentLength > 0.0001f
                    ? (
                        targetDistance -
                        segmentStartDistance
                      ) /
                      segmentLength
                    : 0f;

            Vector2 finalPoint =
                Vector2.Lerp(
                    samples[
                        previousSampleIndex],
                    samples[
                        currentSampleIndex],
                    segmentT);

            result.Add(
                new Vector3(
                    finalPoint.x,
                    finalPoint.y,
                    0f));
        }

        return result;
    }

    /*
     * Равномерная симметричная выпуклость:
     *
     * в начале маршрута коэффициент равен 0;
     * в центре — 1;
     * в конце снова равен 0.
     *
     * Знак curveStrength определяет сторону изгиба.
     */
    private Vector2 EvaluateCurvePoint(
        Vector2 fromPosition,
        Vector2 toPosition,
        float t,
        float curveStrength)
    {
        Vector2 direction =
            toPosition -
            fromPosition;

        float routeLength =
            direction.magnitude;

        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x)
            .normalized;

        Vector2 straightPoint =
            Vector2.Lerp(
                fromPosition,
                toPosition,
                t);

        float safeCurveStrength =
            Mathf.Clamp(
                curveStrength,
                -0.5f,
                0.5f);

        /*
         * 4 * t * (1 - t):
         *
         * t = 0.0 -> 0
         * t = 0.5 -> 1
         * t = 1.0 -> 0
         */
        float bulgeFactor =
            4f *
            t *
            (1f - t);

        float bulgeDistance =
            routeLength *
            safeCurveStrength *
            bulgeFactor;

        return
            straightPoint +
            perpendicular *
            bulgeDistance;
    }

    public static string MakeRouteKey(
        string firstSystemId,
        string secondSystemId)
    {
        if (string.CompareOrdinal(
                firstSystemId,
                secondSystemId) <= 0)
        {
            return
                firstSystemId +
                "__" +
                secondSystemId;
        }

        return
            secondSystemId +
            "__" +
            firstSystemId;
    }
}