using UnityEngine;

public sealed class Pseudo3DDepthByY2 : MonoBehaviour
{
    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = Pseudo3DRenderOrder2.ObjectsLayer;
    [SerializeField] private int baseSortingOrder = Pseudo3DRenderOrder2.PlanetBase;
    [SerializeField] private int depthSortingRange = 200;

    [Header("Depth Y Range")]
    [SerializeField] private float farY = 900f;
    [SerializeField] private float nearY = -900f;

    [Header("Depth Scale")]
    [SerializeField] private bool applyScale = true;
    [SerializeField] private float farScale = 0.85f;
    [SerializeField] private float nearScale = 1.15f;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateRendererCount;
    private static double _aggregateTotalMs;
    private static double _aggregateInitializeMs;
    private static double _aggregateApplyDepthMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxObjectName = string.Empty;

    private Vector3 _baseScale;
    private bool _initialized;

    private void Reset()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Start()
    {
        InitializeIfNeeded();
        ApplyDepth();
    }

    private void LateUpdate()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double initializeMs = 0.0;
        double applyDepthMs = 0.0;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            InitializeIfNeeded();

            initializeMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplyDepth();

            applyDepthMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordLateUpdateAggregate(
                elapsedMs,
                initializeMs,
                applyDepthMs,
                spriteRenderers != null ? spriteRenderers.Length : 0,
                gameObject.name);

            VisualUpdatePerfLog.LogIfSlow(
                "Pseudo3DDepthByY2.LateUpdate",
                startedAt,
                "Name=" + gameObject.name +
                " | RendererCount=" + (spriteRenderers != null ? spriteRenderers.Length : 0) +
                " | InitializeMs=" + initializeMs.ToString("F3") +
                " | ApplyDepthMs=" + applyDepthMs.ToString("F3"));
        }
    }

    public void SetBaseWorldSize(
        SpriteRenderer referenceRenderer,
        float targetSize)
    {
        if (referenceRenderer == null ||
            referenceRenderer.sprite == null)
        {
            return;
        }

        Vector2 spriteWorldSize =
            referenceRenderer
                .sprite
                .bounds
                .size;

        float maxSide =
            Mathf.Max(
                spriteWorldSize.x,
                spriteWorldSize.y);

        if (maxSide <= 0f)
            return;

        float scale =
            Mathf.Max(0f, targetSize) /
            maxSide;

        _baseScale =
            new Vector3(
                scale,
                scale,
                transform.localScale.z);

        _initialized =
            true;

        ApplyDepth();
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;

        _baseScale = transform.localScale;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        _initialized = true;
    }

    private void ApplyDepth()
    {
        float y = transform.position.y;
        float depth01 = Mathf.InverseLerp(farY, nearY, y);

        int sortingOrder =
            baseSortingOrder +
            Mathf.RoundToInt(depth01 * depthSortingRange);

        if (spriteRenderers != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer =
                    spriteRenderers[i];

                if (spriteRenderer == null)
                    continue;

                spriteRenderer.sortingLayerName = sortingLayerName;
                spriteRenderer.sortingOrder = sortingOrder;
            }
        }

        if (applyScale)
        {
            float scale = Mathf.Lerp(farScale, nearScale, depth01);
            transform.localScale = _baseScale * scale;
        }
    }

    private static void RecordLateUpdateAggregate(
        double elapsedMs,
        double initializeMs,
        double applyDepthMs,
        int rendererCount,
        string objectName)
    {
        int frame =
            Time.frameCount;

        if (_aggregateFrame != frame)
        {
            FlushLateUpdateAggregate();
            ResetLateUpdateAggregate(frame);
        }

        _aggregateCount++;
        _aggregateRendererCount += rendererCount;
        _aggregateTotalMs += elapsedMs;
        _aggregateInitializeMs += initializeMs;
        _aggregateApplyDepthMs += applyDepthMs;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxObjectName = objectName ?? string.Empty;
        }
    }

    private static void ResetLateUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateRendererCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateInitializeMs = 0.0;
        _aggregateApplyDepthMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxObjectName = string.Empty;
    }

    private static void FlushLateUpdateAggregate()
    {
        if (_aggregateFrame < 0 ||
            _aggregateCount <= 0)
        {
            return;
        }

        if (!VisualUpdatePerfLog.ShouldLog(_aggregateTotalMs))
            return;

        VisualUpdatePerfLog.LogMeasured(
            "Pseudo3DDepthByY2.LateUpdate.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | RendererCount=" + _aggregateRendererCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxObject=" + _aggregateMaxObjectName +
            " | InitializeMs=" + _aggregateInitializeMs.ToString("F3") +
            " | ApplyDepthMs=" + _aggregateApplyDepthMs.ToString("F3"));
    }
}