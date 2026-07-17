using System.Collections.Generic;
using UnityEngine;

public sealed class TravelLineView2A : CustomMonoBehaviour
{
    private const int DotTextureSize = 64;
    private const float DotPixelsPerUnit = 100f;

    [Header("Legacy Line")]
    [SerializeField] private SpriteRenderer legacyLineSpriteRenderer;

    [Header("Dots")]
    [SerializeField] private int smallDotsBetweenTickDots = 4;
    [SerializeField] private float bigDotDiameter = 28f;
    [SerializeField] private float smallDotDiameter = 10f;
    [SerializeField] private Color bigDotColor = new Color(0.55f, 0.9f, 1f, 0.95f);
    [SerializeField] private Color smallDotColor = new Color(0.55f, 0.9f, 1f, 0.45f);

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "SystemShipFX";
    [SerializeField] private int bigDotSortingOrder = 715;
    [SerializeField] private int smallDotSortingOrder = 710;

    [Header("Limits")]
    [SerializeField] private float minDistanceToShow = 3f;
    [SerializeField] private int maxBigDots = 160;
    [SerializeField] private int maxSmallDots = 900;

    [Header("Fallback")]
    [SerializeField] private float fallbackShipSpeedUnitsPerSecond = 100f;
    [SerializeField] private float fallbackSecondsPerTick = 1f;

    private readonly List<SpriteRenderer> _bigDotPool = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _smallDotPool = new List<SpriteRenderer>();

    private Sprite _dotSprite;
    private float _alphaMultiplier = 1f;

    public int LastEstimatedTickCount { get; private set; }

    private void Awake()
    {
        if (legacyLineSpriteRenderer == null)
            legacyLineSpriteRenderer = GetComponent<SpriteRenderer>();

        if (legacyLineSpriteRenderer != null)
            legacyLineSpriteRenderer.enabled = false;

        _dotSprite = CreateDotSprite();
        Hide();
    }

    private void Reset()
    {
        legacyLineSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Show(Vector3 from, Vector3 to)
    {
        Show(
            from,
            to,
            fallbackShipSpeedUnitsPerSecond,
            fallbackSecondsPerTick
        );
    }

    public void Show(
        Vector3 from,
        Vector3 to,
        float shipSpeedUnitsPerSecond,
        float secondsPerTick
    )
    {
        gameObject.SetActive(true);

        UpdateRoute(
            from,
            to,
            shipSpeedUnitsPerSecond,
            secondsPerTick
        );
    }

    public void UpdateLine(Vector3 from, Vector3 to)
    {
        UpdateRoute(
            from,
            to,
            fallbackShipSpeedUnitsPerSecond,
            fallbackSecondsPerTick
        );
    }

    public void UpdateRoute(
        Vector3 from,
        Vector3 to,
        float shipSpeedUnitsPerSecond,
        float secondsPerTick
    )
    {
        if (_dotSprite == null)
            _dotSprite = CreateDotSprite();

        float distance = Vector3.Distance(from, to);

        if (distance <= minDistanceToShow)
        {
            Hide();
            return;
        }

        float safeSpeed = Mathf.Max(0.01f, shipSpeedUnitsPerSecond);
        float safeSecondsPerTick = Mathf.Max(0.01f, secondsPerTick);
        float distancePerTick = safeSpeed * safeSecondsPerTick;

        LastEstimatedTickCount = Mathf.CeilToInt(distance / distancePerTick);
        LastEstimatedTickCount = Mathf.Max(1, LastEstimatedTickCount);
        LastEstimatedTickCount = Mathf.Min(LastEstimatedTickCount, maxBigDots);

        HideAllDots();

        int bigDotIndex = 0;
        int smallDotIndex = 0;

        Vector3 previousAnchor = from;

        for (int tickIndex = 1; tickIndex <= LastEstimatedTickCount; tickIndex++)
        {
            float distanceAtTick = Mathf.Min(
                tickIndex * distancePerTick,
                distance
            );

            float route01 = Mathf.Clamp01(distanceAtTick / distance);
            Vector3 tickPosition = EvaluateRoutePoint(from, to, route01);

            DrawSmallDotsBetween(
                previousAnchor,
                tickPosition,
                ref smallDotIndex
            );

            DrawDot(
                GetOrCreateDot(_bigDotPool, "BigTickDot"),
                tickPosition,
                bigDotDiameter,
                bigDotColor,
                bigDotSortingOrder
            );

            bigDotIndex++;
            previousAnchor = tickPosition;

            if (route01 >= 1f)
                break;
        }

        DisableUnusedDots(_bigDotPool, bigDotIndex);
        DisableUnusedDots(_smallDotPool, smallDotIndex);
    }

    private void DrawSmallDotsBetween(
        Vector3 from,
        Vector3 to,
        ref int smallDotIndex
    )
    {
        if (smallDotsBetweenTickDots <= 0)
            return;

        if (smallDotIndex >= maxSmallDots)
            return;

        float segmentDistance = Vector3.Distance(from, to);

        if (segmentDistance <= minDistanceToShow)
            return;

        for (int i = 1; i <= smallDotsBetweenTickDots; i++)
        {
            if (smallDotIndex >= maxSmallDots)
                return;

            float t = i / (smallDotsBetweenTickDots + 1f);
            Vector3 position = Vector3.Lerp(from, to, t);

            DrawDot(
                GetOrCreateDot(_smallDotPool, "SmallRouteDot"),
                position,
                smallDotDiameter,
                smallDotColor,
                smallDotSortingOrder
            );

            smallDotIndex++;
        }
    }

    private Vector3 EvaluateRoutePoint(Vector3 from, Vector3 to, float route01)
    {
        // Сейчас маршрут прямой.
        // В будущем именно здесь можно заменить прямую на кривую,
        // например Bezier-кривую или путь из нескольких сегментов.
        return Vector3.Lerp(from, to, route01);
    }

    private SpriteRenderer GetOrCreateDot(
        List<SpriteRenderer> pool,
        string objectName
    )
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf)
                return pool[i];
        }

        GameObject dotObject = new GameObject(objectName);
        dotObject.transform.SetParent(transform, true);

        SpriteRenderer spriteRenderer = dotObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _dotSprite;

        pool.Add(spriteRenderer);

        return spriteRenderer;
    }

    private void DrawDot(
        SpriteRenderer spriteRenderer,
        Vector3 position,
        float diameter,
        Color color,
        int sortingOrder
    )
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.gameObject.SetActive(true);
        spriteRenderer.transform.position = position;
        spriteRenderer.transform.rotation = Quaternion.identity;

        float spriteWorldDiameter = DotTextureSize / DotPixelsPerUnit;
        float scale = diameter / spriteWorldDiameter;
        spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);

        Color effectiveColor = color;
        effectiveColor.a *= _alphaMultiplier;

        spriteRenderer.color = effectiveColor;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void HideAllDots()
    {
        DisableUnusedDots(_bigDotPool, 0);
        DisableUnusedDots(_smallDotPool, 0);
    }

    private void DisableUnusedDots(
        List<SpriteRenderer> pool,
        int firstUnusedIndex
    )
    {
        for (int i = firstUnusedIndex; i < pool.Count; i++)
        {
            if (pool[i] != null)
                pool[i].gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        HideAllDots();
        gameObject.SetActive(false);
    }

    public void SetAlpha(float alpha)
    {
        _alphaMultiplier = Mathf.Clamp01(alpha);

        ApplyAlphaToPool(_bigDotPool, bigDotColor);
        ApplyAlphaToPool(_smallDotPool, smallDotColor);
    }

    private void ApplyAlphaToPool(
        List<SpriteRenderer> pool,
        Color baseColor
    )
    {
        foreach (SpriteRenderer spriteRenderer in pool)
        {
            if (spriteRenderer == null)
                continue;

            Color color = baseColor;
            color.a *= _alphaMultiplier;
            spriteRenderer.color = color;
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

        texture.name = "procedural_route_dot";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[DotTextureSize * DotTextureSize];

        float center = (DotTextureSize - 1) * 0.5f;
        float radius = DotTextureSize * 0.5f;
        float softEdge = 0.16f;

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

    public void ShowAnchored(
    Vector3 routeStart,
    Vector3 currentPosition,
    Vector3 destinationPosition,
    float shipSpeedUnitsPerSecond,
    float secondsPerTick
)
    {
        gameObject.SetActive(true);

        UpdateAnchoredRoute(
            routeStart,
            currentPosition,
            destinationPosition,
            shipSpeedUnitsPerSecond,
            secondsPerTick
        );
    }

    private void UpdateAnchoredRoute(
    Vector3 routeStart,
    Vector3 currentPosition,
    Vector3 destinationPosition,
    float shipSpeedUnitsPerSecond,
    float secondsPerTick
)
    {
        if (_dotSprite == null)
            _dotSprite = CreateDotSprite();

        Vector3 routeVector = destinationPosition - routeStart;
        float totalDistance = routeVector.magnitude;

        if (totalDistance <= minDistanceToShow)
        {
            Hide();
            return;
        }

        Vector3 routeDirection = routeVector / totalDistance;
        float passedDistance = Vector3.Dot(
            currentPosition - routeStart,
            routeDirection
        );

        passedDistance = Mathf.Clamp(
            passedDistance,
            0f,
            totalDistance
        );
        if (totalDistance <= minDistanceToShow)
        {
            Hide();
            return;
        }

        float safeSpeed = Mathf.Max(0.01f, shipSpeedUnitsPerSecond);
        float safeSecondsPerTick = Mathf.Max(0.01f, secondsPerTick);
        float distancePerTick = safeSpeed * safeSecondsPerTick;

        LastEstimatedTickCount = Mathf.CeilToInt(totalDistance / distancePerTick);
        LastEstimatedTickCount = Mathf.Max(1, LastEstimatedTickCount);
        LastEstimatedTickCount = Mathf.Min(LastEstimatedTickCount, maxBigDots);

        HideAllDots();

        int bigDotIndex = 0;
        int smallDotIndex = 0;

        Vector3 previousVisibleAnchor = currentPosition;

        for (int tickIndex = 1; tickIndex <= LastEstimatedTickCount; tickIndex++)
        {
            float distanceAtTick = Mathf.Min(
                tickIndex * distancePerTick,
                totalDistance
            );

            // Если корабль уже прошёл точку этого тика — не рисуем её.
            if (distanceAtTick <= passedDistance)
                continue;

            float route01 = Mathf.Clamp01(distanceAtTick / totalDistance);
            Vector3 tickPosition = EvaluateRoutePoint(
                routeStart,
                destinationPosition,
                route01
            );

            DrawSmallDotsBetween(
                previousVisibleAnchor,
                tickPosition,
                ref smallDotIndex
            );

            DrawDot(
                GetOrCreateDot(_bigDotPool, "BigTickDot"),
                tickPosition,
                bigDotDiameter,
                bigDotColor,
                bigDotSortingOrder
            );

            bigDotIndex++;
            previousVisibleAnchor = tickPosition;

            if (distanceAtTick >= totalDistance)
                break;
        }

        DisableUnusedDots(_bigDotPool, bigDotIndex);
        DisableUnusedDots(_smallDotPool, smallDotIndex);
    }
}