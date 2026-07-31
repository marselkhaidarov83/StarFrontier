using System.Collections;
using TMPro;
using UnityEngine;

public sealed class GalaxyMapViewFitter : MonoBehaviour
{
    [Header("Камера")]
    [SerializeField] private Camera mapCamera;

    [Header("Содержимое карты")]
    [SerializeField] private Transform[] contentRoots;

    [Header("Интерфейс")]
    [SerializeField] private RectTransform topHudRoot;
    [SerializeField] private RectTransform bottomHudRoot;

    [Header("Фон")]
    [SerializeField] private SpriteRenderer mapBackground;

    [Header("Запасные границы карты")]
    [SerializeField] private Vector2 fallbackContentCenter =
        new Vector2(0f, -0.25f);

    [SerializeField] private Vector2 fallbackContentSize =
        new Vector2(10.8f, 15.7f);

    [Header("Отступ вокруг содержимого")]
    [SerializeField] private float contentPadding = 0.3f;

    [Header("Безопасная область")]
    [SerializeField] private bool useSafeArea = true;

    [Header("Автоматическое обновление")]
    [SerializeField] private bool fitOnEnable = true;
    [SerializeField] private bool recalculateOnResolutionChange = true;

    private Coroutine _fitCoroutine;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    public float FitOrthographicSize { get; private set; }
    public Bounds ContentBounds { get; private set; }

    private void Awake()
    {
        if (mapCamera == null)
            mapCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        if (fitOnEnable)
            RequestFit();
    }

    private void LateUpdate()
    {
        if (!recalculateOnResolutionChange)
            return;

        if (_lastScreenWidth == Screen.width &&
            _lastScreenHeight == Screen.height)
        {
            return;
        }

        RequestFit();
    }

    public void RequestFit()
    {
        if (!isActiveAndEnabled)
            return;

        if (_fitCoroutine != null)
            StopCoroutine(_fitCoroutine);

        _fitCoroutine = StartCoroutine(FitAtEndOfFrame());
    }

    private IEnumerator FitAtEndOfFrame()
    {
        // Ждём, пока создадутся системы, сектора, маршруты
        // и TextMeshPro построит геометрию текстов.
        yield return new WaitForEndOfFrame();

        FitNow();

        _fitCoroutine = null;
    }

    public float FitNow()
    {
        if (mapCamera == null)
        {
            Debug.LogError(
                "[GalaxyMapViewFitter] Камера не назначена."
            );

            return 0f;
        }

        if (!mapCamera.orthographic)
        {
            Debug.LogError(
                "[GalaxyMapViewFitter] Камера должна быть ортографической."
            );

            return 0f;
        }

        ForceTextMeshUpdate();

        ContentBounds = CalculateContentBounds();

        Rect usableScreenRect = CalculateUsableScreenRect();

        float usableWidthFraction =
            usableScreenRect.width / Screen.width;

        float usableHeightFraction =
            usableScreenRect.height / Screen.height;

        usableWidthFraction = Mathf.Clamp01(usableWidthFraction);
        usableHeightFraction = Mathf.Clamp01(usableHeightFraction);

        if (usableWidthFraction <= 0f ||
            usableHeightFraction <= 0f)
        {
            Debug.LogError(
                "[GalaxyMapViewFitter] Некорректная рабочая область экрана."
            );

            return 0f;
        }

        float requiredWidth =
            ContentBounds.size.x + contentPadding * 2f;

        float requiredHeight =
            ContentBounds.size.y + contentPadding * 2f;

        float sizeRequiredByWidth =
            requiredWidth /
            (2f * mapCamera.aspect * usableWidthFraction);

        float sizeRequiredByHeight =
            requiredHeight /
            (2f * usableHeightFraction);

        FitOrthographicSize = Mathf.Max(
            sizeRequiredByWidth,
            sizeRequiredByHeight
        );

        mapCamera.orthographicSize = FitOrthographicSize;

        PositionCameraForUsableArea(usableScreenRect);
        FitBackgroundToCamera();

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        return FitOrthographicSize;
    }

    private Bounds CalculateContentBounds()
    {
        Bounds result = new Bounds(
            new Vector3(
                fallbackContentCenter.x,
                fallbackContentCenter.y,
                0f
            ),
            new Vector3(
                fallbackContentSize.x,
                fallbackContentSize.y,
                0.1f
            )
        );

        if (contentRoots == null)
            return result;

        foreach (Transform contentRoot in contentRoots)
        {
            if (contentRoot == null)
                continue;

            Renderer[] renderers =
                contentRoot.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (mapBackground != null &&
                    renderer == mapBackground)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;

                if (rendererBounds.size.sqrMagnitude <= 0.0001f)
                    continue;

                result.Encapsulate(rendererBounds);
            }
        }

        return result;
    }

    private void ForceTextMeshUpdate()
    {
        if (contentRoots == null)
            return;

        foreach (Transform contentRoot in contentRoots)
        {
            if (contentRoot == null)
                continue;

            TMP_Text[] texts =
                contentRoot.GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text == null)
                    continue;

                text.ForceMeshUpdate(
                    ignoreActiveState: true,
                    forceTextReparsing: true
                );
            }
        }
    }

    private Rect CalculateUsableScreenRect()
    {
        Rect result = useSafeArea
            ? Screen.safeArea
            : new Rect(0f, 0f, Screen.width, Screen.height);

        if (topHudRoot != null)
        {
            GetScreenVerticalBounds(
                topHudRoot,
                out float topHudBottom,
                out _
            );

            result.yMax = Mathf.Min(
                result.yMax,
                topHudBottom
            );
        }

        if (bottomHudRoot != null)
        {
            GetScreenVerticalBounds(
                bottomHudRoot,
                out _,
                out float bottomHudTop
            );

            result.yMin = Mathf.Max(
                result.yMin,
                bottomHudTop
            );
        }

        if (result.width <= 1f || result.height <= 1f)
        {
            Debug.LogWarning(
                "[GalaxyMapViewFitter] Не удалось определить рабочую область. " +
                "Используется весь экран."
            );

            return new Rect(
                0f,
                0f,
                Screen.width,
                Screen.height
            );
        }

        return result;
    }

    private void GetScreenVerticalBounds(
        RectTransform rectTransform,
        out float minY,
        out float maxY)
    {
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);

        Camera canvasCamera = GetCanvasCamera(rectTransform);

        minY = float.MaxValue;
        maxY = float.MinValue;

        foreach (Vector3 corner in worldCorners)
        {
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    canvasCamera,
                    corner
                );

            minY = Mathf.Min(minY, screenPoint.y);
            maxY = Mathf.Max(maxY, screenPoint.y);
        }
    }

    private Camera GetCanvasCamera(RectTransform rectTransform)
    {
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();

        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private void PositionCameraForUsableArea(Rect usableScreenRect)
    {
        float normalizedCenterX =
            usableScreenRect.center.x / Screen.width;

        float normalizedCenterY =
            usableScreenRect.center.y / Screen.height;

        float normalizedOffsetX =
            normalizedCenterX - 0.5f;

        float normalizedOffsetY =
            normalizedCenterY - 0.5f;

        float worldOffsetX =
            normalizedOffsetX *
            FitOrthographicSize *
            2f *
            mapCamera.aspect;

        float worldOffsetY =
            normalizedOffsetY *
            FitOrthographicSize *
            2f;

        Vector3 cameraPosition = mapCamera.transform.position;

        cameraPosition.x =
            ContentBounds.center.x - worldOffsetX;

        cameraPosition.y =
            ContentBounds.center.y - worldOffsetY;

        mapCamera.transform.position = cameraPosition;
    }

    private void FitBackgroundToCamera()
    {
        if (mapBackground == null ||
            mapBackground.sprite == null)
        {
            return;
        }

        float visibleHeight =
            mapCamera.orthographicSize * 2f;

        float visibleWidth =
            visibleHeight * mapCamera.aspect;

        Vector2 spriteSize =
            mapBackground.sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float scaleByWidth =
            visibleWidth / spriteSize.x;

        float scaleByHeight =
            visibleHeight / spriteSize.y;

        // Аналог режима Cover:
        // фон заполняет экран без пустых полос,
        // но может немного обрезаться.
        float requiredScale = Mathf.Max(
            scaleByWidth,
            scaleByHeight
        );

        Vector3 localScale =
            mapBackground.transform.localScale;

        localScale.x = requiredScale;
        localScale.y = requiredScale;

        mapBackground.transform.localScale = localScale;

        Vector3 backgroundPosition =
            mapBackground.transform.position;

        backgroundPosition.x =
            mapCamera.transform.position.x;

        backgroundPosition.y =
            mapCamera.transform.position.y;

        mapBackground.transform.position = backgroundPosition;
    }
}