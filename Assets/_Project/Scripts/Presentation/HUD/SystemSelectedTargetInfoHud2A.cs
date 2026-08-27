using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SystemSelectedTargetInfoHud2A :
    CustomMonoBehaviour,
    ISystemHudWidgetBinder2A
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text rowsText;

    [Header("Layout")]
    [SerializeField] private float headerHeight = 48f;
    [SerializeField] private float rowHeight = 40f;
    [SerializeField] private float verticalPadding = 8f;
    [SerializeField] private float minPanelHeight = 126f;
    [SerializeField] private float maxPanelHeight = 680f;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    private readonly StringBuilder _builder = new();
    private readonly Dictionary<string, WeaponConfig> _weaponConfigsById = new();

    private SimpleEventBus _eventBus;
    private ISystemNpcRuntimeService _npcRuntimeService;
    private ISystemEnemyService _enemyService;
    private IConfigService _configService;

    private string _currentTargetId;
    private SystemGameplayTargetType _currentTargetType;
    private float _refreshTimer;
    private bool _isBound;

    public bool IsBound => _isBound;

    private void Awake()
    {
        ResolveSerializedReferences();
        HidePanel();
    }

    private void OnEnable()
    {
        ResolveSerializedReferences();
    }

    private void OnDisable()
    {
        if (_isBound)
            Unbind();
    }

    private void Update()
    {
        if (panelRoot == null ||
            !panelRoot.activeSelf ||
            string.IsNullOrWhiteSpace(_currentTargetId))
        {
            return;
        }

        _refreshTimer -= Time.unscaledDeltaTime;

        if (_refreshTimer > 0f)
            return;

        _refreshTimer = Mathf.Max(0.05f, refreshIntervalSeconds);
        RefreshCurrentTarget();
    }

    public void Bind(SystemHudBindingContext2A context)
    {
        if (context == null)
            return;

        _eventBus = context.Get<SimpleEventBus>();
        context.TryGet(out _npcRuntimeService);
        context.TryGet(out _enemyService);
        context.TryGet(out _configService);

        RebuildWeaponConfigCache();

        _eventBus.Subscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(
            OnPanelRequested);
        _eventBus.Subscribe<SystemSelectedTargetInfoPanelCloseRequestedEvent2A>(
            OnPanelCloseRequested);
        _eventBus.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(
            OnObjectsPanelCloseRequested);
        _eventBus.Subscribe<SystemObjectsPanelRequestedEvent2A>(
            OnObjectsPanelRequested);
        _eventBus.Subscribe<SystemNpcDamagedEvent>(
            OnNpcDamaged);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(
            OnNpcDestroyed);
        _eventBus.Subscribe<SystemEnemyDestroyedEvent>(
            OnEnemyDestroyed);

        _isBound = true;
        HidePanel();
    }

    public void Unbind()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(
                OnPanelRequested);
            _eventBus.Unsubscribe<SystemSelectedTargetInfoPanelCloseRequestedEvent2A>(
                OnPanelCloseRequested);
            _eventBus.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(
                OnObjectsPanelCloseRequested);
            _eventBus.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(
                OnObjectsPanelRequested);
            _eventBus.Unsubscribe<SystemNpcDamagedEvent>(
                OnNpcDamaged);
            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(
                OnNpcDestroyed);
            _eventBus.Unsubscribe<SystemEnemyDestroyedEvent>(
                OnEnemyDestroyed);
        }

        _eventBus = null;
        _npcRuntimeService = null;
        _enemyService = null;
        _configService = null;
        _weaponConfigsById.Clear();
        _isBound = false;
    }

    private void OnPanelRequested(
        SystemSelectedTargetInfoPanelRequestedEvent2A evt)
    {
        ShowTarget(evt.TargetId, evt.TargetType);
    }

    private void OnPanelCloseRequested(
        SystemSelectedTargetInfoPanelCloseRequestedEvent2A evt)
    {
        HidePanel();
    }

    private void OnObjectsPanelCloseRequested(
        SystemObjectsPanelCloseRequestedEvent2A evt)
    {
        HidePanel();
    }

    private void OnObjectsPanelRequested(
        SystemObjectsPanelRequestedEvent2A evt)
    {
        HidePanel();
    }

    private void OnNpcDamaged(SystemNpcDamagedEvent evt)
    {
        if (evt.RuntimeNpcId != _currentTargetId)
            return;

        RefreshCurrentTarget();
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        if (evt.RuntimeNpcId == _currentTargetId)
            HidePanel();
    }

    private void OnEnemyDestroyed(SystemEnemyDestroyedEvent evt)
    {
        if (evt.RuntimeEnemyId == _currentTargetId)
            HidePanel();
    }

    private void ShowTarget(
        string targetId,
        SystemGameplayTargetType targetType)
    {
        if (!IsSystemScene())
        {
            HidePanel();
            return;
        }

        _currentTargetId = targetId ?? string.Empty;
        _currentTargetType = targetType;
        _refreshTimer = 0f;

        if (!RefreshCurrentTarget())
        {
            HidePanel();
            return;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    private bool RefreshCurrentTarget()
    {
        if (string.IsNullOrWhiteSpace(_currentTargetId))
            return false;

        if (_npcRuntimeService != null &&
            _npcRuntimeService.TryGetNpc(
                _currentTargetId,
                out SystemNpcRuntimeState npc))
        {
            return RefreshNpc(npc);
        }

        if (_enemyService != null &&
            _enemyService.TryGetEnemy(
                _currentTargetId,
                out SystemEnemyRuntimeState enemy))
        {
            return RefreshEnemy(enemy);
        }

        return false;
    }

    private bool RefreshNpc(SystemNpcRuntimeState npc)
    {
        if (npc == null || !npc.IsAlive)
            return false;

        SetHeader(BuildNpcDisplayName(npc));

        _builder.Clear();
        int rows = 0;

        AppendStatLine("Здоровье", npc.CurrentHull);
        rows++;

        AppendStatLine("Щит", npc.CurrentShield);
        rows++;

        rows += AppendNpcWeapons(npc);

        ApplyHeight(rows);
        SetRowsText();

        return true;
    }

    private bool RefreshEnemy(SystemEnemyRuntimeState enemy)
    {
        if (enemy == null || !enemy.IsAlive)
            return false;

        SetHeader(BuildEnemyDisplayName(enemy));

        _builder.Clear();
        int rows = 0;

        AppendStatLine("Здоровье", enemy.CurrentHull);
        rows++;

        AppendStatLine("Щит", enemy.CurrentShield);
        rows++;

        rows += AppendLegacyEnemyWeapon(enemy);

        ApplyHeight(rows);
        SetRowsText();

        return true;
    }

    private int AppendNpcWeapons(SystemNpcRuntimeState npc)
    {
        if (npc.Weapons == null || npc.Weapons.Count == 0)
            return 0;

        int rows = CountGroupSpacerRows();
        AppendGroup("Оружие");
        rows++;

        for (int i = 0; i < npc.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weapon = npc.Weapons[i];

            if (weapon == null)
                continue;

            WeaponConfig config =
                GetWeaponConfig(weapon.WeaponConfigId);

            string weaponName =
                BuildWeaponDisplayName(config, i);

            int damage =
                GetWeaponDamage(config);

            int range =
                Mathf.RoundToInt(Mathf.Max(0f, weapon.ShotDistance));

            AppendWeaponLine(weaponName, damage, range);
            rows++;
        }

        return rows;
    }

    private int AppendLegacyEnemyWeapon(SystemEnemyRuntimeState enemy)
    {
        if (enemy.BaseAttackDamage <= 0 &&
            enemy.AttackRange <= 0f)
        {
            return 0;
        }

        int rows = CountGroupSpacerRows();
        AppendGroup("Оружие");
        AppendWeaponLine(
            "Оружие1",
            Mathf.Max(0, enemy.BaseAttackDamage),
            Mathf.RoundToInt(Mathf.Max(0f, enemy.AttackRange)));

        return rows + 2;
    }

    private void AppendStatLine(
        string label,
        int value)
    {
        _builder
            .Append("<color=#FFFFFF>")
            .Append(label)
            .Append(" ")
            .Append(Mathf.Max(0, value))
            .Append("</color>")
            .AppendLine();
    }

    private void AppendGroup(string text)
    {
        if (_builder.Length > 0)
            _builder.AppendLine();

        _builder
            .Append("<color=#58BBF6><b>")
            .Append(text)
            .Append("</b></color>")
            .AppendLine();
    }

    private int CountGroupSpacerRows()
    {
        return _builder.Length > 0
            ? 1
            : 0;
    }

    private void AppendWeaponLine(
        string weaponName,
        int damage,
        int range)
    {
        _builder
            .Append("<color=#FFFFFF>")
            .Append(string.IsNullOrWhiteSpace(weaponName)
                ? "Оружие"
                : weaponName)
            .Append(" ")
            .Append(Mathf.Max(0, damage))
            .Append("/")
            .Append(Mathf.Max(0, range))
            .Append("</color>")
            .AppendLine();
    }

    private void SetHeader(string text)
    {
        if (headerText == null)
            return;

        headerText.SetText(
            string.IsNullOrWhiteSpace(text)
                ? "ЦЕЛЬ"
                : text.ToUpperInvariant());
    }

    private void SetRowsText()
    {
        if (rowsText != null)
            rowsText.SetText(_builder.ToString());
    }

    private void ApplyHeight(int visualRows)
    {
        if (panelRect == null)
            return;

        float rowsHeight =
            Mathf.Max(0, visualRows) * rowHeight;

        float targetHeight =
            Mathf.Clamp(
                headerHeight + verticalPadding + rowsHeight,
                minPanelHeight,
                maxPanelHeight);

        Vector2 size = panelRect.sizeDelta;
        size.y = targetHeight;
        panelRect.sizeDelta = size;
    }

    private string BuildNpcDisplayName(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(npc.DisplayName))
            return npc.DisplayName;

        return string.IsNullOrWhiteSpace(npc.ConfigId)
            ? npc.RuntimeNpcId
            : npc.ConfigId;
    }

    private string BuildEnemyDisplayName(SystemEnemyRuntimeState enemy)
    {
        if (enemy == null)
            return string.Empty;

        if (enemy.EnemyConfig != null &&
            !string.IsNullOrWhiteSpace(enemy.EnemyConfig.DisplayName))
        {
            return enemy.EnemyConfig.DisplayName;
        }

        return string.IsNullOrWhiteSpace(enemy.EnemyConfigId)
            ? enemy.RuntimeEnemyId
            : enemy.EnemyConfigId;
    }

    private string BuildWeaponDisplayName(
        WeaponConfig config,
        int index)
    {
        if (config != null &&
            !string.IsNullOrWhiteSpace(config.DisplayName))
        {
            return config.DisplayName;
        }

        return "Оружие" + (index + 1);
    }

    private int GetWeaponDamage(WeaponConfig config)
    {
        if (config == null)
            return 0;

        return Mathf.RoundToInt(
            (Mathf.Max(0, config.BaseDamageMin) +
             Mathf.Max(0, config.BaseDamageMax)) * 0.5f);
    }

    private WeaponConfig GetWeaponConfig(string weaponConfigId)
    {
        if (string.IsNullOrWhiteSpace(weaponConfigId))
            return null;

        if (_weaponConfigsById.TryGetValue(
                weaponConfigId,
                out WeaponConfig cached))
        {
            return cached;
        }

        if (_configService == null)
            return null;

        WeaponConfig config =
            _configService.GetWeaponConfigById(weaponConfigId);

        if (config != null)
            _weaponConfigsById[weaponConfigId] = config;

        return config;
    }

    private void RebuildWeaponConfigCache()
    {
        _weaponConfigsById.Clear();

        if (_configService == null)
            return;

        IReadOnlyList<WeaponConfig> configs =
            _configService.GetAllWeapons();

        if (configs == null)
            return;

        for (int i = 0; i < configs.Count; i++)
        {
            WeaponConfig config = configs[i];

            if (config == null ||
                string.IsNullOrWhiteSpace(config.Id))
            {
                continue;
            }

            _weaponConfigsById[config.Id] = config;
        }
    }

    private bool IsSystemScene()
    {
        return SceneManager.GetActiveScene().name == "SystemScene";
    }

    private void HidePanel()
    {
        _currentTargetId = string.Empty;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void ResolveSerializedReferences()
    {
        if (panelRoot == null)
            panelRoot = transform.Find("SelectedTargetInfoPanel")?.gameObject;

        if (panelRect == null && panelRoot != null)
            panelRect = panelRoot.GetComponent<RectTransform>();

        if (headerText == null && panelRoot != null)
            headerText = panelRoot
                .transform
                .Find("HeaderText")
                ?.GetComponent<TMP_Text>();

        if (rowsText == null && panelRoot != null)
            rowsText = panelRoot
                .transform
                .Find("RowsText")
                ?.GetComponent<TMP_Text>();
    }
}
