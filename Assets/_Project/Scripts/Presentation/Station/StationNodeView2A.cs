using TMPro;
using UnityEngine;

public sealed class StationNodeView2A : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer stationImage;
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject selectedTargetMarkerPrefab;
    [SerializeField] private TargetMarkerView2A targetMarkerView;
    [SerializeField] private float selectedTargetMarkerScale = 5f;

    [SerializeField, Range(0f, 1f)] private float unbuiltBrightness = 0.45f;
    [SerializeField, Range(0f, 1f)] private float destroyedBrightness = 0.35f;
    private Color _originalColor = Color.white;
    private bool _colorCaptured;
    private float _stationWorldSize;
    private StationConfig _station;
    private SimpleEventBus _stationEvents;
    private IStationService _stationService;
    private IGameSessionService _stationSession;
    private GameObject _targetMarkerInstance;

    public void Initialize(
        StationConfig station,
        SystemVisualConfig visualConfig = null)
    {
        _station = station;

        if (_station == null)
            return;

        ResolveReferences();

        transform.position =
            new Vector3(
                _station.LocalOffset.x,
                _station.LocalOffset.y,
                transform.position.z);

        if (stationImage != null)
        {
            stationImage.sprite =
                _station.IsDestroyed
                    ? _station.DestroyedSprite
                    : _station.StationSprite;

            if (stationImage.sprite == null)
                stationImage.sprite = _station.StationSprite;

            float worldSize =
                visualConfig != null
                    ? visualConfig.GetStationWorldSize(_station)
                    : _station.VisualSize;

            _stationWorldSize = worldSize;
            SpriteRendererSizeUtility.SetWorldSize(
                stationImage,
                worldSize);

            ConfigureSelectionCollider();
            ConfigureTargetMarker();
        }

        if (Bootstrapper.Instance != null)
        {
            var registry = Bootstrapper.Instance.ServiceRegistry;
            registry.TryGet(out _stationService); registry.TryGet(out _stationSession);
            if (_stationEvents != null) _stationEvents.Unsubscribe<StationStateChangedEvent>(OnStationState);
            _stationEvents = registry.Get<SimpleEventBus>();
            _stationEvents.Subscribe<StationStateChangedEvent>(OnStationState);
            if (stationImage != null && !_colorCaptured) { _originalColor = stationImage.color; _colorCaptured = true; }
            ApplyRuntimeVisual();
        }
        if (label != null)
        {
            label.text =
                _station.DisplayName;
        }
    }

    private void ConfigureSelectionCollider()
    {
        if (stationImage == null)
            return;

        GameObject selectionObject =
            stationImage.gameObject;

        BoxCollider2D collider =
            selectionObject.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider =
                selectionObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger =
            false;

        if (stationImage.sprite != null)
        {
            collider.size =
                stationImage.sprite.bounds.size;
        }

        StationSelectableView2A selectable =
            selectionObject.GetComponent<StationSelectableView2A>();

        if (selectable == null)
        {
            selectable =
                selectionObject.AddComponent<StationSelectableView2A>();
        }

        selectable.Initialize(
            _station);
    }

    private void ConfigureTargetMarker()
    {
        if (stationImage == null || _station == null)
            return;

        GameObject markerOwner =
            stationImage.gameObject;

        if (targetMarkerView == null)
        {
            targetMarkerView =
                markerOwner.GetComponent<TargetMarkerView2A>();
        }

        if (targetMarkerView == null)
        {
            targetMarkerView =
                markerOwner.AddComponent<TargetMarkerView2A>();
        }

        GameObject markerRoot =
            ResolveTargetMarkerRoot(markerOwner.transform);

        if (markerRoot == null)
            return;

        markerRoot.transform.localScale =
            new Vector3(
                selectedTargetMarkerScale,
                selectedTargetMarkerScale,
                1f);

        SpriteRenderer markerRenderer =
            markerRoot.GetComponent<SpriteRenderer>();

        targetMarkerView.ConfigureMarker(
            markerRoot,
            markerRenderer);

        targetMarkerView.Initialize(
            _station.Id,
            true,
            _station.IsActive);
    }

    private GameObject ResolveTargetMarkerRoot(
        Transform markerParent)
    {
        if (_targetMarkerInstance != null)
            return _targetMarkerInstance;

        Transform existingMarker =
            markerParent.Find("TargetMarker");

        if (existingMarker != null)
        {
            _targetMarkerInstance =
                existingMarker.gameObject;

            return _targetMarkerInstance;
        }

        if (selectedTargetMarkerPrefab == null)
        {
            return null;
        }

        _targetMarkerInstance =
            Instantiate(
                selectedTargetMarkerPrefab,
                markerParent,
                false);

        _targetMarkerInstance.name =
            "TargetMarker";

        return _targetMarkerInstance;
    }

    private void ResolveReferences()
    {
        if (stationImage == null)
        {
            stationImage =
                GetComponentInChildren<SpriteRenderer>(
                    true);
        }

        if (label == null)
        {
            label =
                GetComponentInChildren<TMP_Text>(
                    true);
        }
    }
    private void OnStationState(StationStateChangedEvent evt) { ApplyRuntimeVisual(); }
    private void OnDestroy() { _stationEvents?.Unsubscribe<StationStateChangedEvent>(OnStationState); }
    private void ApplyRuntimeVisual()
    {
        string systemId = _stationSession?.State?.Player?.CurrentSystemId;
        if (stationImage == null || _station == null || _stationService == null ||
            !_stationService.TryGetStation(systemId, out var state) || state.ConfigId != _station.Id) return;
        float brightness = state.IsDestroyed ? destroyedBrightness : state.Level == 0 ? unbuiltBrightness : 1f;
        stationImage.color = new Color(_originalColor.r * brightness, _originalColor.g * brightness, _originalColor.b * brightness, _originalColor.a);
        Sprite sprite = state.IsDestroyed && _station.DestroyedSprite != null ? _station.DestroyedSprite : _station.StationSprite;
        if (stationImage.sprite != sprite)
        {
            stationImage.sprite = sprite;
            if (_stationWorldSize > 0) SpriteRendererSizeUtility.SetWorldSize(stationImage, _stationWorldSize);
            var collider = stationImage.GetComponent<BoxCollider2D>();
            if (collider != null && sprite != null) collider.size = sprite.bounds.size;
        }
    }

}
