using System.Collections.Generic;
using UnityEngine;

public sealed class PlanetOrbitLineView2 : MonoBehaviour
{
    private const int DotTextureSize = 64;
    private const float DotPixelsPerUnit = 100f;

    [Header("Legacy PNG Orbit")]
    [SerializeField] private SpriteRenderer orbitLineImage;
    [SerializeField] private Sprite normalPlanetOrbit;
    [SerializeField] private Sprite selectedPlanetOrbit;

    [Header("Procedural Dots")]
    [SerializeField] private float dotDiameter = 8f;
    [SerializeField] private Color dotColor = new Color(0.55f, 0.9f, 1f, 0.32f);

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "SystemObjects";
    [SerializeField] private int sortingOrder = -50;

    [Header("Safety")]
    [SerializeField] private int minDotCount = 24;
    [SerializeField] private int maxDotCount = 360;

    private readonly List<SpriteRenderer> _dotPool = new List<SpriteRenderer>();

    private Sprite _dotSprite;

    private void Awake()
    {
        if (orbitLineImage == null)
            orbitLineImage = GetComponent<SpriteRenderer>();

        if (orbitLineImage != null)
            orbitLineImage.enabled = false;

        _dotSprite = CreateDotSprite();
    }

    private void Reset()
    {
        orbitLineImage = GetComponent<SpriteRenderer>();
    }

    public void Initialize(PlanetOrbitConfig orbitConfig)
    {
        if (orbitConfig == null)
        {
            HideAllDots();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (orbitLineImage != null)
            orbitLineImage.enabled = false;

        if (_dotSprite == null)
            _dotSprite = CreateDotSprite();

        transform.position = orbitConfig.OrbitCenterOffset;

        BuildOrbitDots(
            orbitConfig.OrbitRadius,
            orbitConfig.OrbitDotCount
        );
    }

    private void BuildOrbitDots(float orbitRadius, int requestedDotCount)
    {
        HideAllDots();

        if (orbitRadius <= 0f)
            return;

        int dotCount = Mathf.Clamp(
            requestedDotCount,
            minDotCount,
            maxDotCount
        );

        for (int i = 0; i < dotCount; i++)
        {
            float angle01 = i / (float)dotCount;
            float angleRad = angle01 * Mathf.PI * 2f;

            Vector3 localPosition = new Vector3(
                Mathf.Cos(angleRad) * orbitRadius,
                Mathf.Sin(angleRad) * orbitRadius,
                0f
            );

            SpriteRenderer dot = GetOrCreateDot(i);
            DrawDot(dot, localPosition);
        }

        DisableUnusedDots(dotCount);
    }

    private SpriteRenderer GetOrCreateDot(int index)
    {
        if (index < _dotPool.Count && _dotPool[index] != null)
            return _dotPool[index];

        GameObject dotObject = new GameObject("OrbitDot");
        dotObject.transform.SetParent(transform, false);

        SpriteRenderer spriteRenderer = dotObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _dotSprite;

        _dotPool.Add(spriteRenderer);

        return spriteRenderer;
    }

    private void DrawDot(SpriteRenderer spriteRenderer, Vector3 localPosition)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.gameObject.SetActive(true);
        spriteRenderer.transform.localPosition = localPosition;
        spriteRenderer.transform.localRotation = Quaternion.identity;

        float spriteWorldDiameter = DotTextureSize / DotPixelsPerUnit;
        float scale = dotDiameter / spriteWorldDiameter;
        spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);

        spriteRenderer.sprite = _dotSprite;
        spriteRenderer.color = dotColor;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void HideAllDots()
    {
        DisableUnusedDots(0);
    }

    private void DisableUnusedDots(int firstUnusedIndex)
    {
        for (int i = firstUnusedIndex; i < _dotPool.Count; i++)
        {
            if (_dotPool[i] != null)
                _dotPool[i].gameObject.SetActive(false);
        }
    }

    private Sprite CreateDotSprite()
    {
        Texture2D texture = new Texture2D(
            DotTextureSize,
            DotTextureSize,
            TextureFormat.RGBA32,
            false
        );

        texture.name = "procedural_orbit_dot";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[DotTextureSize * DotTextureSize];

        float center = (DotTextureSize - 1) * 0.5f;
        float radius = DotTextureSize * 0.5f;
        float softEdge = 0.18f;

        for (int y = 0; y < DotTextureSize; y++)
        {
            for (int x = 0; x < DotTextureSize; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance01 = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01((1f - distance01) / softEdge);

                if (distance01 <= 1f - softEdge)
                    alpha = 1f;

                pixels[y * DotTextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, DotTextureSize, DotTextureSize),
            new Vector2(0.5f, 0.5f),
            DotPixelsPerUnit
        );
    }
}