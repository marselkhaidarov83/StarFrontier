using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class SystemHudPointerClickRelay2A :
    MonoBehaviour,
    IPointerClickHandler
{
    [SerializeField] private MonoBehaviour receiver;

    public void SetReceiver(MonoBehaviour value)
    {
        receiver = value;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (receiver is ISystemHudPointerClickReceiver2A clickReceiver)
            clickReceiver.OnHudPointerClicked(gameObject, eventData);
    }
}

public interface ISystemHudPointerClickReceiver2A
{
    void OnHudPointerClicked(GameObject source, PointerEventData eventData);
}
