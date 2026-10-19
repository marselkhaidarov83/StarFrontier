using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SystemInvasionWarningHud2A : SystemHudWidgetBinderBase2A
{
    private IGameSessionService _session;
    private IConfigService _configs;
    private SystemCameraController2A _camera;
    private SystemInvasionWarningGraphic2A _graphic;
    private Canvas _canvas;
    private SystemInvasionWarningGraphic2A _damageGraphic;
    private float _damageRemaining;
    private float _damageIntensity;
    private string _damageSystem;
    private RectTransform _topPanel;
    private RectTransform _bottomPanel;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly HashSet<string> _seen = new HashSet<string>();
    private float _elapsed = -1f;
    private string _warningSystem;

    protected override void OnBind()
    {
        _session = Context.Get<IGameSessionService>();
        _configs = Context.Get<IConfigService>();
        _camera = FindFirstObjectByType<SystemCameraController2A>(FindObjectsInactive.Include);
        Canvas parent = GetComponentInParent<Canvas>();
        if (parent == null)
            return;
        _canvas = parent.rootCanvas;
        _bottomPanel = transform as RectTransform;
        MetaHudController top = _canvas.GetComponentInChildren<MetaHudController>(true);
        _topPanel = top != null ? top.transform as RectTransform : null;
        _damageGraphic = CreateOverlay("PlayerDamagePulse");
        _graphic = CreateOverlay("InvasionWarning");
        SimpleEventBus bus = Context.Get<SimpleEventBus>();
        bus.Subscribe<WarNewsItemCreatedEvent>(OnWarNews);
        TrackSubscription(() => bus.Unsubscribe<WarNewsItemCreatedEvent>(OnWarNews));
        bus.Subscribe<PlayerDamageImpactEvent2A>(OnPlayerDamageImpact);
        TrackSubscription(() => bus.Unsubscribe<PlayerDamageImpactEvent2A>(OnPlayerDamageImpact));
    }

    private SystemInvasionWarningGraphic2A CreateOverlay(string name)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(SystemInvasionWarningGraphic2A));
        overlay.layer = _canvas.gameObject.layer;
        RectTransform rect = (RectTransform)overlay.transform;
        rect.SetParent(_canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
        var graphic = overlay.GetComponent<SystemInvasionWarningGraphic2A>();
        graphic.raycastTarget = false;
        graphic.enabled = false;
        return graphic;
    }

    protected override void OnUnbind()
    {
        Hide();
        HideDamagePulse();
        if (_damageGraphic != null)
            Destroy(_damageGraphic.gameObject);
        _damageGraphic = null;
        if (_graphic != null)
            Destroy(_graphic.gameObject);
        _graphic = null;
        _canvas = null;
        _seen.Clear();
    }

    private void OnWarNews(WarNewsItemCreatedEvent evt)
    {
        SystemHudConfig config = _configs.SystemHudConfig;
        if (evt.Kind != WarNewsEventKind.InvasionStarted || config == null ||
            !config.ShowInvasionWarning || !isActiveAndEnabled || _graphic == null ||
            string.IsNullOrWhiteSpace(evt.SystemId) ||
            evt.SystemId != _session.State?.Player?.CurrentSystemId || !IsSystemVisible())
            return;

        string key = evt.SystemId + ":" + evt.FactionId + ":" + evt.CreatedAtTick;
        if (!_seen.Add(key))
            return;
        _warningSystem = evt.SystemId;
        _elapsed = 0f;
        AppLog.Info(DebugLogChannel.CombatPresentation,
            "[INVASION_WARNING] Started | System=" + evt.SystemId +
            " | Tick=" + evt.CreatedAtTick +
            " | Duration=" + config.InvasionWarningDurationSeconds +
            " | Pulses=" + config.InvasionWarningPulseCount);
    }

    private void OnPlayerDamageImpact(PlayerDamageImpactEvent2A evt)
    {
        SystemHudConfig config = _configs.SystemHudConfig;
        if (config == null || !config.ShowPlayerDamagePulse || evt.IncomingDamage <= 0 ||
            !isActiveAndEnabled || _damageGraphic == null || !IsSystemVisible() ||
            string.IsNullOrWhiteSpace(evt.SystemId) ||
            evt.SystemId != _session.State?.Player?.CurrentSystemId)
            return;

        _damageSystem = evt.SystemId;
        _damageRemaining = config.PlayerDamagePulseDurationSeconds;
        // Keep the current brightness on repeated hits instead of resetting to black.
    }

    private void UpdateDamagePulse()
    {
        if (!IsBound || _damageGraphic == null || _damageRemaining <= 0f)
            return;
        SystemHudConfig config = _configs.SystemHudConfig;
        if (config == null || !config.ShowPlayerDamagePulse || !IsSystemVisible() ||
            _damageSystem != _session.State?.Player?.CurrentSystemId ||
            !TryGetPlayRect(config, out Rect bounds))
        {
            HideDamagePulse();
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        _damageRemaining = Mathf.Max(0f, _damageRemaining - deltaTime);
        float target = Mathf.SmoothStep(0f, 1f,
            _damageRemaining / config.PlayerDamagePulseDurationSeconds);
        float smoothingSeconds = Mathf.Min(0.06f, config.PlayerDamagePulseDurationSeconds * 0.1f);
        _damageIntensity = Mathf.Lerp(_damageIntensity, target,
            1f - Mathf.Exp(-deltaTime / smoothingSeconds));
        if (_damageRemaining <= 0f)
        {
            HideDamagePulse();
            return;
        }
        _damageGraphic.Show(bounds, config.PlayerDamagePulseColor,
            config.PlayerDamagePulseOpacity * _damageIntensity, 0f,
            config.PlayerDamagePulseWidth, 0f, false);
    }

    private void HideDamagePulse()
    {
        _damageRemaining = 0f;
        _damageIntensity = 0f;
        _damageSystem = null;
        if (_damageGraphic != null)
            _damageGraphic.enabled = false;
    }

    private bool IsSystemVisible()
    {
        if (_camera == null)
            _camera = FindFirstObjectByType<SystemCameraController2A>(FindObjectsInactive.Include);
        return _camera != null && _camera.IsSystemCameraActive &&
            _camera.TargetCamera != null && _camera.TargetCamera.isActiveAndEnabled;
    }

    private void LateUpdate()
    {
        UpdateDamagePulse();
        if (!IsBound || _elapsed < 0f || _graphic == null)
            return;
        SystemHudConfig config = _configs.SystemHudConfig;
        if (config == null || !config.ShowInvasionWarning || !IsSystemVisible() ||
            _warningSystem != _session.State?.Player?.CurrentSystemId)
        {
            Hide();
            return;
        }
        // The notification keeps its real-time duration while gameplay is paused.
        _elapsed += Time.unscaledDeltaTime;
        float duration = config.InvasionWarningDurationSeconds;
        if (_elapsed >= duration || !TryGetPlayRect(config, out Rect bounds))
        {
            Hide();
            return;
        }
        float progress = _elapsed / duration;
        float fadeSeconds = Mathf.Min(0.35f, duration * 0.2f);
        float fade = Mathf.SmoothStep(0f, 1f, _elapsed / fadeSeconds) *
            Mathf.SmoothStep(0f, 1f, (duration - _elapsed) / fadeSeconds);
        float pulse = Mathf.Pow(Mathf.Sin(progress * config.InvasionWarningPulseCount * Mathf.PI), 2f);
        _graphic.Show(bounds, config.InvasionWarningColor,
            config.InvasionWarningEdgeOpacity * pulse * fade, fade,
            config.InvasionWarningEdgeWidth, config.InvasionWarningSignSize);
    }

    private void Hide()
    {
        _elapsed = -1f;
        _warningSystem = null;
        if (_graphic != null)
            _graphic.enabled = false;
    }

    private bool TryGetPlayRect(SystemHudConfig config, out Rect result)
    {
        result = default;
        Rect screen = _camera.TargetCamera.pixelRect;
        if (config.UseSafeArea)
        {
            Rect safe = Screen.safeArea;
            screen = Rect.MinMaxRect(Mathf.Max(screen.xMin, safe.xMin), Mathf.Max(screen.yMin, safe.yMin),
                Mathf.Min(screen.xMax, safe.xMax), Mathf.Min(screen.yMax, safe.yMax));
        }
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (TryPanelRect(_bottomPanel, uiCamera, out Rect bottom) && bottom.Overlaps(screen))
            screen.yMin = Mathf.Clamp(bottom.yMax, screen.yMin, screen.yMax);
        if (TryPanelRect(_topPanel, uiCamera, out Rect top) && top.Overlaps(screen))
            screen.yMax = Mathf.Clamp(top.yMin, screen.yMin, screen.yMax);
        if (screen.width <= 0f || screen.height <= 0f ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_graphic.rectTransform, screen.min, uiCamera, out Vector2 min) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_graphic.rectTransform, screen.max, uiCamera, out Vector2 max))
            return false;
        result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return result.width > 0f && result.height > 0f;
    }

    private bool TryPanelRect(RectTransform panel, Camera camera, out Rect rect)
    {
        rect = default;
        if (panel == null || !panel.gameObject.activeInHierarchy)
            return false;
        panel.GetWorldCorners(_corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in _corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 0f && rect.height > 0f;
    }
}
