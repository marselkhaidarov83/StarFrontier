using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class SystemSpaceLootView : MonoBehaviour, IPointerClickHandler
{
    private string _lootId;
    private SystemSpaceLootViewController _controller;
    private SpriteRenderer _collectionMarker;
    private LineRenderer _pullBeam;
    private MaterialPropertyBlock _pullBeamProperties;
    private static readonly int BeamPhaseId = Shader.PropertyToID("_BeamPhase");

    public string LootId => _lootId;

    public void Initialize(string lootId, Sprite icon, Vector3 position,
        float worldSize, GameObject markerPrefab,
        SystemSpaceLootViewController controller, Sprite collectionMarkerIcon = null)
    {
        _lootId = lootId;
        _controller = controller;
        transform.position = position;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = icon;
        renderer.sortingLayerName = Pseudo3DRenderOrder2.ShipFxLayer;
        renderer.sortingOrder = Pseudo3DRenderOrder2.Ship + 20;
        SpriteRendererSizeUtility.SetWorldSize(renderer, Mathf.Max(1f, worldSize));

        CircleCollider2D hitArea = GetComponent<CircleCollider2D>();
        hitArea.isTrigger = true;
        hitArea.radius = icon != null
            ? Mathf.Max(icon.bounds.extents.x, icon.bounds.extents.y, 0.5f)
            : 1f;

        if (markerPrefab != null)
        {
            GameObject markerRoot = Instantiate(markerPrefab, transform, false);
            markerRoot.name = "TargetMarker";
            SpriteRenderer markerRenderer = markerRoot.GetComponent<SpriteRenderer>();
            if (markerRenderer != null)
            {
                markerRenderer.sortingLayerID = renderer.sortingLayerID;
                markerRenderer.sortingOrder = renderer.sortingOrder + 1;
                if (markerRenderer.sprite != null)
                {
                    float markerWidth = markerRenderer.sprite.bounds.size.x;
                    if (markerWidth > 0f)
                    {
                        float parentScale = Mathf.Abs(transform.lossyScale.x);
                        float scale = worldSize * 1.2f /
                            (markerWidth * Mathf.Max(parentScale, 0.001f));
                        markerRoot.transform.localScale = new Vector3(scale, scale, 1f);
                    }
                }
            }

            var targetMarker = gameObject.AddComponent<TargetMarkerView2A>();
            targetMarker.ConfigureMarker(markerRoot, markerRenderer);
            targetMarker.Initialize(lootId, true, true);
        }

        var collectionObject = new GameObject("CollectionMarker", typeof(SpriteRenderer));
        collectionObject.transform.SetParent(transform, false);
        _collectionMarker = collectionObject.GetComponent<SpriteRenderer>();
        _collectionMarker.sprite = collectionMarkerIcon;
        _collectionMarker.color = Color.white;
        _collectionMarker.sortingLayerID = renderer.sortingLayerID;
        _collectionMarker.sortingOrder = renderer.sortingOrder + 2;
        SpriteRendererSizeUtility.SetWorldSize(_collectionMarker,
            Mathf.Max(4f, worldSize * 0.32f));
        // The loot root is scaled to fit its sprite. Compensate so the badge
        // keeps its world size and sits just outside the visible loot bounds.
        Vector3 markerScale = collectionObject.transform.localScale;
        Vector3 lootRootScale = transform.lossyScale;
        collectionObject.transform.localScale = new Vector3(
            markerScale.x / Mathf.Max(Mathf.Abs(lootRootScale.x), 0.001f),
            markerScale.y / Mathf.Max(Mathf.Abs(lootRootScale.y), 0.001f), 1f);
        float gap = Mathf.Max(0.25f, worldSize * 0.02f);
        collectionObject.transform.position = new Vector3(
            renderer.bounds.max.x + gap + _collectionMarker.bounds.extents.x,
            renderer.bounds.max.y + gap + _collectionMarker.bounds.extents.y,
            renderer.bounds.center.z - 0.1f);
        _collectionMarker.enabled = false;
    }

    public void SetCollectionMarked(bool marked)
    {
        if (_collectionMarker != null)
            _collectionMarker.enabled = marked;
    }

    public void SetPullBeam(bool pulling, Vector3 shipPosition, Material material,
        float simulationTime)
    {
        if (!pulling || material == null || !isActiveAndEnabled)
        {
            if (_pullBeam != null && _pullBeam.enabled)
            {
                _pullBeam.enabled = false;
                AppLog.Info(DebugLogChannel.Loot, "[LOOT_BEAM_HIDDEN] Id=" + _lootId);
            }
            return;
        }

        if (_pullBeam == null)
        {
            var beamObject = new GameObject("LootTractorBeam", typeof(LineRenderer));
            beamObject.transform.SetParent(transform, false);
            Vector3 rootScale = transform.lossyScale;
            beamObject.transform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(rootScale.x), 0.001f),
                1f / Mathf.Max(Mathf.Abs(rootScale.y), 0.001f), 1f);
            _pullBeam = beamObject.GetComponent<LineRenderer>();
            _pullBeam.enabled = false;
            _pullBeam.useWorldSpace = true;
            _pullBeam.positionCount = 2;
            _pullBeam.alignment = LineAlignment.View;
            _pullBeam.textureMode = LineTextureMode.Stretch;
            _pullBeam.numCapVertices = 4;
            _pullBeam.sharedMaterial = material;
            SpriteRenderer lootRenderer = GetComponent<SpriteRenderer>();
            _pullBeam.sortingLayerID = lootRenderer.sortingLayerID;
            _pullBeam.sortingOrder = lootRenderer.sortingOrder - 1;
            _pullBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pullBeam.receiveShadows = false;
            _pullBeamProperties = new MaterialPropertyBlock();
        }

        Vector3 end = transform.position;
        shipPosition.z = end.z;
        float width = Mathf.Min(5f, Vector3.Distance(shipPosition, end) * 0.25f);
        _pullBeam.startWidth = width * 0.45f;
        _pullBeam.endWidth = width;
        _pullBeam.SetPosition(0, shipPosition);
        _pullBeam.SetPosition(1, end);
        _pullBeamProperties.SetFloat(BeamPhaseId, simulationTime * 0.8f);
        _pullBeam.SetPropertyBlock(_pullBeamProperties);
        if (!_pullBeam.enabled)
            AppLog.Info(DebugLogChannel.Loot, "[LOOT_BEAM_SHOWN] Id=" + _lootId);
        _pullBeam.enabled = true;
    }

    private void OnDisable()
    {
        if (_pullBeam != null)
            _pullBeam.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!string.IsNullOrWhiteSpace(_lootId))
            _controller?.OpenLoot(_lootId);
    }
}
