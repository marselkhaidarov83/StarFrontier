using UnityEngine;

/// <summary>
/// Визуальное представление корабля игрока.
///
/// Отвечает только за спрайты, Order in Layer,
/// тень, свечение двигателя и замену временной графики
/// на финальную.
///
/// Не читает ввод, не двигает корабль,
/// не обращается к ShipMovementService.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerShipView2A : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Transform visualRoot;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private SpriteRenderer engineGlowRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private Sprite engineGlowSprite;

    [Header("Sorting")]
    [SerializeField] private bool configureSortingOnAwake = true;
    [SerializeField] private int shadowOrderInLayer = 0;
    [SerializeField] private int engineGlowOrderInLayer = 5;
    [SerializeField] private int bodyOrderInLayer = 10;

    [Header("Scale")]
    [SerializeField]
    [Min(0.01f)]
    private float visualWorldScale = 1f;

    [Header("Visibility")]
    [SerializeField] private bool hideEmptyRenderers = true;

    public Transform VisualRoot => visualRoot != null
        ? visualRoot
        : transform;

    public SpriteRenderer BodyRenderer => bodyRenderer;
    public SpriteRenderer ShadowRenderer => shadowRenderer;
    public SpriteRenderer EngineGlowRenderer => engineGlowRenderer;

    private void Awake()
    {
        ApplyInspectorSprites();
        SetVisualScale(visualWorldScale);

        if (configureSortingOnAwake)
            ConfigureSorting();

        UpdateRendererVisibility();
    }

    private void OnValidate()
    {
        if (visualRoot == null)
            visualRoot = transform;

        ApplyInspectorSprites();
        SetVisualScale(visualWorldScale);
        ConfigureSorting();
        UpdateRendererVisibility();
    }

    public bool HasRequiredReferences()
    {
        return bodyRenderer != null
            && shadowRenderer != null
            && engineGlowRenderer != null;
    }

    public void ApplyInspectorSprites()
    {
        if (bodyRenderer != null)
            bodyRenderer.sprite = bodySprite;

        if (shadowRenderer != null)
            shadowRenderer.sprite = shadowSprite;

        if (engineGlowRenderer != null)
            engineGlowRenderer.sprite = engineGlowSprite;

        UpdateRendererVisibility();
    }

    public void SetFinalShipSprite(Sprite finalShipSprite)
    {
        if (finalShipSprite == null)
            return;

        SetBodySprite(finalShipSprite);
    }

    public void SetBodySprite(Sprite sprite)
    {
        bodySprite = sprite;

        if (bodyRenderer == null)
            return;

        bodyRenderer.sprite = sprite;
        UpdateRendererVisibility();
    }

    public void SetShadowSprite(Sprite sprite)
    {
        shadowSprite = sprite;

        if (shadowRenderer == null)
            return;

        shadowRenderer.sprite = sprite;
        UpdateRendererVisibility();
    }

    public void SetEngineGlowSprite(Sprite sprite)
    {
        engineGlowSprite = sprite;

        if (engineGlowRenderer == null)
            return;

        engineGlowRenderer.sprite = sprite;
        UpdateRendererVisibility();
    }

    public void SetEngineGlowVisible(bool isVisible)
    {
        if (engineGlowRenderer == null)
            return;

        engineGlowRenderer.enabled = isVisible
            && (!hideEmptyRenderers
                || engineGlowRenderer.sprite != null);
    }

    public void SetVisualScale(float worldScale)
    {
        visualWorldScale =
            Mathf.Max(0.01f, worldScale);

        VisualRoot.localScale =
            new Vector3(
                visualWorldScale,
                visualWorldScale,
                1f);
    }

    public void ConfigureSorting()
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.sortingOrder =
                shadowOrderInLayer;
        }

        if (engineGlowRenderer != null)
        {
            engineGlowRenderer.sortingOrder =
                engineGlowOrderInLayer;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.sortingOrder =
                bodyOrderInLayer;
        }
    }

    private void UpdateRendererVisibility()
    {
        UpdateRendererVisibility(bodyRenderer);
        UpdateRendererVisibility(shadowRenderer);
        UpdateRendererVisibility(engineGlowRenderer);
    }

    private void UpdateRendererVisibility(
        SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
            return;

        if (!hideEmptyRenderers)
        {
            spriteRenderer.enabled = true;
            return;
        }

        spriteRenderer.enabled =
            spriteRenderer.sprite != null;
    }
}