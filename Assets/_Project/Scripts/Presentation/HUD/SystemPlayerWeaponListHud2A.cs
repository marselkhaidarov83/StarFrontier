using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class SystemPlayerWeaponListHud2A :
    MonoBehaviour,
    ISystemHudWidgetBinder2A,
    ISystemHudPointerClickReceiver2A
{
    private const string PanelName = "PlayerWeaponListPanel";
    private const string HeaderTextName = "PlayerWeaponListHeaderText";
    private const string ContentName = "PlayerWeaponListContent";
    private const string RowsTextName = "PlayerWeaponRowsText";

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button headerButton;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private GameObject contentRoot;

    [Header("Rows")]
    [SerializeField] private TMP_Text rowsText;
    [SerializeField] private Button rowsButton;
    [SerializeField] private SystemHudPointerClickRelay2A rowsClickRelay;

    [Header("Layout")]
    [SerializeField] private float headerHeight = 48f;
    [SerializeField] private float rowHeight = 32f;
    [SerializeField] private float rowSpacing = 0f;
    [SerializeField] private float verticalPadding = 8f;
    [SerializeField] private float collapsedHeight = 48f;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.15f;

    [Header("Threat Rules")]
    [SerializeField] private float weaponRangeMultiplier = 5f;
    [SerializeField] private float fallbackShotDistance = 12f;
    [SerializeField] private bool showEmptyPanel;

    [Header("Runtime")]
    [SerializeField] private bool isCollapsed;
    [SerializeField] private int currentWeaponLineCount;

    private readonly List<int> _weaponSlotByVisualLine = new();

    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ISystemNpcRuntimeService _npcRuntimeService;
    private IPlayerCombatTargetService _playerTargetService;
    private IPlayerAttackService _playerAttackService;
    private SimpleEventBus _eventBus;

    private float _nextRefreshTime;
    private bool _isBound;

    public bool IsBound => _isBound;

    private void Awake()
    {
        ResolvePrefabUi();

        if (headerButton != null)
            headerButton.onClick.AddListener(ToggleCollapsed);

        if (rowsClickRelay != null)
            rowsClickRelay.SetReceiver(this);

        RefreshCollapsedState();
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (headerButton != null)
            headerButton.onClick.RemoveListener(ToggleCollapsed);

        Unbind();
    }

    private void Update()
    {
        if (!_isBound)
            return;

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime =
            Time.unscaledTime +
            Mathf.Max(0.05f, refreshIntervalSeconds);

        RefreshState();
    }

    public void Bind(SystemHudBindingContext2A context)
    {
        if (context == null)
        {
            Debug.LogError(
                "[SystemPlayerWeaponListHud2A] Missing HUD binding context.",
                this);
            return;
        }

        ResolvePrefabUi();

        if (!HasRequiredUi())
        {
            Debug.LogError(
                "[SystemPlayerWeaponListHud2A] Weapon HUD prefab UI is not fully bound.",
                this);
            return;
        }

        _gameSessionService = context.Get<IGameSessionService>();
        _configService = context.Get<IConfigService>();
        _npcRuntimeService = context.Get<ISystemNpcRuntimeService>();
        _playerTargetService = context.Get<IPlayerCombatTargetService>();
        _playerAttackService = context.Get<IPlayerAttackService>();
        _eventBus = context.Get<SimpleEventBus>();

        _eventBus.Subscribe<CombatWeaponTargetAssignmentsChangedEvent2A>(
            OnAssignmentsChanged);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Subscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);
        _eventBus.Subscribe<GameTickStartedEvent>(OnGameTickStarted);

        _isBound = true;
        _nextRefreshTime = 0f;
        RefreshState();
    }

    public void Unbind()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<CombatWeaponTargetAssignmentsChangedEvent2A>(
                OnAssignmentsChanged);
            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
            _eventBus.Unsubscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);
            _eventBus.Unsubscribe<GameTickStartedEvent>(OnGameTickStarted);
        }

        _isBound = false;
        _gameSessionService = null;
        _configService = null;
        _npcRuntimeService = null;
        _playerTargetService = null;
        _playerAttackService = null;
        _eventBus = null;

        _weaponSlotByVisualLine.Clear();
        currentWeaponLineCount = 0;
        SetPanelVisible(false);
    }

    private void OnAssignmentsChanged(
        CombatWeaponTargetAssignmentsChangedEvent2A evt)
    {
        RefreshState();
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        RefreshState();
    }

    private void OnPlayerDestroyed(PlayerDestroyedEvent evt)
    {
        _weaponSlotByVisualLine.Clear();
        currentWeaponLineCount = 0;
        SetPanelVisible(false);
    }

    private void OnGameTickStarted(GameTickStartedEvent evt)
    {
        RefreshState();
    }

    private void RefreshState()
    {
        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null ||
            activeShip.EquippedWeaponIds == null ||
            activeShip.EquippedWeaponIds.Count == 0)
        {
            _weaponSlotByVisualLine.Clear();
            currentWeaponLineCount = 0;
            SetPanelVisible(false);
            return;
        }

        bool hasNearbyHostile = HasNearbyHostile();

        if (!hasNearbyHostile && !showEmptyPanel)
        {
            _weaponSlotByVisualLine.Clear();
            currentWeaponLineCount = 0;
            SetPanelVisible(false);
            return;
        }

        SetPanelVisible(true);
        RefreshHeader();
        RebuildRows(activeShip);
        RefreshCollapsedState();
        RefreshPanelHeight();
    }

    private void ToggleCollapsed()
    {
        isCollapsed = !isCollapsed;
        RefreshCollapsedState();
    }

    private void RefreshCollapsedState()
    {
        if (contentRoot != null)
            contentRoot.SetActive(!isCollapsed);

        RefreshHeader();
        RefreshPanelHeight();
    }

    private void RefreshHeader()
    {
        if (headerText == null)
            return;

        ShipRuntimeData activeShip = GetActiveShip();
        int totalWeaponCount =
            activeShip?.EquippedWeaponIds != null
                ? activeShip.EquippedWeaponIds.Count
                : 0;

        int targetedWeaponCount =
            CountTargetedWeapons(totalWeaponCount);

        string selectedName = ResolveSelectedTargetName();
        if (!string.IsNullOrWhiteSpace(selectedName))
        {
            headerText.text = "ЦЕЛЬ " + selectedName;

            return;
        }

        headerText.text =
            "ОРУЖИЕ " +
            targetedWeaponCount +
            "/" +
            totalWeaponCount;
    }

    private int CountTargetedWeapons(int totalWeaponCount)
    {
        if (_playerAttackService == null ||
            totalWeaponCount <= 0)
        {
            return 0;
        }

        int count = 0;

        foreach (var pair in _playerAttackService.WeaponTargetNpcIdsBySlot)
        {
            if (pair.Key >= 0 &&
                pair.Key < totalWeaponCount &&
                !string.IsNullOrWhiteSpace(pair.Value))
            {
                count++;
            }
        }

        return count;
    }

    private string ResolveSelectedTargetName()
    {
        if (_playerAttackService == null || _npcRuntimeService == null)
            return string.Empty;

        string selectedTargetNpcId = _playerAttackService.CurrentTargetNpcId;

        if (string.IsNullOrWhiteSpace(selectedTargetNpcId))
            return string.Empty;

        if (!_npcRuntimeService.TryGetNpc(
                selectedTargetNpcId,
                out SystemNpcRuntimeState target))
        {
            return string.Empty;
        }

        if (!target.IsAlive)
            return string.Empty;

        return GetDisplayName(target);
    }

    private void RebuildRows(ShipRuntimeData activeShip)
    {
        _weaponSlotByVisualLine.Clear();

        if (rowsText == null)
            return;

        string text = string.Empty;
        var groupedSlots = BuildAssignedGroups(activeShip);

        foreach (var pair in groupedSlots)
        {
            if (!_npcRuntimeService.TryGetNpc(pair.Key, out SystemNpcRuntimeState target))
                continue;

            AppendTargetHeaderLine(
                ref text,
                target.RuntimeNpcId,
                "Враг " + GetDisplayName(target));

            List<int> slots = pair.Value;
            slots.Sort();

            for (int i = 0; i < slots.Count; i++)
            {
                int slotIndex = slots[i];

                if (slotIndex < 0 || slotIndex >= activeShip.EquippedWeaponIds.Count)
                    continue;

                string weaponConfigId = activeShip.EquippedWeaponIds[slotIndex];
                string color = CanWeaponReachTarget(weaponConfigId, target)
                    ? "#61FF7A"
                    : "#FF4A4A";

                AppendWeaponLine(
                    ref text,
                    slotIndex,
                    weaponConfigId,
                    target,
                    color);
            }
        }

        bool hasUntargetedWeapons = false;

        for (int slotIndex = 0;
             slotIndex < activeShip.EquippedWeaponIds.Count;
             slotIndex++)
        {
            if (_playerAttackService.WeaponTargetNpcIdsBySlot.ContainsKey(slotIndex))
                continue;

            if (!hasUntargetedWeapons)
            {
                AppendUntargetedHeaderLine(ref text, "Без цели");
                hasUntargetedWeapons = true;
            }

            AppendWeaponLine(
                ref text,
                slotIndex,
                activeShip.EquippedWeaponIds[slotIndex],
                null,
                "#FFFFFF");
        }

        rowsText.text = text;
        currentWeaponLineCount = _weaponSlotByVisualLine.Count;
        rowsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
    }

    private Dictionary<string, List<int>> BuildAssignedGroups(
        ShipRuntimeData activeShip)
    {
        var result = new Dictionary<string, List<int>>();

        foreach (var pair in _playerAttackService.WeaponTargetNpcIdsBySlot)
        {
            int slotIndex = pair.Key;
            string targetNpcId = pair.Value;

            if (slotIndex < 0 ||
                slotIndex >= activeShip.EquippedWeaponIds.Count ||
                string.IsNullOrWhiteSpace(targetNpcId))
            {
                continue;
            }

            if (!result.TryGetValue(targetNpcId, out List<int> slots))
            {
                slots = new List<int>();
                result[targetNpcId] = slots;
            }

            slots.Add(slotIndex);
        }

        return result;
    }

    private void AppendHeaderLine(ref string text, string title)
    {
        AppendLineBreakIfNeeded(ref text);
        _weaponSlotByVisualLine.Add(-1);
        text += "<color=#FFD86B>" + EscapeRichText(title) + "</color>";
    }

    private void AppendTargetHeaderLine(
        ref string text,
        string targetNpcId,
        string title)
    {
        AppendLineBreakIfNeeded(ref text);
        _weaponSlotByVisualLine.Add(-1);
        text +=
            "<link=\"target:" +
            EscapeLinkId(targetNpcId) +
            "\">" +
            "<color=#FFD86B>" +
            EscapeRichText(title) +
            "</color>" +
            "</link>";
    }

    private void AppendUntargetedHeaderLine(ref string text, string title)
    {
        AppendLineBreakIfNeeded(ref text);
        _weaponSlotByVisualLine.Add(-1);
        text +=
            "<link=\"untargeted\">" +
            "<color=#FFD86B>" +
            EscapeRichText(title) +
            "</color>" +
            "</link>";
    }

    private void AppendWeaponLine(
        ref string text,
        int slotIndex,
        string weaponConfigId,
        SystemNpcRuntimeState target,
        string color)
    {
        AppendLineBreakIfNeeded(ref text);
        _weaponSlotByVisualLine.Add(slotIndex);

        string label =
            ResolveWeaponName(weaponConfigId) +
            " (" +
            ResolveWeaponDamage(weaponConfigId) +
            ")";

        text +=
            "<link=\"weapon:" +
            slotIndex +
            "\">" +
            "<color=" +
            color +
            ">" +
            EscapeRichText(label) +
            "</color>" +
            "</link>";
    }

    private int ResolveWeaponDamage(string weaponConfigId)
    {
        WeaponConfig weaponConfig =
            _configService.GetWeaponConfigById(weaponConfigId);

        if (weaponConfig == null)
            return 0;

        return Mathf.RoundToInt(
            (weaponConfig.BaseDamageMin + weaponConfig.BaseDamageMax) * 0.5f);
    }

    private static void AppendLineBreakIfNeeded(ref string text)
    {
        if (!string.IsNullOrEmpty(text))
            text += System.Environment.NewLine;
    }

    public void OnHudPointerClicked(
        GameObject source,
        PointerEventData eventData)
    {
        if (source == null || rowsText == null || source != rowsText.gameObject)
            return;

        OnRowsClicked(eventData);
    }

    private void OnRowsClicked(PointerEventData eventData)
    {
        if (!_isBound ||
            isCollapsed ||
            rowsText == null ||
            contentRoot == null ||
            _playerAttackService == null ||
            eventData == null)
        {
            return;
        }

        string linkId = ResolveClickedLinkId(eventData);

        if (string.IsNullOrWhiteSpace(linkId))
            return;

        if (linkId == "untargeted")
        {
            AssignAllUntargetedWeaponsToSelectedTarget();
            RefreshState();
            return;
        }

        const string targetPrefix = "target:";
        if (linkId.StartsWith(
                targetPrefix,
                System.StringComparison.Ordinal))
        {
            ClearAllWeaponsFromTarget(linkId.Substring(targetPrefix.Length));
            RefreshState();
            return;
        }

        int weaponSlotIndex = ResolveWeaponSlotIndex(linkId);

        if (weaponSlotIndex < 0)
            return;

        string selectedTargetNpcId = _playerAttackService.CurrentTargetNpcId;

        if (_playerAttackService.WeaponTargetNpcIdsBySlot.TryGetValue(
                weaponSlotIndex,
                out string currentTargetNpcId))
        {
            if (!string.IsNullOrWhiteSpace(selectedTargetNpcId) &&
                !string.Equals(
                    selectedTargetNpcId,
                    currentTargetNpcId,
                    System.StringComparison.Ordinal))
            {
                _playerAttackService.AssignWeaponSlotToTarget(
                    weaponSlotIndex,
                    selectedTargetNpcId);
            }
            else
            {
                _playerAttackService.ClearWeaponSlotTarget(weaponSlotIndex);
            }

            RefreshState();
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedTargetNpcId))
        {
            _playerAttackService.AssignWeaponSlotToTarget(
                weaponSlotIndex,
                selectedTargetNpcId);
        }

        RefreshState();
    }

    private string ResolveClickedLinkId(PointerEventData eventData)
    {
        if (eventData == null || rowsText == null)
            return string.Empty;

        Camera eventCamera =
            eventData.pressEventCamera != null
                ? eventData.pressEventCamera
                : eventData.enterEventCamera;

        Canvas canvas = rowsText.GetComponentInParent<Canvas>();

        if (eventCamera == null &&
            canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        rowsText.ForceMeshUpdate();

        int linkIndex =
            TMP_TextUtilities.FindIntersectingLink(
                rowsText,
                eventData.position,
                eventCamera);

        if (linkIndex < 0 ||
            linkIndex >= rowsText.textInfo.linkCount)
        {
            return string.Empty;
        }

        TMP_LinkInfo linkInfo =
            rowsText.textInfo.linkInfo[linkIndex];

        return linkInfo.GetLinkID();
    }

    private static int ResolveWeaponSlotIndex(string linkId)
    {
        const string prefix = "weapon:";

        if (string.IsNullOrWhiteSpace(linkId) ||
            !linkId.StartsWith(
                prefix,
                System.StringComparison.Ordinal))
        {
            return -1;
        }

        string slotText =
            linkId.Substring(prefix.Length);

        return int.TryParse(slotText, out int weaponSlotIndex)
            ? weaponSlotIndex
            : -1;
    }

    private void AssignAllUntargetedWeaponsToSelectedTarget()
    {
        string selectedTargetNpcId = _playerAttackService.CurrentTargetNpcId;

        if (string.IsNullOrWhiteSpace(selectedTargetNpcId))
            return;

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip?.EquippedWeaponIds == null)
            return;

        for (int slotIndex = 0;
             slotIndex < activeShip.EquippedWeaponIds.Count;
             slotIndex++)
        {
            if (_playerAttackService.WeaponTargetNpcIdsBySlot.ContainsKey(slotIndex))
                continue;

            _playerAttackService.AssignWeaponSlotToTarget(
                slotIndex,
                selectedTargetNpcId);
        }
    }

    private void ClearAllWeaponsFromTarget(string targetNpcId)
    {
        if (string.IsNullOrWhiteSpace(targetNpcId))
            return;

        var slotsToClear = new List<int>();

        foreach (var pair in _playerAttackService.WeaponTargetNpcIdsBySlot)
        {
            if (string.Equals(
                    pair.Value,
                    targetNpcId,
                    System.StringComparison.Ordinal))
            {
                slotsToClear.Add(pair.Key);
            }
        }

        for (int i = 0; i < slotsToClear.Count; i++)
            _playerAttackService.ClearWeaponSlotTarget(slotsToClear[i]);
    }

    private bool HasNearbyHostile()
    {
        if (_npcRuntimeService == null ||
            _playerTargetService == null)
        {
            return false;
        }

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        if (npcs == null)
            return false;

        for (int i = 0; i < npcs.Count; i++)
        {
            if (IsHostileInThreatZone(npcs[i]))
                return true;
        }

        return false;
    }

    private bool IsHostileInThreatZone(SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            !npc.IsAlive ||
            !npc.IsHostileToPlayer ||
            npc.IsOnPlanet ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return false;
        }

        if (!_playerTargetService.IsPlayerAvailableInSystem(npc.CurrentSystemId))
            return false;

        float maxShotDistance = ResolveMaxShotDistance(npc);

        if (maxShotDistance <= 0f)
            maxShotDistance = fallbackShotDistance;

        Vector3 playerPosition =
            ToFlat(_playerTargetService.GetPlayerPosition());

        Vector3 npcPosition =
            ToFlat(npc.CurrentPosition);

        float threatDistance =
            maxShotDistance * weaponRangeMultiplier;

        return Vector3.Distance(playerPosition, npcPosition) <= threatDistance;
    }

    private float ResolveMaxShotDistance(SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            npc.Weapons == null ||
            npc.Weapons.Count == 0)
        {
            return 0f;
        }

        float maxDistance = 0f;

        for (int i = 0; i < npc.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weapon = npc.Weapons[i];

            if (weapon == null)
                continue;

            if (weapon.ShotDistance > maxDistance)
                maxDistance = weapon.ShotDistance;
        }

        if (maxDistance <= 0f)
            return fallbackShotDistance;

        return maxDistance;
    }

    private bool CanWeaponReachTarget(
        string weaponConfigId,
        SystemNpcRuntimeState target)
    {
        if (target == null || _playerTargetService == null)
            return false;

        WeaponConfig weaponConfig =
            _configService.GetWeaponConfigById(weaponConfigId);

        if (weaponConfig == null)
            return false;

        Vector3 playerPosition = ToFlat(_playerTargetService.GetPlayerPosition());
        Vector3 targetPosition = ToFlat(target.CurrentPosition);
        float distance = Vector3.Distance(playerPosition, targetPosition);

        return distance <= weaponConfig.RangeMax;
    }

    private string ResolveWeaponName(string weaponConfigId)
    {
        WeaponConfig weaponConfig =
            _configService.GetWeaponConfigById(weaponConfigId);

        if (weaponConfig != null &&
            !string.IsNullOrWhiteSpace(weaponConfig.DisplayName))
        {
            return weaponConfig.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(weaponConfigId)
            ? "Оружие"
            : weaponConfigId;
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService?.State?.Player == null)
            return null;

        return _gameSessionService
            .State
            .Player
            .PlayerShipState
            .GetActiveShip();
    }

    private void RefreshPanelHeight()
    {
        if (panelRoot == null)
            return;

        RectTransform panelRect =
            panelRoot.GetComponent<RectTransform>();

        if (panelRect == null)
            return;

        float height =
            Mathf.Max(1f, collapsedHeight);

        if (!isCollapsed)
        {
            int rowCount = Mathf.Max(0, currentWeaponLineCount);

            height =
                headerHeight +
                verticalPadding;

            if (rowCount > 0)
            {
                height +=
                    rowCount * Mathf.Max(1f, rowHeight) +
                    Mathf.Max(0, rowCount - 1) * Mathf.Max(0f, rowSpacing);
            }
        }

        panelRect.sizeDelta =
            new Vector2(panelRect.sizeDelta.x, Mathf.Max(1f, height));
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);
    }

    private void ResolvePrefabUi()
    {
        if (panelRoot == null)
            panelRoot = ResolveChild(PanelName);

        if (panelRoot == null)
            return;

        if (headerButton == null)
        {
            GameObject headerObject =
                ResolveChild(HeaderTextName, panelRoot.transform);

            if (headerObject != null)
                headerButton = headerObject.GetComponent<Button>();
        }

        if (headerText == null)
        {
            GameObject headerObject =
                ResolveChild(HeaderTextName, panelRoot.transform);

            if (headerObject != null)
                headerText = headerObject.GetComponent<TMP_Text>();
        }

        if (contentRoot == null)
            contentRoot = ResolveChild(ContentName, panelRoot.transform);

        if (rowsText == null && contentRoot != null)
        {
            GameObject rowsObject =
                ResolveChild(RowsTextName, contentRoot.transform);

            if (rowsObject != null)
                rowsText = rowsObject.GetComponent<TMP_Text>();
        }

        if (rowsText != null)
            rowsText.alignment = TextAlignmentOptions.TopLeft;

        if (rowsButton == null && rowsText != null)
            rowsButton = rowsText.GetComponent<Button>();

        if (rowsClickRelay == null && rowsText != null)
            rowsClickRelay = rowsText.GetComponent<SystemHudPointerClickRelay2A>();

        if (rowsClickRelay != null)
            rowsClickRelay.SetReceiver(this);
    }

    private bool HasRequiredUi()
    {
        return panelRoot != null &&
               headerText != null &&
               headerButton != null &&
               contentRoot != null &&
               rowsText != null &&
               rowsButton != null &&
               rowsClickRelay != null;
    }

    private GameObject ResolveChild(string objectName)
    {
        return ResolveChild(objectName, transform);
    }

    private static GameObject ResolveChild(
        string objectName,
        Transform root)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] children =
            root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
                return children[i].gameObject;
        }

        return null;
    }

    private static string GetDisplayName(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "Враг";

        if (!string.IsNullOrWhiteSpace(npc.DisplayName))
            return npc.DisplayName.Trim();

        return "Враг";
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string EscapeLinkId(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\"", string.Empty);
    }

    private static Vector3 ToFlat(Vector3 value)
    {
        value.z = 0f;
        return value;
    }
}
