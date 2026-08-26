using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatEnemyStatusMarkerView2A : MonoBehaviour
{
    private const string SortingLayerName = "SystemShipFX";

    [Header("Root")]
    [SerializeField] private GameObject markerRoot;

    [Header("Bars")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer shieldFillRenderer;
    [SerializeField] private SpriteRenderer hullFillRenderer;

    [Header("Layout")]
    [SerializeField] private float widthMultiplier = 1.15f;
    [SerializeField] private float minWidth = 1.15f;
    [SerializeField] private float barHeight = 1.2f;
    [SerializeField] private float gap = 0.18f;
    [SerializeField] private float bottomOffsetMultiplier = 0.62f;

    [Header("Visual")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color shieldColor = new Color(0.25f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color hullColor = new Color(1f, 0.16f, 0.12f, 0.95f);
    [SerializeField] private int backgroundSortingOrder = 235;
    [SerializeField] private int shieldSortingOrder = 236;
    [SerializeField] private int hullSortingOrder = 237;

    private SimpleEventBus _eventBus;
    private ISystemNpcRuntimeService _runtimeService;
    private SystemNpcView _npcView;

    private string _runtimeNpcId = string.Empty;
    private float _baseWidth = 1f;

    private void Awake()
    {
        _npcView = GetComponent<SystemNpcView>();
        ResolveObjects();
        ApplyStaticVisualSettings();
        SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveObjects();
        ApplyStaticVisualSettings();
        ResolveServices();

        if (_eventBus != null)
        {
            _eventBus.Subscribe<SystemNpcDamagedEvent>(OnNpcDamaged);
            _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        }

        RefreshRuntimeNpcId();
        RefreshFromState();
    }

    private void OnDisable()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemNpcDamagedEvent>(OnNpcDamaged);
            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        }

        SetVisible(false);
    }

    private void Update()
    {
        RefreshRuntimeNpcId();
        RefreshFromState();
    }

    private void OnNpcDamaged(SystemNpcDamagedEvent evt)
    {
        RefreshRuntimeNpcId();

        if (!string.Equals(evt.RuntimeNpcId, _runtimeNpcId, System.StringComparison.Ordinal))
            return;

        RefreshFromState();
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        RefreshRuntimeNpcId();

        if (!string.Equals(evt.RuntimeNpcId, _runtimeNpcId, System.StringComparison.Ordinal))
            return;

        SetVisible(false);
    }

    private void RefreshFromState()
    {
        if (string.IsNullOrWhiteSpace(_runtimeNpcId) ||
            _runtimeService == null ||
            !_runtimeService.TryGetNpc(_runtimeNpcId, out SystemNpcRuntimeState npc))
        {
            SetVisible(false);
            return;
        }

        if (!npc.IsAlive || !npc.IsHostileToPlayer)
        {
            SetVisible(false);
            return;
        }

        RefreshLayout();

        ApplyFill(shieldFillRenderer, GetNormalized(npc.CurrentShield, npc.MaxShield));
        ApplyFill(hullFillRenderer, GetNormalized(npc.CurrentHull, npc.MaxHull));

        SetVisible(true);
    }

    private void RefreshRuntimeNpcId()
    {
        if (_npcView == null)
            _npcView = GetComponent<SystemNpcView>();

        if (_npcView == null || !_npcView.IsBound)
        {
            _runtimeNpcId = string.Empty;
            return;
        }

        _runtimeNpcId = _npcView.RuntimeNpcId;
    }

    private void RefreshLayout()
    {
        float shipWorldSize = ResolveShipWorldSize();
        _baseWidth = Mathf.Max(minWidth, shipWorldSize * widthMultiplier);

        if (markerRoot != null)
        {
            float y = -shipWorldSize * bottomOffsetMultiplier;
            markerRoot.transform.localPosition = new Vector3(0f, y, -0.025f);
        }

        float totalHeight = barHeight * 2f + gap;

        ApplyBarTransform(backgroundRenderer, _baseWidth, totalHeight, Vector3.zero);
        ApplyBarTransform(
            shieldFillRenderer,
            _baseWidth,
            barHeight,
            new Vector3(0f, (barHeight + gap) * 0.5f, -0.01f));
        ApplyBarTransform(
            hullFillRenderer,
            _baseWidth,
            barHeight,
            new Vector3(0f, -(barHeight + gap) * 0.5f, -0.01f));
    }

    private void ApplyBarTransform(
        SpriteRenderer renderer,
        float width,
        float height,
        Vector3 localPosition)
    {
        if (renderer == null)
            return;

        renderer.transform.localPosition = localPosition;
        renderer.transform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplyFill(SpriteRenderer renderer, float value01)
    {
        if (renderer == null)
            return;

        float width = _baseWidth * Mathf.Clamp01(value01);

        renderer.transform.localScale =
            new Vector3(width, renderer.transform.localScale.y, 1f);

        renderer.transform.localPosition =
            new Vector3(
                -_baseWidth * 0.5f + width * 0.5f,
                renderer.transform.localPosition.y,
                renderer.transform.localPosition.z);
    }

    private float ResolveShipWorldSize()
    {
        if (_npcView != null && _npcView.WorldSize > 0f)
            return _npcView.WorldSize;

        SpriteRenderer shipRenderer = ResolveShipRenderer();

        if (shipRenderer == null)
            return minWidth;

        Bounds bounds = shipRenderer.bounds;

        return Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            minWidth);
    }

    private SpriteRenderer ResolveShipRenderer()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null ||
                renderer == backgroundRenderer ||
                renderer == shieldFillRenderer ||
                renderer == hullFillRenderer)
            {
                continue;
            }

            if (markerRoot != null &&
                renderer.transform.IsChildOf(markerRoot.transform))
            {
                continue;
            }

            return renderer;
        }

        return null;
    }

    private void ResolveObjects()
    {
        if (markerRoot == null)
        {
            Transform existing = transform.Find("CombatEnemyStatusMarker");

            if (existing != null)
                markerRoot = existing.gameObject;
        }

        if (backgroundRenderer == null && markerRoot != null)
        {
            Transform item = markerRoot.transform.Find("StatusBarBackground");

            if (item != null)
                backgroundRenderer = item.GetComponent<SpriteRenderer>();
        }

        if (shieldFillRenderer == null && markerRoot != null)
        {
            Transform item = markerRoot.transform.Find("ShieldFill");

            if (item != null)
                shieldFillRenderer = item.GetComponent<SpriteRenderer>();
        }

        if (hullFillRenderer == null && markerRoot != null)
        {
            Transform item = markerRoot.transform.Find("HullFill");

            if (item != null)
                hullFillRenderer = item.GetComponent<SpriteRenderer>();
        }
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (_eventBus == null)
        {
            _eventBus =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<SimpleEventBus>();
        }

        if (_runtimeService == null)
        {
            _runtimeService =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<ISystemNpcRuntimeService>();
        }
    }

    private void ApplyStaticVisualSettings()
    {
        ApplyRenderer(backgroundRenderer, backgroundColor, backgroundSortingOrder);
        ApplyRenderer(shieldFillRenderer, shieldColor, shieldSortingOrder);
        ApplyRenderer(hullFillRenderer, hullColor, hullSortingOrder);
    }

    private void ApplyRenderer(
        SpriteRenderer renderer,
        Color color,
        int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.color = color;
        renderer.sortingLayerName = SortingLayerName;
        renderer.sortingOrder = sortingOrder;
    }

    private void SetVisible(bool visible)
    {
        if (markerRoot != null && markerRoot.activeSelf != visible)
            markerRoot.SetActive(visible);
    }

    private static float GetNormalized(int current, int max)
    {
        if (max <= 0)
            return 0f;

        return Mathf.Clamp01((float)current / max);
    }
}
