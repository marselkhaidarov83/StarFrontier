using UnityEngine;

public sealed class ShipPseudo3DFx2 : CustomMonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private SpriteRenderer shipSprite;
    [SerializeField] private SpriteRenderer shadowSprite;
    [SerializeField] private SpriteRenderer engineGlowSprite;
    [SerializeField] private TrailRenderer engineTrail;

    [Header("Shadow")]
    [SerializeField] private Vector3 shadowLocalOffset = new Vector3(0f, -0.28f, 0f);
    [SerializeField] private Vector3 shadowLocalScale = new Vector3(1.15f, 0.55f, 1f);
    [SerializeField] private float shadowAlpha = 0.35f;

    [Header("Engine Glow")]
    [SerializeField] private float glowMinAlpha = 0.45f;
    [SerializeField] private float glowMaxAlpha = 0.85f;
    [SerializeField] private float glowPulseSpeed = 6f;

    [Header("Engine Trail")]
    [SerializeField] private float minSpeedForTrail = 0.5f;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static double _aggregateTotalMs;
    private static double _aggregateInitializeMs;
    private static double _aggregateShadowMs;
    private static double _aggregateGlowMs;
    private static double _aggregateTrailMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxObjectName = string.Empty;

    private Vector3 _lastPosition;
    private bool _initialized;

    private void Start()
    {
        Initialize();
        ApplyStaticSettings();
    }

    private void LateUpdate()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double initializeMs = 0.0;
        double shadowMs = 0.0;
        double glowMs = 0.0;
        double trailMs = 0.0;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            Initialize();

            initializeMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            UpdateShadow();

            shadowMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            UpdateGlow();

            glowMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            UpdateTrail();

            trailMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            _lastPosition = transform.position;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordLateUpdateAggregate(
                elapsedMs,
                initializeMs,
                shadowMs,
                glowMs,
                trailMs,
                gameObject.name);

            VisualUpdatePerfLog.LogIfSlow(
                "ShipPseudo3DFx2.LateUpdate",
                startedAt,
                "Name=" + gameObject.name +
                " | InitializeMs=" + initializeMs.ToString("F3") +
                " | ShadowMs=" + shadowMs.ToString("F3") +
                " | GlowMs=" + glowMs.ToString("F3") +
                " | TrailMs=" + trailMs.ToString("F3"));
        }
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        _lastPosition = transform.position;
        _initialized = true;
    }

    private void ApplyStaticSettings()
    {
        if (shipSprite != null)
        {
            shipSprite.sortingLayerName = Pseudo3DRenderOrder2.ShipFxLayer;
            shipSprite.sortingOrder = Pseudo3DRenderOrder2.Ship;
        }

        if (shadowSprite != null)
        {
            shadowSprite.sortingLayerName = Pseudo3DRenderOrder2.ShipFxLayer;
            shadowSprite.sortingOrder = Pseudo3DRenderOrder2.ShipShadow;
            SetAlpha(shadowSprite, shadowAlpha);
        }

        if (engineGlowSprite != null)
        {
            engineGlowSprite.sortingLayerName = Pseudo3DRenderOrder2.ShipFxLayer;
            engineGlowSprite.sortingOrder = Pseudo3DRenderOrder2.EngineGlow;
        }

        if (engineTrail != null)
        {
            engineTrail.sortingLayerName = Pseudo3DRenderOrder2.ShipFxLayer;
            engineTrail.sortingOrder = Pseudo3DRenderOrder2.EngineTrail;
        }
    }

    private void UpdateShadow()
    {
        if (shadowSprite == null)
            return;

        shadowSprite.transform.localPosition = shadowLocalOffset;
        shadowSprite.transform.localScale = shadowLocalScale;
        shadowSprite.transform.localRotation = Quaternion.identity;
    }

    private void UpdateGlow()
    {
        if (engineGlowSprite == null)
            return;

        float pulse01 = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, pulse01);

        SetAlpha(engineGlowSprite, alpha);
    }

    private void UpdateTrail()
    {
        if (engineTrail == null)
            return;

        float speed =
            (transform.position - _lastPosition).magnitude /
            Mathf.Max(Time.deltaTime, 0.0001f);

        engineTrail.emitting =
            speed >= minSpeedForTrail;
    }

    private void SetAlpha(SpriteRenderer spriteRenderer, float alpha)
    {
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private static void RecordLateUpdateAggregate(
        double elapsedMs,
        double initializeMs,
        double shadowMs,
        double glowMs,
        double trailMs,
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
        _aggregateTotalMs += elapsedMs;
        _aggregateInitializeMs += initializeMs;
        _aggregateShadowMs += shadowMs;
        _aggregateGlowMs += glowMs;
        _aggregateTrailMs += trailMs;

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
        _aggregateTotalMs = 0.0;
        _aggregateInitializeMs = 0.0;
        _aggregateShadowMs = 0.0;
        _aggregateGlowMs = 0.0;
        _aggregateTrailMs = 0.0;
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
            "ShipPseudo3DFx2.LateUpdate.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxObject=" + _aggregateMaxObjectName +
            " | InitializeMs=" + _aggregateInitializeMs.ToString("F3") +
            " | ShadowMs=" + _aggregateShadowMs.ToString("F3") +
            " | GlowMs=" + _aggregateGlowMs.ToString("F3") +
            " | TrailMs=" + _aggregateTrailMs.ToString("F3"));
    }
}