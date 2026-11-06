using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StationResearchPanelView2A : MonoBehaviour
{
    private Image _hero, _work, _info, _repairTrack, _repairFill;
    private TMP_Text _sciencePower, _station, _health, _repair, _groupTitle, _notice, _upgradeText, _template;
    private RectTransform _groupContent, _treeContent;
    private ScrollRect _groups, _tree;
    private GameObject _prefab;
    private readonly List<ResearchCardView2A> _cards = new List<ResearchCardView2A>();
    private readonly List<Button> _groupButtons = new List<Button>();
    private readonly List<TMP_Text> _groupLabels = new List<TMP_Text>();
    private readonly List<GameObject> _connectors = new List<GameObject>();
    private Vector2 _lastSize;
    public Button Upgrade { get; private set; }
    public bool IsReady => _hero != null && _work != null && _info != null && Upgrade != null;
    public int VisibleCardCount => _cards.Count;
    public static void AttachToLocation(RectTransform research, Transform locationRoot, Transform government, Transform background)
    {
        if (research == null) return;
        // Mission/fuel panels live INSIDE BackgroundImage. A first sibling of BackgroundImage is covered by it.
        Transform host = government != null && government.parent != null ? government.parent :
            background != null ? background : locationRoot;
        if (host == null) return;
        research.SetParent(host, false); ResearchPanelUi2A.Stretch(research); research.SetAsFirstSibling();
    }
    public void Build(TMP_Text template)
    {
        if (IsReady) return;
        _template = template; ResearchPanelUi2A.Stretch((RectTransform)transform); transform.SetAsFirstSibling();
        _hero = ResearchPanelUi2A.Picture(transform, "LocationIllustration", Color.white); _hero.preserveAspect = true;
        _work = ResearchPanelUi2A.Picture(transform, "ResearchWorkspace", MissionPanelStyle2A.Workspace); _work.raycastTarget = true;
        _info = ResearchPanelUi2A.Picture(_work.transform, "StationInformation", MissionPanelStyle2A.CardBackground);
        MissionPanelStyle2A.Frame(_info, MissionPanelStyle2A.CardAccent);
        _station = ResearchPanelUi2A.Text(_info.transform, template, "StationLevel", "");
        _health = ResearchPanelUi2A.Text(_info.transform, template, "StationHealth", "");
        _sciencePower = ResearchPanelUi2A.Text(_info.transform, template, "ResearchPower", "");
        _sciencePower.alignment = TextAlignmentOptions.Right;
        _repair = ResearchPanelUi2A.Text(_info.transform, template, "SelfRepair", "");
        _repairTrack = ResearchPanelUi2A.Picture(_info.transform, "SelfRepairProgress", MissionPanelStyle2A.DisabledBackground);
        _repairFill = ResearchPanelUi2A.Picture(_repairTrack.transform, "Fill", new Color32(50, 178, 112, 255)); ResearchPanelUi2A.Stretch(_repairFill.rectTransform);
        Upgrade = ResearchPanelUi2A.Button(_info.transform, template, "UpgradeStation", "", out _upgradeText);
        _groups = ResearchPanelUi2A.Scroll(_work.transform, "ResearchGroups", true, out _groupContent);
        _groupTitle = ResearchPanelUi2A.Text(_work.transform, template, "BranchTitle", "");
        _groupTitle.gameObject.SetActive(false);
        _notice = ResearchPanelUi2A.Text(_work.transform, template, "ResearchNotice", "");
        _tree = ResearchPanelUi2A.Scroll(_work.transform, "ResearchChain", false, out _treeContent);
        _treeContent.GetComponent<VerticalLayoutGroup>().spacing = 4;
        _prefab = Resources.Load<GameObject>("UI/ResearchCard2A");
        if (_prefab == null) AppLog.Error(DebugLogChannel.Research, "[ResearchUI] Shared card prefab UI/ResearchCard2A is missing");
        Layout();
    }
    public void SetStation(StationCardSnapshot card, Sprite illustration)
    {
        if (!IsReady || card?.Station == null) return;
        var s = card.Station; _hero.sprite = illustration; _hero.enabled = illustration != null;
        _station.SetText("Уровень станции: " + s.Level + "/10"); _health.SetText("Здоровье: " + s.Health + " / " + s.MaxHealth);
        _sciencePower.gameObject.SetActive((s.StationType == StationType.Science || s.StationType == StationType.Military) && s.Level > 0 && !s.IsDestroyed);
        _sciencePower.SetText(StationInterfaceRules.StationMetricText(card));
        var settings = card.Config?.LevelEffects?.GetLevel(s.Level);
        bool repair = s.Health > 0 && s.Health < s.MaxHealth && settings?.HealthPerTick > 0 && !card.HasEnemies;
        _repairTrack.gameObject.SetActive(repair); _repair.gameObject.SetActive(s.Health < s.MaxHealth);
        _repair.SetText(card.HasEnemies ? "Самовосстановление приостановлено: враги" : settings?.HealthPerTick > 0
            ? "Самовосстановление: +" + settings.HealthPerTick + " здоровья за тик" : "Самовосстановление отключено");
        _repairFill.rectTransform.anchorMax = new Vector2(s.MaxHealth > 0 ? Mathf.Clamp01((float)s.Health / s.MaxHealth) : 0, 1);
        _repairFill.rectTransform.offsetMin = _repairFill.rectTransform.offsetMax = Vector2.zero;
        var next = card.Config?.LevelEffects?.GetLevel(s.Level + 1);
        bool running = StationPanelStyle2A.IsDevelopmentRunning(s);
        bool show = !card.IsCaptured && (running || (s.Level < 10 && (next == null || card.GalaxyLevel >= next.RequiredGalaxyLevel)));
        Upgrade.gameObject.SetActive(show);
        // Shared progress renderer, including the red invasion pause state.
        StationPanelStyle2A.UpdateConstructionFooter(Upgrade, s, card.HasEnemies);
        if (running) MissionPanelStyle2A.Frame(Upgrade.GetComponent<Image>(), MissionPanelStyle2A.DisabledAccent);
        else
        {
            bool can = show && StationLifecycleRules.CanBegin(s, StationOperationKind.Upgrade, out _);
            Upgrade.interactable = can; MissionPanelStyle2A.Action(Upgrade, can);
            _upgradeText.SetText("Улучшить до уровня " + (s.Level + 1));
        }
        Layout();
    }
    public void SetGroups(ResearchBranchConfig[] branches, int selected, Action<int> select, bool showGroups = true)
    {
        _groups.gameObject.SetActive(showGroups);
        if (!showGroups) { Layout(); return; }
        while (_groupButtons.Count < branches.Length)
        {
            int index = _groupButtons.Count;
            var button = ResearchPanelUi2A.Button(_groupContent, _template, "ResearchGroup_" + index, "", out var label);
            var layout = button.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 70;
            button.onClick.AddListener(() => select(index)); _groupButtons.Add(button); _groupLabels.Add(label);
        }
        for (int i = 0; i < _groupButtons.Count; i++)
        {
            bool visible = i < branches.Length; _groupButtons[i].gameObject.SetActive(visible); if (!visible) continue;
            string name = branches[i].DisplayName ?? "Группа исследований"; _groupLabels[i].SetText(name);
            _groupButtons[i].GetComponent<LayoutElement>().preferredWidth = Mathf.Max(280, name.Length * 16 + 48);
            _groupButtons[i].GetComponent<Image>().color = i == selected ? StationPanelStyle2A.PrimaryButtonColor : MissionPanelStyle2A.CardBackground;
        }
        Layout();
    }
    public void SetHeading(string branch, int galaxyLevel, string notice)
    { _groupTitle.gameObject.SetActive(false); _notice.SetText(notice ?? string.Empty); Layout(); }
    public void ClearCards()
    {
        foreach (var card in _cards) Retire(card.gameObject);
        foreach (var connector in _connectors) Retire(connector);
        _cards.Clear(); _connectors.Clear();
    }
    public void SetCards(IReadOnlyList<ResearchCardData2A> data, Action<int, string> pay)
    {
        if (_prefab == null) return;
        if (_cards.Count != data.Count)
        {
            ClearCards();
            for (int i = 0; i < data.Count; i++)
            {
                if (i > 0)
                {
                    var line = ResearchPanelUi2A.Text(_treeContent, _template, "DependencyArrow", "↓");
                    line.alignment = TextAlignmentOptions.Center; line.color = MissionPanelStyle2A.CardAccent;
                    line.gameObject.AddComponent<LayoutElement>().preferredHeight = 18; _connectors.Add(line.gameObject);
                }
                var instance = Instantiate(_prefab, _treeContent, false); instance.name = "ResearchCard_" + i;
                _cards.Add(instance.GetComponent<ResearchCardView2A>());
            }
        }
        for (int i = 0; i < _cards.Count; i++) { int index = i; _cards[i].Bind(data[i], _template, option => pay(index, option)); }
    }
    public void ResetTreeScroll() { if (_tree != null) { _tree.StopMovement(); _tree.verticalNormalizedPosition = 1; } }
    private static void Retire(GameObject target)
    { if (target == null) return; target.SetActive(false); if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
    private void LateUpdate()
    {
        if (!IsReady) return;
        var size = ((RectTransform)transform).rect.size;
        if (size != _lastSize) Layout();
    }
    private void Layout()
    {
        if (!IsReady) return;
        var root = (RectTransform)transform; _lastSize = root.rect.size;
        float width = root.rect.width;
        float hero = _hero.sprite != null && _hero.sprite.rect.width > 0 ? width * _hero.sprite.rect.height / _hero.sprite.rect.width : width * 4f / 9f;
        ResearchPanelUi2A.Place(_hero.rectTransform, 0, 194, width, hero);
        ResearchPanelUi2A.Place(_work.rectTransform, 0, 194 + hero, width, root.rect.height - 194 - hero - 142);
        float w = Mathf.Max(0, width - 36);
        float scienceHeight = _sciencePower.gameObject.activeSelf ? 32 : 0;
        float infoHeight = (_repair.gameObject.activeSelf ? 194 : 122) + scienceHeight;
        ResearchPanelUi2A.Place(_info.rectTransform, 18, 18, w, infoHeight);
        float button = Mathf.Min(440, w * .45f);
        float leftWidth = Mathf.Max(0, w - button - 54);
        ResearchPanelUi2A.Place(_station.rectTransform, 18, 10, _sciencePower.gameObject.activeSelf ? leftWidth : w - 36, 38);
        ResearchPanelUi2A.Place(Upgrade.GetComponent<RectTransform>(), w - button - 18, infoHeight - 20 - 58, button, 58);
        ResearchPanelUi2A.Place(_health.rectTransform, 18, 58, leftWidth, 42);
        ResearchPanelUi2A.Place(_sciencePower.rectTransform, w - button - 18, 10, button, 38);
        ResearchPanelUi2A.Place(_repairTrack.rectTransform, 18, 166, leftWidth, 10);
        ResearchPanelUi2A.Place(_repair.rectTransform, 18, 106, leftWidth, 50);
        float groupsTop = 18 + infoHeight + 12;
        ResearchPanelUi2A.Place(_groups.GetComponent<RectTransform>(), 18, groupsTop, w, 82); _groupContent.sizeDelta = new Vector2(_groupContent.sizeDelta.x, 78);
        _groupTitle.gameObject.SetActive(false);
        bool message = !string.IsNullOrEmpty(_notice.text); _notice.gameObject.SetActive(message);
        float listTop = groupsTop + (_groups.gameObject.activeSelf ? 98 : 0);
        ResearchPanelUi2A.Place(_notice.rectTransform, 18, listTop, w, 58);
        float treeTop = listTop + (message ? 66 : 0);
        ResearchPanelUi2A.Place(_tree.GetComponent<RectTransform>(), 18, treeTop, w, _work.rectTransform.rect.height - treeTop - 18);
    }
}
