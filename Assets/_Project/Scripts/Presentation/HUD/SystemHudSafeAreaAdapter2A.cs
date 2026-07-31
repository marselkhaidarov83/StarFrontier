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
        /*
         * Это не перестраивает HUD каждый кадр.
         * Anchors меняются только при изменении
         * размера экрана или safe area.
         */
        ApplySafeArea(false);
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
            ApplySafeArea(true);
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
            Debug.LogError(
                "[2A-S03-03-T03] Safe Area root " +
                "does not have a parent.",
                this);

            return;
        }

        Canvas directParentCanvas =
            transform.parent.GetComponent<Canvas>();

        if (directParentCanvas == null)
        {
            Debug.LogError(
                "[2A-S03-03-T03] Safe Area root must " +
                "be a direct child of a Canvas.",
                this);

            return;
        }

        CanvasScaler canvasScaler =
            directParentCanvas.GetComponent<CanvasScaler>();

        if (canvasScaler == null)
        {
            Debug.LogError(
                "[2A-S03-03-T03] Parent Canvas does " +
                "not have CanvasScaler.",
                directParentCanvas);

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
            Debug.LogError(
                "[2A-S03-03-T03] CanvasScaler setup " +
                "does not match the required " +
                "1080x1920 Scale With Screen Size " +
                "configuration.",
                directParentCanvas);

            return;
        }

        Debug.Log(
            "[2A-S03-03-T03] Safe Area setup is valid " +
            "for " +
            gameObject.name +
            ".",
            this);
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
            Debug.Log(
                "[2A-S03-03-T03] Applied Safe Area " +
                effectiveSafeArea +
                " to " +
                gameObject.name +
                ".",
                this);
        }
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