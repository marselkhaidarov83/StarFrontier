using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Применяет Screen.safeArea к RectTransform HUD.
///
/// Компонент должен находиться на объекте,
/// который является прямым дочерним объектом
/// screen-space Canvas.
///
/// Adapter:
/// - не создаёт gameplay-сервисы;
/// - не хранит gameplay State;
/// - меняет только anchors/offsets RectTransform;
/// - повторно применяет Safe Area только при изменении
///   размера экрана или самой safe area.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SystemHudSafeAreaAdapter2A :
    MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution =
        new Vector2(1080f, 1920f);

    [Header("Application")]

    [SerializeField]
    private bool applyHorizontal = true;

    [SerializeField]
    private bool applyVertical = true;

    [SerializeField]
    private bool resetOffsetsAfterApply = true;

    [SerializeField]
    private bool fillTopUnsafeArea;

    [SerializeField]
    private bool fillBottomUnsafeArea;

    // SF 03 prototype only. The galaxy adapter retains its old full-screen
    // placement for every other profile until their layouts are reviewed.
    [SerializeField] private bool sf03Only;
    [SerializeField] private Sprite sf03TallTopArtwork;
    private Image _sf03OriginalArtwork;
    private Image _sf03TallImage;
    private Color _sf03OriginalColor;
    private bool _sf03ArtworkActive;

    private readonly RectTransform[] _insetCaps = new RectTransform[3];
    private RectTransform _leftCap;
    private RectTransform _rightCap;
    private static readonly Color CapBase = new Color(0.035f, 0.075f, 0.13f, 1f);
    private static readonly Color CapTrim = new Color(0.12f, 0.28f, 0.42f, 1f);

    [Header("Editor simulation")]

    [Tooltip(
        "Работает только в Unity Editor. " +
        "В Android build всегда используется Screen.safeArea.")]
    [SerializeField]
    private bool simulateSafeAreaInEditor;

    [Tooltip(
        "X = left, Y = bottom, Z = right, W = top. " +
        "Значения задаются в пикселях текущего Game View.")]
    [SerializeField]
    private Vector4 simulatedInsetsPixels =
        Vector4.zero;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logChanges;

    [SerializeField]
    private Rect lastAppliedSafeAreaPixels;

    [SerializeField]
    private Vector2 lastAppliedAnchorMin;

    [SerializeField]
    private Vector2 lastAppliedAnchorMax;

    [SerializeField]
    private int lastScreenWidth;

    [SerializeField]
    private int lastScreenHeight;

    private RectTransform _rectTransform;

    private Rect _cachedSafeArea;

    private int _cachedScreenWidth = -1;

    private int _cachedScreenHeight = -1;

    private bool _hasApplied;

    private bool _isApplying;

    private void Awake()
    {
        CacheRectTransform();
    }

    private void OnEnable()
    {
        CacheRectTransform();
        ApplySafeArea(true);
    }

    private void Start()
    {
        ApplySafeArea(true);
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double applyMs = 0.0;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ApplySafeArea(false);

            applyMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + name +
                " | HasApplied=" + _hasApplied +
                " | Screen=" + Screen.width + "x" + Screen.height +
                " | SafeArea=" + Screen.safeArea +
                " | ApplyMs=" + applyMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "SystemHudSafeAreaAdapter2A.Update",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "SystemHudSafeAreaAdapter2A.Update",
                startedAt,
                details);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isApplying ||
            !isActiveAndEnabled)
        {
            return;
        }

        ApplySafeArea(false);
    }

    private void OnValidate()
    {
        simulatedInsetsPixels =
            new Vector4(
                Mathf.Max(
                    0f,
                    simulatedInsetsPixels.x),
                Mathf.Max(
                    0f,
                    simulatedInsetsPixels.y),
                Mathf.Max(
                    0f,
                    simulatedInsetsPixels.z),
                Mathf.Max(
                    0f,
                    simulatedInsetsPixels.w));

        if (Application.isPlaying)
        {
            _hasApplied = false;
        }
    }

    [ContextMenu("Apply Safe Area Now")]
    public void ApplySafeAreaNow()
    {
        ApplySafeArea(true);
    }

    [ContextMenu("Validate Safe Area Setup")]
    public void ValidateSetup()
    {
        CacheRectTransform();

        if (transform.parent == null)
        {
            AppLog.Error(
                DebugLogChannel.SystemHud,
                "[2A-S03-03-T03] Safe Area root " +
                "does not have a parent.");

            return;
        }

        Canvas directParentCanvas =
            transform.parent.GetComponent<Canvas>();

        if (directParentCanvas == null)
        {
            AppLog.Error(
                DebugLogChannel.SystemHud,
                "[2A-S03-03-T03] Safe Area root must " +
                "be a direct child of a Canvas.");

            return;
        }

        CanvasScaler canvasScaler =
            directParentCanvas.GetComponent<CanvasScaler>();

        if (canvasScaler == null)
        {
            AppLog.Error(
                DebugLogChannel.SystemHud,
                "[2A-S03-03-T03] Parent Canvas does " +
                "not have CanvasScaler.");

            return;
        }

        bool scaleModeValid =
            canvasScaler.uiScaleMode ==
            CanvasScaler.ScaleMode
                .ScaleWithScreenSize;

        bool resolutionValid =
            Vector2.Distance(
                canvasScaler.referenceResolution,
                ReferenceResolution) < 0.01f;

        bool matchModeValid =
            canvasScaler.screenMatchMode ==
            CanvasScaler.ScreenMatchMode
                .MatchWidthOrHeight;

        if (!scaleModeValid ||
            !resolutionValid ||
            !matchModeValid)
        {
            AppLog.Error(
                DebugLogChannel.SystemHud,
                "[2A-S03-03-T03] CanvasScaler setup " +
                "does not match the required " +
                "1080x1920 Scale With Screen Size " +
                "configuration.");

            return;
        }

        AppLog.Info(
            DebugLogChannel.SystemHud,
            "[2A-S03-03-T03] Safe Area setup is valid " +
            "for " +
            gameObject.name +
            ".");
    }

    private void ApplySafeArea(
        bool force)
    {
        CacheRectTransform();

        int screenWidth =
            Screen.width;

        int screenHeight =
            Screen.height;

        if (_rectTransform == null ||
            screenWidth <= 0 ||
            screenHeight <= 0)
        {
            return;
        }

        Rect effectiveSafeArea =
            GetEffectiveSafeArea(
                screenWidth,
                screenHeight);

        bool screenChanged =
            screenWidth != _cachedScreenWidth ||
            screenHeight != _cachedScreenHeight;

        bool safeAreaChanged =
            !RectsApproximatelyEqual(
                effectiveSafeArea,
                _cachedSafeArea);

        if (!force &&
            _hasApplied &&
            !screenChanged &&
            !safeAreaChanged)
        {
            return;
        }

        float normalizedMinX =
            effectiveSafeArea.xMin /
            screenWidth;

        float normalizedMinY =
            effectiveSafeArea.yMin /
            screenHeight;

        float normalizedMaxX =
            effectiveSafeArea.xMax /
            screenWidth;

        float normalizedMaxY =
            effectiveSafeArea.yMax /
            screenHeight;

        bool sf03 = IsSf03Profile(effectiveSafeArea, screenWidth, screenHeight);
        if (sf03Only && !sf03)
        {
            normalizedMinX = normalizedMinY = 0f;
            normalizedMaxX = normalizedMaxY = 1f;
        }

        Vector2 anchorMin =
            new Vector2(
                applyHorizontal
                    ? normalizedMinX
                    : 0f,
                applyVertical
                    ? normalizedMinY
                    : 0f);

        Vector2 anchorMax =
            new Vector2(
                applyHorizontal
                    ? normalizedMaxX
                    : 1f,
                applyVertical
                    ? normalizedMaxY
                    : 1f);

        _isApplying = true;

        _rectTransform.anchorMin =
            anchorMin;

        _rectTransform.anchorMax =
            anchorMax;

        if (resetOffsetsAfterApply)
        {
            _rectTransform.offsetMin =
                Vector2.zero;

            _rectTransform.offsetMax =
                Vector2.zero;
        }

        bool tallArtwork = UpdateSf03TopArtwork(sf03, effectiveSafeArea, screenHeight);
        if (!tallArtwork && (!sf03Only || sf03))
            UpdateHudDecoration(effectiveSafeArea, screenWidth, screenHeight);
        else
            HideInsetCaps();

        _isApplying = false;

        _cachedSafeArea =
            effectiveSafeArea;

        _cachedScreenWidth =
            screenWidth;

        _cachedScreenHeight =
            screenHeight;

        _hasApplied = true;

        lastAppliedSafeAreaPixels =
            effectiveSafeArea;

        lastAppliedAnchorMin =
            anchorMin;

        lastAppliedAnchorMax =
            anchorMax;

        lastScreenWidth =
            screenWidth;

        lastScreenHeight =
            screenHeight;

        if (logChanges)
        {
            AppLog.Info(
                DebugLogChannel.SystemHud,
                "[2A-S03-03-T03] Applied Safe Area " +
                effectiveSafeArea +
                " to " +
                gameObject.name +
                ".");
        }
    }

    // Match the checked-in SF_03 profile by geometry, not by an arbitrary
    // notch size threshold. SF_01/SF_02 and all other profiles keep their paths.
    public static bool IsSf03Profile(Rect area, int width, int height)
    {
        return width == 1440 && height == 2960 &&
            Mathf.Abs(area.xMin) < 1f && Mathf.Abs(area.yMin) < 1f &&
            Mathf.Abs(area.width - 1440f) < 1f && Mathf.Abs(area.height - 2780f) < 1f;
    }

    private bool UpdateSf03TopArtwork(bool sf03, Rect area, int screenHeight)
    {
        bool show = sf03 && sf03TallTopArtwork != null;
        if (!show)
        {
            if (_sf03ArtworkActive && _sf03OriginalArtwork != null)
                _sf03OriginalArtwork.color = _sf03OriginalColor;
            if (_sf03TallImage != null)
                _sf03TallImage.gameObject.SetActive(false);
            _sf03ArtworkActive = false;
            return false;
        }

        var hud = transform.Find("PF_HudTopRoot") as RectTransform;
        if (hud == null) return false;
        if (_sf03OriginalArtwork == null)
        {
            var background = hud.Find("BackgroundImage");
            if (background == null || !background.TryGetComponent(out _sf03OriginalArtwork))
                return false;
            _sf03OriginalColor = _sf03OriginalArtwork.color;
        }
        if (_sf03TallImage == null)
        {
            var artwork = new GameObject("SF03_TallHudArtwork", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            artwork.layer = gameObject.layer;
            var rt = artwork.GetComponent<RectTransform>();
            rt.SetParent(_rectTransform, false);
            rt.SetAsFirstSibling();
            _sf03TallImage = artwork.GetComponent<Image>();
            _sf03TallImage.sprite = sf03TallTopArtwork;
            _sf03TallImage.material = _sf03OriginalArtwork.material;
            _sf03TallImage.color = _sf03OriginalColor;
            _sf03TallImage.raycastTarget = false;
            _sf03TallImage.preserveAspect = false;
        }

        // The safe HUD retains its 200-unit rectangle. The new sprite
        // runs from its original bottom edge up to the physical screen top.
        var rect = _sf03TallImage.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f + (screenHeight - area.yMax) / Mathf.Max(1f, area.height));
        rect.offsetMin = new Vector2(0f, -hud.rect.height);
        rect.offsetMax = Vector2.zero;
        _sf03TallImage.gameObject.SetActive(true);
        var color = _sf03OriginalColor;
        color.a = 0f;
        _sf03OriginalArtwork.color = color;
        _sf03ArtworkActive = true;
        return true;
    }

    private void HideInsetCaps()
    {
        foreach (var cap in _insetCaps)
            if (cap != null) cap.gameObject.SetActive(false);
        if (_leftCap != null) _leftCap.gameObject.SetActive(false);
        if (_rightCap != null) _rightCap.gameObject.SetActive(false);
    }

    // Three independent decorative caps. Only their flat center stretches;
    // the original HUD sprite and all of its children are never resized.
    private void UpdateHudDecoration(Rect safeArea, int screenWidth, int screenHeight)
    {
        if (!fillTopUnsafeArea && !fillBottomUnsafeArea) return;
        if (_insetCaps[0] == null)
        {
            string edge = fillTopUnsafeArea ? "Top" : "Bottom";
            _insetCaps[0] = CreateCap(edge + "UnsafeAreaBackdropCompact", 0);
            _insetCaps[1] = CreateCap(edge + "UnsafeAreaBackdropMedium", 1);
            _insetCaps[2] = CreateCap(edge + "UnsafeAreaBackdropExtended", 2);
            _leftCap = CreateDecoration(edge + "LeftInset", _rectTransform, CapBase);
            _rightCap = CreateDecoration(edge + "RightInset", _rectTransform, CapBase);
            _leftCap.SetAsFirstSibling();
            _rightCap.SetAsFirstSibling();
        }
        float safeHeight = Mathf.Max(1f, safeArea.height);
        float safeWidth = Mathf.Max(1f, safeArea.width);
        float pixels = applyVertical ? (fillTopUnsafeArea
            ? screenHeight - safeArea.yMax : safeArea.yMin) : 0f;
        float left = applyHorizontal ? safeArea.xMin / safeWidth : 0f;
        float right = applyHorizontal ? (screenWidth - safeArea.xMax) / safeWidth : 0f;
        float fraction = pixels / safeHeight;
        // Thresholds are fractions of the physical screen, independent of DPI.
        float ratio = pixels / Mathf.Max(1f, screenHeight);
        int variant = ratio <= 0.035f ? 0 : ratio <= 0.075f ? 1 : 2;
        for (int i = 0; i < _insetCaps.Length; i++)
        {
            var cap = _insetCaps[i];
            bool show = pixels > 0.5f && i == variant;
            cap.gameObject.SetActive(show);
            if (!show) continue;
            cap.anchorMin = new Vector2(-left, fillTopUnsafeArea ? 1f : -fraction);
            cap.anchorMax = new Vector2(1f + right, fillTopUnsafeArea ? 1f + fraction : 0f);
            cap.offsetMin = cap.offsetMax = Vector2.zero;
        }

        // Side caps cover only the vertical HUD band, not the gameplay viewport.
        Transform hud = transform.Find(fillTopUnsafeArea ? "PF_HudTopRoot" : "PF_SystemHudBottomRoot");
        float hudHeight = hud is RectTransform hudRect ? hudRect.rect.height : 0f;
        LayoutSideCap(_leftCap, -left, 0f, hudHeight, left > 0f);
        LayoutSideCap(_rightCap, 1f, 1f + right, hudHeight, right > 0f);
    }

    private RectTransform CreateCap(string capName, int variant)
    {
        var cap = CreateDecoration(capName, _rectTransform, CapBase);
        cap.SetAsFirstSibling();
        bool top = fillTopUnsafeArea;
        // Fixed-height trim is anchored to the outer edge; no HUD frame is stretched.
        AddCapLine(cap, "OuterTrim", top, 0f, 3f, CapTrim);
        if (variant >= 1)
            AddCapLine(cap, "InnerTrim", top, 7f, 2f, new Color(0.08f, 0.18f, 0.28f, 1f));
        if (variant >= 2)
        {
            AddCapLine(cap, "PanelSeam", top, 16f, 2f, CapTrim);
            var center = CreateDecoration("CenterPanel", cap, new Color(0.045f, 0.095f, 0.16f, 1f));
            center.anchorMin = new Vector2(0.14f, 0.25f);
            center.anchorMax = new Vector2(0.86f, 0.75f);
            center.offsetMin = center.offsetMax = Vector2.zero;
            center.SetAsFirstSibling();
        }
        return cap;
    }

    private void AddCapLine(RectTransform parent, string lineName, bool top,
        float inset, float thickness, Color color)
    {
        var line = CreateDecoration(lineName, parent, color);
        float edge = top ? 1f : 0f;
        line.anchorMin = new Vector2(0f, edge);
        line.anchorMax = new Vector2(1f, edge);
        line.pivot = new Vector2(0.5f, edge);
        line.sizeDelta = new Vector2(0f, thickness);
        line.anchoredPosition = new Vector2(0f, top ? -inset : inset);
    }

    private RectTransform CreateDecoration(string decorationName, Transform parent, Color color)
    {
        var go = new GameObject(decorationName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void LayoutSideCap(RectTransform cap, float minX, float maxX,
        float height, bool visible)
    {
        float edge = fillTopUnsafeArea ? 1f : 0f;
        cap.anchorMin = new Vector2(minX, edge);
        cap.anchorMax = new Vector2(maxX, edge);
        cap.pivot = new Vector2(0.5f, edge);
        cap.anchoredPosition = Vector2.zero;
        cap.sizeDelta = new Vector2(0f, height);
        cap.gameObject.SetActive(visible && height > 0f);
    }

    private Rect GetEffectiveSafeArea(
        int screenWidth,
        int screenHeight)
    {
        Rect safeArea =
            Screen.safeArea;

#if UNITY_EDITOR
        if (simulateSafeAreaInEditor)
        {
            float left =
                simulatedInsetsPixels.x;

            float bottom =
                simulatedInsetsPixels.y;

            float right =
                simulatedInsetsPixels.z;

            float top =
                simulatedInsetsPixels.w;

            float width =
                Mathf.Max(
                    1f,
                    screenWidth -
                    left -
                    right);

            float height =
                Mathf.Max(
                    1f,
                    screenHeight -
                    bottom -
                    top);

            safeArea =
                new Rect(
                    left,
                    bottom,
                    width,
                    height);
        }
#endif

        float xMin =
            Mathf.Clamp(
                safeArea.xMin,
                0f,
                screenWidth);

        float yMin =
            Mathf.Clamp(
                safeArea.yMin,
                0f,
                screenHeight);

        float xMax =
            Mathf.Clamp(
                safeArea.xMax,
                xMin,
                screenWidth);

        float yMax =
            Mathf.Clamp(
                safeArea.yMax,
                yMin,
                screenHeight);

        return Rect.MinMaxRect(
            xMin,
            yMin,
            xMax,
            yMax);
    }

    private void CacheRectTransform()
    {
        if (_rectTransform == null)
        {
            _rectTransform =
                GetComponent<RectTransform>();
        }
    }

    private static bool RectsApproximatelyEqual(
        Rect first,
        Rect second)
    {
        return
            Mathf.Abs(
                first.x -
                second.x) < 0.01f &&
            Mathf.Abs(
                first.y -
                second.y) < 0.01f &&
            Mathf.Abs(
                first.width -
                second.width) < 0.01f &&
            Mathf.Abs(
                first.height -
                second.height) < 0.01f;
    }
}