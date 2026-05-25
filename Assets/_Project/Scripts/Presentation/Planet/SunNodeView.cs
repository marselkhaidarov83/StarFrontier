using UnityEngine;

public class SunNodeView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sunImage;
    
    private SunConfig _sun;

    public void Initialize(SunConfig sun, System.Action<string> onClick)
    {
        _sun = sun;

        transform.position = _sun.LocalOffset;
        sunImage.sprite = _sun.SunSprite;

        SpriteRendererSizeUtility.SetWorldSize(
            sunImage,
            _sun.VisualSize
        );
    }
}