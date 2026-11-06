using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResearchPaymentChoice2A
{
    public string OptionId, Label;
    public bool Available;
}
public sealed class ResearchCardData2A
{
    public string Title, Condition, ProgressText;
    public int Level, TargetLevel;
    public bool Completed, Running, Locked;
    public float Progress;
    public Sprite Icon;
    public ResearchPaymentChoice2A[] Choices = Array.Empty<ResearchPaymentChoice2A>();
}

/// <summary>The single Resources/UI/ResearchCard2A prefab is used for every node and visual state.</summary>
public sealed class ResearchCardView2A : MonoBehaviour
{
    private TMP_Text _title, _level, _condition, _progressText, _template;
    private Image _background, _icon, _track, _fill;
    private ResearchButtonIcon2A _fallback;
    private LayoutElement _layout;
    private readonly List<Button> _buttons = new List<Button>();
    private readonly List<TMP_Text> _labels = new List<TMP_Text>();
    private ResearchCardData2A _data;
    private Action<string> _pay;
    public ResearchCardData2A Data => _data;
    public int PaymentButtonCount => _data?.Choices?.Length ?? 0;
    public void Bind(ResearchCardData2A data, TMP_Text template, Action<string> pay)
    {
        _data = data; _pay = pay; _template = template;
        if (_background == null) Build();
        int targetLevel = data.TargetLevel > data.Level ? data.TargetLevel : data.Level + 1;
        string levelText = data.Running ? (data.Level > 0 ? data.Level + " -> " : "-> ") + targetLevel : data.Level.ToString();
        _title.SetText(data.Title + (data.Level > 0 || data.Running ? " (уровень " + levelText + ")" : string.Empty));
        _level.gameObject.SetActive(false); _condition.SetText(data.Condition);
        _condition.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.Condition));
        _condition.color = data.Completed ? new Color32(126, 219, 176, 255) : Color.white;
        _background.color = data.Locked ? MissionPanelStyle2A.Workspace : MissionPanelStyle2A.CardBackground;
        MissionPanelStyle2A.Frame(_background, data.Locked ? MissionPanelStyle2A.DisabledAccent : MissionPanelStyle2A.CardAccent);
        _icon.sprite = data.Icon; _icon.enabled = data.Icon != null; _fallback.enabled = data.Icon == null;
        while (_buttons.Count < data.Choices.Length)
        {
            int index = _buttons.Count;
            var button = ResearchPanelUi2A.Button(transform, _template, "ResearchPayment_" + index, "", out var label);
            button.onClick.AddListener(() => { if (_data != null && index < _data.Choices.Length && _data.Choices[index].Available) _pay?.Invoke(_data.Choices[index].OptionId); });
            _buttons.Add(button); _labels.Add(label);
        }
        for (int i = 0; i < _buttons.Count; i++)
        {
            bool visible = i < data.Choices.Length; _buttons[i].gameObject.SetActive(visible);
            if (!visible) continue;
            _labels[i].SetText(data.Choices[i].Label); _buttons[i].interactable = data.Choices[i].Available;
            MissionPanelStyle2A.Action(_buttons[i], data.Choices[i].Available);
        }
        _track.gameObject.SetActive(data.Running); _progressText.SetText(data.ProgressText ?? string.Empty);
        _fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(data.Progress), 1);
        _fill.rectTransform.offsetMin = _fill.rectTransform.offsetMax = Vector2.zero;
        _layout.preferredHeight = (_condition.gameObject.activeSelf ? 148 : 88) + data.Choices.Length * 62 + (data.Running ? 58 : 6);
        Layout();
    }
    private void Build()
    {
        _background = GetComponent<Image>() ?? gameObject.AddComponent<Image>(); _background.raycastTarget = true;
        _layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        _icon = ResearchPanelUi2A.Picture(transform, "ResearchIcon", Color.white); _icon.preserveAspect = true;
        _fallback = ResearchPanelUi2A.Rect(transform, "AtomIcon").gameObject.AddComponent<ResearchButtonIcon2A>();
        _fallback.raycastTarget = false; _fallback.color = MissionPanelStyle2A.ActionAccent;
        _title = ResearchPanelUi2A.Text(transform, _template, "Title", "");
        _level = ResearchPanelUi2A.Text(transform, _template, "Level", ""); _level.alignment = TextAlignmentOptions.Right;
        _condition = ResearchPanelUi2A.Text(transform, _template, "Condition", "");
        _track = ResearchPanelUi2A.Picture(transform, "ResearchProgress", MissionPanelStyle2A.DisabledBackground);
        _fill = ResearchPanelUi2A.Picture(_track.transform, "Fill", MissionPanelStyle2A.ActionBackground);
        ResearchPanelUi2A.Stretch(_fill.rectTransform);
        _progressText = ResearchPanelUi2A.Text(_track.transform, _template, "RemainingTime", "");
        _progressText.alignment = TextAlignmentOptions.Center; ResearchPanelUi2A.Stretch(_progressText.rectTransform);
    }
    private void OnRectTransformDimensionsChange() { if (_data != null && _progressText != null && _background != null) Layout(); }
    private void Layout()
    {
        float w = ((RectTransform)transform).rect.width;
        ResearchPanelUi2A.Place(_icon.rectTransform, 18, 16, 62, 62);
        ResearchPanelUi2A.Place(_fallback.rectTransform, 18, 16, 62, 62);
        ResearchPanelUi2A.Place(_title.rectTransform, 96, 14, w - 114, 68);
        ResearchPanelUi2A.Place(_level.rectTransform, w - 148, 16, 130, 62);
        ResearchPanelUi2A.Place(_condition.rectTransform, 18, 88, w - 36, 54);
        float actionsTop = _condition.gameObject.activeSelf ? 148 : 88;
        for (int i = 0; i < _buttons.Count; i++) ResearchPanelUi2A.Place(_buttons[i].GetComponent<RectTransform>(), 18, actionsTop + i * 62, w - 36, 50);
        ResearchPanelUi2A.Place(_track.rectTransform, 18, actionsTop, w - 36, 46);
    }
}
