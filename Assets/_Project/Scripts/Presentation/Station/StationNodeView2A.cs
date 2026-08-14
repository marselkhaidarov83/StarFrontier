using TMPro;
using UnityEngine;

public sealed class StationNodeView2A : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer stationImage;
    [SerializeField] private TMP_Text label;

    private StationConfig _station;

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
