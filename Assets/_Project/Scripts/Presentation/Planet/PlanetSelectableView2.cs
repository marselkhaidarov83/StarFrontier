using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlanetSelectableView2 : CustomMonoBehaviour, IPointerClickHandler
{
    [SerializeField] private PlanetConfig _planet;

    public PlanetConfig Planet => _planet;
    private SimpleEventBus _simpleEventBus;

    public void Initialize(PlanetConfig planet)
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _planet = planet;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LogCustom("EventData = " + eventData);
        LogCustom("Planet = " + _planet.Id);

        _simpleEventBus.Publish(new PlanetSelectedEvent(_planet));
    }       
}