using UnityEngine;

public sealed class ShipPseudo3DFx2 : MonoBehaviour
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

    private Vector3 _lastPosition;
    private bool _initialized;

    private void Start()
    {
        Initialize();
        ApplyStaticSettings();
    }

    private void LateUpdate()
    {
        Initialize();
        UpdateShadow();
        UpdateGlow();
        UpdateTrail();
        _lastPosition = transform.position;
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

        float speed = (transform.position - _lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        engineTrail.emitting = speed >= minSpeedForTrail;
    }

    private void SetAlpha(SpriteRenderer spriteRenderer, float alpha)
    {
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }
}