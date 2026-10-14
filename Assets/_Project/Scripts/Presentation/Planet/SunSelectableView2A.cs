using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SunSelectableView2A :
    CustomMonoBehaviour,
    IPointerClickHandler
{
    [SerializeField]
    private SunNodeView sunNodeView;

    public void Initialize(
        SunNodeView nodeView)
    {
        sunNodeView = nodeView;

        if (sunNodeView == null)
            sunNodeView =
                GetComponentInParent<SunNodeView>();
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (sunNodeView == null)
            sunNodeView =
                GetComponentInParent<SunNodeView>();

        if (sunNodeView == null)
        {
            AppLog.Warning(DebugLogChannel.PlanetUi,
                typeof(SunSelectableView2A),
                "SunNodeView is null.");

            return;
        }

        sunNodeView.OpenSystemObjectsPanelFromSun();
    }
}
