using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Both hangar layouts share the same controls and card rendering.
public sealed partial class HangarPanelView2A : MonoBehaviour
{
    private RectTransform _screen, _work, _left, _right;
    private Vector2 _lastSize;
    private Sprite _lastSprite;
    // Right menu is 90 units wide with a 35-unit margin. The station entry mirrors it.
    private const float ServiceMenuInset = 136;
    private bool _stacked;
    public Image Header { get; private set; }
    public bool IsReady => _screen != null && _work != null && (_stacked || Header != null) && _left != null && _right != null;
    private void BuildFrame()
    {
        if (transform.parent is RectTransform) ResearchPanelUi2A.Stretch((RectTransform)transform);
        transform.SetAsFirstSibling();
        _screen = ResearchPanelUi2A.Rect(transform, _stacked ? "StackedHangarLayout" : "SharedRepairLayout");
        ResearchPanelUi2A.Stretch(_screen);
        if (!_stacked)
        {
            Header = ResearchPanelUi2A.Picture(_screen, "LocationIllustration", Color.white);
            Header.preserveAspect = true;
        }
        _work = ResearchPanelUi2A.Picture(_screen, "RepairWorkspace", MissionPanelStyle2A.Workspace).rectTransform;
        _work.GetComponent<Image>().raycastTarget = true;
        _left = Panel(_work, "ShipRepair"); _right = Panel(_work, "HangarEquipment");
    }
    private static RectTransform Panel(Transform parent, string name)
    {
        var image = ResearchPanelUi2A.Picture(parent, name, MissionPanelStyle2A.CardBackground);
        MissionPanelStyle2A.Frame(image, MissionPanelStyle2A.CardAccent); return image.rectTransform;
    }
    private static void PrepareContent(RectTransform content)
    {
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 22, 22); layout.spacing = 14;
    }
    private static void Move(Component control, Transform parent, float height)
    {
        if (control == null) return;
        control.transform.SetParent(parent, false); control.gameObject.SetActive(true);
        var rect = control.transform as RectTransform;
        if (rect != null) rect.localScale = Vector3.one;
        var size = control.GetComponent<LayoutElement>() ?? control.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 0; size.preferredWidth = 0; size.flexibleWidth = 1;
        size.minHeight = height; size.preferredHeight = height; size.flexibleHeight = 0;
        if (control is TMP_Text text)
        {
            text.fontSize = 28; text.enableAutoSizing = true; text.fontSizeMax = 28; text.fontSizeMin = 20;
            text.color = Color.white; text.alignment = TextAlignmentOptions.Left;
            text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
        }
        if (control is Button button)
        {
            var image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            button.targetGraphic = image; image.color = MissionPanelStyle2A.ActionBackground;
            MissionPanelStyle2A.Action(button, button.interactable);
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                ResearchPanelUi2A.Stretch(label.rectTransform); label.margin = new Vector4(8, 2, 8, 2);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 24; label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
            }
        }
    }
    public void HideLayout()
    {
        _screen.gameObject.SetActive(false);
        enabled = false;
    }

    public void SetHeader(Sprite sprite)
    {
        if (!IsReady || Header == null) return;
        Header.sprite = sprite; Header.enabled = sprite != null; RefreshLayout();
    }
    private void LateUpdate()
    { if (IsReady && (_lastSize != _screen.rect.size || _lastSprite != Header?.sprite)) RefreshLayout(); }
    public void RefreshLayout()
    {
        if (!IsReady) return;
        _lastSize = _screen.rect.size; _lastSprite = Header?.sprite;
        float width = _lastSize.x, height = _lastSize.y;
        if (width <= 0 || height <= 0) return;
        if (_modalCard != null) LayoutEquipmentModal();
        const float topBar = 194;
        const float bottomBar = 142;
        float hero = _lastSprite != null && _lastSprite.rect.width > 0
            ? width * _lastSprite.rect.height / _lastSprite.rect.width : 0;
        if (Header != null) ResearchPanelUi2A.Place(Header.rectTransform, 0, topBar, width, hero);

        _work.anchorMin = Vector2.zero; _work.anchorMax = Vector2.one;
        _work.offsetMin = new Vector2(0, bottomBar); _work.offsetMax = new Vector2(0, -topBar - hero);
        float workHeight = Mathf.Max(0, height - topBar - hero - bottomBar);
        const float gap = 18;
        if (_stacked)
        {
            float availableHeight = Mathf.Max(0, workHeight - gap);
            float inventoryHeight = Mathf.Min(StackedInventoryHeight, availableHeight);
            float shipHeight = availableHeight - inventoryHeight;
            float menuInset = Mathf.Min(ServiceMenuInset, width * .2f);
            float shipWidth = width - 2 * menuInset;
            ResearchPanelUi2A.Place(_left, menuInset, 0, shipWidth, shipHeight);
            ResearchPanelUi2A.Place(_right, 0, workHeight - inventoryHeight, width, inventoryHeight);
            LayoutShip(shipWidth, shipHeight);
            LayoutStackedInventory(width, inventoryHeight);
            return;
        }

        float contentWidth = Mathf.Max(0, width - 3 * gap);
        float left = contentWidth * .677f;
        float right = contentWidth - left;
        float heightInColumns = Mathf.Max(0, workHeight - 2 * gap);
        ResearchPanelUi2A.Place(_left, gap, gap, left, heightInColumns);
        ResearchPanelUi2A.Place(_right, left + 2 * gap, gap, right, heightInColumns);
        LayoutShip(left, heightInColumns);
        LayoutLegacyInventory(right, heightInColumns);
    }
}
