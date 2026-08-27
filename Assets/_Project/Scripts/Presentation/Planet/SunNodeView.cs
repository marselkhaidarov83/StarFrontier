using UnityEngine;

public class SunNodeView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sunImage;
    [SerializeField] private CircleCollider2D clickCollider;
    
    private SunConfig _sun;
    private SimpleEventBus _eventBus;
    private System.Action<string> _onClick;

    public void Initialize(
        SunConfig sun,
        System.Action<string> onClick,
        SystemVisualConfig visualConfig = null)
    {
        _sun = sun;
        _onClick = onClick;

        transform.position = _sun.LocalOffset;
        sunImage.sprite = _sun.SunSprite;

        float worldSize =
            visualConfig != null
                ? visualConfig.GetSunWorldSize(_sun)
                : _sun.VisualSize;

        SpriteRendererSizeUtility.SetWorldSize(
            sunImage,
            worldSize);

        if (clickCollider == null)
            clickCollider = sunImage != null
                ? sunImage.GetComponent<CircleCollider2D>()
                : null;

        if (clickCollider == null)
            clickCollider = GetComponent<CircleCollider2D>();

        if (clickCollider != null &&
            sunImage != null &&
            sunImage.sprite != null)
        {
            Vector2 spriteWorldSize =
                sunImage.sprite.bounds.size;

            float maxSide =
                Mathf.Max(
                    spriteWorldSize.x,
                    spriteWorldSize.y);

            clickCollider.radius =
                Mathf.Max(
                    0.5f,
                    maxSide * 0.5f);
        }

        SunSelectableView2A selectableView =
            sunImage != null
                ? sunImage.GetComponent<SunSelectableView2A>()
                : null;

        if (selectableView != null)
            selectableView.Initialize(this);
    }

    public void OpenSystemObjectsPanelFromSun()
    {
        if (_onClick != null)
        {
            _onClick.Invoke(
                _sun != null
                    ? _sun.Id
                    : string.Empty);

            return;
        }

        ResolveEventBus();

        _eventBus?.Publish(
            new SystemObjectsPanelRequestedEvent2A());
    }

    private void ResolveEventBus()
    {
        if (_eventBus != null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        _eventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();
    }
}
