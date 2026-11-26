using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.UI;
#endif

[DisallowMultipleComponent]
public sealed class SystemCameraActionButtonsThreatPanelOffset2A :
    MonoBehaviour
{
    private const string ThreatPanelName = "PlayerThreatListPanel";

    [SerializeField] private RectTransform actionButtonsRoot;
    [SerializeField] private RectTransform threatPanel;
    [SerializeField] private float panelGap = 18f;

#if UNITY_EDITOR
    [SerializeField] private Sprite editorZoomButtonBackground;
    private MapCameraController _editorMapCamera;
    private Button _zoomInButton;
    private Button _zoomOutButton;

    private void EnsureEditorZoomButtons()
    {
        if (_zoomInButton != null || actionButtonsRoot == null) return;
        var returnButton = actionButtonsRoot.Find("ReturnToShipButton");
        if (returnButton == null || !returnButton.TryGetComponent<Button>(out var template)) return;
        _zoomInButton = CreateZoomButton(template, true);
        _zoomOutButton = CreateZoomButton(template, false);
        LayoutRebuilder.MarkLayoutForRebuild(actionButtonsRoot);
    }

    private Button CreateZoomButton(Button template, bool zoomIn)
    {
        var go = new GameObject(zoomIn ? "EditorZoomInButton" : "EditorZoomOutButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.layer = template.gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(actionButtonsRoot, false);
        // Above the existing controls: the original bottom-anchored positions stay unchanged.
        rect.SetSiblingIndex(zoomIn ? 0 : 1);
        var layout = go.GetComponent<LayoutElement>();
        var templateLayout = template.GetComponent<LayoutElement>();
        layout.preferredWidth = templateLayout != null ? templateLayout.preferredWidth : 90f;
        layout.preferredHeight = templateLayout != null ? templateLayout.preferredHeight : 90f;
        var image = go.GetComponent<Image>();
        image.sprite = editorZoomButtonBackground;
        image.color = editorZoomButtonBackground != null ? Color.white : new Color(0.08f, 0.23f, 0.38f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = template.colors;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(() => { if (_editorMapCamera != null) _editorMapCamera.EditorZoomStep(zoomIn); });

        // Draw a cyan magnifier with a plus/minus; no font or additional asset needed.
        var icon = new GameObject("ZoomGlyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(SystemEditorZoomGlyph2A));
        icon.layer = go.layer;
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.SetParent(rect, false);
        iconRect.anchorMin = new Vector2(.18f, .18f);
        iconRect.anchorMax = new Vector2(.82f, .82f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        var glyph = icon.GetComponent<SystemEditorZoomGlyph2A>();
        glyph.ZoomIn = zoomIn;
        glyph.color = new Color(.55f, .89f, 1f, 1f);
        glyph.raycastTarget = false;
        return button;
    }

    private void RefreshEditorZoomButtons()
    {
        if (_zoomInButton == null || _zoomOutButton == null) return;
        if (_editorMapCamera == null)
        {
            var controller = FindFirstObjectByType<SystemCameraController2A>();
            if (controller != null && controller.TargetCamera != null)
                _editorMapCamera = controller.TargetCamera.GetComponent<MapCameraController>();
        }
        bool available = _editorMapCamera != null && _editorMapCamera.isActiveAndEnabled;
        _zoomInButton.interactable = available;
        _zoomOutButton.interactable = available;
    }
#endif

    private Vector2 _baseAnchoredPosition;
    private bool _basePositionCaptured;

    private void Awake()
    {
        ResolveActionButtonsRoot();
        CaptureBasePosition();
        ResolveThreatPanel();
#if UNITY_EDITOR
        EnsureEditorZoomButtons();
#endif
        RefreshPosition();
    }

    private void OnEnable()
    {
        ResolveActionButtonsRoot();
        CaptureBasePosition();
        ResolveThreatPanel();
        RefreshPosition();
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        RefreshEditorZoomButtons();
#endif
        ResolveThreatPanel();
        RefreshPosition();
    }

    private void ResolveActionButtonsRoot()
    {
        if (actionButtonsRoot != null)
            return;

        actionButtonsRoot = GetComponent<RectTransform>();
    }

    private void CaptureBasePosition()
    {
        if (_basePositionCaptured ||
            actionButtonsRoot == null)
        {
            return;
        }

        _baseAnchoredPosition = actionButtonsRoot.anchoredPosition;
        _basePositionCaptured = true;
    }

    private void ResolveThreatPanel()
    {
        if (threatPanel != null)
            return;

        RectTransform[] rectTransforms =
            Resources.FindObjectsOfTypeAll<RectTransform>();

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform candidate = rectTransforms[i];

            if (candidate == null ||
                candidate.gameObject == null ||
                candidate.gameObject.scene != gameObject.scene ||
                candidate.name != ThreatPanelName)
            {
                continue;
            }

            threatPanel = candidate;
            return;
        }
    }

    private void RefreshPosition()
    {
        if (actionButtonsRoot == null ||
            !_basePositionCaptured)
        {
            return;
        }

        Vector2 position = _baseAnchoredPosition;

        if (IsThreatPanelVisible())
        {
            float panelTopY =
                threatPanel.anchoredPosition.y +
                threatPanel.rect.height;

            position.y =
                Mathf.Max(
                    position.y,
                    panelTopY + panelGap);
        }

        if (actionButtonsRoot.anchoredPosition != position)
            actionButtonsRoot.anchoredPosition = position;
    }

    private bool IsThreatPanelVisible()
    {
        return threatPanel != null &&
               threatPanel.gameObject != null &&
               threatPanel.gameObject.activeInHierarchy &&
               threatPanel.rect.height > 0f;
    }
}
