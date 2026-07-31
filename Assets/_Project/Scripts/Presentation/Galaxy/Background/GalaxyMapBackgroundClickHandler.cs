using UnityEngine;
using UnityEngine.EventSystems;

public class GalaxyMapBackgroundClickHandler : CustomMonoBehaviour, IPointerClickHandler
{
    private SimpleEventBus _eventBus;

    public void Initialize()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _eventBus.Publish(new GalaxyMapSelectionClearedEvent());
    }
}