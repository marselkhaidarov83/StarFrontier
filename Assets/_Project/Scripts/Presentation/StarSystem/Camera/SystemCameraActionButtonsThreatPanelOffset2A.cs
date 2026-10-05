using UnityEngine;

[DisallowMultipleComponent]
public sealed class SystemCameraActionButtonsThreatPanelOffset2A :
    MonoBehaviour
{
    private const string ThreatPanelName = "PlayerThreatListPanel";

    [SerializeField] private RectTransform actionButtonsRoot;
    [SerializeField] private RectTransform threatPanel;
    [SerializeField] private float panelGap = 18f;

    private Vector2 _baseAnchoredPosition;
    private bool _basePositionCaptured;

    private void Awake()
    {
        ResolveActionButtonsRoot();
        CaptureBasePosition();
        ResolveThreatPanel();
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
