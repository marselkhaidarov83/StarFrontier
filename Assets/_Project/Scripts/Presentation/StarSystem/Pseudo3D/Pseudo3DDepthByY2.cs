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
        InitializeIfNeeded();
        ApplyDepth();
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

        int sortingOrder = baseSortingOrder + Mathf.RoundToInt(depth01 * depthSortingRange);

        if (spriteRenderers != null)
        {
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
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
}
