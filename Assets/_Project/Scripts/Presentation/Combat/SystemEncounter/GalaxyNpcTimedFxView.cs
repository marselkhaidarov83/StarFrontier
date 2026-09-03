using UnityEngine;

public sealed class GalaxyNpcTimedFxView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.35f;
    [SerializeField] private Color defaultTint = Color.white;

    [Header("Scale Animation")]
    [SerializeField] private bool animateScale = false;
    [SerializeField, Min(0f)] private float startScale = 1f;
    [SerializeField, Min(0f)] private float endScale = 1f;
    [SerializeField] private AnimationCurve scaleCurve =
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
        transform.position = position;

        ResolveSpriteRenderer();
        ApplyTint(tint);
        ApplyScale(0f);

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

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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