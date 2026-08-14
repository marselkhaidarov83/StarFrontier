using TMPro;
using UnityEngine;

public sealed class StationNodeView2A : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer stationImage;
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject selectedTargetMarkerPrefab;
    [SerializeField] private TargetMarkerView2A targetMarkerView;
    [SerializeField] private float selectedTargetMarkerScale = 5f;

    private StationConfig _station;
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

            SpriteRendererSizeUtility.SetWorldSize(
                stationImage,
                worldSize);

            ConfigureSelectionCollider();
            ConfigureTargetMarker();
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
}
