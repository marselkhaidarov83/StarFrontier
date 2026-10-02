using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatDamagePopupView2A : CustomMonoBehaviour
{
    private TextMeshPro _text;
    private CombatDamagePopupVisualConfig2A _config;
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private float _elapsedSeconds;
    private float _lifetimeSeconds;

    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateDestroyedCount;
    private static double _aggregateTotalMs;
    private static double _aggregateApplyVisualMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxObjectName = string.Empty;

    public void Init(
        CombatDamagePopupVisualConfig2A config,
        int damage,
        Vector3 targetPosition,
        Vector3 damageSourcePosition,
        Vector3 popupDirection)
    {
        _config = config;
        _elapsedSeconds = 0f;
        _lifetimeSeconds = config != null ? config.LifetimeSeconds : 0.85f;

        ResolveText();

        Vector3 direction = ResolvePopupDirection(
            targetPosition,
            damageSourcePosition,
            popupDirection);

        _startPosition =
            targetPosition + direction * ResolveStartDistance();

        _endPosition =
            _startPosition + direction * ResolveTravelDistance();

        ApplyWorldZ(ref _startPosition);
        ApplyWorldZ(ref _endPosition);

        transform.position = _startPosition;

        _text.text = Mathf.Max(0, damage).ToString();
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.fontStyle = FontStyles.Bold;

        ApplyRenderOrder();
        ApplyVisual(0f);

        gameObject.SetActive(true);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double applyVisualMs = 0.0;

        bool textAvailable = false;
        bool destroyed = false;
        float progress01 = 0f;

        try
        {
            textAvailable =
                _text != null;

            if (!textAvailable)
                return;

            _elapsedSeconds += Time.deltaTime;

            progress01 =
                Mathf.Clamp01(_elapsedSeconds / Mathf.Max(0.01f, _lifetimeSeconds));

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplyVisual(progress01);

            applyVisualMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (progress01 >= 1f)
            {
                destroyed = true;
                Destroy(gameObject);
            }
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordUpdateAggregate(
                elapsedMs,
                applyVisualMs,
                destroyed,
                gameObject.name);

            VisualUpdatePerfLog.LogIfSlow(
                "CombatDamagePopupView2A.Update",
                startedAt,
                "Name=" + gameObject.name +
                " | TextAvailable=" + textAvailable +
                " | ElapsedSeconds=" + _elapsedSeconds.ToString("F3") +
                " | LifetimeSeconds=" + _lifetimeSeconds.ToString("F3") +
                " | Progress01=" + progress01.ToString("F3") +
                " | Destroyed=" + destroyed +
                " | ApplyVisualMs=" + applyVisualMs.ToString("F3"));
        }
    }


    private static void RecordUpdateAggregate(
        double elapsedMs,
        double applyVisualMs,
        bool destroyed,
        string objectName)
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
        _aggregateApplyVisualMs += applyVisualMs;

        if (destroyed)
            _aggregateDestroyedCount++;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxObjectName = objectName ?? string.Empty;
        }
    }

    private static void ResetUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateDestroyedCount = 0;
        _aggregateTotalMs = 0.0;
        _aggregateApplyVisualMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxObjectName = string.Empty;
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
            "CombatDamagePopupView2A.Update.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | DestroyedCount=" + _aggregateDestroyedCount +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxObject=" + _aggregateMaxObjectName +
            " | ApplyVisualMs=" + _aggregateApplyVisualMs.ToString("F3"));
    }

    private void ResolveText()
    {
        if (_text != null)
            return;

        _text = GetComponent<TextMeshPro>();

        if (_text == null)
            _text = gameObject.AddComponent<TextMeshPro>();
    }

    private void ApplyVisual(float progress01)
    {
        float moveProgress = progress01;

        if (_config != null && _config.MoveCurve != null)
            moveProgress = _config.MoveCurve.Evaluate(progress01);

        transform.position =
            Vector3.LerpUnclamped(_startPosition, _endPosition, moveProgress);

        float startSize = _config != null ? _config.StartFontSize : 4.2f;
        float endSize = _config != null ? _config.EndFontSize : 2.8f;

        Color startColor =
            _config != null ? _config.StartColor : new Color(1f, 0.86f, 0.28f, 1f);

        Color endColor =
            _config != null ? _config.EndColor : new Color(1f, 0.25f, 0.12f, 0f);

        _text.fontSize = Mathf.Lerp(startSize, endSize, progress01);
        _text.color = Color.Lerp(startColor, endColor, progress01);
    }

    private void ApplyRenderOrder()
    {
        Renderer renderer = _text.GetComponent<Renderer>();

        if (renderer == null)
            return;

        renderer.sortingLayerName =
            _config != null ? _config.SortingLayerName : "SystemForegroundFX";

        renderer.sortingOrder =
            _config != null ? _config.SortingOrder : 1350;
    }

    private void ApplyWorldZ(ref Vector3 position)
    {
        if (_config == null || !_config.ForceWorldZ)
            return;

        position.z = _config.WorldZ;
    }

    private float ResolveStartDistance()
    {
        return _config != null ? _config.StartDistanceFromShip : 0.55f;
    }

    private float ResolveTravelDistance()
    {
        return _config != null ? _config.TravelDistance : 1.15f;
    }

    private static Vector3 ResolvePopupDirection(
     Vector3 targetPosition,
     Vector3 damageSourcePosition,
     Vector3 popupDirection)
    {
        popupDirection.z = 0f;

        if (popupDirection.sqrMagnitude > 0.0001f)
            return popupDirection.normalized;

        Vector3 direction = targetPosition - damageSourcePosition;
        direction.z = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return direction.normalized;
    }
}