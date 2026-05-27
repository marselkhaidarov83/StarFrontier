using UnityEngine;

public sealed class MobileUiRootView : MonoBehaviour
{
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private RectTransform hudRoot;
    [SerializeField] private RectTransform topPanel;
    [SerializeField] private RectTransform centerLayer;
    [SerializeField] private RectTransform bottomPanel;

    public RectTransform SafeAreaRoot => safeAreaRoot;
    public RectTransform HudRoot => hudRoot;
    public RectTransform TopPanel => topPanel;
    public RectTransform CenterLayer => centerLayer;
    public RectTransform BottomPanel => bottomPanel;
}