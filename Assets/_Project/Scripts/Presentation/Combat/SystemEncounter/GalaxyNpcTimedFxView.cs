using UnityEngine;

public sealed class GalaxyNpcTimedFxView : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.35f;
    [SerializeField] private Color defaultTint = Color.white;

    [Header("Render Order")]
    [SerializeField] private string sortingLayerName = "SystemForegroundFX";
    [SerializeField] private int sortingOrder = 1200;
    [SerializeField] private bool forceWorldZ = true;
    [SerializeField] private float worldZ = -9f;

    [Header("Scale Animation")]
    [SerializeField] private bool animateScale = false;
    [SerializeField, Min(0f)] private float startScale = 1f;
    [SerializeField, Min(0f)] private float endScale = 1f;
    [SerializeField]
    private AnimationCurve scaleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float _remainingSeconds;
    private float _activeLifetimeSeconds;
    private GalaxyNpcCombatVisualController _owner;

    public bool IsActive { get; private set; }

    public void Init(
        GalaxyNpcCombatVisualController owner,
        Vector3 position,
        float lifetimeOverrideSeconds)
    {
        Init(owner, position, lifetimeOverrideSeconds, defaultTint);
    }

    public void Init(
        GalaxyNpcCombatVisualController owner,
        Vector3 position,
        float lifetimeOverrideSeconds,
        Color tint)
    {
        _owner = owner;

        _activeLifetimeSeconds = lifetimeOverrideSeconds > 0f
            ? lifetimeOverrideSeconds
            : lifetimeSeconds;

        _remainingSeconds = _activeLifetimeSeconds;

        IsActive = true;

        ResolveSpriteRenderer();
        SetPosition(position);
        ApplyTint(tint);
        ApplyScale(0f);
        ApplyRenderOrder();

        gameObject.SetActive(true);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public void Tick(float deltaTime)
    {
        if (!IsActive)
            return;

        _remainingSeconds -= deltaTime;

        float progress01 = 1f;

        if (_activeLifetimeSeconds > 0.01f)
        {
            progress01 = Mathf.Clamp01(
                1f - _remainingSeconds / _activeLifetimeSeconds);
        }

        ApplyScale(progress01);

        if (_remainingSeconds > 0f)
            return;

        Complete();
    }

    public void SetPosition(Vector3 position)
    {
        if (forceWorldZ)
            position.z = worldZ;

        transform.position = position;
        ApplyRenderOrder();
    }

    public void RestartLifetime(float lifetimeOverrideSeconds)
    {
        _activeLifetimeSeconds = lifetimeOverrideSeconds > 0f
            ? lifetimeOverrideSeconds
            : lifetimeSeconds;

        _remainingSeconds = _activeLifetimeSeconds;
    }

    public void Complete()
    {
        if (!IsActive)
            return;

        IsActive = false;
        gameObject.SetActive(false);

        if (_owner != null)
            _owner.ReturnFxToPool(this);
    }

    private void ResolveSpriteRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void ApplyRenderOrder()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
            ? "SystemForegroundFX"
            : sortingLayerName;

        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void ApplyTint(Color tint)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = tint;
    }

    private void ApplyScale(float progress01)
    {
        if (!animateScale)
            return;

        float curveValue = scaleCurve != null
            ? scaleCurve.Evaluate(Mathf.Clamp01(progress01))
            : Mathf.Clamp01(progress01);

        float scale = Mathf.Lerp(
            startScale,
            endScale,
            curveValue);

        transform.localScale = Vector3.one * scale;
    }
}