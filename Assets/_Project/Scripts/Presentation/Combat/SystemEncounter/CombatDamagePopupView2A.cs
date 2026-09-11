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
        _text.enableWordWrapping = false;
        _text.fontStyle = FontStyles.Bold;

        ApplyRenderOrder();
        ApplyVisual(0f);

        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (_text == null)
            return;

        _elapsedSeconds += Time.deltaTime;

        float progress01 =
            Mathf.Clamp01(_elapsedSeconds / Mathf.Max(0.01f, _lifetimeSeconds));

        ApplyVisual(progress01);

        if (progress01 >= 1f)
            Destroy(gameObject);
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