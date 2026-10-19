using UnityEngine;

[DisallowMultipleComponent]
public sealed class SystemOffscreenEnemyIndicatorsHud2A : MonoBehaviour, ISystemHudWidgetBinder2A
{
    private ISystemNpcRuntimeService _npcs;
    private IPlayerCombatTargetService _player;
    private IConfigService _configs;
    private IGameSessionService _session;
    private SystemCameraController2A _cameraController;
    private SystemEnemyIndicatorGraphic2A _graphic;
    private Canvas _canvas;
    private RectTransform _topHudPanel;
    private RectTransform _bottomHudPanel;
    private readonly Vector3[] _panelCorners = new Vector3[4];

    public bool IsBound { get; private set; }

    public void Bind(SystemHudBindingContext2A context)
    {
        Unbind();
        _npcs = context.Get<ISystemNpcRuntimeService>();
        _player = context.Get<IPlayerCombatTargetService>();
        _configs = context.Get<IConfigService>();
        _session = context.Get<IGameSessionService>();
        _cameraController = FindFirstObjectByType<SystemCameraController2A>(FindObjectsInactive.Include);
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            return;

        _canvas = parentCanvas.rootCanvas;
        _bottomHudPanel = transform as RectTransform;
        MetaHudController topHud = _canvas.GetComponentInChildren<MetaHudController>(true);
        _topHudPanel = topHud != null ? topHud.transform as RectTransform : null;
        // The bottom HUD is only 150 units high. Draw on its full-screen canvas instead.
        GameObject overlay = new GameObject("OffscreenShipIndicators", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(SystemEnemyIndicatorGraphic2A));
        overlay.layer = _canvas.gameObject.layer;
        RectTransform rect = (RectTransform)overlay.transform;
        rect.SetParent(_canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
        _graphic = overlay.GetComponent<SystemEnemyIndicatorGraphic2A>();
        _graphic.raycastTarget = false;
        _graphic.enabled = false;
        IsBound = true;
        // Bind at the normal lifecycle order, but project markers after camera movement.
        Canvas.preWillRenderCanvases += RefreshIndicators;
    }

    public void Unbind()
    {
        Canvas.preWillRenderCanvases -= RefreshIndicators;
        IsBound = false;
        if (_graphic != null)
        {
            _graphic.enabled = false;
            Destroy(_graphic.gameObject);
        }
        _graphic = null;
        _canvas = null;
        _topHudPanel = null;
        _bottomHudPanel = null;
        _cameraController = null;
        _npcs = null;
        _player = null;
        _configs = null;
        _session = null;
    }

    private void OnDisable()
    {
        if (_graphic != null)
            _graphic.enabled = false;
    }

    private void OnDestroy() => Unbind();

    private void RefreshIndicators()
    {
        if (!IsBound || _graphic == null)
            return;

        if (!isActiveAndEnabled)
        {
            _graphic.enabled = false;
            return;
        }

        if (_cameraController == null)
            _cameraController = FindFirstObjectByType<SystemCameraController2A>(FindObjectsInactive.Include);

        Camera worldCamera = _cameraController != null ? _cameraController.TargetCamera : null;
        SystemHudConfig config = _configs.SystemHudConfig;
        if (_cameraController == null || !_cameraController.IsSystemCameraActive ||
            worldCamera == null || !worldCamera.isActiveAndEnabled || config == null ||
            !_player.IsPlayerAvailableInSystem(_session.State?.Player?.CurrentSystemId))
        {
            _graphic.enabled = false;
            return;
        }

        if (!config.ShowOffscreenEnemyIndicators && !config.ShowOffscreenNpcIndicators &&
            !config.ShowOffscreenPlayerIndicator)
        {
            _graphic.enabled = false;
            return;
        }

        _graphic.BeginFrame(config.OffscreenEnemyIndicatorSize);
        Rect screenBounds = worldCamera.pixelRect;
        if (config.UseSafeArea)
        {
            Rect safe = Screen.safeArea;
            screenBounds = Rect.MinMaxRect(
                Mathf.Max(screenBounds.xMin, safe.xMin), Mathf.Max(screenBounds.yMin, safe.yMin),
                Mathf.Min(screenBounds.xMax, safe.xMax), Mathf.Min(screenBounds.yMax, safe.yMax));
        }

        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        // The game is visible only between the HUD panels, even though the camera
        // continues to render the world behind those panels.
        if (TryGetPanelScreenRect(_bottomHudPanel, uiCamera, out Rect bottomPanel) &&
            bottomPanel.Overlaps(screenBounds))
        {
            screenBounds.yMin = Mathf.Clamp(bottomPanel.yMax, screenBounds.yMin, screenBounds.yMax);
        }
        if (TryGetPanelScreenRect(_topHudPanel, uiCamera, out Rect topPanel) &&
            topPanel.Overlaps(screenBounds))
        {
            screenBounds.yMax = Mathf.Clamp(topPanel.yMin, screenBounds.yMin, screenBounds.yMax);
        }

        RectTransform rect = _graphic.rectTransform;
        if (screenBounds.width <= 0f || screenBounds.height <= 0f ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenBounds.min, uiCamera, out Vector2 min) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenBounds.max, uiCamera, out Vector2 max))
        {
            _graphic.enabled = false;
            return;
        }

        // A circumscribed radius keeps the whole rotated triangle inside the safe area.
        float inset = config.OffscreenEnemyIndicatorEdgePadding +
            config.OffscreenEnemyIndicatorSize.magnitude * 0.5f;
        Rect edge = Rect.MinMaxRect(min.x + inset, min.y + inset, max.x - inset, max.y - inset);
        if (edge.width <= 0f || edge.height <= 0f)
        {
            _graphic.enabled = false;
            return;
        }

        Vector3 playerPosition = _player.GetPlayerPosition();
        var npcs = _npcs.Npcs;
        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];
            if (npc == null || !npc.IsAlive || npc.IsOnPlanet ||
                string.IsNullOrWhiteSpace(npc.CurrentSystemId) ||
                !_player.IsPlayerAvailableInSystem(npc.CurrentSystemId))
                continue;

            bool isHostile = npc.IsHostileToPlayer;
            if ((isHostile && !config.ShowOffscreenEnemyIndicators) ||
                (!isHostile && !config.ShowOffscreenNpcIndicators))
                continue;

            if (isHostile)
            {
                float shotDistance = ResolveShotDistance(npc);
                float maxDistance = shotDistance * config.OffscreenEnemyRangeMultiplier;
                Vector2 delta = (Vector2)npc.CurrentPosition - (Vector2)playerPosition;
                // The range limit applies only to enemies; peaceful NPCs may be unarmed.
                if (maxDistance <= 0f || delta.sqrMagnitude >= maxDistance * maxDistance)
                    continue;
            }

            Color tint = isHostile
                ? config.OffscreenEnemyIndicatorColor : config.OffscreenNpcIndicatorColor;
            AddOffscreenIndicator(npc.CurrentPosition, tint, worldCamera, uiCamera, screenBounds, edge);
        }

        // The player's marker remains available even when the camera is far from the ship.
        if (config.ShowOffscreenPlayerIndicator)
        {
            AddOffscreenIndicator(playerPosition, config.OffscreenPlayerIndicatorColor,
                worldCamera, uiCamera, screenBounds, edge);
        }
        _graphic.EndFrame();
    }

    private void AddOffscreenIndicator(Vector3 position, Color tint, Camera worldCamera,
        Camera uiCamera, Rect screenBounds, Rect edge)
    {
        position.z = 0f;
        Vector3 screen = worldCamera.WorldToScreenPoint(position);
        if (screen.z > 0f && screenBounds.Contains(new Vector2(screen.x, screen.y)))
            return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _graphic.rectTransform, screen, uiCamera, out Vector2 local))
            return;

        Vector2 direction = local - edge.center;
        if (screen.z < 0f)
            direction = -direction;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        Vector2 halfSize = edge.size * 0.5f;
        float xDistance = Mathf.Abs(direction.x) > 0.0001f
            ? halfSize.x / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float yDistance = Mathf.Abs(direction.y) > 0.0001f
            ? halfSize.y / Mathf.Abs(direction.y) : float.PositiveInfinity;
        Vector2 center = edge.center + direction * Mathf.Min(xDistance, yDistance);
        _graphic.AddIndicator(center, direction, tint);
    }

    private bool TryGetPanelScreenRect(RectTransform panel, Camera uiCamera, out Rect bounds)
    {
        bounds = default;
        if (panel == null || !panel.gameObject.activeInHierarchy)
            return false;

        panel.GetWorldCorners(_panelCorners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < _panelCorners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, _panelCorners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return bounds.width > 0f && bounds.height > 0f;
    }

    private static float ResolveShotDistance(SystemNpcRuntimeState npc)
    {
        float distance = 0f;
        if (npc.Weapons == null)
            return distance;
        for (int i = 0; i < npc.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weapon = npc.Weapons[i];
            if (weapon != null)
                distance = Mathf.Max(distance, weapon.ShotDistance);
        }
        return distance;
    }
}
