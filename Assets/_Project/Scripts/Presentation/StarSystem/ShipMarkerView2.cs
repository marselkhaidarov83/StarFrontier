using UnityEngine;

public sealed class ShipMarkerView2 : MonoBehaviour
{
    [SerializeField] private SpriteRenderer shipImage;

    private void Reset()
    {
        shipImage = GetComponent<SpriteRenderer>();
    }

    public void SetPosition(Vector3 mapPosition)
    {
        transform.position = mapPosition;
    }
}