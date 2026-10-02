using UnityEngine;

public sealed class CombatWaveView2A : CustomMonoBehaviour
{
    private const string DefaultSortingLayerName = "SystemShipFX";
    private const float WaveVisualGeometryLogStep = 0.1f;

    private ISystemNpcCombatService _combatService;
    private WaveWeaponVisualSettings2A _settings;
    private SpriteRenderer[] _renderers;
    private Color[] _originalRendererColors;
    private Color _runtimeTint = Color.white;
    private bool _hasCachedOriginalColors;
    private int _lastVisualGeometryLogBucket = -1;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateCompletedCount;
    private static double _aggregateTotalMs;
    private static double _aggregateResolveServiceMs;
    private static double _aggregateTryGetWaveMs;
    private static double _aggregateApplyVisualMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxWaveId = string.Empty;

    public string WaveId { get; private set; }

    public void Init(CombatWaveStartedEvent2A evt, Color tint)
    {
        WaveId = evt.WaveId;
        _runtimeTint = tint;
        _lastVisualGeometryLogBucket = -1;

        ResolveSettings();
        ResolveRenderers();
        CacheOriginalRendererColors();
        ResolveCombatService();

        Vector3 position = evt.CenterPosition;

        if (_settings != null && _settings.ForceWorldZ)
            position.z = _settings.WorldZ;

        transform.position = position;

        ApplyRenderOrder();
        ApplyVisual(0f, 0f);

        LogWaveVisualGeometry(
            "VISUAL_INIT",
            0f,
            0f,
            0f,
            0f,
            -1,
            null);

        gameObject.SetActive(true);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveServiceMs = 0.0;
        double tryGetWaveMs = 0.0;
        double applyVisualMs = 0.0;

        bool completed = false;

        try
        {
            if (string.IsNullOrWhiteSpace(WaveId))
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolveCombatService();

            resolveServiceMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (_combatService == null)
                return;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            bool waveFound =
                _combatService.TryGetWave(
                    WaveId,
                    out CombatWaveRuntimeState2A wave);

            tryGetWaveMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!waveFound ||
                wave == null ||
                wave.IsResolved)
            {
                completed = true;
                Complete();
                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplyVisual(wave.Progress01, wave.CurrentRadius);

            applyVisualMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordUpdateAggregate(
                elapsedMs,
                resolveServiceMs,
                tryGetWaveMs,
                applyVisualMs,
                completed,
                WaveId);

            VisualUpdatePerfLog.LogIfSlow(
                "CombatWaveView2A.Update",
                startedAt,
                "WaveId=" + (WaveId ?? string.Empty) +
                " | Completed=" + completed +
                " | ResolveServiceMs=" + resolveServiceMs.ToString("F3") +
                " | TryGetWaveMs=" + tryGetWaveMs.ToString("F3") +
                " | ApplyVisualMs=" + applyVisualMs.ToString("F3"));
        }
    }

    private static void RecordUpdateAggregate(
        double elapsedMs,
        double resolveServiceMs,
        double tryGetWaveMs,
        double applyVisualMs,
        bool completed,
        string waveId)
    {
        int frame =
            Time.frameCount;

        if (_aggregateFrame != frame)
        {
            FlushUpdateAggregate();
            ResetUpdateAggregate(frame);
        }

        _aggregateCount++;
        _aggregateTotalMs += elapsedMs;
        _aggregateResolveServiceMs += resolveServiceMs;
        _aggregateTryGetWaveMs += tryGetWaveMs;
        _aggregateApplyVisualMs += applyVisualMs;

        if (completed)
            _aggregateCompletedCount++;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxWaveId = waveId ?? string.Empty;
        }
    }

    private static void ResetUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateCompletedCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateResolveServiceMs = 0.0;
        _aggregateTryGetWaveMs = 0.0;
        _aggregateApplyVisualMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxWaveId = string.Empty;
    }

    private static void FlushUpdateAggregate()
    {
        if (_aggregateFrame < 0 ||
            _aggregateCount <= 0)
        {
            return;
        }

        if (!VisualUpdatePerfLog.ShouldLog(_aggregateTotalMs))
            return;

        VisualUpdatePerfLog.LogMeasured(
            "CombatWaveView2A.Update.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | CompletedCount=" + _aggregateCompletedCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxWaveId=" + _aggregateMaxWaveId +
            " | ResolveServiceMs=" + _aggregateResolveServiceMs.ToString("F3") +
            " | TryGetWaveMs=" + _aggregateTryGetWaveMs.ToString("F3") +
            " | ApplyVisualMs=" + _aggregateApplyVisualMs.ToString("F3"));
    }

    public void Complete()
    {
        WaveId = string.Empty;
        _runtimeTint = Color.white;

        RestoreOriginalRendererColors();

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = false;
            }
        }

        gameObject.SetActive(false);
    }

    private void ResolveSettings()
    {
        if (_settings == null)
            _settings = GetComponent<WaveWeaponVisualSettings2A>();
    }

    private void ResolveRenderers()
    {
        if (_renderers != null && _renderers.Length > 0)
            return;

        if (_settings != null &&
            _settings.WaveRenderers != null &&
            _settings.WaveRenderers.Length > 0)
        {
            _renderers = _settings.WaveRenderers;
            return;
        }

        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void CacheOriginalRendererColors()
    {
        if (_renderers == null)
            return;

        if (_hasCachedOriginalColors &&
            _originalRendererColors != null &&
            _originalRendererColors.Length == _renderers.Length)
        {
            return;
        }

        _originalRendererColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            _originalRendererColors[i] =
                _renderers[i] != null
                    ? _renderers[i].color
                    : Color.white;
        }

        _hasCachedOriginalColors = true;
    }

    private void RestoreOriginalRendererColors()
    {
        if (_renderers == null ||
            _originalRendererColors == null)
        {
            return;
        }

        int count =
            Mathf.Min(_renderers.Length, _originalRendererColors.Length);

        for (int i = 0; i < count; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].color = _originalRendererColors[i];
        }
    }

    private void ApplyRenderOrder()
    {
        if (_renderers == null)
            return;

        string layerName = _settings != null
            ? _settings.SortingLayerName
            : DefaultSortingLayerName;

        int baseOrder = _settings != null
            ? _settings.SortingOrder
            : 900;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null)
                continue;

            _renderers[i].sortingLayerName = layerName;
            _renderers[i].sortingOrder = baseOrder + i;
        }
    }

    private void ApplyVisual(
        float progress01,
        float currentRadius)
    {
        if (_renderers == null)
            return;

        float safeProgress01 =
            Mathf.Clamp01(progress01);

        float safeCurrentRadius =
            Mathf.Max(0f, currentRadius);

        float alphaStart =
            _settings != null ? _settings.StartAlpha : 0.9f;

        float alphaEnd =
            _settings != null ? _settings.EndAlpha : 0f;

        float layerStep =
            _settings != null ? _settings.LayerRadiusStep : 0.08f;

        float alpha =
            Mathf.Lerp(alphaStart, alphaEnd, safeProgress01);

        bool shouldLogGeometry =
            ShouldLogWaveVisualGeometry(safeProgress01);

        for (int i = 0; i < _renderers.Length; i++)
        {
            SpriteRenderer renderer = _renderers[i];

            if (renderer == null)
                continue;

            bool shouldShowRenderer =
                safeCurrentRadius > 0.001f;

            renderer.enabled = shouldShowRenderer;

            if (!shouldShowRenderer)
                continue;

            float layerFactor =
                i == 0
                    ? 1f
                    : Mathf.Max(0.1f, 1f - layerStep * i);

            float visualLayerRadius =
                Mathf.Max(
                    0.01f,
                    safeCurrentRadius * layerFactor);

            ApplyRendererRadius(
                renderer,
                visualLayerRadius);

            Color originalColor =
                _originalRendererColors != null &&
                i < _originalRendererColors.Length
                    ? _originalRendererColors[i]
                    : Color.white;

            Color color = new Color(
                originalColor.r * _runtimeTint.r,
                originalColor.g * _runtimeTint.g,
                originalColor.b * _runtimeTint.b,
                originalColor.a * alpha);

            renderer.color = color;

            if (shouldLogGeometry && i == 0)
            {
                LogWaveVisualGeometry(
                    "VISUAL_FRAME",
                    safeProgress01,
                    safeProgress01,
                    safeCurrentRadius,
                    visualLayerRadius,
                    i,
                    renderer);
            }
        }
    }

    private bool ShouldLogWaveVisualGeometry(float progress01)
    {
        if (!IsWaveVisualGeometryDebugLogEnabled())
            return false;

        int bucket =
            Mathf.FloorToInt(
                Mathf.Clamp01(progress01) /
                Mathf.Max(0.01f, WaveVisualGeometryLogStep));

        if (bucket == _lastVisualGeometryLogBucket)
            return false;

        _lastVisualGeometryLogBucket = bucket;
        return true;
    }

    private void LogWaveVisualGeometry(
        string phase,
        float progress01,
        float curvedProgress,
        float combatCurrentRadius,
        float visualLayerRadius,
        int rendererIndex,
        SpriteRenderer renderer)
    {
        if (!IsWaveVisualGeometryDebugLogEnabled())
            return;

        bool previousDebugEnabled = _debugEnabled;
        bool previousDebugStop = _debugStop;

        _debugEnabled = true;
        _debugStop = false;

        float actualRendererRadius = 0f;
        Vector3 rendererScale = Vector3.zero;
        string spriteName = string.Empty;

        if (renderer != null)
        {
            actualRendererRadius =
                renderer.bounds.size.x * 0.5f;

            rendererScale =
                renderer.transform.lossyScale;

            spriteName =
                renderer.sprite != null
                    ? renderer.sprite.name
                    : string.Empty;
        }

        LogCustom(
            "[WaveVisualGeometry] " +
            phase +
            " | WaveId=" + WaveId +
            " | Progress01=" + progress01.ToString("F3") +
            " | CurvedProgress=" + curvedProgress.ToString("F3") +
            " | CombatCurrentRadius=" + combatCurrentRadius.ToString("F2") +
            " | VisualLayerRadius=" + visualLayerRadius.ToString("F2") +
            " | ActualRendererRadius=" + actualRendererRadius.ToString("F2") +
            " | RendererIndex=" + rendererIndex +
            " | RendererScale=" + rendererScale +
            " | Sprite=" + spriteName +
            " | ViewPosition=" + transform.position);

        _debugEnabled = previousDebugEnabled;
        _debugStop = previousDebugStop;
    }

    private bool IsWaveVisualGeometryDebugLogEnabled()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.DebugLogConfig == null)
        {
            return false;
        }

        return Bootstrapper.Instance.DebugLogConfig.WaveVisualGeometryLogs;
    }

    private static void ApplyRendererRadius(
        SpriteRenderer renderer,
        float radius)
    {
        if (renderer == null)
            return;

        if (renderer.sprite == null)
            return;

        float spriteLocalDiameter =
            Mathf.Max(
                0.01f,
                renderer.sprite.bounds.size.x);

        float parentWorldScale =
            1f;

        if (renderer.transform.parent != null)
        {
            parentWorldScale =
                Mathf.Abs(
                    renderer.transform.parent.lossyScale.x);
        }

        parentWorldScale =
            Mathf.Max(
                0.0001f,
                parentWorldScale);

        float desiredWorldDiameter =
            Mathf.Max(
                0.01f,
                radius * 2f);

        float localScale =
            desiredWorldDiameter /
            (spriteLocalDiameter * parentWorldScale);

        renderer.transform.localScale =
            new Vector3(
                localScale,
                localScale,
                1f);
    }

    private void ResolveCombatService()
    {
        if (_combatService != null)
            return;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return;

        bootstrapper.ServiceRegistry.TryGet(out _combatService);
    }
}